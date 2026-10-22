#!/usr/bin/env python3
"""Baseline research-progression analysis (M6 knowledge redesign, measurement package).

READS ONLY measured CLI artefacts in ./raw (and the authored content files, read-only):
  raw/research-seed{S}.stdout.txt            `sim research ... --auto cheapest` report (MEASURED)
  raw/research-auto-orders-seed{S}.bin       the driver's order log (--emit-orders, MEASURED)
  raw/population-trace-seed{S}.csv           --trace-population (MEASURED)
  raw/turn0-session-trace-seed{S}.csv        `sim run --founded --turns 1 --emit-session` trace, row 0 (MEASURED)
  raw/checks/*.stdout.txt                     prefix runs used as direct cross-checks (MEASURED), if present
  raw/ai/...                                  AI session extracts (MEASURED), if present
WRITES baseline-node-completions.csv and baseline-summary.json next to this file, and prints a
plain-text digest that baseline-progression.md quotes.

Nothing here runs or changes the simulation. Every derived number is an INFERENCE from the
measured artefacts under the reconstruction rule documented in baseline-progression.md section 3.

Run:  python3 -I baseline_analysis.py   (from any directory)
"""
import csv
import json
import math
import re
import statistics
import struct
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
RAW = HERE / "raw"
ROOT = HERE.parents[3]          # repository root (docs/m6-plan/knowledge-redesign/measurements -> root)
RESEARCH = ROOT / "Sim.Data" / "content" / "research.json"
AGES = ROOT / "Sim.Data" / "content" / "ages.json"
SEEDS = [1, 2, 42, 7]
TURNS = 1630
SELECTED_TURNS = [1, 100, 200, 250, 300, 500, 650, 800, 950, 1000, 1150, 1200, 1270, 1400, 1430, 1630]
CIVIC_KEYS = [1001, 1002, 1003, 1004, 1005, 1006, 1007]
KIND_SET_RESEARCH_TARGET = 6
KIND_ADVANCE_AGE = 7


# ----------------------------------------------------------------------------- content

def load_content():
    r = json.loads(RESEARCH.read_text(encoding="utf-8"))
    nodes = {}
    for tree, items in (("technology", r["technologies"]), ("civics", r["civics"])):
        for n in items:
            branch = "civics" if tree == "civics" else (n.get("branch") or "main")
            evaluable = [e for e in (n.get("eurekas") or []) if e.get("when")]
            nodes[n["key"]] = dict(key=n["key"], id=n["id"], tree=tree, branch=branch, age=n["age"],
                                   base=float(n["cost"]), prereq=n.get("prereq"),
                                   repeatable=bool(n.get("repeatable")),
                                   eureka_evaluable=len(evaluable) > 0,
                                   eureka_weight=sum(float(e.get("weight") or 0) for e in evaluable))
    by_id = {v["id"]: k for k, v in nodes.items()}
    tuning = r["tuning"]
    stage = r["researchStage"]["requires"]
    return nodes, by_id, tuning, stage


def load_ages():
    a = json.loads(AGES.read_text(encoding="utf-8"))
    out = []
    for age in a["ages"]:
        entry = age.get("entry")
        if not entry:
            continue
        for role in ("core", "supporting"):
            for m in entry[role]:
                f = m["fact"]
                if f["kind"] in ("research", "research_count"):
                    out.append(dict(age=age["key"], age_id=age["id"], role=role.upper(), id=m["id"],
                                    kind=f["kind"], nodes=f.get("nodes"), tree=f.get("tree"), min=f.get("min")))
    return out


# --- the D-020 predicate dialect, atoms only (stage expression); mirrors scripts/research_lib.py ---
TOKEN = re.compile(r"\s*(\(|\)|[A-Za-z_][A-Za-z0-9_.]*)")


def parse(expr):
    toks, pos = [], 0
    while pos < len(expr):
        m = TOKEN.match(expr, pos)
        if not m:
            if expr[pos:].strip() == "":
                break
            raise ValueError(expr)
        toks.append(m.group(1))
        pos = m.end()
    i = 0

    def p_or():
        nonlocal i
        items = [p_and()]
        while i < len(toks) and toks[i] == "OR":
            i += 1
            items.append(p_and())
        return items[0] if len(items) == 1 else ("or", items)

    def p_and():
        nonlocal i
        items = [p_un()]
        while i < len(toks) and toks[i] == "AND":
            i += 1
            items.append(p_un())
        return items[0] if len(items) == 1 else ("and", items)

    def p_un():
        nonlocal i
        t = toks[i]
        if t == "(":
            i += 1
            x = p_or()
            i += 1
            return x
        i += 1
        return ("atom", t)

    return p_or()


def holds(node, complete_ids):
    if node[0] == "atom":
        return node[1] in complete_ids
    vals = [holds(c, complete_ids) for c in node[1]]
    return all(vals) if node[0] == "and" else any(vals)


# ----------------------------------------------------------------------------- artefacts

