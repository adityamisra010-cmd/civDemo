"""Render the markdown tables of baseline-progression.md from baseline-summary.json (no hand transcription).

Run:  python3 -I baseline_tables.py baseline-summary.json > tables.md
"""
import json
import statistics
import sys

d = json.load(open(sys.argv[1]))
S = ["1", "2", "42", "7"]
seeds = d["seeds"]
ai = d["ai_polity"]["seeds"]


def f0(x):
    return f"{x:,.0f}"


def f1(x):
    return f"{x:,.1f}"


def ty(w):
    return "—" if w is None else f'{w["turn"]} ({w["simYear"]})'


def spread(vals):
    v = [x for x in vals if x is not None]
    if len(v) < len(vals):
        return "n/a"
    return f"{max(v) - min(v)}"


out = []
p = out.append

# ---- key numbers
p("### T0 key numbers\n")
p("| | label | " + " | ".join(f"seed {s}" for s in S) + " |")
p("|---|---|" + "---|" * len(S))


def civ(s, cid, src):
    c = next(c for c in src[s]["INFERENCE"]["civics"] if c["id"] == cid) if src is seeds else \
        next(c for c in ai[s]["civics"] if c["id"] == cid)
    return "never" if c["completion"] is None else str(c["completion"]["turn"])


p("| driver: research stage reached, turn (sim-year) | MEASURED | " + " | ".join(
    f'{seeds[s]["MEASURED"]["stage_turn"]} ({seeds[s]["MEASURED"]["stage_simYear"]})' for s in S) + " |")
p("| driver: nodes completed by turn 1630 (of 433) | MEASURED | " + " | ".join(
    str(sum(seeds[s]["MEASURED"]["completed_by_view"].values())) for s in S) + " |")
p("| driver: RP generated over the 1630 steps | INFERENCE | " + " | ".join(
    f0(seeds[s]["INFERENCE"]["rp_generated_in_run"]) for s in S) + " |")
for cid in ["taxation", "law_code", "legal_code_roman", "census", "coined_wage", "joint_stock", "patent"]:
    p(f"| driver: {cid} completes (turn) | MEASURED | " + " | ".join(civ(s, cid, seeds) for s in S) + " |")
p("| AI polity: research stage reached (turn) | INFERENCE | " + " | ".join(str(ai[s]["stage"]["turn"]) for s in S) + " |")
p("| AI polity: nodes completed by turn 1630 | INFERENCE | " + " | ".join(str(ai[s]["completed_total"]) for s in S) + " |")
p("| AI polity: RP generated over the 1630 steps | INFERENCE | " + " | ".join(
    f0(ai[s]["rp_inferred"]["generated_in_run"]) for s in S) + " |")
for cid in ["taxation", "law_code", "legal_code_roman", "census", "coined_wage", "joint_stock", "patent"]:
    p(f"| AI polity: {cid} completes (turn) | INFERENCE | " + " | ".join(civ(s, cid, ai) for s in S) + " |")
p("| AI polity: population at turn 1630 | MEASURED | " + " | ".join(f0(ai[s]["population"]["final"]) for s in S) + " |")
p("| driver: population at turn 1630 | MEASURED | " + " | ".join(f0(seeds[s]["MEASURED"]["final_population"]) for s in S) + " |")
p("")

# ---- time base (identical across seeds: check)
bands = [seeds[s]["MEASURED"]["dt_bands"] for s in S]
assert all(b == bands[0] for b in bands)
p("### T1 time base\n")
p("| turns | dtYears | turns in band | sim-year at band end |")
p("|---|---|---|---|")
for b in bands[0]:
    p(f'| {b["first_turn"]}–{b["last_turn"]} | {b["dtYears"]:g} | {b["turns"]} | {b["simYear_end"]} |')

# ---- population / RP at selected turns
p("\n### T2 population, RP/turn, completed nodes at selected turns\n")
p("| turn | sim-year | " + " | ".join(f"pop s{s}" for s in S) + " | " + " | ".join(f"RP s{s}" for s in S) + " | "
  + " | ".join(f"done s{s}" for s in S) + " |")
