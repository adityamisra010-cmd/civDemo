"""Markdown tables from ResearchEconomyMeasurement output (Sim.Tests/Kernel/ResearchPacingRunner.cs).

Usage:
  python3 -I tools/research_economy_report.py trial   <dir>   (RouteOverflowTrial_FourSeeds: labels off / route)
  python3 -I tools/research_economy_report.py reserve <dir> <trial-dir>   (EmptyLaneReserveStudy_FourSeeds; the AI
                                                                           reference is the trial's 'off' run)
<dir> holds <label>-s<seed>-{summary.txt,turns.csv,nodes.csv,milestones.csv}. Prints Markdown.
"""
import csv
import re
import statistics
import sys
from pathlib import Path

SEEDS = [1, 2, 42, 7]


def load(d, label, seed):
    stem = Path(d) / f"{label}-s{seed}"
    if not Path(f"{stem}-summary.txt").exists():
        return None
    summ = Path(f"{stem}-summary.txt").read_text(encoding="utf-8")
    run = {"summary": summ}
    m = re.search(r"Age entered \(effective turn\):(.*)", summ)
    run["entered"] = {int(a): int(t) for a, t in re.findall(r"A(\d)=(-?\d+)", m.group(1))} if m else {}
    for key, pat in (("techDone", r"Age complete \(turn\) technology:(.*)"), ("civDone", r"Age complete \(turn\) civics:(.*)")):
        m = re.search(pat, summ)
        run[key] = {int(a): int(t) for a, t in re.findall(r"A(\d)=(-?\d+)", m.group(1))} if m else {}
    m = re.search(r"final hash (\w+)(; replay (\w+))?(; save/load at turn \d+ (\w+))?", summ)
    run["replay"] = m.group(3) if m else None
    run["saveload"] = m.group(5) if m else None
    m = re.search(r"reconstruction mismatches (\d+)", summ)
    run["mismatches"] = int(m.group(1)) if m else None
    m = re.search(r"end: Age (\d+) tech (\d+) civics (\d+) levels (\d+) population (\d+) settlements (\d+)", summ)
    run["end"] = tuple(int(x) for x in m.groups()) if m else None
    m = re.search(r"infrastructure: structures (\d+) road developments (\d+) institutions (\d+); discovery: surveys (\d+) "
                  r"explorations (\d+) known cells (\d+)", summ)
    run["infra"] = tuple(int(x) for x in m.groups()) if m else None
    m = re.search(r"CP generated (\d+) spent (\d+) banked (\d+); identity (\w+)", summ)
    run["cp"] = (int(m.group(1)), int(m.group(2)), int(m.group(3)), m.group(4)) if m else None
    m = re.search(r"bursts: most technologies completed in one turn (\d+), civics (\d+), repeatable levels (\d+); first level turn (-?\d+)", summ)
    run["bursts"] = tuple(int(x) for x in m.groups()) if m else None
    run["turns"] = list(csv.DictReader(open(f"{stem}-turns.csv", encoding="utf-8")))
    run["nodes"] = {r["id"]: r for r in csv.DictReader(open(f"{stem}-nodes.csv", encoding="utf-8"))}
    run["milestones"] = {r["key"]: int(r["firstMetTurn"]) for r in csv.DictReader(open(f"{stem}-milestones.csv", encoding="utf-8"))}
    return run


def rp_by_age(run):
    gen, spent, surplus, empty = {}, {}, {}, {}
    for r in run["turns"]:
        a = int(r["age"])
        gen[a] = gen.get(a, 0.0) + float(r["pool"])
        spent[a] = spent.get(a, 0.0) + float(r["spent"])
        surplus[a] = surplus.get(a, 0.0) + float(r["lostSurplus"])
        empty[a] = empty.get(a, 0.0) + float(r["lostEmpty"])
    return gen, spent, surplus, empty


def pct(x, y):
    return f"{100.0 * x / y:.1f}%" if y else "-"


def spearman(a, b):
    def ranks(v):
        order = sorted(range(len(v)), key=lambda i: (v[i], i))
        r = [0.0] * len(v)
        i = 0
        while i < len(order):
            j = i
            while j + 1 < len(order) and v[order[j + 1]] == v[order[i]]:
                j += 1
            for k in range(i, j + 1):
                r[order[k]] = (i + j) / 2.0
            i = j + 1
        return r
    ra, rb = ranks(a), ranks(b)
    ma, mb = statistics.fmean(ra), statistics.fmean(rb)
    num = sum((x - ma) * (y - mb) for x, y in zip(ra, rb))
    den = (sum((x - ma) ** 2 for x in ra) * sum((y - mb) ** 2 for y in rb)) ** 0.5
    return num / den if den else 1.0