def read_orders(path):
    data = path.read_bytes()
    if data[:8] != b"CIVORDR\x00":
        raise ValueError(f"{path}: bad magic")
    version, count = struct.unpack_from("<ii", data, 8)
    assert version == 1, version
    off, recs = 16, []
    for _ in range(count):
        turn, actor, kind, target, bits = struct.unpack_from("<qiiiq", data, off)
        off += 28
        recs.append(dict(turn=turn, actor=actor, kind=kind, target=target,
                         amount=struct.unpack("<d", struct.pack("<q", bits))[0]))
    assert off == len(data), f"{path}: trailing bytes"
    return recs


def read_trace(path):
    rows = {}
    with path.open(newline="") as f:
        for r in csv.DictReader(f):
            rows[int(r["turn"])] = dict(simYear=int(r["simYear"]), dt=float(r["dtYears"]),
                                        population=int(r["population"]), rp=float(r["researchPointsThisTurn"]))
    return rows


def read_turn0_population(path):
    with path.open(newline="") as f:
        for r in csv.DictReader(f):
            if int(r["turn"]) == 0:
                return int(r["population"])
    raise ValueError(path)


VIEW = re.compile(r"^  Tree (1|2) — (.+?)\s+(\d+) / (\d+)\s+(\d+)\s*$")


def parse_report(text):
    rep = dict(views={}, partial=[], milestones=[], stage_turn=None)
    lines = text.splitlines()
    for i, line in enumerate(lines):
        m = re.match(r"research report: seed (\d+), turn (\d+) \(sim-year (\d+)\), polity (\d+), (\d+) order", line)
        if m:
            rep.update(seed=int(m[1]), turn=int(m[2]), simYear=int(m[3]), polity=int(m[4]), orders=int(m[5]))
        m = re.match(r"Research Points: ([\d.]+) RP this turn from population (\d+)", line)
        if m:
            rep.update(rp_final=float(m[1]), population_final=int(m[2]))
        m = re.match(r"active target: (\S+) \((.+?)\) ([\d.]+) / ([\d.]+) RP", line)
        if m:
            rep.update(target=m[1], target_progress=float(m[3]), target_cost=float(m[4]))
        if line.startswith("active target: none"):
            rep.update(target=None)
        if line.startswith("research stage (university"):
            rep["stage_reached_final"] = "REACHED" in line
        m = VIEW.match(line)
        if m:
            rep["views"][m[2].strip()] = dict(complete=int(m[3]), total=int(m[4]), available=int(m[5]))
        m = re.match(r"available now \((\d+)\): (.*)$", line)
        if m:
            rep["available_count"] = int(m[1])
            rep["available"] = [x.strip() for x in m[2].split(",")]
        m = re.match(r"partial progress kept on (\d+) node", line)
        if m:
            for j in range(int(m[1])):
                mm = re.match(r"^\s+(\S+)\s+([\d.]+) / ([\d.]+)", lines[i + 1 + j])
                rep["partial"].append(dict(id=mm[1], progress=float(mm[2]), cost=float(mm[3])))
        m = re.match(r"Eurekas fired: (\d+)", line)
        if m:
            rep["eurekas_fired"] = int(m[1])
        m = re.match(r"acceleration credit .*: Eureka ([\d.]+) RP, foreign exposure ([\d.]+) RP", line)
        if m:
            rep.update(eureka_credit=float(m[1]), exposure_credit=float(m[2]))
        m = re.match(r"\s+(\d+) nodes complete by turn (\d+) \(sim-year (\d+)\)", line)
        if m:
            rep["milestones"].append(dict(count=int(m[1]), turn=int(m[2]), simYear=int(m[3])))
        m = re.match(r"\s+research stage reached at turn (\d+) \(sim-year (\d+)\)", line)
        if m:
            rep.update(stage_turn=int(m[1]), stage_simYear=int(m[2]))
        m = re.match(r"specialized-university cost modifiers: (.*)$", line)
        if m:
            rep["cost_modifiers"] = m[1]
        m = re.match(r"node (\S+) \(key (\d+)\): .*$", line)
        if m:
            rep["node"] = dict(id=m[1], key=int(m[2]))
        m = re.match(r"  state: (\w+); subtree open: (\w+)", line)
        if m and "node" in rep:
            rep["node"]["state"] = m[1]
        m = re.match(r"  effective: .* progress ([\d.]+)", line)
        if m and "node" in rep:
            rep["node"]["progress"] = float(m[1])
        m = re.match(r"  acceleration pool: ceiling ([\d.]+) RP; credited Eureka ([\d.]+)", line)
        if m and "node" in rep:
            rep["node"]["eureka_credit"] = float(m[2])
        m = re.match(r"world hash (\w+)", line)
        if m:
            rep["world_hash"] = m[1]
    return rep


def rp_of(tuning, population):
    c = tuning["rpPerTurn"]
    return c["coefficient"] * population ** c["exponent"] if population > 0 else 0.0


# ----------------------------------------------------------------------------- reconstruction