p("|" + "---|" * (2 + 3 * len(S)))
sel = seeds["1"]["MEASURED"]["selected_turns"]
for t in sel:
    yr = sel[t]["simYear"]
    pops = [f0(seeds[s]["MEASURED"]["selected_turns"][t]["population"]) for s in S]
    rps = [f1(seeds[s]["MEASURED"]["selected_turns"][t]["rp"]) for s in S]
    dn = [str(seeds[s]["MEASURED"]["selected_turns"][t]["completed"]) for s in S]
    p(f"| {t} | {yr} | " + " | ".join(pops) + " | " + " | ".join(rps) + " | " + " | ".join(dn) + " |")

# ---- stats
p("\n### T3 trajectory statistics and RP totals\n")
p("| metric | label | " + " | ".join(f"seed {s}" for s in S) + " |")
p("|---|---|" + "---|" * len(S))
rows = [
    ("population at turn 0 (session trace row 0)", "MEASURED", lambda s: f0(seeds[s]["MEASURED"]["population_turn0"])),
    ("population min / median / max (turns 1–1630)", "MEASURED", lambda s: "{} / {} / {}".format(
        f0(seeds[s]["MEASURED"]["population"]["min"]), f0(seeds[s]["MEASURED"]["population"]["median"]),
        f0(seeds[s]["MEASURED"]["population"]["max"]))),
    ("RP/turn min / median / max", "MEASURED", lambda s: "{} / {} / {}".format(
        f1(seeds[s]["MEASURED"]["rp_per_turn"]["min"]), f1(seeds[s]["MEASURED"]["rp_per_turn"]["median"]),
        f1(seeds[s]["MEASURED"]["rp_per_turn"]["max"]))),
    ("cumulative RP, prior-report convention (Σ trace RP, turns 1–1630)", "MEASURED (sum of printed values)",
     lambda s: f0(seeds[s]["MEASURED"]["cumulative_rp_prior_report_convention"])),
    ("RP generated in the run's 1630 steps (RP of states 0–1629)", "INFERENCE",
     lambda s: f0(seeds[s]["INFERENCE"]["rp_generated_in_run"])),
    ("RP applied to research (progress, excl. Eureka credit)", "INFERENCE",
     lambda s: f0(seeds[s]["INFERENCE"]["rp_applied_to_research"])),
    ("RP lost as overflow past completion", "INFERENCE", lambda s: "{} ({:.1%})".format(
        f0(seeds[s]["INFERENCE"]["rp_overflow_lost"]),
        seeds[s]["INFERENCE"]["rp_overflow_lost"] / seeds[s]["INFERENCE"]["rp_generated_in_run"])),
    ("Eurekas fired / Eureka credit RP", "MEASURED", lambda s: "{} / {}".format(
        seeds[s]["MEASURED"]["eurekas_fired"], f0(seeds[s]["MEASURED"]["eureka_credit_rp"]))),
    ("SetResearchTarget orders issued", "MEASURED", lambda s: str(seeds[s]["MEASURED"]["orders"])),
    ("nodes completed by turn 1630", "MEASURED (view table sum)", lambda s: str(sum(seeds[s]["MEASURED"]["completed_by_view"].values()))),
    ("final active target (progress / cost)", "MEASURED", lambda s: "{} ({} / {})".format(
        seeds[s]["MEASURED"]["final_active_target"], seeds[s]["MEASURED"]["final_target_progress"],
        f0(seeds[s]["MEASURED"]["final_target_cost"]))),
    ("available nodes at turn 1630", "MEASURED", lambda s: str(seeds[s]["MEASURED"]["available_at_end"])),
    ("world hash at turn 1630", "MEASURED", lambda s: seeds[s]["MEASURED"]["world_hash"][:16] + "…"),
]
for name, label, fn in rows:
    p(f"| {name} | {label} | " + " | ".join(fn(s) for s in S) + " |")

