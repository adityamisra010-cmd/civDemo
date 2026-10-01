#!/usr/bin/env python3
"""Research-content calibration report (Director ruling 2026-10-01 §3, §4).

Reads the GENERATED research content (Sim.Data/content/research.json), the per-node
calibration bands (scripts/research-calibration/nodes.json) and a MEASURED population
trace of the canonical world (scripts/research-calibration/population-trace-seed42.csv,
written by `sim research --seed 42 --turns 1630 --trace-population`), and writes
docs/research-calibration-report.md:

  * node count, total / average / median / min / max cost, by Age;
  * total finite research workload (finite = no repeatable descriptor);
  * Eureka credit under the current model (evaluable conditions) and its upper bound;
  * a turn-by-turn pacing projection over the whole campaign (reference civilization);
  * the FINAL-AGE pacing estimate under stated scenarios, classified <150 / 150-250 / >250.

Every assumption is a named constant below and is printed in the report. Nothing here
is ratified architecture: it is PROVISIONAL CALIBRATION.

Usage:  python3 scripts/research-calibration-report.py            (write the report)
        python3 scripts/research-calibration-report.py --check    (exit 1 if the report is stale
                                                                   or AGE_UNIT disagrees with the derivation)
        python3 scripts/research-calibration-report.py --derive   (print the derived AGE_UNIT)
"""
import csv
import importlib.util
import json
import math
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONTENT = ROOT / "Sim.Data" / "content" / "research.json"
TRACE = ROOT / "scripts" / "research-calibration" / "population-trace-seed42.csv"
REPORT = ROOT / "docs" / "research-calibration-report.md"

AGES = ["A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9"]
FINAL_AGE = "A9"

# --------------------------------------------------------------------------
# ASSUMPTIONS (PROVISIONAL CALIBRATION — every one is printed in the report)
# --------------------------------------------------------------------------
# Calendar window each Age's research content is paced to, for the REFERENCE
# civilization (calendar years; the campaign starts at -4000). The Age -> calendar /
# dt mapping is OPEN (architecture §12.3); these windows are calibration targets only,
# never gates. A1 and A2 predate the campaign start: with zero starting knowledge
# (Director ruling §1) they are researched in the opening turns.
AGE_WINDOW_END = {"A1": -3600, "A2": -3000, "A3": -1200, "A4": -300, "A5": 500,
                  "A6": 1450, "A7": 1800, "A8": 1920}
CAMPAIGN_START = -4000

# FINAL-AGE target (Director ruling §4): ~200 turns of finite meaningful research for an
# exceptionally well-developed, research-optimized civilization; 150 lower bound, 250 upper.
FINAL_TARGET_TURNS = 200
FINAL_BAND = (150, 250)
# The research-optimized civilization (the scenario the target is calibrated on):
#   population  — the canonical seed-42 polity's measured population over the first
#                 FINAL_TARGET_TURNS turns of the final-Age window (it is the most
#                 developed polity the simulation produces);
#   modifiers   — FUTURE university specialization and research-efficiency modifiers do
#                 not exist yet; assumed to multiply effective throughput by OPT_MODIFIER
#                 (equivalently EffectiveCost x 1/OPT_MODIFIER);
#   Eurekas     — every counted Eureka condition satisfied (the 40 % upper bound), since
#                 future systems will make the C/F conditions evaluable.
OPT_MODIFIER = 1.5
# The final Age begins at this calendar year in the pacing projection (the start of the
# current "Modern" dt band; OPEN like every Age -> calendar mapping).
FINAL_AGE_START = 1920
# Sensitivity rows shown beside the optimized scenario.
SENSITIVITY = [  # label, modifier, eureka mode
    ("research-optimized, current Eurekas only", OPT_MODIFIER, "current"),
    ("strongly optimized (modifiers x2.0), all Eurekas", 2.0, "full"),
    ("well developed, no future modifiers (x1.0), all Eurekas", 1.0, "full"),
    ("poorly developed: no modifiers, current Eurekas", 1.0, "current"),
]


