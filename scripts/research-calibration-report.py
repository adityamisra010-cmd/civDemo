#!/usr/bin/env python3
"""Research calibration report — PROVISIONAL, NOT FROZEN (ADR-029 addendum A, ADR-030,
research-foundation gate rulings 1 and 6).

Reads the AUTHORED research content (Sim.Data/content/research.json) and a MEASURED
population trace of the canonical world (scripts/research-calibration/population-trace-seed42.csv,
written by `sim research --seed 42 --turns 1630 --auto cheapest --trace-population <file>`), and
writes docs/research-calibration-report.md:

  * the RP model and the cost model, with the A1 = 5,000 U-calibration anchor checked;
  * the finite workload by Age (recursive research excluded) and the final-Age workload;
  * the provisional population pacing scenarios — static estimates, scenario inputs only;
  * the content-derived Eureka components beside the proposal's scenario multipliers;
  * the measured scenario: what the canonical world's own population pays over the campaign.

Nothing here moves a cost (gate ruling 1: no calibration adjustment to hit Age-duration
targets), and nothing here is ratified: every population below is a SCENARIO INPUT.

Usage:  python3 scripts/research-calibration-report.py            (write the report)
        python3 scripts/research-calibration-report.py --check    (exit 1 if the report is stale)
"""
import csv
import sys

sys.path.insert(0, str(__import__("pathlib").Path(__file__).resolve().parent))
import research_lib as rl  # noqa: E402

TRACE = rl.ROOT / "scripts" / "research-calibration" / "population-trace-seed42.csv"
REPORT = rl.ROOT / "docs" / "research-calibration-report.md"
TOOL = "research-calibration-report.py"
FINAL_AGE = "A9"

# --------------------------------------------------------------------------
# SCENARIO INPUTS (provisional — printed in the report; none is ratified)
# --------------------------------------------------------------------------
# Provisional population per Age (the finalization proposal's reference civilization).
# A1 = 5,000 is the U calibration anchor (gate ruling 1); every other value is a scenario.
PROVISIONAL_POPULATION = {"A1": 5_000, "A2": 20_000, "A3": 80_000, "A4": 200_000, "A5": 600_000,
                          "A6": 1_500_000, "A7": 3_000_000, "A8": 10_000_000, "A9": 40_000_000}
# Final-Age scenario populations (gate ruling 1: ~108M is NOT ratified — a scenario input only).
FINAL_AGE_SCENARIOS = [40_000_000, 80_000_000, 107_944_361, 113_300_000, 160_000_000]
# The proposal's net workload multipliers (research-final-cost-report §3), as stated there:
#   optimised          — a university-style 15 % cost reduction (illustrative, above the 20 % floor)
#                        and Eureka plus foreign exposure drawing on one <= 40 % pool;
#   lower development  — HALF the population, no universities, no foreign exposure, half the
#                        Eureka achievement.
OPTIMISED_MULTIPLIER = 0.756
LOWER_DEV_MULTIPLIER = 0.985
LOWER_DEV_POPULATION_FACTOR = 0.5
UNIVERSITY_FACTOR = 0.85
# The proposal's own static tables (research-final-cost-report §3–§4), printed beside the
# recomputation so a reader sees where they agree and where they do not.
PROPOSAL_AGE_TURNS = {"A1": (271, 575), "A2": (557, 1184), "A3": (273, 587), "A4": (179, 376), "A5": (134, 284),
                      "A6": (89, 187), "A7": (83, 176), "A8": (168, 359), "A9": (400, 846)}
PROPOSAL_FINAL_TURNS = {40_000_000: (400, 846), 80_000_000: (247, 521), 113_300_000: (193, 409),
                        160_000_000: (152, 321)}
PROPOSAL_POPULATION_FOR = {250: 78_442_798, 200: 107_944_361, 150: 162_909_676}
TARGET_BAND = (150, 200, 250)  # final-Age turns (the proposal's band; a target, never a gate)
U_ANCHOR_POPULATION = 5_000
U_ANCHOR_TURNS = 3


def eureka_entitlement(node, ceiling):
    eus = node.get("eurekas") or []
    if not eus:
        return 0.0, 0.0
    if all(e.get("weight") is None for e in eus):
        ws = [ceiling / len(eus)] * len(eus)
    else:
        ws = [e["weight"] for e in eus]
    full = min(sum(ws), ceiling)
    evaluable = min(sum(w for w, e in zip(ws, eus) if e.get("when")), ceiling)
    return full, evaluable


