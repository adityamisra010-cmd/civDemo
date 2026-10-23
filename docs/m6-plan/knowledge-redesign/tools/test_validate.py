#!/usr/bin/env python3
"""Adversarial fixtures for tools/validate.py (Director review item 1, 2026-10-23).

Each fixture copies the inputs the validator reads (the two proposal JSONs, data/current-tree-inventory.json,
corpus/*.json, sources/register.json and, when a fixture edits content, Sim.Data/content) into a temporary
directory, applies ONE deliberate defect, runs the REAL tools/validate.py against the copy (KR_PKG / KR_CONTENT) and
checks that it fails FOR THE INTENDED REASON: exit 1, the intended check code among the ERROR rows with the expected
text, and no ERROR from any other check (so a fixture cannot pass by tripping something incidental). Valid fixtures
must exit 0 with no ERROR. The tracked package is never written.

  python3 -I tools/test_validate.py            # run every fixture
  python3 -I tools/test_validate.py --mutants  # also prove each check has teeth: disable it, the fixture must miss
Exit 1 if any fixture (or, with --mutants, any mutant) misbehaves.
"""
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402

TOOLS = Path(__file__).resolve().parent
VALIDATE = TOOLS / "validate.py"
ROW = re.compile(r"^\| (ERROR|WARN|INFO) \| (\w+) \| (.*) \|$")


# ---------------------------------------------------------------- workspace

def make_workspace(tmp, content=False):
    pkg = tmp / "pkg"
    for rel in ("technology-tree-proposal.json", "civics-tree-proposal.json", "data/current-tree-inventory.json",
                "sources/register.json"):
        (pkg / rel).parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(kr.PKG / rel, pkg / rel)
    (pkg / "corpus").mkdir(parents=True, exist_ok=True)
    for f in (kr.PKG / "corpus").glob("*.json"):
        shutil.copy2(f, pkg / "corpus" / f.name)
    env = dict(os.environ, KR_PKG=str(pkg))
    if content:
        cdir = tmp / "content"
        shutil.copytree(kr.CONTENT, cdir)
        env["KR_CONTENT"] = str(cdir)
    return pkg, env


def run_validator(pkg, env, corpus, validator=VALIDATE):
    args = [sys.executable, "-I", str(validator)] + ([] if corpus else ["--no-corpus"])
    proc = subprocess.run(args, env=env, capture_output=True, text=True)
    rows = []
    for line in (pkg / "validation-report.md").read_text(encoding="utf-8").splitlines():
        m = ROW.match(line)
        if m:
            rows.append(m.groups())
    return proc.returncode, rows, proc.stdout + proc.stderr


# ---------------------------------------------------------------- mutation helpers

def edit_json(path, fn):
    data = kr.load_json(path)
    fn(data)
    kr.dump_json(data, path)


def node(doc, nid):
    return next(n for n in doc["nodes"] if n["id"] == nid)


def corpus_entries(pkg):
    for f in sorted((pkg / "corpus").glob("*.json")):
        if f.name in ("index.json", "plan.json"):
            continue
        yield f


def edit_first_entry(pkg, pred, fn):
    """Apply fn to the first corpus entry (file order, then list order) matching pred; return its id."""
    for f in corpus_entries(pkg):
        data = kr.load_json(f)
        for e in data:
            if pred(e):
                fn(e)
                kr.dump_json(data, f)
                return e["id"]
    raise AssertionError("no corpus entry matches the fixture precondition")


def status(e):
    return (e.get("factCheck") or {}).get("status")


def first_optional_micro_civic(pkg):
    civ = kr.load_json(pkg / "civics-tree-proposal.json")
    return next(n["id"] for n in civ["nodes"] if n["status"] == "new" and n["civic"]["category"] == "micro"
                and n["civic"]["required"] is False)


def first_regime_form(pkg):
    civ = kr.load_json(pkg / "civics-tree-proposal.json")
    return next(n["id"] for n in civ["nodes"] if n["status"] == "new" and n["civic"]["category"] == "macro")


def first_case_study_node(pkg):
    tech = kr.load_json(pkg / "technology-tree-proposal.json")
    return next(n["id"] for n in tech["nodes"] if n["status"] == "new" and n.get("lane", "").startswith("D11")
                and n.get("branch") != "trunk")


def append_to_stage(pkg, atom):
    edit_json(pkg / "technology-tree-proposal.json",
              lambda t: t["researchStage"].__setitem__("requires", t["researchStage"]["requires"] + " AND " + atom))
    return atom