def load_generator():
    spec = importlib.util.spec_from_file_location("gen", ROOT / "scripts" / "migrate-research-corpus.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def load_trace():
    rows = []
    with TRACE.open(encoding="utf-8") as f:
        for r in csv.DictReader(f):
            rows.append({"turn": int(r["turn"]), "year": float(r["simYear"]) + CAMPAIGN_START,
                         "dt": float(r["dtYears"]), "pop": int(r["population"])})
    return rows


def rp_per_turn(tuning, pop, dt):
    """The engine's credit for one turn: RP(P) x dt / rpReferenceTurnYears (ResearchQuery)."""
    a, b = tuning["rpAnchors"]
    e = math.log(b["rpPerTurn"] / a["rpPerTurn"]) / math.log(b["population"] / a["population"])
    rp = 0.0 if pop <= 0 else a["rpPerTurn"] * (pop / a["population"]) ** e
    return rp * dt / tuning["rpReferenceTurnYears"]


def eureka_share(node, mode):
    """Fraction of the full Eureka the scenario credits: 'current' = evaluable conditions,
    'full' = every counted condition (A, B, C, F), 'none' = 0."""
    if mode == "none":
        return 0.0
    if mode == "full":
        return 1.0 if any(e["category"] in "ABCF" for e in node["eurekas"]) else 0.0
    total = sum(1.0 if e["weight"] is None else e["weight"] for e in node["eurekas"] if e["category"] in "ABCF")
    ev = sum(1.0 if e["weight"] is None else e["weight"] for e in node["eurekas"] if e["category"] in "AB")
    return 0.0 if total == 0 else ev / total


class Graph:
    def __init__(self, content, gen):
        self.nodes = content["technologies"] + content["civics"]
        self.ids = [n["id"] for n in self.nodes]
        self.gen = gen
        self.stage = content["researchStage"]["requires"]
        self.full = content["tuning"]["eurekaFullCreditFraction"]

    def available(self, n, done):
        if n["id"] in done:
            return False
        if n.get("branch") is not None and not self.gen.holds_on(self.stage, done):
            return False
        return n["prereq"] is None or self.gen.holds_on(n["prereq"], done)


def simulate(graph, todo, done, rp_at, modifier, mode, max_turns=100000, start_turn=1, priority=None):
    """Research `todo` (ids) from `done`, one target at a time, cheapest available first
    (cost, key) — the `--auto cheapest` measurement driver. `modifier` stands for FUTURE
    cost modifiers: EffectiveCost = cost / modifier. Eureka credit (40 % of BASE cost x the
    scenario's share) lands when a node becomes available, capped at EffectiveCost; CLP past
    a completion is lost (ADR-029 R-2). Returns ({id: turns used when it completed}, turns
    used for all of `todo`, or None if not finished)."""
    done, todo = set(done), set(todo)
    by_id = {n["id"]: n for n in graph.nodes}
    progress, credited, finished = {}, set(), {}
    t, target = 0, None
    while todo and t < max_turns:
        avail = [by_id[i] for i in sorted(todo, key=lambda i: by_id[i]["key"]) if graph.available(by_id[i], done)]
        for n in avail:
            if n["id"] not in credited:
                credited.add(n["id"])
                eff = n["cost"] / modifier
                progress[n["id"]] = min(eff, progress.get(n["id"], 0.0) + graph.full * n["cost"] * eureka_share(n, mode))
        if target is None:
            pick = min(avail, key=priority or (lambda n: (n["cost"], n["key"])), default=None)
            if pick is None:
                return finished, None
            target = pick["id"]
        eff = by_id[target]["cost"] / modifier
        progress[target] = min(eff, progress.get(target, 0.0) + rp_at(start_turn + t))
        t += 1
        for n in avail:
            if progress.get(n["id"], 0.0) >= n["cost"] / modifier:
                todo.discard(n["id"])
                done.add(n["id"])
                finished[n["id"]] = t
                if n["id"] == target:
                    target = None
    return finished, (t if not todo else None)


def age_stats(nodes):
    out = {}
    for a in AGES:
        cs = [n["cost"] for n in nodes if n["age"] == a]
        fin = [n["cost"] for n in nodes if n["age"] == a and not n["repeatable"]]
        out[a] = {"n": len(cs), "total": sum(cs), "avg": sum(cs) / len(cs), "median": statistics.median(cs),
                  "min": min(cs), "max": max(cs), "finite_n": len(fin), "finite_total": sum(fin)}
    return out


def build_report(derive_only=False):
    gen = load_generator()
    content = json.loads(CONTENT.read_text(encoding="utf-8"))
    tuning = content["tuning"]
    trace = load_trace()
    graph = Graph(content, gen)
    techs = content["technologies"]
    by_turn = {r["turn"]: r for r in trace}
    last = trace[-1]["turn"]

    def rp_ref(turn):
        r = by_turn[min(max(turn, 1), last)]
        return rp_per_turn(tuning, r["pop"], r["dt"])

    def turn_of_year(year):
        for r in trace:
            if r["year"] >= year:
                return r["turn"]
        return last

    # ---- derivation of AGE_UNIT (what the generator's costs should be) ----
    # Each Age A1..A8: the largest unit (to 0.01) with which the REFERENCE civilization,
    # starting at its window's first turn with every earlier Age complete (and any
    # later-Age prerequisite treated as known), finishes the Age's content (technologies and
    # civics) by the window's end — turn by turn, current Eurekas, overflow loss included.
    windows, derived = {}, {}
    gwin = Graph(json.loads(CONTENT.read_text(encoding="utf-8")), gen)
    by_id = {n["id"]: n for n in gwin.nodes}
    prereq_atoms = {n["id"]: gen.atoms(n["prereq"]) for n in gwin.nodes}
    stage_atoms = gen.atoms(gwin.stage)
    start = 1
    for a in AGES[:-1]:
        end = turn_of_year(AGE_WINDOW_END[a])
        windows[a] = (start, end)
        todo = [n["id"] for n in gwin.nodes if n["age"] == a]
        roots = [x for i in todo for x in prereq_atoms[i]] + (stage_atoms if any(by_id[i].get("branch") for i in todo) else [])
        need = gen.closure(roots, prereq_atoms)
        done = {n["id"] for n in gwin.nodes if AGES.index(n["age"]) < AGES.index(a)} | {
            i for i in need if AGES.index(by_id[i]["age"]) > AGES.index(a)}

        def turns_at(u, todo=todo, done=done, start=start):
            for i in todo:
                by_id[i]["cost"] = float(max(1, int(round(gen.BAND_WEIGHT[n_band(gen, by_id[i])] * u))))
            used = simulate(gwin, todo, done, rp_ref, 1.0, "current", start_turn=start)[1]
            return math.inf if used is None else used
        span = end - start + 1
        lo, hi = 0.01, 1.0
        while turns_at(hi) <= span:
            hi *= 2
        while hi - lo > 0.005:
            mid = (lo + hi) / 2
            lo, hi = (mid, hi) if turns_at(mid) <= span else (lo, mid)
        derived[a] = math.floor(lo * 100) / 100
        start = end + 1
    f0 = turn_of_year(FINAL_AGE_START)
    window = range(f0 + 1, min(last, f0 + FINAL_TARGET_TURNS) + 1)   # the turns AFTER the final-Age start
    pop_opt = statistics.mean(by_turn[t]["pop"] for t in window)
    dt_final = statistics.mean(by_turn[t]["dt"] for t in window)
    rp_opt = statistics.mean(rp_per_turn(tuning, by_turn[t]["pop"], by_turn[t]["dt"]) for t in window)
    finals = [n for n in techs if n["age"] == FINAL_AGE and not n["repeatable"]]
    load9 = sum(gen.BAND_WEIGHT[n_band(gen, n)] * (1 / OPT_MODIFIER - tuning["eurekaFullCreditFraction"] * eureka_share(n, "full"))
                for n in finals)
    derived[FINAL_AGE] = FINAL_TARGET_TURNS * rp_opt / load9
    # Overflow loss (R-2) is not in that budget arithmetic, and it is not smooth in the unit
    # (it depends on how node costs fall against the per-turn RP). So the final-Age unit is
    # found on the turn-by-turn projection: bisection finds the step where the optimized
    # projection crosses the target; of the two units either side (0.01 apart) the one
    # whose turns are CLOSER to the target is taken (ties: the smaller unit).
    graph9 = Graph(content, gen)
    earlier9 = {n["id"] for n in graph9.nodes if n.get("age") != FINAL_AGE}
    final_ids9 = [n["id"] for n in finals]

    def turns_at(u):
        for n in graph9.nodes:
            if n.get("age") == FINAL_AGE:
                n["cost"] = float(max(1, int(round(gen.BAND_WEIGHT[n_band(gen, n)] * u))))
        return simulate(graph9, final_ids9, earlier9, lambda t: rp_opt, OPT_MODIFIER, "full")[1]
    lo, hi = 0.01, derived[FINAL_AGE] * 2
    while turns_at(hi) <= FINAL_TARGET_TURNS:
        hi *= 2
    while hi - lo > 0.005:
        mid = (lo + hi) / 2
        lo, hi = (mid, hi) if turns_at(mid) <= FINAL_TARGET_TURNS else (lo, mid)
    below = math.floor(lo * 100) / 100
    above = math.ceil(hi * 100) / 100
    while turns_at(below) > FINAL_TARGET_TURNS:      # rounding to 0.01 must keep each side on its side of the step
        below = round(below - 0.01, 2)
    while turns_at(above) <= FINAL_TARGET_TURNS:
        above = round(above + 0.01, 2)
    t_below, t_above = turns_at(below), turns_at(above)
    derived[FINAL_AGE] = above if abs(t_above - FINAL_TARGET_TURNS) < abs(t_below - FINAL_TARGET_TURNS) else below
    step_rows = [(u, turns_at(u)) for u in (0.5 * derived[FINAL_AGE], 0.75 * derived[FINAL_AGE], below, above,
                                             1.25 * derived[FINAL_AGE], 1.5 * derived[FINAL_AGE], 2 * derived[FINAL_AGE])]
    content = json.loads(CONTENT.read_text(encoding="utf-8"))   # graph9 edited costs in place
    techs = content["technologies"]
    graph = Graph(content, gen)
    finals = [n for n in techs if n["age"] == FINAL_AGE and not n["repeatable"]]
    derived = {a: round(v, 2) for a, v in derived.items()}
    if derive_only:
        return derived, None
    floor_turns = len(finals)

    stats = age_stats(techs)
    L = []
    L.append("# Research calibration report")
    L.append("")
    L.append("**GENERATED** by `scripts/research-calibration-report.py` (do not edit by hand) from `Sim.Data/content/research.json`, "
             "`scripts/research-calibration/nodes.json` and the measured population trace "
             "`scripts/research-calibration/population-trace-seed42.csv`. Everything here is **PROVISIONAL CALIBRATION** "
             "(Director ruling 2026-10-01 §2–§4); nothing in it is ratified architecture.")
    L.append("")
    L.append("## 1. The model being calibrated")
    L.append("")
    a, b = tuning["rpAnchors"]
    e = math.log(b["rpPerTurn"] / a["rpPerTurn"]) / math.log(b["population"] / a["population"])
    L.append(f"- **Research capacity:** RP(P) = {a['rpPerTurn']} × (P / {a['population']})^{e:.5f} per turn of "
             f"{tuning['rpReferenceTurnYears']} sim-years (anchors {a['population']} → {a['rpPerTurn']}, "
             f"{b['population']} → {b['rpPerTurn']}). A step credits RP(P) × dtYears / {tuning['rpReferenceTurnYears']} "
             "(law 3). P = the polity's total population. The per-turn vs per-sim-year reading is OPEN (CR-018).")
    L.append(f"- **Eureka:** a fully satisfied Eureka credits {tuning['eurekaFullCreditFraction']:.0%} of BASE cost; "
             "each condition its normalized share; capped at the remaining cost; never twice.")
    L.append(f"- **Cost:** cost = round(BAND_WEIGHT[band] × AGE_UNIT[age]). BAND_WEIGHT = `{json.dumps(gen.BAND_WEIGHT)}`. "
             f"AGE_UNIT = `{json.dumps(gen.AGE_UNIT)}`.")
    L.append("- **Throughput loss:** CLP past a node's completion is lost (ADR-029 §13 R-2, no general bank), so many "
             "cheap nodes cost more turns than their summed cost suggests. The projections below simulate it turn by turn.")
    L.append("")
    L.append("## 2. Assumptions (all of them)")
    L.append("")
    L.append(f"1. **Population:** the canonical world (seed 42), polity 1, measured every turn to turn {last}. Founding "
             f"population {trace[0]['pop']}; at the final-Age start (year {FINAL_AGE_START}, turn {f0}) {by_turn[f0]['pop']}; "
             f"at turn {last} {trace[-1]['pop']}. It is the most developed polity the simulation currently produces.")
    L.append("2. **Age pacing windows** (reference civilization; calendar year each Age's content should be done by): "
             + ", ".join(f"{k} {v}" for k, v in AGE_WINDOW_END.items())
             + f"; {FINAL_AGE} from {FINAL_AGE_START}. The Age → calendar/dt mapping is OPEN (architecture §12.3); these "
             "are calibration targets, never gates (Age stays metadata, law 4).")
    L.append(f"3. **Reference civilization** (Ages A1–A8): that measured population, no future modifiers (×1.0), "
             "every currently evaluable Eureka condition credited when its node becomes available.")
    L.append(f"4. **Research-optimized civilization** (the final-Age target): mean population over the first "
             f"{FINAL_TARGET_TURNS} final-Age turns = {pop_opt:,.0f}; mean dt {dt_final} (current Modern / Information+ "
             f"bands) → mean {rp_opt:.3f} RP/turn; FUTURE university specialization and research-efficiency "
             f"modifiers assumed to divide EffectiveCost by **{OPT_MODIFIER}** (they cannot be simulated yet); every counted "
             "Eureka condition satisfied (the 40 % upper bound).")
    L.append("5. **Order of research:** cheapest available first, ties by key — the `sim research --auto cheapest` driver.")
    L.append("6. **Finite vs recursive:** the final-Age target applies to the FINITE corpus: nodes with a repeatable "
             "descriptor (recursive-research candidates) are excluded and reported separately.")
    L.append("")
    L.append("## 3. Cost by Age")
    L.append("")
    L.append("| Age | nodes | total cost | average | median | min | max | finite nodes | finite total | AGE_UNIT (in use) | AGE_UNIT (derived) |")
    L.append("|---|---|---|---|---|---|---|---|---|---|---|")
    for ag in AGES:
        s = stats[ag]
        L.append(f"| {ag} | {s['n']} | {s['total']:,.0f} | {s['avg']:.1f} | {s['median']:.1f} | {s['min']:.0f} | {s['max']:.0f} | "
                 f"{s['finite_n']} | {s['finite_total']:,.0f} | {gen.AGE_UNIT[ag]} | {derived[ag]} |")
    civ = content["civics"]
    L.append(f"| Civics | {len(civ)} | {sum(c['cost'] for c in civ):,.0f} | | | {min(c['cost'] for c in civ):.0f} | "
             f"{max(c['cost'] for c in civ):.0f} | | | | |")
    tot = sum(n["cost"] for n in techs)
    fin = sum(n["cost"] for n in techs if not n["repeatable"])
    L.append("")
    L.append(f"- **Total research workload:** {tot:,.0f} RP over {len(techs)} technologies (+ {sum(c['cost'] for c in civ):,.0f} "
             f"for {len(civ)} civics). **Finite** (no repeatable descriptor): {fin:,.0f} RP over "
             f"{sum(1 for n in techs if not n['repeatable'])} technologies.")
    bands = {}
    for n in techs:
        bands.setdefault(n_band(gen, n), []).append(n["cost"])
    L.append("- **Bands:** " + ", ".join(f"{bd} ×{gen.BAND_WEIGHT[bd]} — {len(v)} nodes" for bd, v in sorted(bands.items())))
    L.append("")
    L.append("## 4. Eureka credit under the current model")
    L.append("")
    L.append("| Age | nodes with an evaluable condition | current credit (RP) | current credit % of cost | upper bound (all counted conditions) % |")
    L.append("|---|---|---|---|---|")
    full = tuning["eurekaFullCreditFraction"]
    for ag in AGES:
        ns = [n for n in techs if n["age"] == ag]
        cur = sum(full * n["cost"] * eureka_share(n, "current") for n in ns)
        up = sum(full * n["cost"] * eureka_share(n, "full") for n in ns)
        tc = sum(n["cost"] for n in ns)
        L.append(f"| {ag} | {sum(1 for n in ns if eureka_share(n, 'current') > 0)} | {cur:,.1f} | {cur / tc:.1%} | {up / tc:.1%} |")
    L.append("")
    L.append("## 5. Whole-campaign projection (reference civilization)")
    L.append("")
    fin_ids, used = simulate(graph, [n["id"] for n in graph.nodes], set(), rp_ref, 1.0, "current", max_turns=last,
                             priority=lambda n: (AGES.index(n["age"]), n["cost"], n["key"]))
    L.append(f"Turn-by-turn from founding with zero knowledge, the measured RP per turn, earliest Age first then cheapest "
             f"(a historically-paced player), current Eurekas, no future modifiers. "
             f"{'All nodes complete by turn ' + str(used) if used else f'{len(fin_ids)} of {len(graph.nodes)} nodes complete by turn {last}'}.")
    L.append("")
    L.append("| Age | nodes | done by turn (year) | window ends turn (year) | verdict |")
    L.append("|---|---|---|---|---|")
    for ag in AGES:
        ids = [n["id"] for n in techs if n["age"] == ag]
        dn = [fin_ids[i] for i in ids if i in fin_ids]
        wend = turn_of_year(AGE_WINDOW_END.get(ag, 2100))
        if len(dn) == len(ids):
            t = max(dn)
            L.append(f"| {ag} | {len(ids)} | {t} ({by_turn[min(t, last)]['year']:.0f}) | {wend} ({by_turn[wend]['year']:.0f}) | "
                     f"{'within window' if t <= wend else 'late by ' + str(t - wend) + ' turns'} |")
        else:
            L.append(f"| {ag} | {len(ids)} | {len(dn)} done by turn {last} | {wend} | incomplete |")
    L.append("")
    L.append("## 6. Final-Age pacing (the hard target)")
    L.append("")
    earlier = {n["id"] for n in graph.nodes if not (n.get("age") == FINAL_AGE)}
    final_ids = [n["id"] for n in finals]
    rows = [("**research-optimized (the calibration target)**", OPT_MODIFIER, "full")] + SENSITIVITY
    L.append(f"Start: every non-{FINAL_AGE} node complete. Research the {len(final_ids)} finite {FINAL_AGE} nodes "
             f"({sum(n['cost'] for n in finals):,.0f} RP base) at a constant {rp_opt:.3f} RP/turn, EffectiveCost = cost / modifier.")
    L.append("")
    L.append("| scenario | modifier | Eurekas | turns | band |")
    L.append("|---|---|---|---|---|")
    results = []
    for label, mod, mode in rows:
        _, used9 = simulate(graph, final_ids, earlier, lambda t: rp_opt, mod, mode)
        if used9 is None:
            sys.exit(f"final-Age projection deadlocked for scenario {label!r}")
        verdict = "<150" if used9 < FINAL_BAND[0] else ("150–250" if used9 <= FINAL_BAND[1] else ">250")
        results.append((label, used9, verdict))
        L.append(f"| {label} | ×{mod} | {mode} | **{used9}** | {verdict} |")
    rep = [n["id"] for n in techs if n["age"] == FINAL_AGE and n["repeatable"]]
    _, used_rep = simulate(graph, final_ids + rep, earlier, lambda t: rp_opt, OPT_MODIFIER, "full")
    L.append("")
    L.append(f"- **Result:** the research-optimized civilization needs **{results[0][1]} turns** for the finite final-Age corpus "
             f"— **{results[0][2]}** (target {FINAL_TARGET_TURNS}, band {FINAL_BAND[0]}–{FINAL_BAND[1]}).")
    L.append(f"- Including the {len(rep)} repeatable-descriptor nodes once each (recursive-research candidates, completed once "
             f"under idempotent completion): {used_rep} turns.")
    L.append("- **What drives it:** (1) AGE_UNIT[A9] — derived from the target, it scales every final-Age cost; (2) the "
             "final-Age population and dt (RP per turn ∝ P^0.699 × dt); (3) the assumed future-modifier multiplier; (4) Eureka "
             "satisfaction (up to −40 % of each node's base cost); (5) completion-overflow loss, which grows as per-turn RP "
             "approaches node costs; (6) the band mix (M1 ×1 … M5 ×10). Prerequisites order the work but add none, because "
             "research is serial (one target).")
    L.append(f"- **Structural floor — a finding for the Director:** research is serial (one target) and CLP past a "
             f"completion is lost (ADR-029 R-2), so every node costs at least one turn: the {floor_turns} finite final-Age "
             f"nodes can never take fewer than {floor_turns} turns, whatever their cost. At the final-Age RP per turn "
             f"({rp_opt:.1f}) most final-Age nodes take one or two turns, so turns move in STEPS as costs cross whole-turn "
             "multiples, not smoothly with cost:")
    L.append("")
    L.append("  | AGE_UNIT[A9] | optimized-scenario turns |")
    L.append("  |---|---|")
    for u, tu in step_rows:
        L.append(f"  | {u:.2f} | {tu} |")
    L.append("")
    L.append("  The unit in use is the one whose projection is closest to the 200-turn target. Pacing in this regime is "
             "governed by node count, RP per turn and the overflow rule more than by cost; whether completion overflow "
             "should carry to the next target (an R-2 change) is listed as OPEN.")
    L.append("- The pacing was NOT solved by making final-Age nodes enormous: final-Age costs are the same bands × one Age "
             "unit, and the Age total is what was calibrated.")
    L.append("")
    return derived, "\n".join(L) + "\n"


_BANDS = None


def n_band(gen, n):
    global _BANDS
    if _BANDS is None:
        doc = json.loads(gen.CALIBRATION.read_text(encoding="utf-8"))
        _BANDS = {r["id"]: r["band"] for r in doc["nodes"] + doc["civics"]}
    return _BANDS[n["id"]]


def main():
    if "--derive" in sys.argv[1:]:
        derived, _ = build_report(derive_only=True)
        print(json.dumps(derived))
        return
    derived, text = build_report()
    gen = load_generator()
    stale_units = {a: (gen.AGE_UNIT[a], derived[a]) for a in AGES if abs(gen.AGE_UNIT[a] - derived[a]) > 0.005}
    if "--check" in sys.argv[1:]:
        problems = []
        if not REPORT.exists() or REPORT.read_text(encoding="utf-8") != text:
            problems.append(str(REPORT.relative_to(ROOT)) + " is stale")
        if stale_units:
            problems.append(f"AGE_UNIT disagrees with the derivation: {stale_units}")
        if problems:
            sys.exit("; ".join(problems))
        print("the calibration report is up to date and AGE_UNIT matches its derivation")
        return
    REPORT.write_text(text, encoding="utf-8")
    print(f"wrote {REPORT.relative_to(ROOT)}" + (f" — AGE_UNIT differs from the derivation: {stale_units}" if stale_units else ""))


if __name__ == "__main__":
    main()
