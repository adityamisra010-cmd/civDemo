"""Tables for research-cost-pass/overflow-comparison.md from OverflowComparisonMeasurement console output.

Usage: python3 -I tools/overflow_report.py <measurement.txt> [<measurement.txt> ...]  (prints Markdown)
"""
import re
import sys
from collections import defaultdict


def parse(paths):
    runs = {}
    for p in paths:
        cur = None
        for line in open(p, encoding="utf-8"):
            m = re.match(r"\s*seed (\d+) overflow (\S+)", line)
            if m:
                cur = (int(m.group(1)), m.group(2))
                runs[cur] = {}
                continue
            if cur is None:
                continue
            s = line.strip()
            for key, pat in (("entered", r"Age entered \(effective turn\):(.*)"), ("tech", r"Age complete \(turn\) technology:(.*)"),
                             ("civ", r"Age complete \(turn\) civics:(.*)")):
                m = re.match(pat, s)
                if m:
                    runs[cur][key] = {int(a): int(t) for a, t in re.findall(r"A(\d)=(-?\d+)", m.group(1))}
            m = re.match(r"RP by polity Age \(turns, generated, spent, lost %\):(.*)", s)
            if m:
                runs[cur]["rp"] = {int(a): (int(t), float(g), float(sp)) for a, t, g, sp in
                                   re.findall(r"A(\d)\[(\d+)t ([\d.]+) ([\d.]+) [\d.]+%\]", m.group(1))}
            m = re.match(r"first technologies: (.*)", s)
            if m:
                runs[cur]["firstTech"] = m.group(1).split()
            m = re.match(r"first civics:\s+(.*)", s)
            if m:
                runs[cur]["firstCiv"] = m.group(1).split()
            m = re.match(r"end: Age (\d+) tech (\d+) civics (\d+) levels (\d+) population (\d+) settlements (\d+)", s)
            if m:
                runs[cur]["end"] = tuple(int(x) for x in m.groups())
            m = re.match(r"infrastructure: structures (\d+) road developments (\d+) institutions (\d+); discovery: surveys (\d+) "
                         r"explorations (\d+) known cells (\d+)", s)
            if m:
                runs[cur]["infra"] = tuple(int(x) for x in m.groups())
    return runs


def main(paths):
    runs = parse(paths)
    seeds = sorted({s for s, _ in runs})
    modes = [m for m in ("OFF", "ON", "ON-LANE") if any(k[1] == m for k in runs)]
    out = ["### Polity Age entered (effective turn)", "",
           "| Seed | Mode | " + " | ".join(f"A{a}" for a in range(2, 10)) + " |", "|---|---|" + "---:|" * 8]
    for s in seeds:
        for m in modes:
            e = runs.get((s, m), {}).get("entered", {})
            out.append(f"| {s} | {m} | " + " | ".join(str(e.get(a, "-")) for a in range(2, 10)) + " |")
    for key, title in (("tech", "Technology"), ("civ", "Civics")):
        out += ["", f"### {title}: turn all finite content of an Age is complete", "",
                "| Seed | Mode | " + " | ".join(f"A{a}" for a in range(1, 10)) + " |", "|---|---|" + "---:|" * 9]
        for s in seeds:
            for m in modes:
                e = runs.get((s, m), {}).get(key, {})
                out.append(f"| {s} | {m} | " + " | ".join(str(e.get(a, "-")) for a in range(1, 10)) + " |")
    out += ["", "### RP lost (share of RP generated while the polity was in each Age; all four seeds pooled)", "",
            "| Mode | " + " | ".join(f"A{a}" for a in range(1, 10)) + " | All Ages |", "|---|" + "---:|" * 10]
    for m in modes:
        g, sp = defaultdict(float), defaultdict(float)
        for s in seeds:
            for a, (_, gen, spent) in runs.get((s, m), {}).get("rp", {}).items():
                g[a] += gen
                sp[a] += spent
        cells = [f"{100 * (g[a] - sp[a]) / g[a]:.1f}%" if g[a] else "-" for a in range(1, 10)]
        tot = sum(g.values())
        cells.append(f"{100 * (tot - sum(sp.values())) / tot:.1f}%" if tot else "-")
        out.append(f"| {m} | " + " | ".join(cells) + " |")
    out += ["", "### End state (turn 1,630) and the economy around research", "",
            "| Seed | Mode | Age | Tech | Civics | Levels | Population | Structures | Road developments | Institutions | "
            "Surveys | Explorations | Known cells |", "|---|---|" + "---:|" * 11]
    for s in seeds:
        for m in modes:
            r = runs.get((s, m), {})
            e, i = r.get("end"), r.get("infra")
            if e and i:
                out.append(f"| {s} | {m} | {e[0]} | {e[1]} | {e[2]} | {e[3]} | {e[4]:,} | {i[0]} | {i[1]} | {i[2]} | {i[3]} | {i[4]} | {i[5]} |")
    out += ["", "### First five completions per tree (node@turn), seed 42", ""]
    for m in modes:
        r = runs.get((42, m), {})
        out.append(f"- **{m}** technology: {' '.join(r.get('firstTech', [])[:5])}; civics: {' '.join(r.get('firstCiv', [])[:5])}")
    print("\n".join(out))


if __name__ == "__main__":
    main(sys.argv[1:])