def set_age_fact(pkg, milestone, nodes):
    def fn(t):
        f = next(f for f in t["ageFacts"] if f["milestone"] == milestone)
        f["fact"]["nodes"] = nodes
    edit_json(pkg / "technology-tree-proposal.json", fn)


# ---------------------------------------------------------------- fixtures
# (name, needs content copy, corpus run, mutate(pkg, env) -> context, expect)
# expect: None = valid fixture (exit 0, no ERROR); else (code, regex over the ERROR text, allowed ERROR codes)

def fx_legacy_civic_renumbered(pkg, env):
    edit_json(pkg / "civics-tree-proposal.json", lambda c: node(c, "law_code").__setitem__("key", 1500))
    return "law_code"


def fx_legacy_tech_renumbered(pkg, env):
    inv = kr.load_json(pkg / "data/current-tree-inventory.json")
    victim = next(n["id"] for n in inv["nodes"] if n["key"] < 427)
    edit_json(pkg / "technology-tree-proposal.json", lambda t: node(t, victim).__setitem__("key", 998))
    return victim


def fx_new_civic_below_1008(pkg, env):
    civ = kr.load_json(pkg / "civics-tree-proposal.json")
    victim = next(n["id"] for n in civ["nodes"] if n["status"] == "new")
    edit_json(pkg / "civics-tree-proposal.json", lambda c: node(c, victim).__setitem__("key", 1000))
    return victim


def fx_optional_civic_in_stage(pkg, env):
    return append_to_stage(pkg, first_optional_micro_civic(pkg))


def fx_regime_form_in_stage(pkg, env):
    return append_to_stage(pkg, first_regime_form(pkg))


def fx_case_study_as_age_core(pkg, env):
    # an Age CORE fact, not the research stage: every case-study node is a subtree node, and the stage refuses
    # subtree atoms on its own (V06), which would let the fixture pass for the wrong reason
    victim = first_case_study_node(pkg)
    set_age_fact(pkg, "a5_coinage", [victim])
    return victim


def fx_optional_civic_as_age_core(pkg, env):
    victim = first_optional_micro_civic(pkg)
    set_age_fact(pkg, "a5_systematic_law", [victim])
    return victim


def fx_optional_civic_in_tax_gate(pkg, env):
    victim = first_optional_micro_civic(pkg)
    edit_json(Path(env["KR_CONTENT"]) / "sim.json",
              lambda s: s["governance"].__setitem__("taxationRequires", s["governance"]["taxationRequires"] + " AND " + victim))
    return victim


def fx_optional_civic_in_university_gate(pkg, env):
    victim = first_optional_micro_civic(pkg)

    def fn(r):
        e = next(e for e in r["entities"] if e["id"] == "inst.university")
        e["requires"] = e["requires"] + " AND " + victim
    edit_json(Path(env["KR_CONTENT"]) / "research.json", fn)
    return victim


def fx_regime_form_in_supporting_fact(pkg, env):
    victim = first_regime_form(pkg)
    tech = kr.load_json(pkg / "technology-tree-proposal.json")
    fact = next(f for f in tech["ageFacts"] if f["milestone"] == "a3_law")
    set_age_fact(pkg, "a3_law", fact["fact"]["nodes"] + [victim])
    return victim


def fx_regime_form_in_core_fact(pkg, env):
    victim = first_regime_form(pkg)
    set_age_fact(pkg, "a3_bronze", ["arsenical_bronze", "tin_bronze", victim])
    return victim


def fx_corroborated_promoted_to_source_checked(pkg, env):
    return edit_first_entry(pkg, lambda e: status(e) == "search-corroborated",
                            lambda e: e["factCheck"].__setitem__("status", "source-checked"))


def fx_corroborated_with_unsupported_claim(pkg, env):
    def fn(e):
        e["factCheck"]["claims"][0]["result"] = "unsupported"
    return edit_first_entry(pkg, lambda e: status(e) == "search-corroborated" and e["factCheck"].get("claims"), fn)


def fx_corroborated_on_low_confidence_only(pkg, env):
    # the first corroborated entry whose sources are cited by no other entry, so only it can change
    reg = kr.load_json(pkg / "sources/register.json")
    used = {}
    for f in corpus_entries(pkg):
        for e in kr.load_json(f):
            for s in e.get("sources") or []:
                used.setdefault(s["sourceId"], set()).add(e["id"])
    ids = set()

    def pred(e):
        srcs = [s["sourceId"] for s in e.get("sources") or []]
        return status(e) == "search-corroborated" and srcs and all(used[s] == {e["id"]} for s in srcs)

    def fn(e):
        ids.update(s["sourceId"] for s in e["sources"])
    victim = edit_first_entry(pkg, pred, fn)
    for s in reg:
        if s["id"] in ids:
            s["confidence"] = "low"
    kr.dump_json(reg, pkg / "sources/register.json")
    return victim


