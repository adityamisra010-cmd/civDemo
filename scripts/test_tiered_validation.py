"""Tests for scripts/tiered_validation.py and the consistency of scripts/validation-map.json.

Run: python3 -m unittest scripts/test_tiered_validation.py
They read the live map and the live test sources (no build, no dotnet), so they also catch a stale mapping: a check
command whose script was removed, or a test pattern that no longer matches any test class.
"""
import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tiered_validation as tv  # noqa: E402

ROOT = tv.repo_root()
# Never contend for (or block on) the real machine-wide full-suite lock: a live run may hold it.
os.environ["CIVDEMO_LOCK_PATH"] = os.path.join(tempfile.mkdtemp(), "selftest.lock")
VMAP = tv.load_map(ROOT)
INDEX = tv.TestIndex(ROOT)


def plan(paths, tier="targeted", scope=None):
    return tv.select(VMAP, INDEX, paths, tier, scope)


class SelectionRules(unittest.TestCase):
    def test_docs_only_selects_doc_checks_and_no_build_or_tests(self):
        p = plan(["docs/design/notes.md", "docs/adr/adr-001-example.md"])
        self.assertEqual(set(p.checks), {"doc-links"})
        self.assertEqual(p.changed_md, ["docs/design/notes.md", "docs/adr/adr-001-example.md"])
        self.assertNotIn("build", p.checks)

    def test_doc_that_tests_read_pulls_its_readers_and_a_build(self):
        readers = INDEX.referencing("README.md")
        p = plan(["README.md"])
        self.assertIn("doc-links", p.checks)
        if readers:
            self.assertIn("build", p.checks)
            self.assertTrue({"cls:" + c.fqn for c in readers} <= set(p.terms["sim"]) | set(p.terms["ui"]))

    def test_m7_charter_doc_runs_the_charter_checker(self):
        p = plan(["docs/m7-plan/unit-catalogue-and-progression.md"])
        self.assertIn("charters", p.checks)
        self.assertIn("doc-links", p.checks)
        self.assertNotIn("simtests-selected", p.checks)

    def test_isolated_subsystem_selects_its_tests_only(self):
        p = plan(["Sim.Core/Systems/Migration/MigrationSystem.cs"])
        self.assertIn("build", p.checks)
        self.assertIn("simtests-selected", p.checks)
        self.assertNotIn("simtests-full", p.checks)
        self.assertIn(".Migration", p.terms["sim"])
        for t in p.terms["sim"]:
            self.assertTrue(INDEX.match("sim", t), f"term {t} matches no test class")

    def test_research_content_runs_tree_validators_and_research_tests(self):
        p = plan(["Sim.Data/content/research.json"])
        for c in ("research-audit", "tree-closure", "unlock-audit", "research-calibration-report", "simtests-selected"):
            self.assertIn(c, p.checks)
        self.assertIn("Research", p.terms["sim"])

    def test_shared_core_escalates_to_full_suites_at_integration(self):
        targeted = plan(["Sim.Core/Kernel/Snapshot.cs"])
        self.assertNotIn("simtests-full", targeted.checks)
        self.assertIn("trait:suite=determinism", targeted.terms["sim"])
        self.assertTrue(any("first pass only" in n for n in targeted.notes))
        integration = plan(["Sim.Core/Kernel/Snapshot.cs"], "integration")
        self.assertIn("simtests-full", integration.checks)
        self.assertIn("uitests-full", integration.checks)
        self.assertIn("banned-constructs", integration.checks)

    def test_schema_change_is_shared_core(self):
        p = plan(["Sim.Core/State/WorldState.cs"], "integration")
        self.assertEqual(p.categories["Sim.Core/State/WorldState.cs"], "shared-core")
        self.assertIn("simtests-full", p.checks)

    def test_cross_system_change_combines_categories(self):
        p = plan(["Sim.Core/Systems/Research/ResearchSystem.cs", "Sim.Data/content/goods.json",
                  "Sim.Ui/Screens/ResearchPanel.cs"], "integration")
        cats = set(p.categories.values())
        self.assertEqual(cats, {"research-engine", "goods-content", "ui"})
        self.assertIn("Research", p.terms["sim"])
        self.assertIn(".Goods", p.terms["sim"])
        self.assertIn("uitests-full", p.checks)

    def test_ai_and_fog_changes_pull_leak_checks(self):
        p = plan(["Sim.Core/State/PolityKnowledge.cs"], "integration")
        self.assertIn("Leak", p.terms["sim"])
        self.assertIn("Leak", p.terms["ui"])
        self.assertIn("trait:suite=determinism", p.terms["sim"])

    def test_changed_test_file_runs_its_classes(self):
        rel = next(c.file for c in INDEX.classes if c.project == "sim" and c.file.startswith("Sim.Tests/Systems/"))
        p = plan([rel])
        own = {"cls:" + c.fqn for c in INDEX.classes_in_file(rel)}
        self.assertTrue(own and own <= set(p.terms["sim"]))

    def test_unknown_file_takes_the_conservative_fallback_with_a_reason(self):
        p = plan(["tools/something-new.bin"])
        self.assertEqual(p.categories["tools/something-new.bin"], "unknown")
        self.assertTrue(p.terms["sim"], "fallback must select tests, never nothing")
        self.assertTrue(any("matched no category" in n for n in p.notes))

    def test_no_changes_selects_nothing_and_says_so(self):
        p = plan([])
        self.assertFalse(p.checks)
        self.assertTrue(any("nothing selected" in n for n in p.notes))

    def test_explicit_scope(self):
        p = plan([], "integration", ["construction"])
        self.assertIn(".Construction", " ".join(p.terms["sim"]) + " .Construction")
        self.assertIn("simtests-selected", p.checks)
        with self.assertRaises(SystemExit):
            plan([], "targeted", ["no-such-category"])

    def test_full_mode_selects_everything_regardless_of_changes(self):
        p = plan(["docs/x.md"], "full")
        for c in tv.FULL_LOCAL_CHECKS + tv.FULL_ELSEWHERE_CHECKS:
            self.assertIn(c, p.checks)

    def test_widely_referenced_type_escalates_at_integration(self):
        p = plan(["Sim.Core/State/EmpireQuery.cs"], "integration")
        refs = INDEX.referencing("EmpireQuery")
        if len(refs) > VMAP["referenceInference"]["maxClasses"]:
            self.assertIn("simtests-full", p.checks)
        else:
            self.skipTest("EmpireQuery is not widely referenced in this tree")


