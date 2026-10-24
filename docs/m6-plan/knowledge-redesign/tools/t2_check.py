"""Self-check for a tranche-2 lane-group output file (contract §1, §6, §7).

Usage: python3 -I tools/t2_check.py data/tranche2/out/<group>.json
Checks: every input node has exactly one audit record; every audit/new record has >= 1 realized effect or a live
unlock; operand ids and deltas valid; new ids unique and snake_case and not colliding; prerequisite atoms resolve
(existing nodes, or new nodes of the same file); no prerequisite from a later Age; no must-held prerequisite dated
later than the node (era-marked years); civ6/civ7 relation values valid; per-Age quotas reported.
"""
import json
import re
import sys
from pathlib import Path

PKG = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(PKG / "tools"))
import kr_lib as kr  # noqa: E402
from validate import earliest_marked_year, must_hold_atoms  # noqa: E402

OPERANDS = {
    "farming.yieldPerArableKm2PerYear", "farming.outputPerFarmerPerYear", "production.outputPerHerderPerYear",
    "production.outputPerExtractorPerYear", "production.toolYieldBonusMax", "production.toolWearPerEquippedFarmerPerYear",
    "consumption.grainSpoilagePerYear", "consumption.granaryYearsOfDemand", "harvestVariance.sigmaLogYield",
    "granaries.famineReductionMax", "granaries.farmYieldBonusMax", "foodState.adaptationAbsorbableShortfall",
    "housing.buildLaborAdultYearsPerDwelling", "housing.buildTimberPerDwelling", "housing.upkeepTimberPerDwellingYear",
    "housing.tauYears", "housing.personsPerDwelling", "pathBuild.laborPerAdultPerYear", "pathBuild.buildCostMultiplier",
    "pathBuild.dirtPathSpeedFactor", "transport.riverCostFactor", "roads.baselineKmPerDay", "roads.maxRouteKm",
    "catchment.hinterlandRadiusKm", "trade.gapClosingFraction", "trade.costPerBulkCostUnit",
    "price.stockReleaseRatePerYear", "demographics.mortalityPerYear", "demographics.starvationMortalityMaxPerYear",
    "demographics.starvationChildMultiplier", "institutions.universities.maxMortalityReduction",
    "institutions.universities.maxResearchCostReduction", "institutions.universities.maturityTauYears",
    "governance.authorityDecayCostUnits", "governance.taxExtractionResponseMax", "migration.baseRatePerYear",
    "migration.dampingDecayCostUnits", "mobility.promoteRatePerYear", "mobility.targetShareCap",
    "colonization.orderPartyFraction", "fog.exploreStepNodes", "fog.exploreMaxRadiusNodes", "fog.formationSightNodes",
    "research.civicsPointsCoefficient", "research.researchPointsCoefficient", "society.grievanceRelief",
    "society.unrestOutputDrag"}
LIVE_UNLOCK_PREFIX = ("building.", "infra.", "recipe.", "activity.", "inst.university", "order.tax_edict", "order.assay")
RELATIONS = {"direct", "partial", "different", "none-by-design", "not-reviewed"}
ACTIONS = {"keep", "add-prereq", "remove-prereq", "replace-prereq", "change-andor", "move-age", "adjust-cost",
           "add-dependency", "merge", "redesign"}
AGE = {f"A{i}": i for i in range(1, 10)}


