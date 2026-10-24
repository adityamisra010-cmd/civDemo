"""Per-(tree, Age) pacing scale for research costs (D-063 R6, tranche2-contract.md §5).

From a reference empire trajectory (Sim.Tests PacingReferenceMeasurement output: every 10 turns the AI's RP per turn
and CP per turn), the cumulative RP R(T) and CP C(T) are integrated. For target Age entry turns E[a] (a = 2..9), the
content of Ages 1..x in each tree is sized so that it is all researchable at the reference rate by the midpoint of
E[x+1] and E[x+2] (the Age sweep requires it by E[x+2]), times a utilization u < 1 (slack for partial progress and
next-Age nodes):

    cumulative_target[tree][x] = u x Rate_tree(midpoint(E[x+1], E[x+2]))

Both trees use the SAME turns, so at the reference trajectory the two trees finish each Age in the same turns (the
"pincer"). The scale of (tree, Age x) = (cumulative_target[x] - cumulative_target[x-1]) / rubric_total[tree][x]; node
costs keep their rubric magnitudes (content cost = U x K^m) and the engine applies BaseCost = cost x scale
(research.json tuning.pacingScale). Writes the scale into research.json and a report.

Usage: python3 -I tools/calibrate_pacing.py <trajectory.txt> [--seed 42] [--write]
"""
import json
import re
import sys
from pathlib import Path

PKG = Path(__file__).resolve().parent.parent
REPO = PKG.parents[2]
RESEARCH = REPO / "Sim.Data" / "content" / "research.json"

# Target Age entry turns (no slower than the measured pre-tranche AI: A3 ~470, A4 ~700, A5 ~800, A6 ~880, A7 ~1150-1200,
# A8 ~1400 on one seed of four, A9 never). A9 is entered at ~1400 and its finite content is researchable by ~1520, so the
# last ~110 turns of the 1,630 go to the repeatables (E[10] = E[11] = 1520: "Age 10" is the repeatable era).
ENTRY = {2: 80, 3: 400, 4: 620, 5: 760, 6: 880, 7: 1060, 8: 1250, 9: 1400, 10: 1520, 11: 1520}
UTIL = 0.85


def parse(path, seed):
    rows, cur = [], None
    for line in open(path, encoding="utf-8"):
        m = re.match(r"\s*seed (\d+):", line)
        if m:
            cur = int(m.group(1))
            continue
        m = re.match(r"\s*(\d+) \| ([\d.]+) \| ([\d.]+) \| (\d+) \| ([\d.]+) \| ([\d.]+) \|", line)
        if m and cur == seed:
            rows.append((int(m.group(1)), float(m.group(5)), float(m.group(6))))
    return rows


def cumulative(rows, turn, col):
    total, prev = 0.0, 0
    for t, rp, cp in rows:
        if t > turn:
            break
        total += (rp if col == "rp" else cp) * (t - prev)
        prev = t
    return total


def main(argv):
    path = argv[0]
    seed = int(argv[argv.index("--seed") + 1]) if "--seed" in argv else 42
    rows = parse(path, seed)
    if not rows:
        raise SystemExit(f"no rows for seed {seed}")
    d = json.load(open(RESEARCH, encoding="utf-8"))
    rubric = {"technology": [0.0] * 10, "civics": [0.0] * 10}
    for tree, arr in (("technology", d["technologies"]), ("civics", d["civics"])):
        for n in arr:
            if isinstance(n.get("repeatable"), dict):
                continue
            rubric[tree][int(n["age"][1])] += n["cost"]
    scale, report = {"technology": [], "civics": []}, []
    for tree, col in (("technology", "rp"), ("civics", "cp")):
        prev_target = 0.0
        for x in range(1, 10):
            mid = (ENTRY[x + 1] + ENTRY[x + 2]) / 2.0
            target = UTIL * cumulative(rows, mid, col)
            # no floor that overrides the target: a floor above the accrual would make the Age unreachable at the
            # reference rate (the loader bounds the scale to (0, 4]); 1e-6 only guards a non-positive difference
            age_target = max(target - prev_target, 1e-6 * rubric[tree][x])
            s = age_target / rubric[tree][x] if rubric[tree][x] else 1.0
            scale[tree].append(round(s, 6))
            report.append({"tree": tree, "age": x, "rubricTotal": round(rubric[tree][x]), "targetTotal": round(age_target),
                           "scale": round(s, 4), "researchableByTurn": mid})
            prev_target = prev_target + age_target
    out = {"seed": seed, "entryTargets": ENTRY, "utilization": UTIL, "rows": report}
    (PKG / "data" / "tranche2").mkdir(parents=True, exist_ok=True)
    json.dump(out, open(PKG / "data" / "tranche2" / "pacing-calibration.json", "w"), indent=1)
    for r in report:
        print(f"{r['tree']:10s} A{r['age']}  rubric {r['rubricTotal']:>11,}  target {r['targetTotal']:>9,}  "
              f"scale {r['scale']:.4f}  by turn {r['researchableByTurn']:.0f}")
    if "--write" in argv:
        d["tuning"]["pacingScale"] = {
            "technology": scale["technology"], "civics": scale["civics"],
            "_doc": "D-063 R6 (tranche 2): BaseCost = content cost (U x K^magnitude) x pacingScale[tree][Age-1]. Calibrated by "
                    "tools/calibrate_pacing.py from a measured reference empire so both trees complete each Age in the "
                    "same turns ('pincer'); see docs/m6-plan/knowledge-redesign/tranche2-balance-report.md."}
        with open(RESEARCH, "w", encoding="utf-8") as fh:
            json.dump(d, fh, indent=1, ensure_ascii=True)
            fh.write("\n")
        print("pacingScale written")


if __name__ == "__main__":
    main(sys.argv[1:])
