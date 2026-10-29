"""Mutant tests of the research audits' teeth (M6 closure, D-066 R2: the content/consequence audit).

Each test mutates one input in memory and asserts the SPECIFIC rule that must catch it — a semantic kill, never "the file
went stale". The live content is asserted clean first, so every mutant starts from a passing audit.

  python3 -m unittest scripts/test_research_audits.py
"""
import copy
import importlib.util
import json
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


unlock = _load("unlock_audit", "research-gameplay-unlock-audit.py")
consequence = _load("consequence_audit", "research-consequence-audit.py")
CONTENT = HERE.parent / "Sim.Data" / "content"


def _content(name):
    return json.loads((CONTENT / name).read_text(encoding="utf-8"))


# ======================================================================================================================
# H-1: the unlock audit reads the goods.json `knowledge` gate, and --check fails on any unlinked / unreachable / OPEN row
# ======================================================================================================================

class UnlockAuditKnowledgeGate(unittest.TestCase):
    def build_with(self, goods=None, sim=None):
        real = unlock.load
        overrides = {"goods.json": goods, "sim.json": sim}

        def fake(name):
            return copy.deepcopy(overrides[name]) if overrides.get(name) is not None else real(name)

        unlock.load = fake
        try:
            return unlock.build()
        finally:
            unlock.load = real

    @staticmethod
    def row(text, node):
        return next(l for l in text.splitlines() if l.startswith(f"| `{node}` |"))

    def test_live_content_has_no_blocking_row(self):
        self.assertEqual([], unlock.blocking_rows(unlock.build()))

    def test_mutant_recipe_without_its_knowledge_is_an_unlinked_open_row(self):
        goods = _content("goods.json")
        recipe = next(r for r in goods["recipes"] if r["name"] == "iron-smelting")
        del recipe["knowledge"]
        bad = unlock.blocking_rows(self.build_with(goods=goods))
        self.assertEqual(1, len(bad))
        self.assertIn("`recipe iron-smelting`", bad[0])
        self.assertIn("OPEN", bad[0])

    def test_mutant_check_exits_nonzero_on_a_blocking_row_even_when_the_file_is_current(self):
        goods = _content("goods.json")
        del next(r for r in goods["recipes"] if r["name"] == "steel-converting")["knowledge"]
        text = self.build_with(goods=goods)
        real_build, real_write = unlock.build, unlock.rl.write_or_check
        unlock.build = lambda: text
        unlock.rl.write_or_check = lambda *a, **k: None   # the staleness half passes: only the teeth can fail
        try:
            with self.assertRaises(SystemExit) as cm:
                unlock.main(["x", "--check"])
            self.assertIn("blocking", str(cm.exception.code))
            self.assertIn("steel-converting", str(cm.exception.code))
            unlock.main(["x"])   # writing (no --check) never exits on a blocking row
        finally:
            unlock.build, unlock.rl.write_or_check = real_build, real_write

    def test_recipe_knowledge_makes_its_node_class_B_and_links_it(self):
        text = unlock.build()
        self.assertIn("| B |", self.row(text, "steel_carburized"))
        self.assertIn("`recipe-knowledge:steel-cementation`", self.row(text, "steel_carburized"))

    def test_a_disabled_packet_gates_nothing_and_is_not_unlinked(self):
        goods = _content("goods.json")
        next(p for p in goods["packets"] if p["id"] == "ec1")["enabled"] = False
        text = self.build_with(goods=goods)
        self.assertEqual([], unlock.blocking_rows(text))
        self.assertNotIn("recipe-knowledge:steel-cementation", text)
        self.assertIn("| C |", self.row(text, "steel_carburized"))

    def test_deposit_knowledge_and_assay_gates_are_read(self):
        text = unlock.build()
        self.assertIn("`good-knowledge:coal`", self.row(text, "coke"))
        self.assertIn("`assay`", self.row(text, "ore_prospecting"))
        sim = _content("sim.json")
        del sim["discovery"]["assayRequires"]
        self.assertNotIn("`assay`", self.row(self.build_with(sim=sim), "ore_prospecting"))

    def test_blocking_rows_parser(self):
        ok = "| deferred | `unit.x` | finding | DEFERRED — owner |"
        self.assertEqual([], unlock.blocking_rows(ok))
        for bad in ("| unlinked | `recipe x` | f | OPEN |", "| unreachable | `e` | f | content defect |",
                    "| baseline-claim | `e` | f | OPEN |"):
            self.assertEqual([bad], unlock.blocking_rows(bad))