def trial(d):
    runs = {(s, l): load(d, l, s) for s in SEEDS for l in ("off", "route")}
    out = ["### Polity Age entered (effective turn): OFF → ON, and the shift", "",
           "| Seed | " + " | ".join(f"A{a}" for a in range(2, 10)) + " |", "|---|" + "---|" * 8]
    for s in SEEDS:
        off, on = runs[(s, "off")], runs[(s, "route")]
        if not off or not on:
            continue
        cells = []
        for a in range(2, 10):
            x, y = off["entered"].get(a), on["entered"].get(a)
            cells.append(f"{x} → {y} ({y - x:+d})" if x is not None and y is not None else f"{x} → {y}")
        out.append(f"| {s} | " + " | ".join(cells) + " |")
    for key, title in (("techDone", "Technology"), ("civDone", "Civics")):
        out += ["", f"### {title}: turn all finite content of an Age is complete (OFF → ON)", "",
                "| Seed | " + " | ".join(f"A{a}" for a in range(1, 10)) + " |", "|---|" + "---|" * 9]
        for s in SEEDS:
            off, on = runs[(s, "off")], runs[(s, "route")]
            if off and on:
                out.append(f"| {s} | " + " | ".join(f"{off[key].get(a)} → {on[key].get(a)}" for a in range(1, 10)) + " |")
    out += ["", "### RP generated, spent and lost by polity Age (four seeds pooled)", "",
            "| Age | OFF generated | OFF lost | of which surplus / empty | ON generated | ON lost | of which surplus / empty |",
            "|---|---:|---:|---|---:|---:|---|"]
    tot = {}
    for a in range(1, 10):
        row = []
        for l in ("off", "route"):
            g = sp = su = em = 0.0
            for s in SEEDS:
                r = runs[(s, l)]
                if not r:
                    continue
                gen, spent, surplus, empty = rp_by_age(r)
                g += gen.get(a, 0.0); sp += spent.get(a, 0.0); su += surplus.get(a, 0.0); em += empty.get(a, 0.0)
            t = tot.setdefault(l, [0.0, 0.0, 0.0, 0.0])
            t[0] += g; t[1] += sp; t[2] += su; t[3] += em
            row += [f"{g:,.0f}", pct(g - sp, g), f"{pct(su, g)} / {pct(em, g)}"]
        out.append(f"| A{a} | " + " | ".join(row) + " |")
    row = []
    for l in ("off", "route"):
        g, sp, su, em = tot[l]
        row += [f"{g:,.0f}", pct(g - sp, g), f"{pct(su, g)} / {pct(em, g)}"]
    out.append("| **All** | " + " | ".join(row) + " |")

    out += ["", "### Node completion order (finite nodes completed in both runs)", "",
            "| Seed | Nodes | Spearman ρ (completion turns) | Median shift (turns, ON − OFF) | Nodes earlier / later / same | Largest earlier shifts |",
            "|---|---:|---:|---:|---|---|"]
    for s in SEEDS:
        off, on = runs[(s, "off")], runs[(s, "route")]
        if not off or not on:
            continue
        ids = [i for i, r in off["nodes"].items() if r["repeatable"] == "0" and int(r["completedTurn"]) > 0
               and int(on["nodes"][i]["completedTurn"]) > 0]
        a = [int(off["nodes"][i]["completedTurn"]) for i in ids]
        b = [int(on["nodes"][i]["completedTurn"]) for i in ids]
        shifts = [y - x for x, y in zip(a, b)]
        big = sorted(zip(shifts, ids))[:3]
        out.append(f"| {s} | {len(ids)} | {spearman(a, b):.4f} | {statistics.median(shifts):+.0f} | "
                   f"{sum(1 for x in shifts if x < 0)} / {sum(1 for x in shifts if x > 0)} / {sum(1 for x in shifts if x == 0)} | "
                   + ", ".join(f"{i} {x:+d}" for x, i in big) + " |")
    out += ["", "### Age milestones: turn each Age's milestones were first met (median over the milestones met in both runs; OFF → ON)", "",
            "| Seed | " + " | ".join(f"A{a}" for a in range(2, 10)) + " |", "|---|" + "---|" * 8]
    for s in SEEDS:
        off, on = runs[(s, "off")], runs[(s, "route")]
        if not off or not on:
            continue
        cells = []
        for a in range(2, 10):
            keys = [k for k in off["milestones"] if k.startswith(f"A{a}|") and k in on["milestones"]]
            if not keys:
                cells.append("-")
                continue
            mo = statistics.median(off["milestones"][k] for k in keys)
            mn = statistics.median(on["milestones"][k] for k in keys)
            cells.append(f"{mo:.0f} → {mn:.0f}")
        out.append(f"| {s} | " + " | ".join(cells) + " |")
    out += ["", "### CP, repeatable levels and bursts", "",
            "| Seed | Mode | CP generated / spent / banked at end | CP identity | Max CP banked | Levels at end | First level turn | Most tech / civics / levels in one turn |",
            "|---|---|---|---|---:|---:|---:|---|"]
    for s in SEEDS:
        for l in ("off", "route"):
            r = runs[(s, l)]
            if not r:
                continue
            cp = r["cp"]
            maxbank = max(int(t["cpBalanceMilli"]) for t in r["turns"]) / 1000.0
            b = r["bursts"]
            out.append(f"| {s} | {'OFF' if l == 'off' else 'ON'} | {cp[0]:,} / {cp[1]:,} / {cp[2]:,} | {cp[3]} | {maxbank:,.0f} | "
                       f"{r['end'][3]} | {b[3]} | {b[0]} / {b[1]} / {b[2]} |")
    out += ["", "### End state and determinism", "",
            "| Seed | Mode | Age | Tech | Civics | Population | Structures | Road developments | Institutions | Known cells | Reconstruction mismatches | Replay | Save/load |",
            "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|"]
    for s in SEEDS:
        for l in ("off", "route"):
            r = runs[(s, l)]
            if not r:
                continue
            e, i = r["end"], r["infra"]
            out.append(f"| {s} | {'OFF' if l == 'off' else 'ON'} | {e[0]} | {e[1]} | {e[2]} | {e[4]:,} | {i[0]} | {i[1]} | {i[2]} | {i[5]} | "
                       f"{r['mismatches']} | {r['replay'] or '-'} | {r['saveload'] or '-'} |")
    print("\n".join(out))