# ---- completed by view and by Age
p("\n### T4 completed nodes by view (MEASURED, CLI view table at turn 1630)\n")
views = list(seeds["1"]["MEASURED"]["completed_by_view"].keys())
p("| view | total nodes | " + " | ".join(f"seed {s}" for s in S) + " |")
p("|---|---|" + "---|" * len(S))
tot = {"Main Technology": 177, "1.1 Military": 37, "1.2 Medicine": 22, "1.3 Engineering": 131,
       "1.4 Natural Science": 44, "1.5 Agriculture": 15, "Civics": 7}
for v in views:
    p(f"| {v} | {tot[v]} | " + " | ".join(str(seeds[s]["MEASURED"]["completed_by_view"][v]) for s in S) + " |")
p("| **all** | **433** | " + " | ".join("**%d**" % sum(seeds[s]["MEASURED"]["completed_by_view"].values()) for s in S) + " |")

p("\n### T5 completed nodes by Age label (INFERENCE from the reconstruction; totals from research.json)\n")
ages = ["A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9"]
age_tot = {"A1": 28, "A2": 51, "A3": 38, "A4": 32, "A5": 32, "A6": 42, "A7": 32, "A8": 53, "A9": 125}
p("| Age | nodes in content | " + " | ".join(f"seed {s}" for s in S) + " |")
p("|---|---|" + "---|" * len(S))
for a in ages:
    p(f"| {a} | {age_tot[a]} | " + " | ".join(str(seeds[s]["INFERENCE"]["completed_by_age"].get(a, 0)) for s in S) + " |")

# ---- key turns
p("\n### T6 key turns — turn (sim-year)\n")
p("| event | label | " + " | ".join(f"seed {s}" for s in S) + " | spread (turns) |")
p("|---|---|" + "---|" * len(S) + "---|")


def key_rows():
    r = []
    r.append(("research stage reached", "MEASURED (CLI milestone)",
              [dict(turn=seeds[s]["MEASURED"]["stage_turn"], simYear=seeds[s]["MEASURED"]["stage_simYear"]) for s in S]))
    for m in range(4):
        r.append((f'{seeds["1"]["MEASURED"]["milestones_50"][m]["count"]} nodes complete', "MEASURED (CLI milestone)",
                  [dict(turn=seeds[s]["MEASURED"]["milestones_50"][m]["turn"], simYear=seeds[s]["MEASURED"]["milestones_50"][m]["simYear"]) for s in S]))
    for i, c in enumerate(seeds["1"]["INFERENCE"]["civics"]):
        done = [seeds[s]["INFERENCE"]["civics"][i]["completion"] for s in S]
        label = ("MEASURED (prefix runs at c−1 and c, T12; equals the reconstruction)" if all(done)
                 else "MEASURED: never — LOCKED at turn 1630 (`--node patent`: printing_press missing)")
        r.append((f'civic {c["key"]} {c["id"]} completes', label, done))
    for f in seeds["1"]["INFERENCE"]["age_research_facts"]:
        if f["kind"] == "research_count":
            idx = seeds["1"]["INFERENCE"]["age_research_facts"].index(f)
            vals = [seeds[s]["INFERENCE"]["age_research_facts"][idx]["satisfied"] for s in S]
            label = ("MEASURED (prefix runs: technology count 199 → 200, T12)" if all(vals) else
                     "not reached: technology count at turn 1630 = " +
                     "/".join(str(sum(v for k, v in seeds[s]["MEASURED"]["completed_by_view"].items() if k != "Civics"))
                              for s in S) + " (MEASURED)")
            r.append((f'research_count technology >= {f["min"]} ({f["id"]})', label, vals))
    return r


for name, label, vals in key_rows():
    p(f"| {name} | {label} | " + " | ".join(ty(v) for v in vals) + f" | {spread([None if v is None else v['turn'] for v in vals])} |")