def reconstruct(orders, actor, final_target_id, final_turn, nodes):
    """Order i (stamp s_i, node k_i) of `actor`: target set in step s_i -> s_i+1. Since a new order is
    issued only when the actor has NO target, and a target is cleared only by completion (availability
    is monotone; the driver/AI never clears), node k_i completed in the step ending at post-step turn
    s_{i+1}. The last order's node is complete iff the final report shows no active target (then it
    completed in the final step, provided anything was still available)."""
    own = [o for o in orders if o["actor"] == actor and o["kind"] == KIND_SET_RESEARCH_TARGET]
    assert all(o["target"] > 0 for o in own), "a clear-target order (-1) breaks the rule"
    stamps = [o["turn"] for o in own]
    assert all(b > a for a, b in zip(stamps, stamps[1:])), "two orders in one turn breaks the rule"
    rows = []
    for i, o in enumerate(own):
        n = nodes[o["target"]]
        if i + 1 < len(own):
            done = own[i + 1]["turn"]
        else:
            done = None if (final_target_id == n["id"]) else final_turn
        rows.append(dict(key=n["key"], target_turn=o["turn"], completion_turn=done))
    return rows


def analyse_seed(seed, nodes, by_id, tuning, stage_expr, age_facts):
    rep = parse_report((RAW / f"research-seed{seed}.stdout.txt").read_text(encoding="utf-8"))
    orders = read_orders(RAW / f"research-auto-orders-seed{seed}.bin")
    trace = read_trace(RAW / f"population-trace-seed{seed}.csv")
    pop0 = read_turn0_population(RAW / f"turn0-session-trace-seed{seed}.csv")
    T = rep["turn"]
    assert T == TURNS and len(trace) == T and rep["orders"] == len(orders)
    assert all(o["actor"] == 1 and o["kind"] == KIND_SET_RESEARCH_TARGET for o in orders)

    # RP stream: R[t] = RP spent in step t -> t+1 = RP(state t). Trace row t is state t (post-step).
    R = [rp_of(tuning, pop0)] + [rp_of(tuning, trace[t]["population"]) for t in range(1, T + 1)]
    max_print_err = max(abs(round(R[t], 3) - trace[t]["rp"]) for t in range(1, T + 1))

    rows = reconstruct(orders, 1, rep.get("target"), T, nodes)
    completed = [r for r in rows if r["completion_turn"] is not None]

    # --- cross-check 1: per-view completed counts at the end
    view_names = {("technology", "main"): "Main Technology", ("technology", "military"): "1.1 Military",
                  ("technology", "medicine"): "1.2 Medicine", ("technology", "engineering"): "1.3 Engineering",
                  ("technology", "natural_science"): "1.4 Natural Science",
                  ("technology", "agriculture"): "1.5 Agriculture", ("civics", "civics"): "Civics"}
    recon_views = {v: 0 for v in view_names.values()}
    for r in completed:
        n = nodes[r["key"]]
        recon_views[view_names[(n["tree"], n["branch"])]] += 1
    view_check = {v: (recon_views[v], rep["views"][v]["complete"]) for v in recon_views}
    views_ok = all(a == b for a, b in view_check.values())

    # --- cross-check 2: the CLI's 50-node milestones
    by_turn = sorted(r["completion_turn"] for r in completed)
    ms_check = []
    for m in rep["milestones"]:
        ms_check.append(dict(count=m["count"], cli_turn=m["turn"], recon_turn=by_turn[m["count"] - 1],
                             at_most_one_per_turn=len(set(by_turn)) == len(by_turn)))
    ms_ok = all(x["cli_turn"] == x["recon_turn"] for x in ms_check)

    # --- cross-check 3: research stage turn (stage expression over the reconstructed completed set)
    stage_tree = parse(stage_expr)
    done_ids, recon_stage = set(), None
    for r in sorted(completed, key=lambda r: r["completion_turn"]):
        done_ids.add(nodes[r["key"]]["id"])
        if recon_stage is None and holds(stage_tree, done_ids):
            recon_stage = r["completion_turn"]
    stage_ok = recon_stage == rep["stage_turn"]

    # --- cross-check 4: per-node RP-sum bounds (no-Eureka nodes: exact; Eureka nodes: weak)
    prefix = [0.0]
    for x in R:
        prefix.append(prefix[-1] + x)

    def offered(a, b):          # sum of R[t] for t in [a, b)
        return prefix[b] - prefix[a]

    bound_fail, overflow_exact, eureka_rows = [], 0.0, []
    for r in completed:
        n = nodes[r["key"]]
        s, c = r["target_turn"], r["completion_turn"]
        A = offered(s, c)
        prior = A - R[c - 1]
        r["rp_offered"] = A
        if n["eureka_evaluable"]:
            ok = prior < n["base"] and A >= (1.0 - 0.4) * n["base"] - 1e-6
            r["credit_lower_bound"] = max(0.0, n["base"] - A)
            eureka_rows.append(dict(id=n["id"], base=n["base"], offered=A, credit_needed=max(0.0, n["base"] - A)))
        else:
            ok = prior < n["base"] <= A + 1e-9
            overflow_exact += A - n["base"]
        if not ok:
            bound_fail.append(dict(id=n["id"], target_turn=s, completion_turn=c, base=n["base"], offered=A, prior=prior))
    # final target: reported progress against RP offered since it was set
    last = rows[-1]
    final_check = None
    if last["completion_turn"] is None:
        A = offered(last["target_turn"], T)
        n = nodes[last["key"]]
        final_check = dict(id=n["id"], reported_progress=rep["target_progress"], rp_offered=round(A, 3),
                           eureka_possible=n["eureka_evaluable"])

    # --- RP accounting
    G_run = offered(0, T)                                   # RP generated in the run's T steps
    cum_prior_convention = sum(trace[t]["rp"] for t in range(1, T + 1))   # docs/research-calibration-report.md
    base_completed = sum(nodes[r["key"]]["base"] for r in completed)
    partial_total = sum(p["progress"] for p in rep["partial"])
    applied = base_completed + partial_total - rep["eureka_credit"] - rep["exposure_credit"]
    overflow_total = G_run - applied

    # --- derived per-node fields
    for r in rows:
        n = nodes[r["key"]]
        r.update(node_id=n["id"], tree=n["tree"], branch=n["branch"], age=n["age"], base_cost=n["base"])
        c = r["completion_turn"]
        r["completion_sim_year"] = trace[c]["simYear"] if c is not None else None
        r["turns_as_target"] = (c if c is not None else T) - r["target_turn"]

    comp_turn = {nodes[r["key"]]["id"]: r["completion_turn"] for r in completed}

    def when(turn):
        return None if turn is None else dict(turn=turn, simYear=trace[turn]["simYear"])

    civics = []
    for k in CIVIC_KEYS:
        nid = nodes[k]["id"]
        t = comp_turn.get(nid)
        tgt = next((r["target_turn"] for r in rows if r["key"] == k), None)
        civics.append(dict(key=k, id=nid, age=nodes[k]["age"], base=nodes[k]["base"], target_turn=tgt,
                           completion=when(t)))

    tech_turns = sorted(r["completion_turn"] for r in completed if nodes[r["key"]]["tree"] == "technology")
    facts = []
    for f in age_facts:
        if f["kind"] == "research":
            hits = [(comp_turn[x], x) for x in f["nodes"] if x in comp_turn]
            t, via = (min(hits) if hits else (None, None))
            facts.append(dict(age=f["age"], age_id=f["age_id"], role=f["role"], id=f["id"], kind="research",
                              nodes=f["nodes"], satisfied=when(t), via=via))
        else:
            need = f["min"]
            t = tech_turns[need - 1] if len(tech_turns) >= need else None
            facts.append(dict(age=f["age"], age_id=f["age_id"], role=f["role"], id=f["id"], kind="research_count",
                              tree=f["tree"], min=need, satisfied=when(t), via=None,
                              final_count=len(tech_turns)))

    # trajectories (MEASURED)
    pops = [trace[t]["population"] for t in range(1, T + 1)]
    rps = [trace[t]["rp"] for t in range(1, T + 1)]
    bands, start = [], 1
    for t in range(2, T + 2):
        if t == T + 1 or trace[t]["dt"] != trace[start]["dt"]:
            bands.append(dict(first_turn=start, last_turn=t - 1, dtYears=trace[start]["dt"],
                              turns=t - start, simYear_end=trace[t - 1]["simYear"]))
            start = t
    selected = {t: dict(simYear=trace[t]["simYear"], dt=trace[t]["dt"], population=trace[t]["population"],
                        rp=trace[t]["rp"], completed=sum(1 for r in completed if r["completion_turn"] <= t))
                for t in SELECTED_TURNS}

    def counter(keyf):
        out = {}
        for r in completed:
            k = keyf(nodes[r["key"]])
            out[k] = out.get(k, 0) + 1
        return dict(sorted(out.items()))

    total_by_age = {}
    for n in nodes.values():
        total_by_age[n["age"]] = total_by_age.get(n["age"], 0) + 1

    waits = sorted(rows, key=lambda r: (-r["turns_as_target"], r["target_turn"]))[:10]
    return dict(
        seed=seed, report=rep, rows=rows, pop0=pop0, rp0=R[0], max_rp_print_err=max_print_err,
        checks=dict(views=view_check, views_ok=views_ok, milestones=ms_check, milestones_ok=ms_ok,
                    stage_cli=rep["stage_turn"], stage_recon=recon_stage, stage_ok=stage_ok,
                    rp_bound_failures=bound_fail, final_target=final_check),
        eureka_nodes=eureka_rows,
        rp=dict(generated_in_run=G_run, cumulative_prior_convention=cum_prior_convention,
                applied_to_research=applied, overflow_lost=overflow_total,
                overflow_lost_no_eureka_nodes_exact=overflow_exact),
        civics=civics, facts=facts, bands=bands, selected=selected,
        population=dict(min=min(pops), median=statistics.median(pops), max=max(pops),
                        argmin=1 + pops.index(min(pops)), argmax=1 + pops.index(max(pops))),
        rp_stats=dict(min=min(rps), median=statistics.median(rps), max=max(rps)),
        completed_total=len(completed), by_view=recon_views,
        by_age=counter(lambda n: n["age"]), total_by_age=dict(sorted(total_by_age.items())),
        by_tree_branch_age=counter(lambda n: f'{n["tree"]}/{n["branch"]}/{n["age"]}'),
        waits=[dict(id=r["node_id"], key=r["key"], branch=r["branch"], age=r["age"], base=r["base_cost"],
                    target_turn=r["target_turn"], completion_turn=r["completion_turn"],
                    turns=r["turns_as_target"]) for r in waits],
        tech_count_final=len(tech_turns),
    )


