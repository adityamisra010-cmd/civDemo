#!/usr/bin/env python3
"""Pacing and cost analysis for the current and proposed research graphs.

DESIGN-PACKAGE TOOLING ONLY (no runtime coupling). Every turn figure this tool
prints is an ESTIMATE from a static model, never a simulation result:

  turns_to(X) = first turn T at which the cumulative Research Points of an RP
                trajectory reach closure_cost(X)

closure_cost(X) is the cost of X plus a cheapest satisfying prerequisite set
(greedy over OR alternatives). The model assumes a polity researches only that
closure, in a valid order, with no Eureka, no modifier, no foreign exposure and
no overflow; it ignores the one-turn floor per node except as a check. It is
therefore an optimistic LOWER BOUND on the turn of first capability.

RP trajectories: the MEASURED seed traces written by the baseline measurement
(measurements/raw/trace-s<seed>.csv, columns turn,...,researchPointsThisTurn) or,
if absent, scripts/research-calibration/population-trace-seed42.csv. Scenario
multipliers scale RP per turn: weak = 0.5 x population (RP x 0.5^0.699),
average = measured, strong = 4 x population (RP x 4^0.699). These are
sensitivity scenarios, not forecasts.

Usage: python3 -I tools/balance.py [--graph current|proposed] [--json out.json]
"""
import csv
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402

EXPONENT = 0.699
COEFF = 0.08
# Scenarios (stated in research-balance-analysis.md): weak = half the population of a
# non-expanding polity (ESTIMATE: driver RP x 0.5^0.699); average = the MEASURED driver trace
# (a polity that never founds, `sim research --auto cheapest`); strong = the AI empire's own
# population (`sim run --ai-empires 1`, telemetry), RP = 0.08 x P^0.699 on the opening population.
SCENARIOS = {"weak": ("driver", 0.5 ** EXPONENT), "average": ("driver", 1.0), "strong": ("ai", 1.0)}


def load_trace(seed):
    cands = [kr.PKG / "measurements" / "raw" / f"trace-s{seed}.csv",
             kr.PKG / "measurements" / "raw" / f"population-trace-s{seed}.csv",
             kr.PKG / "measurements" / "raw" / f"population-trace-seed{seed}.csv"]
    if seed == 42:
        cands.append(kr.ROOT / "scripts" / "research-calibration" / "population-trace-seed42.csv")
    for p in cands:
        if p.exists():
            rows = []
            with open(p, encoding="utf-8") as fh:
                for r in csv.DictReader(fh):
                    rows.append((int(r["turn"]), float(r["simYear"]), float(r["researchPointsThisTurn"])))
            return p, rows
    return None, None


def load_ai_trace(seed, polity=2):
    p = kr.PKG / "measurements" / "raw" / "ai" / f"ai-polity-population-seed{seed}.csv"
    if not p.exists():
        return None, None
    rows = []
    with open(p, encoding="utf-8") as fh:
        for r in csv.DictReader(fh):
            if int(r["controller"]) == polity:
                pop = float(r["population_opening"])
                rows.append((int(r["turn"]), 0.0, COEFF * pop ** EXPONENT if pop > 0 else 0.0))
    return p, rows


def trajectory(seed, scenario):
    src, mult = SCENARIOS[scenario]
    path, rows = load_trace(seed) if src == "driver" else load_ai_trace(seed)
    return path, rows, mult


def cumulative(rows, mult):
    out, acc = [], 0.0
    for turn, year, rp in rows:
        acc += rp * mult
        out.append((turn, year, acc))
    return out


def turn_reaching(cum, amount):
    for turn, year, acc in cum:
        if acc >= amount:
            return turn, year
    return None, None


def graph_nodes(which):
    if which == "current":
        inv = kr.load_json(kr.DATA / "current-tree-inventory.json")
        return inv["nodes"], inv["summary"]["ageFacts"], inv["summary"]["researchStage"]["requires"]
    prop = kr.load_json(kr.PKG / "technology-tree-proposal.json")
    civ = kr.load_json(kr.PKG / "civics-tree-proposal.json")
    nodes = [n for n in prop["nodes"] if n.get("status") != "retired"] + \
            [n for n in civ["nodes"] if n.get("status") != "retired"]
    return nodes, prop["ageFacts"], prop["researchStage"]["requires"]


def milestone_targets(nodes, age_facts, stage_expr):
    """(label, expression) pairs for the capability milestones analysed."""
    ids = {n["id"] for n in nodes}
    out = [("research stage (five subtrees open)", stage_expr)]
    for age in range(2, 10):
        core = [f for f in age_facts if f["age_key"] == age and f["role"] == "core"
                and f["fact"].get("kind") == "research"]
        if core:
            parts = ["(" + " OR ".join(x for x in f["fact"]["nodes"] if x in ids) + ")" for f in core]
            out.append((f"A{age} CORE research facts", " AND ".join(parts)))
    return out