# ---- Age research facts
p("\n### T7 Age research milestone facts — first turn the fact holds for the driver (INFERENCE), turn (sim-year)\n")
p("| Age | role | milestone | nodes (ANY) | " + " | ".join(f"seed {s}" for s in S) + " | satisfying node |")
p("|---|---|---|---|" + "---|" * len(S) + "---|")
F = seeds["1"]["INFERENCE"]["age_research_facts"]
for i, f in enumerate(F):
    vals = [seeds[s]["INFERENCE"]["age_research_facts"][i] for s in S]
    nodes = ", ".join(f["nodes"]) if f["kind"] == "research" else f'technology count >= {f["min"]}'
    vias = sorted({v["via"] for v in vals if v["via"]})
    p(f'| A{f["age"]} | {f["role"]} | {f["id"]} | {nodes} | ' + " | ".join(ty(v["satisfied"]) for v in vals)
      + f' | {", ".join(vias) if vias else "—"} |')

# ---- waits
p("\n### T8 longest single-target waits (turns as the active target; INFERENCE)\n")
for s in S:
    w = seeds[s]["INFERENCE"]["longest_single_target_waits"]
    p(f"- seed {s}: " + "; ".join(f'{x["id"]} {x["turns"]} ({f0(x["base"])} RP, turns {x["target_turn"]}–{x["completion_turn"] if x["completion_turn"] is not None else "end"})' for x in w))

# ---- AI
p("\n### T9 AI polity (sim run --ai-empires 1) — key results\n")
p("| metric | label | " + " | ".join(f"seed {s}" for s in S) + " |")
p("|---|---|" + "---|" * len(S))
arows = [
    ("orders in the run log (all kinds) / SetResearchTarget", "MEASURED", lambda s: f'{ai[s]["orders_total"]} / {ai[s]["research_orders"]}'),
    ("nodes completed by turn 1630 (last target's status unknown)", "INFERENCE", lambda s: str(ai[s]["completed_total"])),
    ("of which technology / civics", "INFERENCE", lambda s: f'{ai[s]["technology_completed"]} / {ai[s]["by_view"].get("Civics", 0)}'),
    ("last order (target turn)", "MEASURED", lambda s: f'{ai[s]["last_order"]["id"]} ({ai[s]["last_order"]["target_turn"]})'),
    ("research stage reached", "INFERENCE", lambda s: ty(ai[s]["stage"])),
    ("AI population turn 1 / median / turn 1630", "MEASURED (telemetry)", lambda s: "{} / {} / {}".format(
        f0(ai[s]["selected"]["1"]["population"]), f0(ai[s]["population"]["median"]), f0(ai[s]["population"]["final"]))),
    ("AI settlements turn 1 → turn 1630", "MEASURED (telemetry)", lambda s: f'{ai[s]["selected"]["1"]["settlements"]} → {ai[s]["population"]["final_settlements"]}'),
    ("AI RP/turn min / median / max", "INFERENCE (formula on measured population)", lambda s: "{} / {} / {}".format(
        f1(ai[s]["rp_inferred"]["min"]), f1(ai[s]["rp_inferred"]["median"]), f1(ai[s]["rp_inferred"]["max"]))),
    ("AI RP generated in the run", "INFERENCE", lambda s: f0(ai[s]["rp_inferred"]["generated_in_run"])),
]
for name, label, fn in arows:
    p(f"| {name} | {label} | " + " | ".join(fn(s) for s in S) + " |")
for i, c in enumerate(ai["1"]["civics"]):
    p(f'| civic {c["id"]} completes | INFERENCE | ' + " | ".join(ty(ai[s]["civics"][i]["completion"]) for s in S) + " |")
for k in range(len(ai["1"]["advances"])):
    a0 = ai["1"]["advances"][k]
    p(f'| AdvanceAge → Age {a0["age_entered"]} (order turn; Age holds from the next turn) | MEASURED (order log) | '
      + " | ".join(f'{ai[s]["advances"][k]["order_turn"]} ({ai[s]["advances"][k]["simYear_of_order_turn"]})' for s in S) + " |")