# ----------------------------------------------------------------------------- direct spot checks

VIEW_OF = {("technology", "main"): "Main Technology", ("technology", "military"): "1.1 Military",
           ("technology", "medicine"): "1.2 Medicine", ("technology", "engineering"): "1.3 Engineering",
           ("technology", "natural_science"): "1.4 Natural Science",
           ("technology", "agriculture"): "1.5 Agriculture", ("civics", "civics"): "Civics"}


def spot_checks(seed, x, nodes, tuning):
    """Compare every prefix run in raw/checks for this seed with the reconstruction: per-view completed
    counts at that turn, the active target, the asked node's state, and (no-Eureka target) its progress."""
    out = []
    rows = x["rows"]
    trace = read_trace(RAW / f"population-trace-seed{seed}.csv")
    R = [x["rp0"]] + [rp_of(tuning, trace[t]["population"]) for t in range(1, TURNS + 1)]
    for path in sorted((RAW / "checks").glob(f"research-seed{seed}-turns*-node-*.stdout.txt")):
        m = re.match(rf"research-seed{seed}-turns(\d+)-node-(\S+)\.stdout\.txt", path.name)
        turn, asked = int(m[1]), m[2]
        rep = parse_report(path.read_text(encoding="utf-8"))
        if "world_hash" not in rep:      # an unfinished or failed run: never parsed as evidence
            out.append(dict(turn=turn, node=asked, incomplete=True))
            continue
        assert rep["turn"] == turn
        recon = {v: 0 for v in VIEW_OF.values()}
        for r in rows:
            if r["completion_turn"] is not None and r["completion_turn"] <= turn:
                n = nodes[r["key"]]
                recon[VIEW_OF[(n["tree"], n["branch"])]] += 1
        views_ok = all(recon[v] == rep["views"][v]["complete"] for v in recon)
        active = next((r for r in rows if r["target_turn"] < turn and
                       (r["completion_turn"] is None or r["completion_turn"] > turn)), None)
        target_ok = (active is None and rep.get("target") is None) or \
                    (active is not None and nodes[active["key"]]["id"] == rep.get("target"))
        progress_ok = None
        if active is not None and not nodes[active["key"]]["eureka_evaluable"]:
            progress_ok = abs(round(sum(R[active["target_turn"]:turn]), 3) - rep["target_progress"]) < 0.0015
        key = next(k for k, n in nodes.items() if n["id"] == asked)
        recon_done = next((r["completion_turn"] for r in rows if r["key"] == key and r["completion_turn"] is not None), None)
        expect = "COMPLETED" if (recon_done is not None and recon_done <= turn) else None
        state_ok = (rep["node"]["state"] == "COMPLETED") == (expect == "COMPLETED")
        tech_cli = sum(v["complete"] for k, v in rep["views"].items() if k != "Civics")
        out.append(dict(turn=turn, node=asked, node_state=rep["node"]["state"], node_progress=rep["node"].get("progress"),
                        technology_complete_cli=tech_cli, civics_complete_cli=rep["views"]["Civics"]["complete"],
                        active_target_cli=rep.get("target"), stage_reached_cli=rep.get("stage_reached_final"),
                        views_ok=views_ok, target_ok=target_ok, target_progress_ok=progress_ok, state_ok=state_ok,
                        orders=rep["orders"], world_hash=rep["world_hash"]))
    return out