RESERVE = ["ai-cap3", "ai-uncapped", "ai-cap3-route", "ai-cap3-lossy", "ai-uncapped-lossy", "d1-cap3-lossy", "d3-cap3-lossy", "d3-uncapped-lossy", "d1-discard", "d1-uncapped", "d3-discard", "d3-cap1", "d3-cap3", "d3-uncapped", "d3-uncapped-draw1",
           "d3-discard-route", "d3-cap3-route"]


def reserve(d, trial_dir):
    runs = {(s, l): load(d, l, s) for s in SEEDS for l in RESERVE}
    for s in SEEDS:
        runs[(s, "ai")] = load(trial_dir, "off", s)
        runs[(s, "ai-route")] = load(trial_dir, "route", s)
    labels = ["ai", "ai-route"] + RESERVE
    out = ["### RP economy per policy (four seeds pooled)", "",
           "| Policy | RP generated | RP lost | lost: surplus / empty lane | Deposited | Drawn | Max reserve (turns of RP) | Turns a draw exceeded one turn's RP | Largest draw (turns of RP) |",
           "|---|---:|---:|---|---:|---:|---:|---:|---:|"]
    for l in labels:
        g = sp = su = em = dep = dr = 0.0
        maxres = 0.0
        over1 = 0
        largest = 0.0
        for s in SEEDS:
            r = runs.get((s, l))
            if not r:
                continue
            for t in r["turns"]:
                p = float(t["pool"])
                g += p; sp += float(t["spent"]); su += float(t["lostSurplus"]); em += float(t["lostEmpty"])
                dep += float(t["deposit"]); dr += float(t["draw"])
                if p > 0:
                    maxres = max(maxres, float(t["reserve"]) / p)
                    ratio = float(t["draw"]) / p
                    largest = max(largest, ratio)
                    if ratio > 1.0:
                        over1 += 1
        out.append(f"| {l} | {g:,.0f} | {pct(su + em, g)} | {pct(su, g)} / {pct(em, g)} | {dep:,.0f} | {dr:,.0f} | "
                   f"{maxres:.2f} | {over1} | {largest:.2f} |")
    out += ["", "### Pacing: polity Age entered (effective turn), mean over the four seeds, and the shift against the AI reference", "",
            "| Policy | " + " | ".join(f"A{a}" for a in range(2, 10)) + " |", "|---|" + "---|" * 8]
    ref = {}
    for a in range(2, 10):
        v = [runs[(s, "ai")]["entered"].get(a) for s in SEEDS if runs.get((s, "ai"))]
        v = [x for x in v if x is not None]
        ref[a] = statistics.fmean(v) if len(v) == len(SEEDS) else None
    for l in labels:
        cells = []
        for a in range(2, 10):
            v = [runs[(s, l)]["entered"].get(a) for s in SEEDS if runs.get((s, l))]
            v = [x for x in v if x is not None]
            if len(v) < len(SEEDS):
                cells.append(f"{len(v)}/4 seeds")
                continue
            m = statistics.fmean(v)
            cells.append(f"{m:.0f}" + (f" ({m - ref[a]:+.0f})" if ref.get(a) is not None and l != "ai" else ""))
        out.append(f"| {l} | " + " | ".join(cells) + " |")
    out += ["", "### Research bursts and end state (four seeds)", "",
            "| Policy | Most tech in one turn (max over seeds) | Tech completed at end (mean) | Civics (mean) | Levels (mean) | Reconstruction mismatches |",
            "|---|---:|---:|---:|---:|---:|"]
    for l in labels:
        rs = [runs[(s, l)] for s in SEEDS if runs.get((s, l))]
        if not rs:
            continue
        out.append(f"| {l} | {max(r['bursts'][0] for r in rs)} | {statistics.fmean(r['end'][1] for r in rs):.0f} | "
                   f"{statistics.fmean(r['end'][2] for r in rs):.0f} | {statistics.fmean(r['end'][3] for r in rs):.0f} | "
                   f"{sum(r['mismatches'] for r in rs)} |")
    print("\n".join(out))


if __name__ == "__main__":
    if sys.argv[1] == "trial":
        trial(sys.argv[2])
    else:
        reserve(sys.argv[2], sys.argv[3])