class SlowTests(unittest.TestCase):
    SLOW = "Sim.Tests.Systems.MigrationTests.ALongHorizonRun"

    def durations(self):
        return {"slowThresholdSeconds": 15, "slow": {self.SLOW: 400.0}}

    def test_targeted_excludes_recorded_slow_tests_of_selected_classes(self):
        p = tv.select(VMAP, INDEX, ["Sim.Core/Systems/Migration/MigrationSystem.cs"], "targeted", None, self.durations())
        self.assertEqual(p.exclude["sim"], [self.SLOW])
        self.assertIn("FullyQualifiedName!~" + self.SLOW, tv.dotnet_filter(p.terms["sim"], p.exclude["sim"]))
        self.assertTrue(any("slow test" in n for n in p.notes))

    def test_integration_and_include_slow_keep_them(self):
        p = tv.select(VMAP, INDEX, ["Sim.Core/Systems/Migration/MigrationSystem.cs"], "integration", None, self.durations())
        self.assertEqual(p.exclude["sim"], [])
        p = tv.select(VMAP, INDEX, ["Sim.Core/Systems/Migration/MigrationSystem.cs"], "targeted", None, self.durations(),
                      include_slow=True)
        self.assertEqual(p.exclude["sim"], [])

    def test_a_slow_test_in_a_changed_test_file_still_runs(self):
        p = tv.select(VMAP, INDEX, ["Sim.Tests/Systems/MigrationTests.cs"], "targeted", None, self.durations())
        self.assertNotIn(self.SLOW, p.exclude["sim"])

    def test_unrelated_slow_tests_are_not_listed(self):
        p = tv.select(VMAP, INDEX, ["Sim.Core/Systems/Migration/MigrationSystem.cs"], "targeted", None,
                      {"slow": {"Sim.Tests.Kernel.SomethingElseEntirely.Run": 99.0}})
        self.assertEqual(p.exclude["sim"], [])

    def test_trx_parsing_and_recording(self):
        with tempfile.TemporaryDirectory() as d:
            trx = os.path.join(d, "r.trx")
            with open(trx, "w") as fh:
                fh.write('<UnitTestResult testName="A.B.Slow" duration="00:01:40.5000000" />'
                         '<UnitTestResult testName="A.B.Fast" duration="00:00:01.0000000" />'
                         '<UnitTestResult testName="A.B.T(x: 1)" duration="00:00:20.0000000" />')
            got = tv.trx_durations(trx)
            self.assertEqual(got["A.B.Slow"], 100.5)
            self.assertEqual(got["A.B.T"], 20.0)
            os.makedirs(os.path.join(d, "scripts"))
            data = tv.record_durations(d, [trx], "test")
            self.assertEqual(set(data["slow"]), {"A.B.Slow", "A.B.T"})