def fx_unsourced_but_partially_checked(pkg, env):
    return edit_first_entry(pkg, lambda e: status(e) == "unverified" and not e.get("sources"),
                            lambda e: e["factCheck"].__setitem__("status", "partially-checked"))


def fx_partially_checked_with_nothing_supported(pkg, env):
    def fn(e):
        for c in e["factCheck"]["claims"]:
            c["result"] = "unsupported"
    return edit_first_entry(pkg, lambda e: status(e) == "partially-checked" and e["factCheck"].get("claims"), fn)


def fx_source_url_added_to_unverified(pkg, env):
    """A source URL appearing in the register must not by itself make an unverified entry pass as checked:
    the entry cites it, but its status stays 'unverified' and it has no claims — valid, still blocked (C10)."""
    reg = kr.load_json(pkg / "sources/register.json")
    reg.append({"id": "FIXTURE-001", "title": "fixture", "url": "https://example.org/fixture", "type": "academic",
                "confidence": "high", "retrieval": "web-search-result", "claims_supported": []})
    kr.dump_json(reg, pkg / "sources/register.json")
    return edit_first_entry(pkg, lambda e: status(e) == "unverified" and not e.get("sources"),
                            lambda e: e.__setitem__("sources", [{"sourceId": "FIXTURE-001", "claims": ["fixture"]}]))


# valid fixtures --------------------------------------------------------------

def fx_unchanged(pkg, env):
    return None


def fx_honest_downgrade(pkg, env):
    return edit_first_entry(pkg, lambda e: status(e) == "search-corroborated",
                            lambda e: e["factCheck"].__setitem__("status", "partially-checked"))


def fx_disputed_claim_in_corroborated(pkg, env):
    def fn(e):
        e["factCheck"]["claims"][0]["result"] = "disputed"
    return edit_first_entry(pkg, lambda e: status(e) == "search-corroborated" and e["factCheck"].get("claims"), fn)


def fx_page_read_source_checked(pkg, env):
    """The ONLY legitimate upgrade path: every cited source read as a page, every claim supported."""
    reg = {s["id"]: s for s in kr.load_json(pkg / "sources/register.json")}
    ids = set()

    def pred(e):
        claims = e["factCheck"].get("claims") or []
        return status(e) == "search-corroborated" and claims and all(c.get("result") == "supported" for c in claims)

    def fn(e):
        e["factCheck"]["status"] = "source-checked"
        ids.update(s["sourceId"] for s in e["sources"])
    victim = edit_first_entry(pkg, pred, fn)
    for i in ids:
        reg[i]["retrieval"] = "page-read"
    kr.dump_json(list(reg.values()), pkg / "sources/register.json")
    return victim


def fx_required_node_in_tax_gate(pkg, env):
    edit_json(Path(env["KR_CONTENT"]) / "sim.json",
              lambda s: s["governance"].__setitem__("taxationRequires", s["governance"]["taxationRequires"] + " AND token_counting"))
    return "token_counting"


def fx_optional_on_one_or_branch(pkg, env):
    """An Age research fact holds when ANY listed node is complete: a case-study node offered as one alternative
    of a3_bronze beside arsenical_bronze is not required, so it must not be reported as mandatory."""
    victim = first_case_study_node(pkg)
    set_age_fact(pkg, "a3_bronze", ["arsenical_bronze", victim])
    return victim