# ======================================================================================================================
# H-2: the closure-grade consequence audit — one mutant (at least) per rule
# ======================================================================================================================

class ConsequenceAuditRules(unittest.TestCase):
    BASE = consequence.read_inputs()

    def inputs(self):
        return copy.deepcopy(self.BASE)

    @staticmethod
    def node(inp, nid):
        return next(n for n in inp["research"]["technologies"] + inp["research"]["civics"] if n["id"] == nid)

    def findings(self, inp):
        return consequence.audit(inp)[0]

    def assertRule(self, inp, rule, contains=""):
        f = self.findings(inp)
        hits = [x for x in f if x[0] == rule and contains in x[2]]
        self.assertTrue(hits, f"rule {rule} ({contains!r}) did not fire; findings: {f[:5]}")
        return hits

    def test_live_content_passes_every_rule_with_zero_unclassified(self):
        findings, _, text = consequence.audit(self.inputs())
        self.assertEqual([], findings)
        self.assertIn("PASS — zero unclassified items", text)
        self.assertNotIn("| UNCLASSIFIED |", text)

    def test_R1_unregistered_operand(self):
        inp = self.inputs()
        self.node(inp, "fire_making")["effects"]["realized"][0]["operand"] = "farming.noSuchOperand"
        self.assertRule(inp, "R1", "farming.noSuchOperand")
        self.assertRule(inp, "R9", "unclassified")

    def test_R2_operand_not_consumed_in_play(self):
        inp = self.inputs()
        self.node(inp, "fire_making")["effects"]["realized"][0]["operand"] = "roads.baselineKmPerDay"
        self.assertRule(inp, "R2", "roads.baselineKmPerDay")

    def test_R2_level_operand_not_consumed_in_play(self):
        inp = self.inputs()
        self.node(inp, "frontier_crops")["repeatable"]["effects"][0]["operand"] = "roads.baselineKmPerDay"
        self.assertRule(inp, "R2", "level operand")

    def test_R3_mirror_drift_from_the_csharp_registry(self):
        inp = self.inputs()
        inp["registry_cs"] = inp["registry_cs"].replace('"CatchmentSystem"', '"SomeOtherSystem"', 1)
        self.assertRule(inp, "R3")
        inp = self.inputs()
        inp["registry_cs"] = inp["registry_cs"].replace('"no system uses overland travel time yet (CR-025)"', "", 1).replace(
            '"faster overland travel",\n', '"faster overland travel"', 1)
        self.assertRule(inp, "R3")

    def test_R4_future_effect_without_owner_or_with_a_consumed_operand(self):
        inp = self.inputs()
        self.node(inp, "saddle")["effects"]["future"][0]["system"] = ""
        self.assertRule(inp, "R4", "owner")
        inp = self.inputs()
        self.node(inp, "saddle")["effects"]["future"][0]["milestone"] = "someday"
        self.assertRule(inp, "R4", "milestone")
        inp = self.inputs()
        self.node(inp, "saddle")["effects"]["future"][0]["operand"] = "trade.gapClosingFraction"
        self.assertRule(inp, "R4", "consumed in play")

    def test_R5_undeclared_free_text_and_stale_declaration(self):
        inp = self.inputs()
        self.node(inp, "fire_making")["unlocks"]["techniques"].append("Torch making")
        self.assertRule(inp, "R5", "Torch making")
        inp = self.inputs()
        self.node(inp, "knapping_blade")["unlocks"]["applications"] = []
        self.assertRule(inp, "R5", "stale declaration")

    def test_R6_deferred_entity_without_an_owner(self):
        inp = self.inputs()
        inp["charter"] = inp["charter"].replace("| `building.kiln` |", "| `building.kiln_removed` |", 1)
        self.assertRule(inp, "R6", "no owner")
        inp = self.inputs()
        del inp["declarations"]["deferredOwners"]["unit.*"]
        hits = self.assertRule(inp, "R6", "no owner")
        self.assertEqual(23, sum(1 for h in hits if h[1].startswith("unit.")))
        inp = self.inputs()
        inp["declarations"]["deferredOwners"]["activity.no_such"] = {"owner": "x", "target": "M9"}
        self.assertRule(inp, "R6", "stale declaration")

    def test_R7_gate_naming_no_node(self):
        inp = self.inputs()
        inp["sim"]["governance"]["taxationRequires"] = "taxation AND no_such_node"
        self.assertRule(inp, "R7", "no_such_node")
        inp = self.inputs()
        next(r for r in inp["goods"]["recipes"] if r["name"] == "iron-smelting")["knowledge"] = "iron_bloomery AND bogus_node"
        self.assertRule(inp, "R7", "bogus_node")

    def test_R8_DA1_operand_twice_in_one_realized_list(self):
        inp = self.inputs()
        e = self.node(inp, "fire_making")["effects"]["realized"]
        e.append(dict(e[0]))
        self.assertRule(inp, "R8", "DA-1")

    def test_R8_DA2_operand_twice_in_one_level_list(self):
        inp = self.inputs()
        e = self.node(inp, "frontier_crops")["repeatable"]["effects"]
        e.append(dict(e[0]))
        self.assertRule(inp, "R8", "DA-2")

    def test_R8_DA3_future_effect_on_an_operand_the_node_realizes(self):
        inp = self.inputs()
        n = self.node(inp, "wheel_spoked")   # its future effect names roads.baselineKmPerDay
        n["effects"]["realized"] = [{"operand": "roads.baselineKmPerDay", "delta": 0.05}]
        self.assertRule(inp, "R8", "DA-3")

    def test_R8_DA4_entity_unlocked_twice_by_one_node(self):
        inp = self.inputs()
        n = self.node(inp, "composite_bow")
        n["unlocks"]["units"].append(n["unlocks"]["units"][0])
        self.assertRule(inp, "R8", "DA-4")

    def test_R8_DA5_immediate_effect(self):
        inp = self.inputs()
        self.node(inp, "fire_making")["effects"]["immediate"] = [{"kind": "anything"}]
        self.assertRule(inp, "R8", "DA-5")

    def test_R8_DA6_one_consumer_linked_to_two_entities(self):
        inp = self.inputs()
        r = copy.deepcopy(next(x for x in inp["goods"]["recipes"] if x["name"] == "pottery-firing"))
        r["entity"] = "recipe.bronze_casting"
        inp["goods"]["recipes"].append(r)
        self.assertRule(inp, "R8", "DA-6")

    def test_R8_completion_plus_level_stacking_is_recorded_not_failed(self):
        _, info, _ = consequence.audit(self.inputs())
        stacked = sorted(s for r, s, _ in info if r == "R8")
        self.assertEqual(["frontier_climate", "frontier_crops", "frontier_cyber", "frontier_materials", "frontier_medicine",
                          "frontier_military"], stacked)

    def test_R9_entity_list_naming_a_non_entity(self):
        inp = self.inputs()
        self.node(inp, "composite_bow")["unlocks"]["buildings"].append("a stone hut")
        self.assertRule(inp, "R9", "not an entity")

    def test_R10_node_with_no_consequence(self):
        inp = self.inputs()
        n = self.node(inp, "knapping_levallois")
        n["effects"]["realized"] = []
        self.assertRule(inp, "R10")

    def test_check_exits_nonzero_on_a_finding_even_when_the_file_is_current(self):
        inp = self.inputs()
        self.node(inp, "fire_making")["effects"]["realized"][0]["operand"] = "roads.baselineKmPerDay"
        real_read, real_write = consequence.read_inputs, consequence.rl.write_or_check
        consequence.read_inputs = lambda: inp
        consequence.rl.write_or_check = lambda *a, **k: None
        try:
            with self.assertRaises(SystemExit) as cm:
                consequence.main(["x", "--check"])
            self.assertIn("R2", str(cm.exception.code))
        finally:
            consequence.read_inputs, consequence.rl.write_or_check = real_read, real_write


if __name__ == "__main__":
    unittest.main()