# ----------------------------------------------------------------------------- the AI polity (sim run --ai-empires 1)

AI = RAW / "ai"
AI_POLITY = 2


def analyse_ai(seed, nodes, tuning, stage_expr, age_facts):
    """The AI polity's research, reconstructed from the run's own order log (AiResearchPolicy orders a target
    only when it holds none, the driver's rule) and checked against the RP stream recomputed from the
    telemetry population of the settlements it controls (ResearchQuery.Population's own holder rule)."""
    orders = read_orders(AI / f"ai-run-orders-seed{seed}.bin")
    pops = {}
    opening1 = {}
    with (AI / f"ai-polity-population-seed{seed}.csv").open(newline="") as f:
        for r in csv.DictReader(f):
            t, c = int(r["turn"]), int(r["controller"])
            pops.setdefault(t, {})[c] = (int(r["settlements"]), int(r["population_closing"]))
            if t == 1:
                opening1[c] = int(r["population_opening"])
    years = {}
    with (AI / f"ai-run-session-trace-seed{seed}.csv").open(newline="") as f:
        for r in csv.DictReader(f):
            years[int(r["turn"])] = float(r["year"])
    T = max(pops)
    assert T == TURNS

    def pop_of(t):
        return pops[t].get(AI_POLITY, (0, 0))[1]

    R = [rp_of(tuning, opening1.get(AI_POLITY, 0))] + [rp_of(tuning, pop_of(t)) for t in range(1, T + 1)]
    prefix = [0.0]
    for v in R:
        prefix.append(prefix[-1] + v)

    def offered(a, b):
        return prefix[b] - prefix[a]

    own = [o for o in orders if o["actor"] == AI_POLITY and o["kind"] == KIND_SET_RESEARCH_TARGET]
    actors = sorted({o["actor"] for o in orders})
    assert all(o["target"] > 0 for o in own)
    stamps = [o["turn"] for o in own]
    assert all(b > a for a, b in zip(stamps, stamps[1:]))
    rows = []
    for i, o in enumerate(own):
        n = nodes[o["target"]]
        if i + 1 < len(own):
            done, how = own[i + 1]["turn"], "next-order"
        else:
            done, how = None, "unknown (last order; no end-of-run research report exists for this polity)"
            if n["branch"] in ("main", "civics") and not n["eureka_evaluable"]:
                for t in range(o["turn"] + 1, T + 1):
                    if offered(o["turn"], t) >= n["base"]:
                        done, how = t, "rp-crossing (main/civics node, no Eureka, no modifier)"
                        break
                else:
                    how = "not complete by turn 1630 (main/civics node, RP offered < BaseCost)"
        rows.append(dict(key=n["key"], node_id=n["id"], tree=n["tree"], branch=n["branch"], age=n["age"],
                         base_cost=n["base"], target_turn=o["turn"], completion_turn=done, how=how))

    # RP-sum bounds. Main/Civics: EffectiveCost = BaseCost (no university type serves them). Subtrees:
    # EffectiveCost in [(1 - 1/3) BaseCost, BaseCost] (one modifier row per polity x type; sim.json
    # institutions.universities.maxResearchCostReduction = 1/3). Eureka credit in [0, 0.4 BaseCost].
    fails, exact_checked, weak_checked = [], 0, 0
    for r in rows:
        if r["completion_turn"] is None or r["how"] != "next-order":
            continue
        n = nodes[r["key"]]
        s, c = r["target_turn"], r["completion_turn"]
        A = offered(s, c)
        prior = A - R[c - 1]
        lo_cost = n["base"] * (1.0 if n["branch"] in ("main", "civics") else 2.0 / 3.0)
        credit = 0.4 * n["base"] if n["eureka_evaluable"] else 0.0
        ok = prior < n["base"] and A + credit >= lo_cost - 1e-6
        if n["branch"] in ("main", "civics") and not n["eureka_evaluable"]:
            exact_checked += 1
        else:
            weak_checked += 1
        if not ok:
            # R(0) is approximated (turn-1 opening populations under turn-1 control), so a failure on a node
            # targeted at turn 0 is reported with that caveat rather than counted as a refutation.
            fails.append(dict(id=n["id"], target_turn=s, completion_turn=c, base=n["base"], offered=A, prior=prior,
                              uses_approximate_R0=(s == 0)))
        r["rp_offered"] = A

    completed = [r for r in rows if r["completion_turn"] is not None]
    for r in rows:
        c = r["completion_turn"]
        r["completion_sim_year"] = math.floor(years[c]) if c is not None else None
        r["turns_as_target"] = (c if c is not None else T) - r["target_turn"]

    def when(t):
        return None if t is None else dict(turn=t, simYear=math.floor(years[t]))

    comp_turn = {r["node_id"]: r["completion_turn"] for r in completed}
    stage_tree = parse(stage_expr)
    done_ids, stage = set(), None
    for r in sorted(completed, key=lambda r: r["completion_turn"]):
        done_ids.add(r["node_id"])
        if stage is None and holds(stage_tree, done_ids):
            stage = r["completion_turn"]
    civics = [dict(key=k, id=nodes[k]["id"], completion=when(comp_turn.get(nodes[k]["id"])),
                   target_turn=next((r["target_turn"] for r in rows if r["key"] == k), None)) for k in CIVIC_KEYS]
    tech_turns = sorted(r["completion_turn"] for r in completed if r["tree"] == "technology")
    facts = []
    for f in age_facts:
        if f["kind"] == "research":
            hits = [(comp_turn[x], x) for x in f["nodes"] if x in comp_turn]
            t, via = (min(hits) if hits else (None, None))
            facts.append(dict(age=f["age"], role=f["role"], id=f["id"], satisfied=when(t), via=via))
        else:
            t = tech_turns[f["min"] - 1] if len(tech_turns) >= f["min"] else None
            facts.append(dict(age=f["age"], role=f["role"], id=f["id"], satisfied=when(t), via=None))
    advances = [dict(order_turn=o["turn"], age_entered=o["target"], first_turn_in_age=o["turn"] + 1,
                     simYear_of_order_turn=math.floor(years[o["turn"]]))
                for o in orders if o["actor"] == AI_POLITY and o["kind"] == KIND_ADVANCE_AGE]
    # INDEPENDENT CONSISTENCY CHECK: AgeAdvancePolicy issues AdvanceAge (stamped t) only when the polity is
    # eligible on state t, which needs every CORE milestone of that Age met on state t. So each CORE research
    # fact's reconstructed satisfaction turn must be <= t. "binding_research" = the research facts (core or
    # supporting) whose reconstructed satisfaction turn equals t exactly (research was the last thing missing).
    for a in advances:
        core = [f for f in facts if f["age"] == a["age_entered"] and f["role"] == "CORE"]
        sup = [f for f in facts if f["age"] == a["age_entered"] and f["role"] == "SUPPORTING"]
        a["core_research_met_by_order_turn"] = all(f["satisfied"] is not None and f["satisfied"]["turn"] <= a["order_turn"]
                                                   for f in core)
        a["research_supporting_met_by_order_turn"] = sum(1 for f in sup if f["satisfied"] is not None
                                                         and f["satisfied"]["turn"] <= a["order_turn"])
        a["binding_research"] = [f["id"] for f in core + sup
                                 if f["satisfied"] is not None and f["satisfied"]["turn"] == a["order_turn"]]
    counts = {}
    for r in completed:
        v = VIEW_OF[(r["tree"], r["branch"])]
        counts[v] = counts.get(v, 0) + 1
    by_age = {}
    for r in completed:
        by_age[r["age"]] = by_age.get(r["age"], 0) + 1
    p_series = [pop_of(t) for t in range(1, T + 1)]
    settle = [pops[t].get(AI_POLITY, (0, 0))[0] for t in range(1, T + 1)]
    rps = R[1:]
    selected = {t: dict(simYear=math.floor(years[t]), population=pop_of(t), settlements=settle[t - 1],
                        rp=round(R[t], 3), completed=sum(1 for r in completed if r["completion_turn"] <= t))
                for t in SELECTED_TURNS}
    waits = sorted(rows, key=lambda r: (-r["turns_as_target"], r["target_turn"]))[:10]
    return dict(seed=seed, actors_in_log=actors, orders_total=len(orders),
                research_orders=len(own), rows=rows, completed_total=len(completed),
                last_order=dict(id=rows[-1]["node_id"], target_turn=rows[-1]["target_turn"], status=rows[-1]["how"]),
                checks=dict(rp_bound_failures=fails, exact_checked=exact_checked, weak_checked=weak_checked),
                stage=when(stage), civics=civics, facts=facts, advances=advances, by_view=counts,
                by_age=dict(sorted(by_age.items())), technology_completed=len(tech_turns),
                population=dict(min=min(p_series), median=statistics.median(p_series), max=max(p_series),
                                final=p_series[-1], final_settlements=settle[-1], max_settlements=max(settle)),
                rp_inferred=dict(min=min(rps), median=statistics.median(rps), max=max(rps),
                                 generated_in_run=offered(0, T)),
                selected=selected,
                waits=[dict(id=r["node_id"], branch=r["branch"], age=r["age"], base=r["base_cost"],
                            target_turn=r["target_turn"], completion_turn=r["completion_turn"],
                            turns=r["turns_as_target"]) for r in waits])


