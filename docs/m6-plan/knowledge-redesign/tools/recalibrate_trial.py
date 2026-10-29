"""Recalibrate a research OVERLAY's tuning.pacingScale from ResearchPacingRunner runs (Director ruling 2026-10-26).

The algorithm is recalibrate_observed.py's, unchanged (D-063 R6 targets; the slowest seed binds the target term, the
pincer term is the seeds' geometric mean; the target term is damped). Only the input differs: the runner's per-seed
files (<label>-s<seed>-summary.txt for the Age-complete turns, <label>-s<seed>-turns.csv for the RP generated each turn,
`pool`, and the cumulative CP generated, `cpGenMilli`) instead of PacingReferenceMeasurement's text, and the scale read
and written is the overlay's, so the shipped research.json is never touched.

Two guards, added after pass 1 (research-cost-pass/pacing-recalibration.md §2):
  --max-step m   bounds each pass's change of a scale to [1/m, m] (default 1.5). Pass 1, unbounded, multiplied civics
                 Age 7 by 4.9: the slower civics cohort then held the technology lane at the Age sweep for hundreds of
                 turns. The Ages are coupled through the economy, so a bounded step converges and an unbounded one
                 oscillates.
  --freeze A,..  leaves these scales as they are (default 1): an Age number freezes both trees, tree:Age one tree
                 (civics:7). Age 1 is not cost-bound: pass 1 cut its scales by 13-15 % and its completion moved later
                 (255 -> 259, seed 1), because the AI researches its next Age's milestones before the last Age-1 nodes.
                 Civics Age 7 is not independent of civics Age 6: with no milestone goal the Civics lane funds the
                 Cheapest available civic, Age-7 civics open with Age 5, and caliphate (the dearest Age-6 civic) is
                 bought only after every cheaper one, so raising the Age-7 civics scale delays Age 6 too (pass 3:
                 caliphate 277 turns after the rest of Age 6 in seed 2). Frozen residuals are reported, not chased.

  --pincer-only  (ADR-054 per-turn CP recalibration, 2026-10-27) the TARGET term is dropped and each non-frozen scale
                 is moved by the FULL pincer toward the OTHER tree's completion turn (geometric mean over the seeds of
                 inc(t_other) / inc(t_own), damped, bounded by --max-step). For a change that alters ONE lane's point flow
                 (per-turn CP) with the other tree's scales frozen: the frozen tree keeps its calibrated pace (its target
                 residuals are what they were) and the changed lane is re-aligned to it, so the two trees again finish each
                 Age together. The standard (half-pincer + target) step would pull the changed lane toward the targets
                 instead and open the pincer the frozen tree cannot close.

Usage: python3 -I tools/recalibrate_trial.py <run-dir> <label> <overlay.json> [--damp 0.7] [--max-step 1.5]
       [--freeze 1] [--pincer-only] [--write]
"""
import csv
import json
import math
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from calibrate_pacing import ENTRY  # noqa: E402

SEEDS = (1, 2, 42, 7)


def load(run_dir, label, seed):
    done = {}
    for line in open(Path(run_dir) / f"{label}-s{seed}-summary.txt", encoding="utf-8"):
        m = re.match(r"\s*Age complete \(turn\) (technology|civics):\s*(.*)", line)
        if m:
            done[m.group(1)] = {int(a): int(t) for a, t in re.findall(r"A(\d)=(-?\d+)", m.group(2))}
    rp, cp = [0.0], [0.0]            # cumulative after turn t (index t); index 0 = before turn 1
    with open(Path(run_dir) / f"{label}-s{seed}-turns.csv", encoding="utf-8") as fh:
        for row in csv.DictReader(fh):
            rp.append(rp[-1] + float(row["pool"]))
            cp.append(int(row["cpGenMilli"]) / 1000.0)
    return done, {"rp": rp, "cp": cp}


def cum(series, turn):
    t = int(turn)
    if t <= 0:
        return 0.0
    if t >= len(series) - 1:
        return series[-1]
    frac = turn - t                  # midpoints land on half turns
    return series[t] + frac * (series[t + 1] - series[t])