FIXTURES = [
    # name, content, corpus, mutate, expect
    ("legacy civic renumbered", False, False, fx_legacy_civic_renumbered, ("V23", r"legacy civic {id} key", {"V14", "V23"})),
    ("legacy civic renumbered (V14)", False, False, fx_legacy_civic_renumbered, ("V14", r"{id} changed key", {"V14", "V23"})),
    ("legacy technology renumbered", False, False, fx_legacy_tech_renumbered, ("V14", r"{id} changed key", {"V14"})),
    ("new civic keyed below 1008", False, False, fx_new_civic_below_1008, ("V23", r"new civic {id} has key 1000", {"V23"})),
    ("optional civic required by the research stage", False, False, fx_optional_civic_in_stage,
     ("V24", r"civic {id} is declared optional", {"V24"})),
    ("regime form required by the research stage", False, False, fx_regime_form_in_stage,
     ("V24", r"regime form {id} is in the mandatory closure", {"V24"})),
    ("case-study node as an Age CORE fact", False, False, fx_case_study_as_age_core,
     ("V24", r"case-study node {id} ", {"V24"})),
    ("optional civic as an Age CORE fact", False, False, fx_optional_civic_as_age_core,
     ("V24", r"civic {id} is declared optional", {"V24"})),
    ("optional civic in the tax edict gate (sim.json)", True, False, fx_optional_civic_in_tax_gate,
     ("V24", r"civic {id} is declared optional", {"V24"})),
    ("optional civic in the university gate (research.json)", True, False, fx_optional_civic_in_university_gate,
     ("V24", r"civic {id} is declared optional", {"V24"})),
    ("regime form in an Age SUPPORTING fact", False, False, fx_regime_form_in_supporting_fact,
     ("V25", r"Age fact a3_law names regime form {id}", {"V25"})),
    ("regime form in an Age CORE fact", False, False, fx_regime_form_in_core_fact,
     ("V25", r"Age fact a3_bronze names regime form {id}", {"V07", "V25"})),
    ("search-corroborated relabelled source-checked", False, True, fx_corroborated_promoted_to_source_checked,
     ("C11", r"{id}: 'source-checked' but no cited source was read", {"C11"})),
    ("search-corroborated with an unsupported claim", False, True, fx_corroborated_with_unsupported_claim,
     ("C11", r"{id}: 'search-corroborated' but a listed claim is", {"C11"})),
    ("search-corroborated on low-confidence sources only", False, True, fx_corroborated_on_low_confidence_only,
     ("C11", r"{id}: 'search-corroborated' rests only on low-confidence", {"C11"})),
    ("no sources but partially-checked", False, True, fx_unsourced_but_partially_checked,
     ("C03", r"{id}: no sources but status is not 'unverified'", {"C03"})),
    ("partially-checked with no supported claim", False, True, fx_partially_checked_with_nothing_supported,
     ("C11", r"{id}: 'partially-checked' but no listed claim is supported", {"C11"})),
    # valid
    ("VALID unchanged copy", False, True, fx_unchanged, None),
    ("VALID honest downgrade corroborated -> partially-checked", False, True, fx_honest_downgrade, None),
    ("VALID disputed claim inside search-corroborated", False, True, fx_disputed_claim_in_corroborated, None),
    ("VALID page-read sources + source-checked (ships)", False, True, fx_page_read_source_checked, None),
    ("VALID required technology added to the tax gate", True, False, fx_required_node_in_tax_gate, None),
    ("VALID source added to an unverified entry (stays blocked)", False, True, fx_source_url_added_to_unverified, None),
    ("VALID optional node as one OR branch of an Age CORE fact", False, False, fx_optional_on_one_or_branch, None),
]

# extra assertions on valid fixtures: (name -> fn(rows, ctx) -> error string or None)


def ship_count(rows):
    for lvl, code, msg in rows:
        if code == "C10" and msg.startswith("SHIPPING GATE"):
            return int(re.match(r"SHIPPING GATE: (\d+) of", msg).group(1))
    return None


EXTRA = {
    "VALID page-read sources + source-checked (ships)": lambda rows, ctx: None if ship_count(rows) == 1 else
    f"expected exactly 1 entry past the shipping gate, got {ship_count(rows)}",
    "VALID source added to an unverified entry (stays blocked)": lambda rows, ctx: None if ship_count(rows) == 0 else
    f"a source alone moved {ship_count(rows)} entries past the shipping gate",
    "VALID unchanged copy": lambda rows, ctx: None if ship_count(rows) == 0 else
    f"the tracked corpus has {ship_count(rows)} entries past the shipping gate (expected 0)",
}


def run_fixture(fx, validator=VALIDATE):
    name, content, corpus, mutate, expect = fx
    with tempfile.TemporaryDirectory(prefix="kr-fx-") as d:
        pkg, env = make_workspace(Path(d), content)
        ctx = mutate(pkg, env)
        rc, rows, out = run_validator(pkg, env, corpus, validator)
    errors = [(c, m) for lvl, c, m in rows if lvl == "ERROR"]
    if expect is None:
        problem = None
        if rc != 0 or errors:
            problem = f"valid fixture rejected (exit {rc}): {errors[:3]}"
        elif name in EXTRA:
            problem = EXTRA[name](rows, ctx)
        return problem, rows
    code, pattern, allowed = expect
    rx = re.compile(pattern.format(id=re.escape(ctx or "")))
    if rc != 1:
        return f"expected exit 1, got {rc}", rows
    if not any(c == code and rx.search(m) for c, m in errors):
        return f"no {code} ERROR matching /{rx.pattern}/; ERRORs: {errors[:4]}", rows
    stray = sorted({c for c, _ in errors} - allowed)
    if stray:
        return f"fails for an unintended reason too: {stray} {[(c, m) for c, m in errors if c in stray][:3]}", rows
    return None, rows