def turns(workload, multiplier, tuning, population):
    return workload * multiplier / rl.rp_per_turn(tuning, population)


def population_for(workload, multiplier, tuning, target_turns):
    rp = tuning["rpPerTurn"]
    return (workload * multiplier / target_turns / rp["coefficient"]) ** (1.0 / rp["exponent"])


def load_trace():
    with TRACE.open(newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def build():
    content, sha = rl.load()
    g = rl.Graph(content)
    tuning = content["tuning"]
    ceiling = tuning["accelerationCreditCeilingFraction"]
    floor = tuning["effectiveCostFloorFraction"]
    U, K = tuning["costModel"]["U"], tuning["costModel"]["K"]
    rp = tuning["rpPerTurn"]
    finite = g.finite()
    by_age = {a: [n for n in finite if n["age"] == a] for a in rl.AGES}
    workload = {a: sum(n["cost"] for n in by_age[a]) for a in rl.AGES}
    out = []
    w = out.append

    w("# Research calibration report — PROVISIONAL, NOT FROZEN")
    w("")
    w(f"Generated by `scripts/{TOOL}` from `Sim.Data/content/research.json` (SHA-256 `{sha}`) and the measured")
    w("trace `scripts/research-calibration/population-trace-seed42.csv`. Do not edit by hand; CI runs `--check`.")
    w("")
    w("**Status: PROVISIONAL CALIBRATION.** Nothing in this report is frozen architecture. Every population is a")
    w("scenario input (gate ruling 1: the ~108M final-Age figure is NOT ratified). Costs stay content-derived; no")
    w("cost is moved to hit an Age-duration target. The D-045 report this replaces is kept as")
    w("`docs/research-calibration-report-d045.md`; its per-year pacing is obsolete (ADR-030 §4).")
    w("")

    # ---------------------------------------------------------------- models
    w("## 1. The two models")
    w("")
    w(f"- **Research Points (ADR-030):** RP per turn = {rp['coefficient']} × population^{rp['exponent']} — per strategic")
    w("  turn, never multiplied by dtYears; one shared pool for Technology and Civics. Anchors: "
      + ", ".join(f"{int(p):,} → {rl.rp_per_turn(tuning, float(p)):.3f} (stated {v})"
                  for p, v in rp["anchors"].items()) + ".")
    w(f"- **Cost:** BaseCost = U × K^magnitude, U = {U}, K = {K}; EffectiveCost = max({floor} × BaseCost, BaseCost ×")
    w(f"  modifiers); acceleration credit (Eureka + foreign exposure) ≤ {ceiling} × BaseCost per node, capped at the")
    w("  remaining EffectiveCost.")
    a1_rp = rl.rp_per_turn(tuning, U_ANCHOR_POPULATION)
    w(f"- **U calibration anchor (A1 = {U_ANCHOR_POPULATION:,}, gate ruling 1):** RP({U_ANCHOR_POPULATION:,}) = "
      f"{a1_rp:.3f} RP/turn, so a magnitude-0 node ({U} RP) takes {U / a1_rp:.3f} turns "
      f"(stated: {U_ANCHOR_TURNS}).")
    w("")

    # ---------------------------------------------------------------- workload
    w("## 2. Finite workload by Age (RP; the recursive set excluded)")
    w("")
    w("| Age | nodes | workload | cumulative | Eureka entitlement (all) | of which evaluable today |")
    w("|---|---|---|---|---|---|")
    cum = 0.0
    shares = {}
    for a in rl.AGES:
        cum += workload[a]
        full = sum(eureka_entitlement(n, ceiling)[0] * n["cost"] for n in by_age[a])
        ev = sum(eureka_entitlement(n, ceiling)[1] * n["cost"] for n in by_age[a])
        shares[a] = (full / workload[a], ev / workload[a])
        w(f"| {a} | {len(by_age[a])} | {rl.fmt_int(workload[a])} | {rl.fmt_int(cum)} | "
          f"{rl.fmt_int(full)} ({100 * shares[a][0]:.1f} %) | {rl.fmt_int(ev)} ({100 * shares[a][1]:.1f} %) |")
    w(f"| **all** | **{len(finite)}** | **{rl.fmt_int(cum)}** | | | |")
    w("")
    spec = sum(n["cost"] for n in by_age[FINAL_AGE] if n["id"] in g.speculative)
    w(f"**Final-Age finite workload: {rl.fmt_int(workload[FINAL_AGE])} RP across {len(by_age[FINAL_AGE])} nodes** "
      f"(speculative nodes {100 * spec / workload[FINAL_AGE]:.1f} % of it). Recursive research "
      f"({len(g.recursive)} repeatables) is excluded from every figure here.")
    w("")

    # ---------------------------------------------------------------- scenarios
    w("## 3. Provisional population pacing scenarios (static estimates)")
    w("")
    w("turns = workload × multiplier ÷ RP(population). **Optimised:** the provisional population, multiplier "
      f"{OPTIMISED_MULTIPLIER}. **Lower development:** {LOWER_DEV_POPULATION_FACTOR:g} × the population, multiplier "
      f"{LOWER_DEV_MULTIPLIER}. Both multipliers are the proposal's stated scenario inputs (§4 sets them beside the")
    w("content-derived components). Intermediate Ages are observed, not normalised.")
    w("")
    w("| Age | provisional population (scenario) | RP/turn | optimised turns | lower-development turns | proposal |")
    w("|---|---|---|---|---|---|")
    for a in rl.AGES:
        p = PROVISIONAL_POPULATION[a]
        w(f"| {a} | {p:,}{' (U anchor)' if a == 'A1' else ''} | {rl.rp_per_turn(tuning, p):,.1f} | "
          f"{turns(workload[a], OPTIMISED_MULTIPLIER, tuning, p):.0f} | "
          f"{turns(workload[a], LOWER_DEV_MULTIPLIER, tuning, p * LOWER_DEV_POPULATION_FACTOR):.0f} | "
          f"{PROPOSAL_AGE_TURNS[a][0]} / {PROPOSAL_AGE_TURNS[a][1]} |")
    w("")
    w(f"### Final Age ({FINAL_AGE}) — scenario populations (none ratified)")
    w("")
    w("| optimised population (scenario) | optimised turns | lower-development turns | proposal |")
    w("|---|---|---|---|")
    for p in FINAL_AGE_SCENARIOS:
        prop = PROPOSAL_FINAL_TURNS.get(p)
        w(f"| {p:,} | {turns(workload[FINAL_AGE], OPTIMISED_MULTIPLIER, tuning, p):.0f} | "
          f"{turns(workload[FINAL_AGE], LOWER_DEV_MULTIPLIER, tuning, p * LOWER_DEV_POPULATION_FACTOR):.0f} | "
          f"{f'{prop[0]} / {prop[1]}' if prop else '—'} |")
    w("")
    lo, mid, hi = TARGET_BAND
    w(f"Optimised population that would give {hi} / {mid} / {lo} final-Age turns: "
      + " / ".join(f"{population_for(workload[FINAL_AGE], OPTIMISED_MULTIPLIER, tuning, t):,.0f}" for t in (hi, mid, lo))
      + " (proposal: " + " / ".join(f"{PROPOSAL_POPULATION_FOR[t]:,}" for t in (hi, mid, lo))
      + "). These are scenario readings, not a ratified reference population (gate ruling 1).")
    w("")
    w("**Reproduction.** The workloads agree with the proposal's §2 to the RP. The turn counts are recomputed with")
    w(f"the engine's stored exponent {rp['exponent']} and agree with the proposal to within one turn for A1, A5–A7 and")
    w("every final-Age scenario row; A2, A3, A4 and A8 differ by up to 3 % (A3: 281 against 273). The exponent rounding")
    w("(log10 5 = 0.69897…) accounts for under 0.1 % of that, so the proposal's intermediate rows rest on inputs it does")
    w("not state. The recomputed values are the ones this report stands behind; neither set is a ratified pace.")
    w("")

    # ---------------------------------------------------------------- components
    w("## 4. The multipliers against the content")
    w("")
    e_full, e_ev = shares[FINAL_AGE]
    w(f"The content fixes only the Eureka part. In {FINAL_AGE}, the full Eureka entitlement is "
      f"{100 * e_full:.2f} % of the workload ({100 * e_ev:.2f} % from conditions evaluable today). No modifier or")
    w("foreign-exposure source exists, so their sizes are assumptions:")
    w("")
    w(f"- optimised = {UNIVERSITY_FACTOR} (university-style) − Eureka − foreign exposure. With every Eureka fired, the stated "
      f"{OPTIMISED_MULTIPLIER} implies foreign exposure worth {100 * (UNIVERSITY_FACTOR - e_full - OPTIMISED_MULTIPLIER):.2f} % "
      "of the final-Age workload;")
    w(f"- lower development = 1 − ½ × Eureka = **{1 - 0.5 * e_full:.3f}** from the content, against the stated "
      f"{LOWER_DEV_MULTIPLIER} (½ × the evaluable-today share alone gives {1 - 0.5 * e_ev:.3f}).")
    w("")
    w("The proposal's derivation of its two multipliers is not reproducible from the stated assumptions alone; the")
    w("tables above use them as given. Under the content-derived lower-development multiplier the final Age at the")
    w(f"40M scenario takes {turns(workload[FINAL_AGE], 1 - 0.5 * e_full, tuning, 40_000_000 * LOWER_DEV_POPULATION_FACTOR):.0f} "
      "turns instead.")
    w("")

    # ---------------------------------------------------------------- measured
    rows = load_trace()
    w("## 5. Measured scenario — the canonical world's own population")
    w("")
    w(f"`sim research --seed 42 --turns {len(rows)} --auto cheapest --trace-population`: the canonical seed-42 world")
    w("(12 settlements, 1024²). Research reads population and writes only its own tables, so this population is the")
    w("same with or without research (IntegratedPinAttributionTests).")
    w("")
    w("| turn | sim-year | dtYears | population | RP this turn | cumulative RP |")
    w("|---|---|---|---|---|---|")
    checkpoints = {1, 100, 200, 300, 500, 800, 1000, 1200, 1400, len(rows)}
    cum_rp = 0.0
    cum_by_turn = []
    for r in rows:
        cum_rp += float(r["researchPointsThisTurn"])
        cum_by_turn.append((int(r["turn"]), r["simYear"], int(r["population"]), cum_rp))
        if int(r["turn"]) in checkpoints:
            w(f"| {r['turn']} | {r['simYear']} | {r['dtYears']} | {int(r['population']):,} | "
              f"{float(r['researchPointsThisTurn']):,.1f} | {rl.fmt_int(cum_rp)} |")
    w("")
    total = sum(workload.values())
    w(f"Over the whole campaign this world generates **{rl.fmt_int(cum_rp)} RP — {100 * cum_rp / total:.1f} % of the "
      f"{rl.fmt_int(total)} RP finite workload.** Its final population ({cum_by_turn[-1][2]:,}) is "
      f"{PROVISIONAL_POPULATION[FINAL_AGE] / cum_by_turn[-1][2]:.0f}× below the provisional {FINAL_AGE} scenario.")
    w("")
    w("Static Age-order schedule (cumulative RP against cumulative finite workload; no Eureka, no modifiers):")
    w("")
    w("| Age | cumulative workload | covered at turn | sim-year | population then |")
    w("|---|---|---|---|---|")
    cum_w = 0.0
    for a in rl.AGES:
        cum_w += workload[a]
        hit = next((c for c in cum_by_turn if c[3] >= cum_w), None)
        if hit:
            w(f"| {a} | {rl.fmt_int(cum_w)} | {hit[0]} | {hit[1]} | {hit[2]:,} |")
        else:
            w(f"| {a} | {rl.fmt_int(cum_w)} | not within {len(rows)} turns | — | — |")
    w("")
    w("This is a measurement of the present demography, not a pacing verdict: the reference populations, the")
    w("Age-to-dt mapping (ADR-030 §4) and any non-population RP input remain open.")
    w("")

    # ---------------------------------------------------------------- counts
    w("## 6. Counts")
    w("")
    eus = [e for n in g.nodes for e in (n.get("eurekas") or [])]
    w(f"- Eurekas: {len(eus)} on {sum(1 for n in g.nodes if n.get('eurekas'))} nodes; evaluable today "
      f"{sum(1 for e in eus if e.get('when'))}")
    w(f"- repeatables (recursive, excluded): {len(g.recursive)}; speculative finite (counted): {len(g.speculative)}; "
      f"finite: {len(finite)}")
    w("- Group B reachability and the per-node Eureka list: `docs/research-corpus-audit.md`")
    w("")
    return "\n".join(out)


def main():
    rl.write_or_check(REPORT, build(), sys.argv, TOOL)


if __name__ == "__main__":
    main()
