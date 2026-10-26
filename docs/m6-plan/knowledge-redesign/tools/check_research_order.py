"""Whole-run order invariants over ResearchPacingRunner output (<label>-s<seed>-nodes.csv).

For every completed node at turn t (a levelled repeatable: its FIRST completion):
  1. PREREQUISITES: its authored prerequisite expression (AND / OR / parentheses) holds over the nodes completed at a turn
     strictly before t (availability is read from PREV, ADR-050 §2.2).
  2. AGE SWEEP (ADR-048): a node of content Age a >= 3 has every sweep-cohort node (non-repeatable) of Age a - 2, in BOTH
     trees, completed strictly before t.
An independent check of the engine's eligibility predicate on real 1,630-turn runs: it reads only content and the
completion turns, never the engine.

Usage: python3 -I tools/check_research_order.py <run-dir> <label> [seeds...]   (prints violations; exit 1 if any)
"""
import csv
import json
import re
import sys
from pathlib import Path

RESEARCH = Path(__file__).resolve().parents[4] / "Sim.Data" / "content" / "research.json"


def main(argv):
    run_dir, label = argv[0], argv[1]
    seeds = [int(s) for s in argv[2:]] or [1, 2, 42, 7]
    doc = json.load(open(RESEARCH, encoding="utf-8"))
    nodes = {}
    for tree in ("technologies", "civics"):
        for n in doc[tree]:
            nodes[n["id"]] = n
    ids = set(nodes)
    compiled = {}
    for k, n in nodes.items():
        expr = n.get("prereq") or ""
        py = re.sub(r"\bAND\b", " and ", re.sub(r"\bOR\b", " or ", expr))
        py = re.sub(r"[a-z][a-z0-9_]*", lambda m: f"done({m.group(0)!r})" if m.group(0) in ids else m.group(0), py)
        compiled[k] = compile(py.strip() or "True", k, "eval")
    age = {k: int(n["age"][1]) for k, n in nodes.items()}
    cohort = {}
    for k, n in nodes.items():
        if not n.get("repeatable"):
            cohort.setdefault(age[k], []).append(k)
    bad = 0
    for s in seeds:
        ct = {r["id"]: int(r["completedTurn"]) for r in csv.DictReader(open(Path(run_dir) / f"{label}-s{s}-nodes.csv"))}
        checked = 0
        for k, t in ct.items():
            if t < 0:
                continue
            checked += 1
            done = lambda x: 0 <= ct.get(x, -1) < t
            if not eval(compiled[k], {"done": done}):
                bad += 1
                print(f"seed {s}: {k} completed at {t} before its prerequisites ({nodes[k].get('prereq')})")
            if age[k] >= 3:
                late = [c for c in cohort.get(age[k] - 2, []) if not done(c)]
                if late:
                    bad += 1
                    print(f"seed {s}: {k} (A{age[k]}) completed at {t} before the A{age[k] - 2} cohort: {late[:3]}")
        print(f"seed {s}: {checked} completed nodes checked")
    print("order invariants:", "HOLD" if bad == 0 else f"{bad} VIOLATIONS")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
