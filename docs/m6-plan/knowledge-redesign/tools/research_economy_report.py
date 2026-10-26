"""Markdown tables from ResearchEconomyMeasurement output (Sim.Tests/Kernel/ResearchPacingRunner.cs).

Usage:
  python3 -I tools/research_economy_report.py trial   <dir>   (RouteOverflowTrial_FourSeeds: labels off / route)
  python3 -I tools/research_economy_report.py reserve <dir> <trial-dir>   (EmptyLaneReserveStudy_FourSeeds; the AI
                                                                           reference is the trial's 'off' run)
  python3 -I tools/research_economy_report.py pacing  <dir> <label> [<ref-dir> <ref-label>]   (OverlayRun_FourSeeds:
                                                 a pacing pass against the D-063 R6 targets; ref = the run it is compared with)
  python3 -I tools/research_economy_report.py batch   <dir> <base-label> <label>...   (cost batches: each label against
                                                 the calibrated trial baseline, independently)
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


COMBINED = ["off", "trial", "trial-nogate", "d1-off", "d3-off", "d1-trial", "d3-trial", "d6-trial"]


def combined(d):
    """CombinedTrial_FourSeeds: waste uses the LOST columns (with the engine reserve a step may apply more RP than it
    generated, and deposits are not losses)."""
    runs = {(s, l): load(d, l, s) for s in SEEDS for l in COMBINED}
    out = ["### RP waste by polity Age (four seeds pooled): lost / generated", "",
           "| Age | off | trial | trial without the gate rule |", "|---|---:|---:|---:|"]
    tot = {}
    for a in range(1, 10):
        cells = []
        for l in ("off", "trial", "trial-nogate"):
            g = lost = 0.0
            for s in SEEDS:
                r = runs.get((s, l))
                if not r:
                    continue
                for t in r["turns"]:
                    if int(t["age"]) == a:
                        g += float(t["pool"]); lost += float(t["lostSurplus"]) + float(t["lostEmpty"])
            tt = tot.setdefault(l, [0.0, 0.0]); tt[0] += g; tt[1] += lost
            cells.append(pct(lost, g))
        out.append(f"| A{a} | " + " | ".join(cells) + " |")
    out.append("| **All** | " + " | ".join(pct(tot[l][1], tot[l][0]) for l in ("off", "trial", "trial-nogate")) + " |")
    out += ["", "### Polity Age entered (effective turn): off → trial", "",
            "| Seed | " + " | ".join(f"A{a}" for a in range(2, 10)) + " |", "|---|" + "---|" * 8]
    for s in SEEDS:
        o, t = runs.get((s, "off")), runs.get((s, "trial"))
        if o and t:
            out.append(f"| {s} | " + " | ".join(
                f"{o['entered'].get(a)} → {t['entered'].get(a)} ({t['entered'].get(a) - o['entered'].get(a):+d})"
                if o['entered'].get(a) is not None and t['entered'].get(a) is not None else "-" for a in range(2, 10)) + " |")
    for key, title in (("civDone", "Civics: turn each Age's finite civics are complete (off → trial)"),
                       ("techDone", "Technology: turn each Age's finite technologies are complete (off → trial)")):
        out += ["", f"### {title}", "", "| Seed | " + " | ".join(f"A{a}" for a in range(1, 10)) + " |", "|---|" + "---|" * 9]
        for s in SEEDS:
            o, t = runs.get((s, "off")), runs.get((s, "trial"))
            if o and t:
                out.append(f"| {s} | " + " | ".join(f"{o[key].get(a)} → {t[key].get(a)}" for a in range(1, 10)) + " |")
    out += ["", "### Age milestones (median first-met turn over milestones met in both runs; off → trial)", "",
            "| Seed | " + " | ".join(f"A{a}" for a in range(2, 10)) + " |", "|---|" + "---|" * 8]
    for s in SEEDS:
        o, t = runs.get((s, "off")), runs.get((s, "trial"))
        if not (o and t):
            continue
        cells = []
        for a in range(2, 10):
            keys = [k for k in o["milestones"] if k.startswith(f"A{a}|") and k in t["milestones"]]
            cells.append(f"{statistics.median(o['milestones'][k] for k in keys):.0f} → {statistics.median(t['milestones'][k] for k in keys):.0f}" if keys else "-")
        out.append(f"| {s} | " + " | ".join(cells) + " |")
    out += ["", "### End state: research, roads, infrastructure (off → trial)", "",
            "| Seed | Tech | Civics | Levels | Road developments | Structures | Institutions | Known cells | Population |",
            "|---|---|---|---|---|---|---|---|---|"]
    for s in SEEDS:
        o, t = runs.get((s, "off")), runs.get((s, "trial"))
        if o and t:
            out.append(f"| {s} | {o['end'][1]} → {t['end'][1]} | {o['end'][2]} → {t['end'][2]} | {o['end'][3]} → {t['end'][3]} | "
                       f"{o['infra'][1]} → {t['infra'][1]} | {o['infra'][0]} → {t['infra'][0]} | {o['infra'][2]} → {t['infra'][2]} | "
                       f"{o['infra'][5]} → {t['infra'][5]} | {o['end'][4]:,} → {t['end'][4]:,} |")
    out += ["", "### Casual and idling players: polity Age entered, mean over four seeds (shift vs the AI under the same rules)", "",
            "| Configuration | Player | " + " | ".join(f"A{a}" for a in range(2, 10)) + " |", "|---|---|" + "---|" * 8]
    def mean_entry(l, a):
        v = [runs[(s, l)]["entered"].get(a) for s in SEEDS if runs.get((s, l))]
        v = [x for x in v if x is not None]
        return statistics.fmean(v) if len(v) == len(SEEDS) else None
    for cfg, ai, players in (("off (shipped)", "off", ["off", "d1-off", "d3-off"]),
                             ("trial", "trial", ["trial", "d1-trial", "d3-trial", "d6-trial"])):
        for l in players:
            cells = []
            for a in range(2, 10):
                m, r = mean_entry(l, a), mean_entry(ai, a)
                cells.append("-" if m is None else f"{m:.0f}" + ("" if l == ai or r is None else f" ({m - r:+.0f})"))
            who = "AI (no delay)" if l == ai else f"proxy, {l.split('-')[0][1:]}-turn delay"
            out.append(f"| {cfg} | {who} | " + " | ".join(cells) + " |")
    out += ["", "### Age-gate waiting, reserve and goal switching (four seeds pooled)", "",
            "| Run | Technology waiting steps | Empty-lane RP lost | Reserve deposited / drawn | Turns at the reserve cap | Technology goal switches away from an incomplete destination |",
            "|---|---:|---:|---|---:|---:|"]
    for l in COMBINED:
        rs = [runs[(s, l)] for s in SEEDS if runs.get((s, l))]
        if not rs:
            continue
        wait = sum(1 for r in rs for t in r["turns"] if t["techClass"] == "2")
        g = sum(float(t["pool"]) for r in rs for t in r["turns"])
        em = sum(float(t["lostEmpty"]) for r in rs for t in r["turns"])
        dep = sum(float(t["deposit"]) for r in rs for t in r["turns"])
        dr = sum(float(t["draw"]) for r in rs for t in r["turns"])
        sat = sum(int(m.group(1)) for r in rs for m in [re.search(r"turns at the cap (\d+)", r["summary"])] if m)
        sw = 0
        for r in rs:
            m = re.search(r"switched away from an incomplete one, by Age:(.*)", r["summary"])
            if m:
                sw += sum(int(x) for x in re.findall(r"A\d=(\d+)", m.group(1)))
        out.append(f"| {l} | {wait} | {pct(em, g)} | {dep:,.0f} / {dr:,.0f} | {sat} | {sw} |")
    out += ["", "### Determinism (trial runs)", "", "| Seed | Reconstruction mismatches | Replay from the order log | Save/load at mid-run |", "|---|---:|---|---|"]
    for s in SEEDS:
        t = runs.get((s, "trial"))
        if t:
            out.append(f"| {s} | {t['mismatches']} | {t['replay']} | {t['saveload']} |")
    print("\n".join(out))


# D-063 R6: Age x complete in both trees by the midpoint of the entry turns E[x+1] and E[x+2] (calibrate_pacing.ENTRY)
TARGET = {1: 240, 2: 510, 3: 690, 4: 820, 5: 970, 6: 1155, 7: 1325, 8: 1460, 9: 1520}


def lost_by_age(run):
    g, lost = {}, {}
    for t in run["turns"]:
        a = int(t["age"])
        g[a] = g.get(a, 0.0) + float(t["pool"])
        lost[a] = lost.get(a, 0.0) + float(t["lostSurplus"]) + float(t["lostEmpty"])
    return g, lost


def reserve_line(run):
    m = re.search(r"turns at the cap (\d+)", run["summary"])
    z = re.search(r"turns holding a reserve (\d+); largest ([\d.]+); mean while held ([\d.-]+)", run["summary"])
    return (int(m.group(1)) if m else None, ) + ((int(z.group(1)), z.group(2), z.group(3)) if z else (None, "-", "-"))


def pacing(d, label, ref_d=None, ref_label=None):
    runs = {s: load(d, label, s) for s in SEEDS}
    ref = {s: load(ref_d, ref_label, s) for s in SEEDS} if ref_d else {}
    out = []
    for key, title in (("techDone", "Technology"), ("civDone", "Civics")):
        out += [f"### {title}: turn each Age's finite nodes are complete (residual against the target)", "",
                "| Seed | " + " | ".join(f"A{a} ({TARGET[a]})" for a in range(1, 10)) + " |", "|---|" + "---:|" * 9]
        for s in SEEDS:
            r = runs[s]
            out.append(f"| {s} | " + " | ".join(
                (f"{r[key][a]} ({r[key][a] - TARGET[a]:+d})" if r[key].get(a, -1) >= 0 else "never") for a in range(1, 10)) + " |")
        worst = []
        for a in range(1, 10):
            v = [runs[s][key].get(a, -1) for s in SEEDS]
            worst.append("never" if min(v) < 0 else f"{max(v) - TARGET[a]:+d}")
        out.append("| slowest seed | " + " | ".join(worst) + " |")
        out.append("")
    out += ["### Polity Age entered (effective turn)" + (" — reference → this run" if ref else ""), "",
            "| Seed | " + " | ".join(f"A{a}" for a in range(2, 10)) + " |", "|---|" + "---:|" * 8]
    for s in SEEDS:
        r, o = runs[s], ref.get(s)
        cells = []
        for a in range(2, 10):
            t = r["entered"].get(a)
            if o and o["entered"].get(a) is not None and t is not None:
                cells.append(f"{o['entered'][a]} → {t} ({t - o['entered'][a]:+d})")
            else:
                cells.append("-" if t is None else str(t))
        out.append(f"| {s} | " + " | ".join(cells) + " |")
    out += ["", "### RP lost by polity Age (four seeds pooled; lost = surplus past a funded node + a lane that funded nothing)", "",
            "| Age | " + ("reference | " if ref else "") + "this run |", "|---|" + ("---:|" if ref else "") + "---:|"]
    tot = {"r": [0.0, 0.0], "t": [0.0, 0.0]}
    for a in range(1, 10):
        cells = []
        for k, rs in (("r", ref), ("t", runs)) if ref else (("t", runs),):
            g = l = 0.0
            for s in SEEDS:
                if rs.get(s):
                    gg, ll = lost_by_age(rs[s]); g += gg.get(a, 0.0); l += ll.get(a, 0.0)
            tot[k][0] += g; tot[k][1] += l
            cells.append(pct(l, g))
        out.append(f"| A{a} | " + " | ".join(cells) + " |")
    out.append("| **All** | " + " | ".join(pct(tot[k][1], tot[k][0]) for k in (("r", "t") if ref else ("t",))) + " |")
    out += ["", "### Reserve: time at the cap versus size (turns of RP), and the technology lane", "",
            "| Seed | Turns at the cap | Turns holding a reserve | Largest (turns of RP) | Mean while held (turns of RP) | Technology waiting steps | Goal switches |",
            "|---|---:|---:|---:|---:|---:|---:|"]
    for s in SEEDS:
        r = runs[s]
        cap, held, big, mean = reserve_line(r)
        wait = sum(1 for t in r["turns"] if t["techClass"] == "2")
        m = re.search(r"switched away from an incomplete one, by Age:(.*)", r["summary"])
        sw = sum(int(x) for x in re.findall(r"A\d=(\d+)", m.group(1))) if m else 0
        out.append(f"| {s} | {cap} | {held} | {big} | {mean} | {wait} | {sw} |")
    out += ["", "### Civics, roads, milestones and end state", "",
            "| Seed | Civics done | Tech done | Levels | Road developments | Structures | Institutions | Median A5 / A7 / A9 milestone turn | CP identity |",
            "|---|---:|---:|---:|---:|---:|---:|---|---|"]
    for s in SEEDS:
        r = runs[s]
        ms = []
        for a in (5, 7, 9):
            v = [t for k, t in r["milestones"].items() if k.startswith(f"A{a}|")]
            ms.append(f"{statistics.median(v):.0f}" if v else "-")
        out.append(f"| {s} | {r['end'][2]} | {r['end'][1]} | {r['end'][3]} | {r['infra'][1]} | {r['infra'][0]} | {r['infra'][2]} | "
                   f"{' / '.join(ms)} | {r['cp'][3]} |")
    if any(runs[s]["replay"] for s in SEEDS):
        out += ["", "### Determinism", "", "| Seed | Reconstruction mismatches | Replay | Save/load at mid-run |", "|---|---:|---|---|"]
        for s in SEEDS:
            r = runs[s]
            out.append(f"| {s} | {r['mismatches']} | {r['replay']} | {r['saveload']} |")
    print("\n".join(out))


def batch(d, base, labels):
    base_runs = {s: load(d, base, s) for s in SEEDS}
    out = ["### Cost batches, each against the calibrated trial baseline (four seeds; mean shift in turns, slowest seed in brackets)", "",
           "| Batch | Tech A1–A3 complete | Tech A4–A6 | Tech A7–A9 | Civics A1–A3 | Civics A4–A6 | Civics A7–A9 | Age 9 entered | RP lost (base → batch) |",
           "|---|---|---|---|---|---|---|---|---|"]
    def band(runs, key, ages):
        return [[runs[s][key].get(a, -1) for a in ages] for s in SEEDS]
    for l in labels:
        rs = {s: load(d, l, s) for s in SEEDS}
        cells = []
        for key in ("techDone", "civDone"):
            for ages in ((1, 2, 3), (4, 5, 6), (7, 8, 9)):
                shifts, never = [], 0
                for s in SEEDS:
                    for a in ages:
                        b, t = base_runs[s][key].get(a, -1), rs[s][key].get(a, -1)
                        if b < 0 or t < 0:
                            never += (t < 0) and (b >= 0)
                            continue
                        shifts.append(t - b)
                cells.append((f"{statistics.fmean(shifts):+.1f} [{max(shifts):+d}]" if shifts else "-") + (f", {never} never" if never else ""))
        e = [(rs[s]["entered"].get(9), base_runs[s]["entered"].get(9)) for s in SEEDS]
        e9 = [t - b for t, b in e if t is not None and b is not None]
        cells.append(f"{statistics.fmean(e9):+.1f}" + (f" ({len(SEEDS) - len(e9)} seeds n/a)" if len(e9) < len(SEEDS) else "") if e9 else "-")
        gb = lb = gt = lt = 0.0
        for s in SEEDS:
            g, ll = lost_by_age(base_runs[s]); gb += sum(g.values()); lb += sum(ll.values())
            g, ll = lost_by_age(rs[s]); gt += sum(g.values()); lt += sum(ll.values())
        cells.append(f"{pct(lb, gb)} → {pct(lt, gt)}")
        out.append(f"| {l} | " + " | ".join(cells) + " |")
    print("\n".join(out))


if __name__ == "__main__":
    if sys.argv[1] == "trial":
        trial(sys.argv[2])
    elif sys.argv[1] == "combined":
        combined(sys.argv[2])
    elif sys.argv[1] == "pacing":
        pacing(*sys.argv[2:6]) if len(sys.argv) > 4 else pacing(sys.argv[2], sys.argv[3])
    elif sys.argv[1] == "batch":
        batch(sys.argv[2], sys.argv[3], sys.argv[4:])
    else:
        reserve(sys.argv[2], sys.argv[3])
