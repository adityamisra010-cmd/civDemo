"""Correct tuning.pacingScale from OBSERVED Age-completion turns (D-063 R6, the pincer).

calibrate_pacing.py sets each (tree, Age) scale from one reference trajectory's accrual. The trajectory depends on the
costs (research feeds population through the knowledge effects; population feeds RP and CP), so the result is iterated
here against measured completions: for each seed of a PacingReferenceMeasurement run, each tree and each Age x, the turn
T_obs at which all finite Age-x content of that tree was complete, against the target T_tgt = midpoint(E[x+1], E[x+2]).

    T_mid(seed, x)          = midpoint of the two trees' completion turns of Age x
    dR(T)                   = R_tree(T) - R_tree(T_done(x-1))      (accrued since this tree finished Age x-1)
    r_target(seed, tree, x) = dR(T_tgt) / dR(T_mid)                (moves the pair to the target)
    r_pincer(seed, tree, x) = dR(T_mid) / dR(T_obs)                (moves this tree to the pair's midpoint)
    r(tree, x)              = min over seeds of r_target  x  geometric mean over seeds of r_pincer

(R = the seed's cumulative RP or CP.) r < 1 means the Age finished late: it must cost less. The SLOWEST seed binds the
target, so every seed finishes; an Age never finished in a seed counts as r_target = 0.7 x R(T_tgt) / R(1630). Each
scale becomes scale x r_target^d x r_pincer (d damps the target term only). The technology lane completes at most one
node per turn (no RP overflow, D-044), so a late Age with ~140 technologies takes ~140 turns whatever it costs; the civics
lane spends a bank and can complete several per turn, so in those Ages the pincer is met by the civics costs.

Usage: python3 -I tools/recalibrate_observed.py <measurement.txt> [--damp 0.7] [--write]
"""
import json
import re
import sys
from pathlib import Path

PKG = Path(__file__).resolve().parent.parent
REPO = PKG.parents[2]
RESEARCH = REPO / "Sim.Data" / "content" / "research.json"
sys.path.insert(0, str(PKG / "tools"))
from calibrate_pacing import ENTRY  # noqa: E402


def parse(path):
    seeds, cur = {}, None
    for line in open(path, encoding="utf-8"):
        m = re.match(r"\s*seed (\d+):", line)
        if m:
            cur = int(m.group(1))
            seeds[cur] = {"rows": [], "done": {}}
            continue
        if cur is None:
            continue
        m = re.match(r"\s*(\d+) \| ([\d.]+) \| ([\d.]+) \| (\d+) \| ([\d.]+) \| ([\d.]+) \|", line)
        if m:
            seeds[cur]["rows"].append((int(m.group(1)), float(m.group(5)), float(m.group(6))))
            continue
        m = re.match(r"\s*Age complete \(turn\) (technology|civics):\s*(.*)", line)
        if m:
            seeds[cur]["done"][m.group(1)] = {int(a): int(t) for a, t in re.findall(r"A(\d)=(-?\d+)", m.group(2))}
    return seeds


def cum(rows, turn, col):
    total, prev = 0.0, 0
    for t, rp, cp in rows:
        if t > turn:
            break
        total += (rp if col == "rp" else cp) * (t - prev)
        prev = t
    return total


def main(argv):
    seeds = parse(argv[0])
    damp = float(argv[argv.index("--damp") + 1]) if "--damp" in argv else 0.7
    d = json.load(open(RESEARCH, encoding="utf-8"))
    pace = d["tuning"]["pacingScale"]
    report = []
    import math
    for x in range(1, 10):
        tgt = (ENTRY[x + 1] + ENTRY[x + 2]) / 2.0
        for tree, col in (("technology", "rp"), ("civics", "cp")):
            r_target, r_pincer = [], []
            for s, v in sorted(seeds.items()):
                rows = v["rows"]
                t_own = v["done"][tree].get(x, -1)
                t_other = v["done"]["civics" if tree == "technology" else "technology"].get(x, -1)
                if t_own < 0:   # never finished: much cheaper, no pincer term
                    r_target.append(0.7 * cum(rows, tgt, col) / cum(rows, 1630, col))
                    continue
                t_mid = (t_own + t_other) / 2.0 if t_other >= 0 else t_own
                # Increments since this tree finished the previous Age (the window in which Age x's content is paid for):
                # the target term moves the PAIR's midpoint to the target; the pincer term moves this tree to the midpoint
                t0 = v["done"][tree].get(x - 1, 0) if x > 1 else 0
                base = cum(rows, max(t0, 0), col)
                inc = lambda t: max(cum(rows, t, col) - base, 1e-9)
                r_target.append(inc(max(tgt, t0 + 1)) / inc(t_mid))
                r_pincer.append(inc(t_mid) / inc(t_own))
            # the slowest seed binds the target; the pincer is the seeds' geometric mean
            rt = min(r_target)
            rp = math.exp(sum(math.log(q) for q in r_pincer) / len(r_pincer)) if r_pincer else 1.0
            r = rt * rp
            old = pace[tree][x - 1]
            # the target term is damped (it moves both trees and couples through the economy); the pincer term is not
            # (it moves the two trees towards each other and is self-limiting)
            new = min(max(old * rt ** damp * rp, 1e-6), 16.0)
            pace[tree][x - 1] = round(new, 6)
            report.append(f"{tree:10s} A{x} target {tgt:6.0f}  observed " +
                          " ".join(f"{v['done'][tree].get(x, -1):5d}" for _, v in sorted(seeds.items())) +
                          f"  r {r:6.3f}  scale {old:.6f} -> {new:.6f}")
    print("\n".join(report))
    if "--write" in argv:
        with open(RESEARCH, "w", encoding="utf-8") as fh:
            json.dump(d, fh, indent=1, ensure_ascii=True)
            fh.write("\n")
        print("pacingScale written")


if __name__ == "__main__":
    main(sys.argv[1:])