def analyse(which="current"):
    nodes, age_facts, stage_expr = graph_nodes(which)
    g = kr.Graph(nodes)
    targets = milestone_targets(nodes, age_facts, stage_expr)
    res = {"graph": which, "nodeCount": len(nodes),
           "totalCost": round(sum(float(n.get("cost") or 0) for n in nodes), 1), "targets": []}
    for label, expr in targets:
        ast = kr.parse(expr)
        clos = g._expr_closure(ast)
        cost = sum(g.cost(j) for j in clos)
        depth = g._expr_depth(ast)
        row = {"target": label, "expression": expr, "closureCount": len(clos),
               "closureCost": round(cost, 1), "depth": depth, "turns": {}}
        for seed in (1, 2, 42, 7):
            for sc in SCENARIOS:
                path, rows, mult = trajectory(seed, sc)
                if not rows:
                    continue
                t, y = turn_reaching(cumulative(rows, mult), cost)
                row["turns"][f"s{seed}/{sc}"] = {"turn": t, "simYear": y}
        res["targets"].append(row)
    res["traces"] = {f"s{s}/{sc}": (str(trajectory(s, sc)[0].relative_to(kr.ROOT)) if trajectory(s, sc)[0] else None)
                     for s in (1, 2, 42, 7) for sc in SCENARIOS}
    res["totals"] = {f"s{s}/{sc}": round(sum(r[2] for r in (trajectory(s, sc)[1] or [])) * trajectory(s, sc)[2], 0)
                     for s in (1, 2, 42, 7) for sc in SCENARIOS}
    return res


def cp_coupling(which="current", ratios=(0.05, 0.1, 0.15, 0.25, 0.5, 1.0), extra_civics=(0.0, 10000.0, 30000.0)):
    """ESTIMATE of how a separate Civics Points economy could delay technology through civic
    atoms in technology gates (the research stage, Age CORE facts). Model: technology closure
    nodes are paid from RP, civic closure nodes from CP; CP per turn = ratio x RP per turn of the
    same polity (a stand-in until D-061.3 rules CP's inputs); `extra` = CP the polity spends on
    other civics before the gating ones. The gate opens at max(tech turn, civic turn)."""
    nodes, age_facts, stage_expr = graph_nodes(which)
    g = kr.Graph(nodes)
    tree = {n["id"]: n.get("tree", "technology") for n in nodes}
    out = []
    targets = milestone_targets(nodes, age_facts, stage_expr)
    for label, expr in targets:
        clos = g._expr_closure(kr.parse(expr))
        civ = sum(g.cost(j) for j in clos if tree.get(j) == "civics")
        tech = sum(g.cost(j) for j in clos if tree.get(j) != "civics")
        if civ == 0:
            continue
        row = {"target": label, "techRP": round(tech, 1), "civicCP": round(civ, 1), "cases": []}
        for seed in (1, 2, 42, 7):
            path, rows = load_trace(seed)
            if not rows:
                continue
            cum = cumulative(rows, 1.0)
            t_tech, _ = turn_reaching(cum, tech)
            t_one, _ = turn_reaching(cum, tech + civ)
            for r in ratios:
                for extra in extra_civics:
                    t_civ, _ = turn_reaching(cumulative(rows, r), civ + extra)
                    gate = None if (t_tech is None or t_civ is None) else max(t_tech, t_civ)
                    row["cases"].append({"seed": seed, "ratio": r, "extraCP": extra, "techTurn": t_tech,
                                         "civicTurn": t_civ, "gateTurn": gate, "onePoolTurn": t_one,
                                         "delayVsTech": None if gate is None or t_tech is None else gate - t_tech})
        out.append(row)
    return out


def main(argv):
    which = "proposed" if "--graph" in argv and argv[argv.index("--graph") + 1] == "proposed" else "current"
    res = analyse(which)
    res["cpCoupling"] = cp_coupling(which)
    if "--json" in argv:
        kr.dump_json(res, argv[argv.index("--json") + 1])
    print(f"graph {which}: {res['nodeCount']} nodes, total cost {res['totalCost']:,.0f}")
    for r in res["targets"]:
        tv = r["turns"]
        cells = " ".join(f"{k}={v['turn']}" for k, v in tv.items() if k.startswith("s42/"))
        print(f"  {r['target']:38s} n={r['closureCount']:4d} cost={r['closureCost']:>12,.0f} depth={r['depth']:3d} {cells}")
    for c in res["cpCoupling"]:
        s42 = [x for x in c["cases"] if x["seed"] == 42 and x["extraCP"] == 0.0]
        print(f"  CP coupling {c['target']}: tech {c['techRP']:,.0f} RP + civic {c['civicCP']:,.0f} CP; seed42 delay by ratio: " +
              ", ".join(f"{x['ratio']}->{x['delayVsTech']}" for x in s42))


if __name__ == "__main__":
    main(sys.argv[1:])