# ----------------------------------------------------------------------------- main

def main():
    nodes, by_id, tuning, stage_expr = load_content()
    age_facts = load_ages()
    results = {s: analyse_seed(s, nodes, by_id, tuning, stage_expr, age_facts) for s in SEEDS}
    for s in SEEDS:
        results[s]["spot_checks"] = spot_checks(s, results[s], nodes, tuning) if (RAW / "checks").is_dir() else []
    ai = {s: analyse_ai(s, nodes, tuning, stage_expr, age_facts) for s in SEEDS} if AI.is_dir() else {}
    with (HERE / "baseline-ai-node-completions.csv").open("w", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["seed", "node_key", "node_id", "tree", "branch", "age", "base_cost",
                    "target_turn", "completion_turn", "completion_sim_year", "completion_basis"])
        for s in SEEDS:
            for r in ai.get(s, {}).get("rows", []):
                if r["completion_turn"] is None:
                    continue
                w.writerow([s, r["key"], r["node_id"], r["tree"], r["branch"], r["age"], f'{r["base_cost"]:.0f}',
                            r["target_turn"], r["completion_turn"], r["completion_sim_year"], r["how"]])

    with (HERE / "baseline-node-completions.csv").open("w", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["seed", "node_key", "node_id", "tree", "branch", "age", "base_cost",
                    "target_turn", "completion_turn", "completion_sim_year"])
        for s in SEEDS:
            for r in results[s]["rows"]:
                if r["completion_turn"] is None:
                    continue
                w.writerow([s, r["key"], r["node_id"], r["tree"], r["branch"], r["age"],
                            f'{r["base_cost"]:.0f}', r["target_turn"], r["completion_turn"], r["completion_sim_year"]])

    summary = {"_doc": "Per-seed key metrics. See baseline-progression.md for method and labels. "
                       "MEASURED = printed by a CLI run; INFERENCE = derived from measured artefacts by the "
                       "stated reconstruction rule (verified by the listed cross-checks).",
               "configuration": dict(command="dotnet Sim.Cli/bin/Release/net10.0/Sim.Cli.dll research --seed S "
                                             "--turns 1630 --auto cheapest --emit-orders ... --trace-population ... "
                                             "--node patent", turns=TURNS, polity=1, seeds=SEEDS),
               "seeds": {}}
    for s in SEEDS:
        x = results[s]
        rep = x["report"]
        summary["seeds"][str(s)] = {
            "MEASURED": dict(
                orders=rep["orders"], final_turn=rep["turn"], final_simYear=rep["simYear"],
                final_population=rep["population_final"], final_rp_per_turn=rep["rp_final"],
                final_active_target=rep.get("target"), final_target_progress=rep.get("target_progress"),
                final_target_cost=rep.get("target_cost"), completed_by_view=
                {k: v["complete"] for k, v in rep["views"].items()},
                available_at_end=rep.get("available_count"), stage_turn=rep["stage_turn"],
                stage_simYear=rep.get("stage_simYear"), milestones_50=rep["milestones"],
                eurekas_fired=rep["eurekas_fired"], eureka_credit_rp=rep["eureka_credit"],
                foreign_exposure_credit_rp=rep["exposure_credit"], partial_progress=rep["partial"],
                cost_modifiers=rep.get("cost_modifiers"), world_hash=rep.get("world_hash"),
                population_turn0=x["pop0"], population=x["population"], rp_per_turn=x["rp_stats"],
                selected_turns=x["selected"], dt_bands=x["bands"],
                cumulative_rp_prior_report_convention=round(x["rp"]["cumulative_prior_convention"], 3)),
            "INFERENCE": dict(
                completed_nodes=x["completed_total"], completed_by_view=x["by_view"],
                completed_by_age=x["by_age"], completed_by_tree_branch_age=x["by_tree_branch_age"],
                rp_generated_in_run=round(x["rp"]["generated_in_run"], 3),
                rp_applied_to_research=round(x["rp"]["applied_to_research"], 3),
                rp_overflow_lost=round(x["rp"]["overflow_lost"], 3),
                civics=x["civics"], age_research_facts=x["facts"],
                technology_completed_final=x["tech_count_final"],
                longest_single_target_waits=x["waits"], idle_steps_without_target=0),
            "CHECKS": x["checks"],
            "SPOT_CHECKS_MEASURED": x["spot_checks"],
        }
    summary["ai_polity"] = {
        "_doc": "sim run --founded --seed S --ai-empires 1 --turns 1630 --emit-session DIR. The AI polity is "
                "PolityId 2. Research completions are an INFERENCE (order-log reconstruction); AdvanceAge "
                "stamps are MEASURED (decoded from the run's own order log); population is MEASURED "
                "(telemetry); RP is an INFERENCE (the RP formula applied to that population).",
        "seeds": {str(s): {k: v for k, v in ai[s].items() if k != "rows"} for s in ai}}
    (HERE / "baseline-summary.json").write_text(json.dumps(summary, indent=1) + "\n", encoding="utf-8")
    return results, ai