# mutants: (fixture name, check disabled by replacing this exact source line fragment)
MUTANTS = [
    ("legacy civic renumbered", 'rep.add("ERROR", "V23", f"legacy civic', 'rep.add("INFO", "V23", f"legacy civic'),
    ("legacy technology renumbered", 'rep.add("ERROR", "V14", f"existing node {i} changed key', 'rep.add("INFO", "V14", f"existing node {i} changed key'),
    ("new civic keyed below 1008", 'rep.add("ERROR", "V23", f"new civic', 'rep.add("INFO", "V23", f"new civic'),
    ("optional civic required by the research stage", 'is declared optional (civic.required false)', None),
    # the pre-2026-10-23 literals: a gate edited in sim.json is invisible to them
    ("optional civic in the tax edict gate (sim.json)", "live = kr.live_gates(tech)",
     'live = [("tax", "taxation"), ("university", "library AND legal_code_roman")]'),
    # the pre-2026-10-23 union over an Age fact's alternatives
    ("VALID optional node as one OR branch of an Age CORE fact", "mandatory |= set.intersection(*alts)",
     "mandatory |= set.union(*alts)"),
    ("optional civic in the university gate (research.json)", "live = kr.live_gates(tech)", "live = live_gates_tax_only(tech)"),
    ("case-study node as an Age CORE fact", 'rep.add("ERROR", "V24", f"case-study node', 'rep.add("WARN", "V24", f"case-study node'),
    ("regime form in an Age SUPPORTING fact", 'rep.add("ERROR", "V25"', 'rep.add("INFO", "V25"'),
    ("search-corroborated relabelled source-checked", "if st == \"source-checked\" and not (retrievals & PAGE_READ):", "if False:"),
    ("search-corroborated with an unsupported claim", "if st == \"search-corroborated\" and results and any(", "if False and any("),
    ("search-corroborated on low-confidence sources only", "if st == \"search-corroborated\" and srcs and confs <= {\"low\", None}:", "if False:"),
    ("no sources but partially-checked", 'rep.add("ERROR", "C03", f"{e[\'id\']}: no sources', 'rep.add("WARN", "C03", f"{e[\'id\']}: no sources'),
    ("partially-checked with no supported claim", "if st == \"partially-checked\" and results and not any(", "if False and not any("),
]

TAX_ONLY = '''
def live_gates_tax_only(tech):
    return [g for g in kr.live_gates(tech) if g[0].startswith("tax")]
'''


def run_mutants():
    src = VALIDATE.read_text(encoding="utf-8")
    failures = []
    for fx_name, old, new in MUTANTS:
        fx = next(f for f in FIXTURES if f[0] == fx_name)
        if new is None:  # disable the whole optional-civic loop body
            mutated = src.replace("for i in sorted((mandatory & optional_civ) - macro):",
                                  "for i in sorted(set()):")
        else:
            mutated = src.replace(old, new)
            if new.startswith("live = live_gates_tax_only"):
                mutated = mutated.replace("\ndef check_gates(", TAX_ONLY + "\n\ndef check_gates(")
        if mutated == src:
            failures.append(f"mutant for '{fx_name}' did not apply")
            continue
        with tempfile.TemporaryDirectory(prefix="kr-mut-") as d:
            mpath = Path(d) / "validate.py"
            mpath.write_text(mutated, encoding="utf-8")
            shutil.copy2(TOOLS / "kr_lib.py", Path(d) / "kr_lib.py")
            # the mutant's kr_lib resolves ROOT from its own path: pin content explicitly
            os.environ.setdefault("KR_CONTENT", str(kr.CONTENT))
            problem, _ = run_fixture(fx, mpath)
        verdict = "KILLED" if problem else "SURVIVED"
        print(f"  mutant [{fx_name}] -> {verdict}" + (f" ({problem[:90]})" if problem else ""))
        if not problem:
            failures.append(f"mutant for '{fx_name}' survived")
    return failures


def main(argv):
    bad = []
    for fx in FIXTURES:
        problem, _ = run_fixture(fx)
        print(("FAIL " if problem else "ok   ") + fx[0] + (f": {problem}" if problem else ""))
        if problem:
            bad.append(fx[0])
    if "--mutants" in argv:
        print("mutants (each disables the check its fixture targets; the fixture must then misbehave):")
        bad += run_mutants()
    print(f"{len(FIXTURES) - len([b for b in bad if b in {f[0] for f in FIXTURES}])}/{len(FIXTURES)} fixtures behave as intended"
          + ("" if "--mutants" not in argv else f"; {len(MUTANTS)} mutants run"))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