p("\n### T10 AI vs driver, same seed — completed nodes at selected turns (AI: INFERENCE; driver: INFERENCE verified)\n")
p("| turn | " + " | ".join(f"driver s{s} | AI s{s}" for s in S) + " |")
p("|---|" + "---|---|" * len(S))
for t in seeds["1"]["MEASURED"]["selected_turns"]:
    p(f"| {t} | " + " | ".join(f'{seeds[s]["MEASURED"]["selected_turns"][t]["completed"]} | {ai[s]["selected"][t]["completed"]}' for s in S) + " |")

p("\n### T11 AI Age research facts — first turn the fact holds (INFERENCE), turn (sim-year)\n")
p("| Age | role | milestone | " + " | ".join(f"seed {s}" for s in S) + " |")
p("|---|---|---|" + "---|" * len(S))
for i, f in enumerate(ai["1"]["facts"]):
    p(f'| A{f["age"]} | {f["role"]} | {f["id"]} | ' + " | ".join(ty(ai[s]["facts"][i]["satisfied"]) for s in S) + " |")

# ---- AI completions by Age label
p("\n### T14 AI polity — completed nodes by Age label (INFERENCE), beside the driver (INFERENCE, verified)\n")
p("| Age | nodes in content | " + " | ".join(f"AI s{s} | driver s{s}" for s in S) + " |")
p("|---|---|" + "---|---|" * len(S))
for a in ages:
    p(f"| {a} | {age_tot[a]} | " + " | ".join(f'{ai[s]["by_age"].get(a, 0)} | {seeds[s]["INFERENCE"]["completed_by_age"].get(a, 0)}'
                                         for s in S) + " |")

# ---- AI population / settlements / RP at selected turns
p("\n### T13 AI polity at selected turns — population and settlements (MEASURED, telemetry), RP/turn (INFERENCE)\n")
p("| turn | " + " | ".join(f"pop s{s} | settl. s{s} | RP s{s}" for s in S) + " |")
p("|---|" + "---|---|---|" * len(S))
for t in ai["1"]["selected"]:
    p(f"| {t} | " + " | ".join(f'{f0(ai[s]["selected"][t]["population"])} | {ai[s]["selected"][t]["settlements"]} | '
                               f'{f1(ai[s]["selected"][t]["rp"])}' for s in S) + " |")

# ---- direct prefix-run observations (MEASURED)
p("\n### T12 direct prefix-run observations (MEASURED: `research --seed S --turns N --auto cheapest --node ID`)\n")
p("| event | " + " | ".join(f"seed {s}" for s in S) + " |")
p("|---|" + "---|" * len(S))


def ev_of(q):
    return q["node"] if q["node"] != "patent" else "technology count (Tree 1, all views)"


events = []
for q in seeds["1"]["SPOT_CHECKS_MEASURED"]:
    if ev_of(q) not in events:
        events.append(ev_of(q))


def cell(s, ev):
    qs = sorted((q for q in seeds[s]["SPOT_CHECKS_MEASURED"] if ev_of(q) == ev), key=lambda q: q["turn"])
    a, b = qs
    if ev.startswith("technology"):
        return f'{a["turn"]}: {a["technology_complete_cli"]} → {b["turn"]}: {b["technology_complete_cli"]}'
    return f'{a["turn"]}: {a["node_state"]} {a["node_progress"]:,.1f} → {b["turn"]}: {b["node_state"]}'


for ev in events:
    label = ev if ev.startswith("technology") else f"{ev} (state, progress RP)"
    p(f"| {label} | " + " | ".join(cell(s, ev) for s in S) + " |")
allok = all(q["views_ok"] and q["target_ok"] and q["state_ok"] and q["target_progress_ok"] in (True, None)
            for s in S for q in seeds[s]["SPOT_CHECKS_MEASURED"])
p(f"\nAll {sum(len(seeds[s]['SPOT_CHECKS_MEASURED']) for s in S)} prefix runs agree with the reconstruction "
  f"(per-view counts, active target, node state, active-target progress): {allok}.")

print("\n".join(out))