def main(argv):
    run_dir, label, overlay_path = argv[0], argv[1], Path(argv[2])
    damp = float(argv[argv.index("--damp") + 1]) if "--damp" in argv else 0.7
    max_step = float(argv[argv.index("--max-step") + 1]) if "--max-step" in argv else 1.5
    frozen = set()
    for a in (argv[argv.index("--freeze") + 1] if "--freeze" in argv else "1").split(","):
        if ":" in a:
            frozen.add((a.split(":")[0], int(a.split(":")[1])))
        elif a:
            frozen |= {("technology", int(a)), ("civics", int(a))}
    pincer_only = "--pincer-only" in argv
    seeds = {s: load(run_dir, label, s) for s in SEEDS}
    turns = min(len(c["rp"]) - 1 for _, c in seeds.values())
    ov = json.load(open(overlay_path, encoding="utf-8"))
    pace = ov["tuning"]["pacingScale"]
    report = []
    for x in range(1, 10):
        tgt = (ENTRY[x + 1] + ENTRY[x + 2]) / 2.0
        for tree, col in (("technology", "rp"), ("civics", "cp")):
            r_target, r_pincer = [], []
            for s in SEEDS:
                done, series = seeds[s]
                ser = series[col]
                t_own = done[tree].get(x, -1)
                t_other = done["civics" if tree == "technology" else "technology"].get(x, -1)
                if t_own < 0:
                    r_target.append(0.7 * cum(ser, tgt) / cum(ser, turns))
                    continue
                t_mid = (t_own + t_other) / 2.0 if t_other >= 0 else t_own
                t0 = done[tree].get(x - 1, 0) if x > 1 else 0
                base = cum(ser, max(t0, 0))
                inc = lambda t: max(cum(ser, t) - base, 1e-9)
                r_target.append(inc(max(tgt, t0 + 1)) / inc(t_mid))
                r_pincer.append(inc(t_mid) / inc(t_own))
            rt = min(r_target)
            rp = math.exp(sum(math.log(q) for q in r_pincer) / len(r_pincer)) if r_pincer else 1.0
            old = pace[tree][x - 1]
            if pincer_only:
                # the full pincer: inc(t_other) / inc(t_own) = (inc(t_mid) / inc(t_own))^2 approximately; computed exactly
                full = []
                for s in SEEDS:
                    done, series = seeds[s]
                    ser = series[col]
                    t_own = done[tree].get(x, -1)
                    t_other = done["civics" if tree == "technology" else "technology"].get(x, -1)
                    if t_own < 0 or t_other < 0:
                        continue
                    t0 = done[tree].get(x - 1, 0) if x > 1 else 0
                    base = cum(ser, max(t0, 0))
                    full.append(max(cum(ser, t_other) - base, 1e-9) / max(cum(ser, t_own) - base, 1e-9))
                rp = math.exp(sum(math.log(q) for q in full) / len(full)) if full else 1.0
                step = min(max(rp ** damp, 1.0 / max_step), max_step)
            else:
                step = min(max(rt ** damp * rp, 1.0 / max_step), max_step)
            new = old if (tree, x) in frozen else round(min(max(old * step, 1e-6), 16.0), 6)
            pace[tree][x - 1] = new
            obs = " ".join(f"{seeds[s][0][tree].get(x, -1):5d}" for s in SEEDS)
            report.append(f"{tree:10s} A{x} target {tgt:6.0f}  observed {obs}  r_target {rt:6.3f} r_pincer {rp:6.3f}"
                          f"  scale {old:.6f} -> {new:.6f}" + ("  (frozen)" if (tree, x) in frozen else ""))
    print("\n".join(report))
    if "--write" in argv:
        with open(overlay_path, "w", encoding="utf-8") as fh:
            json.dump(ov, fh, indent=1, ensure_ascii=True)
            fh.write("\n")
        print(f"pacingScale written to {overlay_path}")


if __name__ == "__main__":
    main(sys.argv[1:])