class MapConsistency(unittest.TestCase):
    def test_every_referenced_check_is_defined(self):
        for cat in VMAP["categories"]:
            for tier in ("targeted", "integration"):
                for cid in cat.get(tier, {}).get("checks", []):
                    self.assertIn(cid, VMAP["checks"], f"{cat['id']}.{tier} names undefined check {cid}")
        for cid in tv.FULL_LOCAL_CHECKS + tv.FULL_ELSEWHERE_CHECKS:
            self.assertIn(cid, VMAP["checks"])

    def test_every_local_check_command_exists(self):
        for cid, chk in VMAP["checks"].items():
            for req in chk.get("requires", []):
                self.assertTrue(os.path.exists(os.path.join(ROOT, req)), f"{cid}: missing {req}")

    def test_every_test_pattern_matches_at_least_one_class(self):
        for cat in VMAP["categories"]:
            for tier in ("targeted", "integration"):
                for proj in ("sim", "ui"):
                    for term in cat.get(tier, {}).get(proj, []):
                        if term.startswith("{"):
                            continue
                        self.assertTrue(INDEX.match(proj, term), f"{cat['id']}.{tier}.{proj}: '{term}' matches nothing")

    def test_every_category_path_pattern_is_reachable(self):
        for cat in VMAP["categories"]:
            self.assertTrue(cat["paths"], cat["id"])

    def test_exactly_one_fallback_and_it_is_last(self):
        fallbacks = [c for c in VMAP["categories"] if c.get("fallback")]
        self.assertEqual(len(fallbacks), 1)
        self.assertIs(VMAP["categories"][-1], fallbacks[0])


class Execution(unittest.TestCase):
    """Status semantics with a throwaway map: failures propagate, missing commands fail, skips are never passes."""

    def run_checks(self, checks, plan_checks, heavy=False):
        vmap = {"checks": checks, "categories": [{"id": "unknown", "paths": ["**"], "fallback": True}]}
        p = tv.Plan("targeted")
        for c in plan_checks:
            p.add_check(c, "test")
        with tempfile.TemporaryDirectory() as d:
            old = tv.repo_root
            os.makedirs(os.path.join(d, "scripts"))
            tv.repo_root = lambda: d
            try:
                return {r.cid: r for r in tv.execute(d, vmap, p, use_cache=False, heavy=heavy, jobs=2)}
            finally:
                tv.repo_root = old

    def test_pass_fail_missing_and_not_run(self):
        res = self.run_checks({
            "ok": {"cmd": ["python3", "-c", "pass"]},
            "bad": {"cmd": ["python3", "-c", "import sys; sys.exit(3)"]},
            "gone": {"cmd": ["python3", "x.py"], "requires": ["x.py"]},
            "nocmd": {"cmd": ["definitely-not-a-command-xyz"]},
            "ci": {"where": "ci", "note": "ci.yml"},
            "manual": {"where": "manual", "note": "checklist"},
            "heavy": {"where": "heavy", "cmd": ["python3", "-c", "pass"]},
        }, ["ok", "bad", "gone", "nocmd", "ci", "manual", "heavy"])
        self.assertEqual(res["ok"].status, "PASS")
        self.assertEqual(res["bad"].status, "FAIL")
        self.assertEqual(res["gone"].status, "MISSING")
        self.assertEqual(res["nocmd"].status, "MISSING")
        for cid in ("ci", "manual", "heavy"):
            self.assertEqual(res[cid].status, "NOT RUN")
            self.assertFalse(res[cid].status.startswith("PASS"))

    def test_heavy_runs_only_when_requested(self):
        res = self.run_checks({"heavy": {"where": "heavy", "cmd": ["python3", "-c", "pass"]}}, ["heavy"], heavy=True)
        self.assertEqual(res["heavy"].status, "PASS")

    def test_machine_lock_excludes_a_second_holder(self):
        import fcntl
        import tempfile as tf
        path = os.path.join(tf.mkdtemp(), "probe.lock")  # never the real machine lock: a live run may hold it
        held = tv.machine_lock(path)
        try:
            other = open(path, "w")
            with self.assertRaises(BlockingIOError):
                fcntl.flock(other, fcntl.LOCK_EX | fcntl.LOCK_NB)
            other.close()
        finally:
            held.close()

    def test_main_exit_codes(self):
        self.assertEqual(tv.main(["--tier", "targeted", "--paths", "docs/x.md", "--plan"]), 0)
        with self.assertRaises(SystemExit):
            tv.main(["--tier", "nonsense"])


class Globbing(unittest.TestCase):
    def test_double_star(self):
        self.assertTrue(tv.path_matches("docs/a/b/c.md", "docs/**"))
        self.assertTrue(tv.path_matches("docs/c.bin", "docs/**/*.bin"))
        self.assertFalse(tv.path_matches("docsx/c.md", "docs/**"))
        self.assertTrue(tv.path_matches("README.md", "*.md"))
        self.assertFalse(tv.path_matches("docs/README.md", "*.md"))

    def test_dotnet_filter_rendering(self):
        f = tv.dotnet_filter({"trait:suite=determinism": [], "cls:Sim.Tests.Kernel.SnapshotTests": [], "Research": []})
        self.assertEqual(f, "FullyQualifiedName~Research|FullyQualifiedName~Sim.Tests.Kernel.SnapshotTests.|suite=determinism")


if __name__ == "__main__":
    unittest.main()
