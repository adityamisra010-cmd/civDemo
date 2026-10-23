#!/usr/bin/env python3
"""Tests for tools/sourcing_queue.py and tools/unresolved_claims.py (Director review item 4, 2026-10-23).

Every test runs the REAL tool against a temporary copy of the package (KR_PKG) — the tracked files are never written:
  T1 rerunning is idempotent (byte-identical outputs on the second run);
  T2 the tracked outputs are current (a run on an unchanged copy reproduces them byte for byte);
  T3 the tools never write evidence: every corpus/*.json text file and the register are byte-identical after a run;
  T4 plan.json keeps everything but the evidence blocks (including fields the tool does not know);
  T5 the per-node queue matches plan.json: each batch's ids are exactly its corpus file's ids, every node is in one
     batch, blocked = the ids not source-checked, and the status counts add up;
  T6 adding a source (a register URL cited by an unverified entry), or marking a corroborated entry's sources as
     page-read without changing its status, upgrades nothing: statuses, counts and the blocked list are unchanged;
  T7 positive control: an entry whose status really is source-checked leaves the blocked list, counts move by one;
  T8 the unresolved-claims inventory covers every entry, never changes a status and reports 0 may-ship entries.

  python3 -I tools/test_sourcing_queue.py            # run the tests
  python3 -I tools/test_sourcing_queue.py --mutants  # also break the tool five ways; each must be caught
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402

TOOLS = Path(__file__).resolve().parent
QUEUE = TOOLS / "sourcing_queue.py"
UNRESOLVED = TOOLS / "unresolved_claims.py"
OUTPUTS = ("corpus/plan.json", "corpus/sourcing-queue.md")


def workspace(tmp):
    pkg = tmp / "pkg"
    for rel in ("technology-tree-proposal.json", "civics-tree-proposal.json", "sources/register.json"):
        (pkg / rel).parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(kr.PKG / rel, pkg / rel)
    shutil.copytree(kr.PKG / "corpus", pkg / "corpus")
    (pkg / "data").mkdir(parents=True, exist_ok=True)
    return pkg, dict(os.environ, KR_PKG=str(pkg), KR_CONTENT=str(kr.CONTENT))


def run(tool, env):
    p = subprocess.run([sys.executable, "-I", str(tool)], env=env, capture_output=True, text=True)
    if p.returncode != 0:
        raise AssertionError(f"{tool.name} exited {p.returncode}: {p.stderr[-400:]}")


def snapshot(pkg, rels):
    return {r: (pkg / r).read_bytes() for r in rels}


def evidence_files(pkg):
    rels = [f"corpus/{f.name}" for f in sorted((pkg / "corpus").glob("*.json")) if f.name != "plan.json"]
    return rels + ["sources/register.json"]


def entries_by_file(pkg):
    out = {}
    for f in sorted((pkg / "corpus").glob("*.json")):
        if f.name not in ("plan.json", "index.json"):
            out[f.name] = kr.load_json(f)
    return out


def strip_evidence(plan):
    plan = json.loads(json.dumps(plan))
    plan.pop("evidenceDoc", None)
    for b in plan["batches"]:
        b.pop("evidence", None)
    return plan


def status(e):
    return (e.get("factCheck") or {}).get("status", "unverified")


def edit_first(pkg, pred, fn):
    for f in sorted((pkg / "corpus").glob("*.json")):
        if f.name in ("plan.json", "index.json"):
            continue
        data = kr.load_json(f)
        for e in data:
            if pred(e):
                fn(e)
                kr.dump_json(data, f)
                return e["id"]
    raise AssertionError("no entry matches the precondition")


# ------------------------------------------------------------------ tests (each returns None or a failure string)

def t1_idempotent(tool):
    with tempfile.TemporaryDirectory() as d:
        pkg, env = workspace(Path(d))
        run(tool, env)
        first = snapshot(pkg, OUTPUTS)
        run(tool, env)
        second = snapshot(pkg, OUTPUTS)
    diff = [r for r in OUTPUTS if first[r] != second[r]]
    return f"second run changed {diff}" if diff else None


def t2_tracked_current(tool):
    with tempfile.TemporaryDirectory() as d:
        pkg, env = workspace(Path(d))
        run(tool, env)
        diff = [r for r in OUTPUTS if (pkg / r).read_bytes() != (kr.PKG / r).read_bytes()]
    return f"tracked outputs are stale: {diff} (rerun tools/sourcing_queue.py)" if diff else None


def t3_evidence_untouched(tool):
    with tempfile.TemporaryDirectory() as d:
        pkg, env = workspace(Path(d))
        rels = evidence_files(pkg)
        before = snapshot(pkg, rels)
        run(tool, env)
        after = snapshot(pkg, rels)
    diff = [r for r in rels if before[r] != after[r]]
    return f"the tool rewrote evidence files {diff[:5]}" if diff else None


def t4_plan_fields_preserved(tool):
    with tempfile.TemporaryDirectory() as d:
        pkg, env = workspace(Path(d))
        plan = kr.load_json(pkg / "corpus/plan.json")
        plan["reviewerNote"] = "kept"
        plan["batches"][0]["progress"] = {"pass": 3, "owner": "fixture"}
        kr.dump_json(plan, pkg / "corpus/plan.json")
        before = strip_evidence(plan)
        run(tool, env)
        after = strip_evidence(kr.load_json(pkg / "corpus/plan.json"))
    return None if before == after else "plan.json lost or changed a non-evidence field"


def queue_state(pkg):
    plan = kr.load_json(pkg / "corpus/plan.json")
    return {b["file"]: (b["evidence"]["byStatus"], b["evidence"]["blocked"]) for b in plan["batches"]}, plan


def t5_queue_matches_plan(tool):
    with tempfile.TemporaryDirectory() as d:
        pkg, env = workspace(Path(d))
        run(tool, env)
        state, plan = queue_state(pkg)
        files = entries_by_file(pkg)
    problems = []
    seen = {}
    for b in plan["batches"]:
        ids_file = [e["id"] for e in files[b["file"]]]
        if sorted(b["ids"]) != sorted(ids_file):
            problems.append(f"{b['file']}: plan ids != corpus ids")
        if b["count"] != len(ids_file):
            problems.append(f"{b['file']}: count {b['count']} != {len(ids_file)}")
        by_status, blocked = state[b["file"]]
        want_blocked = {e["id"] for e in files[b["file"]] if status(e) != "source-checked"}
        if set(blocked) != want_blocked or len(blocked) != len(want_blocked):
            problems.append(f"{b['file']}: blocked list != entries not source-checked")
        if sum(by_status.values()) != len(ids_file):
            problems.append(f"{b['file']}: status counts {sum(by_status.values())} != {len(ids_file)}")
        for e in files[b["file"]]:
            if by_status.get(status(e)) is None:
                problems.append(f"{b['file']}: status {status(e)} not counted")
        for i in b["ids"]:
            seen[i] = seen.get(i, 0) + 1
    dup = [i for i, c in seen.items() if c != 1]
    if dup:
        problems.append(f"ids in more than one batch: {dup[:5]}")
    if plan["nodeCount"] != len(seen):
        problems.append(f"nodeCount {plan['nodeCount']} != {len(seen)} ids across batches")
    return "; ".join(problems[:4]) or None


def t6_source_alone_upgrades_nothing(tool):
    with tempfile.TemporaryDirectory() as d:
        pkg, env = workspace(Path(d))
        run(tool, env)
        base, _ = queue_state(pkg)
        reg = kr.load_json(pkg / "sources/register.json")
        reg.append({"id": "FIXTURE-URL", "title": "fixture", "url": "https://example.org/a", "type": "academic",
                    "confidence": "high", "retrieval": "web-search-result", "claims_supported": ["x"]})
        a = edit_first(pkg, lambda e: status(e) == "unverified",
                       lambda e: e.setdefault("sources", []).append({"sourceId": "FIXTURE-URL", "claims": ["x"]}))
        # and a corroborated entry whose sources are now recorded as opened, status untouched
        opened = []
        b = edit_first(pkg, lambda e: status(e) == "search-corroborated",
                       lambda e: opened.extend(s["sourceId"] for s in e.get("sources") or []))
        for s in reg:
            if s["id"] in opened:
                s["retrieval"] = "page-read"
        kr.dump_json(reg, pkg / "sources/register.json")
        run(tool, env)
        after, _ = queue_state(pkg)
        statuses = {e["id"]: status(e) for es in entries_by_file(pkg).values() for e in es}
    problems = []
    if after != base:
        problems.append("queue counts or blocked lists moved")
    if statuses[a] != "unverified" or statuses[b] != "search-corroborated":
        problems.append(f"a status changed: {a}={statuses[a]}, {b}={statuses[b]}")
    if not any(a in bl for _, bl in after.values()) or not any(b in bl for _, bl in after.values()):
        problems.append("an entry left the blocked list without becoming source-checked")
    return "; ".join(problems) or None


def t7_source_checked_leaves_queue(tool):
    with tempfile.TemporaryDirectory() as d:
        pkg, env = workspace(Path(d))
        run(tool, env)
        base, _ = queue_state(pkg)
        victim = edit_first(pkg, lambda e: status(e) == "search-corroborated",
                            lambda e: e["factCheck"].__setitem__("status", "source-checked"))
        run(tool, env)
        after, _ = queue_state(pkg)
    moved = [f for f in after if after[f] != base[f]]
    if len(moved) != 1:
        return f"expected exactly one batch to change, got {moved}"
    st, bl = after[moved[0]]
    st0, bl0 = base[moved[0]]
    if victim in bl or len(bl) != len(bl0) - 1:
        return "the source-checked entry is still queued"
    if st["source-checked"] != st0["source-checked"] + 1 or st["search-corroborated"] != st0["search-corroborated"] - 1:
        return "status counts did not move by exactly one"
    return None


def t8_unresolved_inventory(tool):
    with tempfile.TemporaryDirectory() as d:
        pkg, env = workspace(Path(d))
        rels = evidence_files(pkg)
        before = snapshot(pkg, rels)
        run(UNRESOLVED, env)
        first = (pkg / "data/unresolved-claims.json").read_bytes(), (pkg / "corpus/unresolved-claims.md").read_bytes()
        run(UNRESOLVED, env)
        second = (pkg / "data/unresolved-claims.json").read_bytes(), (pkg / "corpus/unresolved-claims.md").read_bytes()
        after = snapshot(pkg, rels)
        inv = json.loads(first[0])
        files = entries_by_file(pkg)
        tracked = ((kr.PKG / "data/unresolved-claims.json").read_bytes(), (kr.PKG / "corpus/unresolved-claims.md").read_bytes())
    problems = []
    if first != second:
        problems.append("inventory not idempotent")
    if before != after:
        problems.append("inventory run rewrote evidence")
    if first != tracked:
        problems.append("tracked inventory is stale (rerun tools/unresolved_claims.py)")
    ids = sorted(e["id"] for es in files.values() for e in es)
    if sorted(r["id"] for r in inv["entries"]) != ids:
        problems.append("inventory does not cover every corpus entry exactly once")
    st = {e["id"]: status(e) for es in files.values() for e in es}
    if any(r["status"] != st[r["id"]] for r in inv["entries"]):
        problems.append("inventory reports a status the entry does not hold")
    if inv["totals"]["mayShip"] != 0 or inv["totals"]["claimsSupportedByReadPage"] != 0:
        problems.append("inventory claims shippable or page-read evidence that does not exist")
    return "; ".join(problems) or None


TESTS = [t1_idempotent, t2_tracked_current, t3_evidence_untouched, t4_plan_fields_preserved, t5_queue_matches_plan,
         t6_source_alone_upgrades_nothing, t7_source_checked_leaves_queue, t8_unresolved_inventory]

# (description, old fragment, new fragment, test that must catch it)
MUTANTS = [
    ("a cited source upgrades an unverified entry",
     'st = Counter((e.get("factCheck") or {}).get("status", "unverified") for e in entries)',
     'st = Counter(("search-corroborated" if e.get("sources") and (e.get("factCheck") or {}).get("status") == "unverified" '
     'else (e.get("factCheck") or {}).get("status", "unverified")) for e in entries)', "t6_source_alone_upgrades_nothing"),
    ("plan.json rebuilt from known fields only",
     "    kr.dump_json(plan, plan_path)",
     '    plan = {"_doc": plan["_doc"], "nodeCount": plan["nodeCount"], "batches": [{k: b[k] for k in ("file", "title", '
     '"lane", "sourcePrefix", "sourcesFile", "ages", "count", "ids", "evidence")} for b in plan["batches"]], '
     '"evidenceDoc": plan["evidenceDoc"]}\n    kr.dump_json(plan, plan_path)', "t4_plan_fields_preserved"),
    ("every entry queued, source-checked or not",
     'blocked = sorted((e for e in entries if (e.get("factCheck") or {}).get("status") != "source-checked"),',
     'blocked = sorted((e for e in entries),', "t7_source_checked_leaves_queue"),
    ("the tool normalizes corpus files while reading them",
     '        entries = kr.load_json(kr.PKG / "corpus" / b["file"])',
     '        entries = kr.load_json(kr.PKG / "corpus" / b["file"])\n'
     '        kr.dump_json(sorted(entries, key=lambda e: e["id"]), kr.PKG / "corpus" / b["file"])', "t3_evidence_untouched"),
    ("evidence appended instead of replaced (grows on every run)",
     '        b["evidence"] = {"byStatus"',
     '        b["evidenceRuns"] = b.get("evidenceRuns", 0) + 1\n        b["evidence"] = {"byStatus"', "t1_idempotent"),
]


def run_mutants():
    src = QUEUE.read_text(encoding="utf-8")
    failures = []
    for desc, old, new, test_name in MUTANTS:
        if src.count(old) != 1:
            failures.append(f"mutant '{desc}' did not apply")
            continue
        with tempfile.TemporaryDirectory(prefix="kr-mut-") as d:
            m = Path(d) / "sourcing_queue.py"
            m.write_text(src.replace(old, new), encoding="utf-8")
            shutil.copy2(TOOLS / "kr_lib.py", Path(d) / "kr_lib.py")
            test = next(t for t in TESTS if t.__name__ == test_name)
            try:
                problem = test(m)
            except AssertionError as ex:
                problem = str(ex)
        print(f"  mutant [{desc}] -> {'KILLED by ' + test_name if problem else 'SURVIVED'}")
        if not problem:
            failures.append(desc)
    return failures


def main(argv):
    bad = []
    for t in TESTS:
        try:
            problem = t(QUEUE)
        except AssertionError as ex:
            problem = str(ex)
        print(("FAIL " if problem else "ok   ") + t.__name__ + (f": {problem}" if problem else ""))
        if problem:
            bad.append(t.__name__)
    if "--mutants" in argv:
        print("mutants:")
        bad += run_mutants()
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