if __name__ == "__main__":
    res, ai = main()
    for s, x in res.items():
        c = x["checks"]
        incomplete = sum(1 for q in x["spot_checks"] if q.get("incomplete"))
        sc = [q for q in x["spot_checks"] if not q.get("incomplete")]
        print(f"   spot-check files incomplete (skipped): {incomplete}")
        print(f"seed {s}: completed {x['completed_total']}  views_ok {c['views_ok']}  milestones_ok {c['milestones_ok']}"
              f"  stage cli {c['stage_cli']} recon {c['stage_recon']}  rp-bound failures {len(c['rp_bound_failures'])}"
              f"  rp print err {x['max_rp_print_err']:.6f}  final {c['final_target']}")
        print(f"   spot checks: {len(sc)} runs; all views_ok {all(q['views_ok'] for q in sc)}; all target_ok "
              f"{all(q['target_ok'] for q in sc)}; all state_ok {all(q['state_ok'] for q in sc)}; progress_ok "
              f"{[q['target_progress_ok'] for q in sc].count(True)} true / {[q['target_progress_ok'] for q in sc].count(False)} false")
    for s, a in ai.items():
        print(f"AI seed {s}: actors {a['actors_in_log']} research orders {a['research_orders']} completed {a['completed_total']}"
              f" last {a['last_order']} bound failures {len(a['checks']['rp_bound_failures'])}"
              f" (exact {a['checks']['exact_checked']}, weak {a['checks']['weak_checked']}) stage {a['stage']}")