def main(path):
    out = json.load(open(path))
    group = out["group"]
    inp = json.load(open(PKG / "data" / "tranche2" / "inputs" / f"{group}.json"))["nodes"]
    index = {n["id"]: n for n in json.load(open(PKG / "data" / "tranche2" / "inputs" / "all-nodes-index.json"))}
    errs, warns = [], []
    new = {n["id"]: n for n in out.get("newNodes", [])}
    known = dict(index)
    for i, n in new.items():
        if i in index:
            errs.append(f"new id {i} collides with an existing node")
        if not re.fullmatch(r"[a-z][a-z0-9_]*", i):
            errs.append(f"new id {i} not snake_case")
        known[i] = {"age": n.get("age"), "emerged": n.get("emerged")}

    def check_effects(rec, where):
        eff = rec.get("effects") or []
        unl = rec.get("liveUnlocks") or []
        if not eff and not unl:
            errs.append(f"{where} {rec['id']}: no realized effect and no live unlock")
        seen = set()
        for e in eff:
            if e.get("operand") not in OPERANDS:
                errs.append(f"{where} {rec['id']}: unknown operand {e.get('operand')}")
            d = e.get("delta")
            if not isinstance(d, (int, float)) or not (0 < d <= 0.5):
                errs.append(f"{where} {rec['id']}: delta {d} out of (0, 0.5]")
            if e.get("operand") in seen:
                errs.append(f"{where} {rec['id']}: duplicate operand {e.get('operand')}")
            seen.add(e.get("operand"))
        for u in unl:
            if not u.startswith(LIVE_UNLOCK_PREFIX):
                errs.append(f"{where} {rec['id']}: liveUnlock {u} is not a live kind")
        for k in ("civ6", "civ7"):
            c = rec.get(k) or {}
            if c.get("relation") not in RELATIONS:
                errs.append(f"{where} {rec['id']}: {k}.relation {c.get('relation')} invalid")

    def check_prereq(rec, expr, where):
        if not expr:
            return
        try:
            ast = kr.parse(expr)
        except Exception as ex:  # noqa: BLE001
            errs.append(f"{where} {rec['id']}: prerequisite does not parse: {ex}")
            return
        my_age = AGE.get(rec.get("age"), 0)
        y = earliest_marked_year(rec.get("emerged"))
        for a in kr.atoms(ast):
            if a not in known:
                errs.append(f"{where} {rec['id']}: unknown prerequisite {a}")
                continue
            if AGE.get(known[a].get("age"), 0) > my_age:
                errs.append(f"{where} {rec['id']}: prerequisite {a} is from a later Age")
        if y is not None and y > -3000:
            for a in must_hold_atoms(ast):
                ya = earliest_marked_year((known.get(a) or {}).get("emerged"))
                if ya is not None and ya > y:
                    errs.append(f"{where} {rec['id']}: must-held {a} ({ya:g}) dated after the node ({y:g})")

    audited = {}
    for r in out.get("audit", []):
        audited[r["id"]] = audited.get(r["id"], 0) + 1
        check_effects(r, "audit")
        if r.get("action") not in ACTIONS:
            errs.append(f"audit {r['id']}: action {r.get('action')} invalid")
        if r.get("newPrereq") is not None:
            check_prereq(r | {"age": r.get("newAge") or index.get(r["id"], {}).get("age"),
                              "emerged": index.get(r["id"], {}).get("emerged")}, r["newPrereq"], "audit")
    for n in inp:
        if audited.get(n["id"], 0) != 1:
            errs.append(f"input node {n['id']}: {audited.get(n['id'], 0)} audit records (need exactly 1)")
    for n in new.values():
        check_effects(n, "new")
        if n.get("age") not in AGE:
            errs.append(f"new {n['id']}: age {n.get('age')}")
        if earliest_marked_year(n.get("emerged")) is None:
            warns.append(f"new {n['id']}: emerged has no era-marked year")
        check_prereq(n, n.get("prereq"), "new")
    per_age = {}
    for n in new.values():
        per_age[n.get("age")] = per_age.get(n.get("age"), 0) + 1
    print(f"{group}: audit {len(out.get('audit', []))}/{len(inp)}, new {len(new)} by Age {dict(sorted(per_age.items()))}")
    for w in warns[:20]:
        print("WARN", w)
    for e in errs[:60]:
        print("ERROR", e)
    print(f"ERRORS {len(errs)} WARNINGS {len(warns)}")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
