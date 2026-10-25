"""Decision table for every node the cost screen flags (D-064 review package; nothing is applied by this tool).

Reads research-cost-pass/baseline-screen.csv (tools/cost_audit.py), the live-versus-placeholder map (--live), the
government packages (data/government-packages.json) and the reference trajectory (data/tranche2/reference-trajectory-
final.txt: RP and CP per turn by turn, seed 42). Writes research-cost-pass/decision-table.csv and .md: one row per
flagged node with its current cost, the screen's proposed disposition and factor change, its live gameplay effect, its
placeholder content and the future system that will realize it, its gate / milestone / dependency role, and the expected
impact in turns at the rate it is typically researched, with overflow OFF (a node never takes less than one turn) and ON.

Classes:
  FUTURE-DEPENDENT  its main value is a system a later milestone realizes (M8 government forms and their modules; M7 battle
                    units), whatever stand-in operand effect it carries today. The screen's reduction is WITHHELD: its
                    price stands for the future system (recorded, not hidden).
  PLACEHOLDER-ONLY  no live value and no future system named: a genuine low-value node; a reduction is a candidate.
  LOW-VALUE         live but weak for its price inside its (tree, Age) cohort: a reduction is a candidate.
  UNDER-PRICED      top live power, bottom price inside its cohort: an increase is a candidate.
Usage: python3 -I tools/decision_table.py --live live-map.json
"""
import csv
import json
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402

OUT = kr.PKG / "research-cost-pass"


def trajectory():
    rows = []
    for line in open(kr.PKG / "data" / "tranche2" / "reference-trajectory-final.txt", encoding="utf-8"):
        if re.match(r"\s*seed (\d+):", line):
            seed = int(re.match(r"\s*seed (\d+):", line).group(1))
            continue
        m = re.match(r"\s*(\d+) \| ([\d.-]+) \| ([\d.]+) \| (\d+) \| ([\d.]+) \| ([\d.]+) \| (\S+) \|", line)
        if m and seed == 42:
            rows.append((int(m.group(1)), float(m.group(5)), float(m.group(6)), m.group(7)))
    return rows


def rate_at_age(rows, age, col):
    """Mean RP (col 1) or CP (col 2) per turn over the turns the reference polity spent in polity Age `age`."""
    vals = [r[col] for r in rows if r[3] == str(age)]
    return sum(vals) / len(vals) if vals else None


def main(argv):
    live_path = Path(argv[argv.index("--live") + 1]) if "--live" in argv else None
    live = kr.load_json(live_path) if live_path and live_path.exists() else {}
    gp = kr.load_json(kr.PKG / "data" / "government-packages.json")
    forms = set(gp["forms"])
    modules = {m for f in gp["forms"].values() for m in f.get("modules", [])}
    units = {u for u in (live.get("placeholderKinds") or [])}
    rows = list(csv.DictReader(open(OUT / "baseline-screen.csv", encoding="utf-8")))
    traj = trajectory()
    out = []
    for r in rows:
        if r["screen"] in ("keep", "repeatable"):
            continue
        age, tree = int(r["age"]), r["tree"]
        value = float(r["value"])
        live_effects = r["effects"] and "NOT LIVE" not in r["effects"]
        placeholder = r["placeholderUnlocks"].split()
        future = None
        if r["id"] in forms:
            future = "M8 government adoption (a government form)"
        elif r["id"] in modules:
            future = "M8 government adoption (a module of a government form)"
        elif placeholder and all(p.startswith("unit.") for p in placeholder):
            future = "M7 battle layer (unit unlocks)"
        if r["screen"] == "reduce" and future:
            cls = "FUTURE-DEPENDENT"   # its main value is a later system, whatever stand-in effect it carries today
        elif r["screen"] == "reduce" and not live_effects and not r["liveUnlocks"] and r["liveGate"] != "True":
            cls = "PLACEHOLDER-ONLY"
        elif r["screen"] == "reduce":
            cls = "LOW-VALUE"
        elif r["screen"] == "increase":
            cls = "UNDER-PRICED"
        else:
            cls = r["screen"].upper()
        step = float(r["screenStep"])
        if cls == "FUTURE-DEPENDENT":
            proposed_step, disposition = 0.0, "hold (future-dependent: priced for the system that will realize it)"
        else:
            proposed_step, disposition = step, r["screen"]
        base = float(r["baseCost"])
        new = base * (2.0 ** proposed_step)
        rate = rate_at_age(traj, max(1, age - 1) if age > 1 else 1, 1 if tree == "technology" else 2)
        if rate:
            off_before, off_after = max(1, math.ceil(base / rate)), max(1, math.ceil(new / rate))
            on_before, on_after = base / rate, new / rate
        else:
            off_before = off_after = on_before = on_after = None
        out.append({
            "id": r["id"], "tree": tree, "age": age, "class": cls, "disposition": disposition,
            "contentCost": r["contentCost"], "baseCost": round(base, 1), "magnitude": r["magnitude"],
            "factorStep": proposed_step, "proposedBaseCost": round(new, 1),
            "changePct": round(100.0 * (2.0 ** proposed_step - 1.0), 1),
            "liveEffect": r["effects"] or "-", "liveUnlocks": r["liveUnlocks"] or "-",
            "placeholderUnlocks": r["placeholderUnlocks"] or "-", "futureSystem": future or "-",
            "role": "; ".join(x for x in [
                ("milestone " + r["milestone"]) if r["milestone"] else "",
                "live gate" if r["liveGate"] == "True" else "", "stage" if r["stage"] == "True" else "",
                f"{r['directDependents']} direct / {r['allDescendants']} total dependents"] if x),
            "crossTreeAtoms": r["crossTreeAtoms"], "sweepBurden": r["sweepBurdenBaseCost"],
            "refRatePerTurn": round(rate, 1) if rate else "-",
            "turnsOffBefore": off_before, "turnsOffAfter": off_after,
            "turnsOnBefore": round(on_before, 2) if on_before is not None else "-",
            "turnsOnAfter": round(on_after, 2) if on_after is not None else "-",
            "reason": r["screenReason"],
        })
    with open(OUT / "decision-table.csv", "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=list(out[0].keys()))
        w.writeheader()
        for o in out:
            w.writerow(o)
    from collections import Counter
    c = Counter(o["class"] for o in out)
    print(dict(c), len(out))


if __name__ == "__main__":
    main(sys.argv[1:])
