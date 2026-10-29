#!/usr/bin/env python3
"""Research consequence audit — closure grade (M6 closure, D-066 R2: "the content/consequence audit").

Classifies EVERY item a Technology or Civics node carries, and every research entity, by what realizes it:

  OPERAND   effects.realized[] and repeatable.effects[] (ADR-047, ADR-049): the operand, mapped to the system that consumes
            it through the C# operand registry (Sim.Core/State/KnowledgeOperands.cs), mirrored here row for row and checked
            against the C# source on every run;
  FUTURE    effects.future[] (D-063 R1, CR-025): a declared effect of a system that does not exist yet — its owner (system)
            and target milestone;
  KNOWLEDGE unlocks.capabilities (the knowledge itself, as text) and unlocks.techniques / unlocks.applications (free text,
            declared knowledge-only one by one in scripts/research-consequence-declarations.json; D-066 A5);
  ENTITY    an entity unlock (units, buildings, institutions, infrastructure, activities, projects, recipes): realized by a
            consumer, baseline, or DEFERRED with an owner and a target (the M7 building charter §9 register, or a declaration);
  GATE      a requirement expression that names the node: the sim.json taxation gate, the research stage gate, the
            university founding chain (research.json inst.university / building.university through goods.json projects),
            the assay gate, goods.json recipe and deposit/herd knowledge, ages.json research milestones and every entity
            requirement — each mapped to the system that enforces it.

and enforces the rules of §1 (each a FAIL): zero unclassified items, a registered and consumed operand, a mirror that
equals the C# registry, an owner on every future effect and deferred entity, gates that resolve, and NO DOUBLE
APPLICATION (§1 R8). Not read here: unit-families.json identity requirements (the KNOW track reads them in the unlock
audit).

Nothing here is a progression table: every node row is derived from content. The C# loader is the authority on validity;
this is an independent re-reading with teeth.

Usage:  python3 scripts/research-consequence-audit.py            (write docs/research-consequence-audit.md)
        python3 scripts/research-consequence-audit.py --check    (exit 1 if the file is stale or any rule FAILs)
"""
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import research_lib as rl  # noqa: E402

TOOL = "research-consequence-audit.py"
OUT = rl.ROOT / "docs" / "research-consequence-audit.md"
CONTENT = rl.ROOT / "Sim.Data" / "content"
REGISTRY_CS = rl.ROOT / "Sim.Core" / "State" / "KnowledgeOperands.cs"
DECLARATIONS = rl.ROOT / "scripts" / "research-consequence-declarations.json"
CHARTER = rl.ROOT / "docs" / "m7-plan" / "building-and-infrastructure-charter.md"

# THE MIRROR of Sim.Core/State/KnowledgeOperands.cs (ADR-047 contract §1.1): operand id -> (the system that consumes it,
# None when consumed in play, else the CR-025 reason no system consumes it). Rule R3 compares it with the C# source.
OPERANDS = [
    ("farming.yieldPerArableKm2PerYear", "ProductionSystem", None),
    ("farming.outputPerFarmerPerYear", "ProductionSystem", None),
    ("production.outputPerHerderPerYear", "ProductionSystem", None),
    ("production.outputPerExtractorPerYear", "ProductionSystem", None),
    ("production.toolYieldBonusMax", "ProductionSystem", None),
    ("production.toolWearPerEquippedFarmerPerYear", "ProductionSystem", None),
    ("consumption.grainSpoilagePerYear", "ConsumptionSystem", None),
    ("consumption.granaryYearsOfDemand", "ConsumptionSystem", None),
    ("harvestVariance.sigmaLogYield", "HarvestWeatherSystem", None),
    ("granaries.famineReductionMax", "GranaryEffects", None),
    ("granaries.farmYieldBonusMax", "GranaryEffects", None),
    ("foodState.adaptationAbsorbableShortfall", "FoodState", None),
    ("housing.buildLaborAdultYearsPerDwelling", "HousingSystem", None),
    ("housing.buildTimberPerDwelling", "HousingSystem", None),
    ("housing.upkeepTimberPerDwellingYear", "HousingSystem", None),
    ("housing.tauYears", "HousingSystem", None),
    ("housing.personsPerDwelling", "HousingSystem and readers", None),
    ("pathBuild.laborPerAdultPerYear", "PathBuildSystem", None),
    ("pathBuild.buildCostMultiplier", "PathBuildSystem", None),
    ("pathBuild.dirtPathSpeedFactor", "PathBuildSystem", None),
    ("transport.riverCostFactor", "transport readers", None),
    ("roads.baselineKmPerDay", "TransportQuery", "no system uses overland travel time yet (CR-025)"),
    ("roads.maxRouteKm", "RoadDevelopmentQuery", None),
    ("catchment.hinterlandRadiusKm", "CatchmentSystem", None),
    ("trade.gapClosingFraction", "TradeArbitrageSystem", None),
    ("trade.costPerBulkCostUnit", "TradeArbitrageSystem", None),
    ("price.stockReleaseRatePerYear", "Price / Trade", None),
    ("demographics.mortalityPerYear", "DemographicsSystem", None),
    ("demographics.starvationMortalityMaxPerYear", "DemographicsSystem", None),
    ("demographics.starvationChildMultiplier", "DemographicsSystem", None),
    ("institutions.universities.maxMortalityReduction", "Institutions", None),
    ("institutions.universities.maxResearchCostReduction", "Institutions", None),
    ("institutions.universities.maturityTauYears", "Institutions", None),
    ("governance.authorityDecayCostUnits", "Governance", None),
    ("governance.taxExtractionResponseMax", "Governance", None),
    ("migration.baseRatePerYear", "MigrationSystem", None),
    ("migration.dampingDecayCostUnits", "MigrationSystem", None),
    ("mobility.promoteRatePerYear", "ClassMobilitySystem", None),
    ("mobility.targetShareCap", "ClassMobilitySystem", None),
    ("colonization.orderPartyFraction", "FoundingQuery", None),
    ("fog.exploreStepNodes", "DiscoveryQuery", None),
    ("fog.exploreMaxRadiusNodes", "DiscoveryQuery", None),
    ("fog.formationSightNodes", "DiscoveryQuery", None),
    ("research.civicsPointsCoefficient", "ResearchQuery (CP)", None),
    ("research.researchPointsCoefficient", "ResearchQuery (RP)", None),
    ("society.grievanceRelief", "NeedsGrievanceSystem", None),
    ("society.unrestOutputDrag", "Governance / Unrest", None),
]

# Per GATE / CONSUMER TYPE (never per node): the enforcing system and its predicate.
CONSUMERS = {
    "tax": "GovernanceSystem — Governance.CanLevyTax (sim.json governance.taxationRequires; order 5 SetTaxRate)",
    "stage": "ResearchSystem — ResearchQuery.StageReached (research.json researchStage.requires opens the subtrees; order 6)",
    "assay": "DiscoverySystem — DiscoveryQuery.AssayUnlocked (sim.json discovery.assayRequires; order 15 Prospect, Amount 1)",
    "recipe-knowledge": "ProductionSystem — CraftingQuery.IsRecipeAvailable (goods.json recipes[].knowledge; EconomyKnowledge.Met)",
    "good-knowledge": "ProductionSystem — EconomyKnowledge.Met (goods.json goods[].knowledge: deposit worked / herd bred only once known)",
    "age": "AgeTransitionSystem — AgeQuery.CheckAdvance (ages.json research milestone fact; order 7 AdvanceAge)",
    "recipe": "ProductionSystem — CraftingQuery.IsRecipeAvailable (goods.json recipes[].entity)",
    "building": "ConstructionSystem — ConstructionQuery.IsProjectAvailable (goods.json projects[].entity; order 4)",
    "institution": "ConstructionSystem + InstitutionsSystem — ConstructionQuery.IsProjectAvailable (projects[].founds; order 4)",
    "road": "RoadDevelopmentSystem — RoadDevelopmentQuery.IsClassKnown (sim.json roads.classes[].entity; order 8)",
    "trade": "TradeArbitrageSystem — TradeQuery.CanTrade (sim.json trade.entity)",
    "activity": "LabourActivities — the sector's activity identity (research.json sectorActivities)",
}
FUTURE_MILESTONES = {"M7", "M8", "M9", "M10", "M10+", "M11+", "unscheduled"}
ENTITY_LISTS = ("units", "buildings", "institutions", "infrastructure", "activities", "projects", "recipes")
TEXT_LISTS = ("techniques", "applications")
CHARTER_KINDS = ("building", "infra", "project", "inst")

RULES = [
    ("R1", "operand registered", "every realized and level operand is a row of the operand registry"),
    ("R2", "operand consumed in play", "every realized and level operand is consumed by a simulation system (CR-025 recurrence guard)"),
    ("R3", "mirror equals C#", "this script's operand table equals Sim.Core/State/KnowledgeOperands.cs row for row (id, reader, consumed)"),
    ("R4", "future effect has an owner", "every effects.future entry names a system and a milestone; its operand is null or registered and NOT consumed"),
    ("R5", "free text declared", "every unlocks.techniques / unlocks.applications string is declared knowledge-only, and every declaration is still in content"),
    ("R6", "entity classified", "every entity is realized by a consumer, baseline (null requirement), or deferred with an owner and a target"),
    ("R7", "gate resolves", "every gate expression parses and names only nodes or institution short ids; every gate has an enforcing system"),
    ("R8", "no double application", "DA-1 an operand twice in one node's realized list · DA-2 twice in one level list · DA-3 a future effect on an "
                                    "operand the same node already realizes or levels · DA-4 one entity unlocked twice by one node · DA-5 a non-empty "
                                    "effects.immediate (a retired channel that would apply on top of realized) · DA-6 one consumer object linked to "
                                    "two different entities"),
    ("R9", "zero unclassified", "every item of every node has a class; an entity list naming a non-entity is unclassified"),
    ("R10", "every node has a consequence", "every node has a realized, level or future effect, a realized or deferred entity link, or a gate"),
]


def read_inputs():
    research, sha = rl.load()

    def load(name):
        return json.loads((CONTENT / name).read_text(encoding="utf-8"))

    return {
        "research": research, "sha": sha, "goods": load("goods.json"), "sim": load("sim.json"), "ages": load("ages.json"),
        "registry_cs": REGISTRY_CS.read_text(encoding="utf-8"),
        "declarations": json.loads(DECLARATIONS.read_text(encoding="utf-8")),
        "charter": CHARTER.read_text(encoding="utf-8"),
    }


def parse_registry_cs(text):
    """The C# registry rows: (id, ReadBy, NotConsumed or None), in declaration order."""
    rows = re.findall(r'new\(KnowledgeOperand\.\w+, "([^"]+)", OperandDirection\.\w+, [\d.]+, (?:true|false), [\w.]+, '
                      r'"([^"]+)", "[^"]+"(?:,\s*"([^"]+)")?\)', text)
    return [(i, r, n or None) for i, r, n in rows]


def charter_register(text):
    """docs/m7-plan/building-and-infrastructure-charter.md §9: entity -> (status code, owner / blocker)."""
    if "## 9. Entity status register" not in text:
        return {}
    reg = text.split("## 9. Entity status register")[1].split("## 10.")[0]
    out = {}
    for m in re.finditer(r"^\| `((?:building|infra|project|inst)\.[a-z0-9_]+)` \| ([^|]+)\| ([^|]*)\|", reg, re.M):
        out[m.group(1)] = (m.group(2).strip(), m.group(3).strip())
    return out


def packet_enabled(goods, row):
    name = row.get("packet")
    return name is None or any(p["id"] == name and p.get("enabled", True) for p in goods.get("packets") or [])


def audit(inp):
    """Returns (findings, info, report text). A finding is (rule, subject, message) and FAILs the audit."""
    research, goods, sim, ages = inp["research"], inp["goods"], inp["sim"], inp["ages"]
    decl = inp["declarations"]
    nodes = rl.nodes_of(research)
    node_ids = {n["id"] for n in nodes}
    entities = {e["id"]: e for e in research["entities"]}
    registry = {i: (r, n) for i, r, n in OPERANDS}
    findings, info = [], []

    def fail(rule, subject, msg):
        findings.append((rule, subject, msg))

    # ---- R3: the mirror against the C# source ------------------------------------------------------------------
    cs = parse_registry_cs(inp["registry_cs"])
    if not cs:
        fail("R3", "KnowledgeOperands.cs", "no registry row parsed from the C# source")
    if cs != OPERANDS:
        cs_ids, py_ids = [r[0] for r in cs], [r[0] for r in OPERANDS]
        diff = [f"{a} != {b}" for a, b in zip(cs, OPERANDS) if a != b][:5]
        fail("R3", "KnowledgeOperands.cs", f"the mirror differs from the C# registry (C# {len(cs)} rows, mirror {len(py_ids)}; "
             f"only in C#: {sorted(set(cs_ids) - set(py_ids))}; only here: {sorted(set(py_ids) - set(cs_ids))}; rows: {diff})")

    # ---- consumers of entities and gates ------------------------------------------------------------------------
    consumers = {}   # entity id -> [(kind, label)]

    def add(eid, kind, label):
        consumers.setdefault(eid, []).append((kind, label))

    linked_by = {}   # DA-6: consumer object -> entity ids it is linked to
    for r in goods["recipes"]:
        if r.get("entity") and packet_enabled(goods, r):
            add(r["entity"], "recipe", f"recipe `{r['name']}`")
            linked_by.setdefault(("recipe", r["name"]), set()).add(r["entity"])
    for p in goods.get("projects") or []:
        if p.get("entity"):
            add(p["entity"], "building", f"project `{p['name']}`")
            linked_by.setdefault(("project", p["name"]), set()).add(p["entity"])
        if p.get("founds"):
            add(p["founds"]["entity"], "institution", f"project `{p['name']}` founds")
    for c in sim["roads"]["classes"]:
        if c.get("entity"):
            add(c["entity"], "road", f"road class `{c['entity']}`")
    if (sim.get("trade") or {}).get("entity"):
        add(sim["trade"]["entity"], "trade", "sim.json trade.entity")
    for sa in research["sectorActivities"]:
        for rr in sa["researched"]:
            add(rr["entity"], "activity", f"sector `{sa['sector']}` → {rr['mode']}")
    for (kind, name), eids in sorted(linked_by.items()):
        if len(eids) > 1:
            fail("R8", f"{kind} {name}", f"DA-6: linked to {len(eids)} entities {sorted(eids)} — one consumer, one knowledge link")

    gates = []   # (gate kind, label, expression)
    gates.append(("tax", "sim.json governance.taxationRequires", (sim.get("governance") or {}).get("taxationRequires")))
    gates.append(("stage", "research.json researchStage.requires", research["researchStage"]["requires"]))
    if (sim.get("discovery") or {}).get("assayRequires"):
        gates.append(("assay", "sim.json discovery.assayRequires", sim["discovery"]["assayRequires"]))
    for r in goods["recipes"]:
        if r.get("knowledge") and packet_enabled(goods, r):
            gates.append(("recipe-knowledge", f"goods.json recipe `{r['name']}`.knowledge", r["knowledge"]))
    for g in goods["goods"]:
        if g.get("knowledge") and packet_enabled(goods, g):
            gates.append(("good-knowledge", f"goods.json {'herd' if g.get('herd') else 'deposit'} `{g['name']}`.knowledge", g["knowledge"]))
    for ag in ages["ages"]:
        for group, items in (ag.get("entry") or {}).items():
            if not isinstance(items, list):
                continue
            for m in items:
                if m["fact"]["kind"] == "research":
                    gates.append(("age", f"ages.json Age {ag['key']} milestone `{m['id']}` ({group})", " OR ".join(m["fact"]["nodes"])))

    def expand(expr, where, seen=None):
        """The node atoms of a requirement, institution short ids expanded through their own requirement."""
        seen = seen or set()
        try:
            atoms = rl.atoms(rl.parse(expr))
        except SystemExit as ex:   # research_lib.fail
            fail("R7", where, f"expression {expr!r} does not parse ({ex})")
            return set()
        out = set()
        for a in sorted(atoms):
            if a in node_ids:
                out.add(a)
            elif "inst." + a in entities:
                if a not in seen:
                    seen.add(a)
                    out |= expand(entities["inst." + a]["requires"] or "", where, seen) if entities["inst." + a]["requires"] else set()
            elif not re.match(r"^[a-z_]+\s*(>=|<=|==|!=|>|<)", a):
                fail("R7", where, f"names `{a}`, which is neither a node nor an institution short id")
        return out

    gate_of = {n["id"]: [] for n in nodes}   # node -> [(gate kind, label)]
    for kind, label, expr in gates:
        if not expr:
            fail("R7", label, "gate expression is empty")
            continue
        if kind not in CONSUMERS:
            fail("R7", label, f"gate kind `{kind}` has no enforcing system")
        for nid in sorted(expand(expr, label)):
            gate_of[nid].append((kind, label))

    # ---- R6: every entity classified --------------------------------------------------------------------------
    register = charter_register(inp["charter"])
    declared_owner = decl.get("deferredOwners") or {}
    ent_class = {}
    for eid in sorted(entities):
        e = entities[eid]
        if e.get("requires"):
            for nid in sorted(expand(e["requires"], f"entity `{eid}`.requires")):
                gate_of[nid].append(("entity", f"entity `{eid}`"))
        kind = eid.split(".")[0]
        if consumers.get(eid):
            how = ", ".join(f"{CONSUMERS[k].split(' — ')[0]}: {lbl}" for k, lbl in consumers[eid])
            ent_class[eid] = ("realized", how + (" (null requirement: from the founding)" if e.get("requires") is None else ""))
        elif e.get("requires") is None:
            ent_class[eid] = ("baseline", "null requirement — available from the founding (no realizing system)")
        elif kind in CHARTER_KINDS and eid in register:
            status, owner = register[eid]
            ent_class[eid] = ("deferred", f"{owner} [charter §9 status {status}]")
        elif eid in declared_owner or (kind + ".*") in declared_owner:
            d = declared_owner.get(eid) or declared_owner[kind + ".*"]
            if not d.get("owner") or not d.get("target"):
                fail("R6", eid, "declared deferred owner without an owner and a target")
            ent_class[eid] = ("deferred", f"{d.get('owner')} [target {d.get('target')}]")
        else:
            fail("R6", eid, f"{e['kind']} entity requires `{e['requires']}`: no consumer, not baseline, no owner "
                            "(charter §9 register or research-consequence-declarations.json)")
            ent_class[eid] = ("UNCLASSIFIED", "")
    for k in sorted(declared_owner):
        if k.endswith(".*"):
            continue
        if k not in entities:
            fail("R6", k, "declared deferred owner for an entity research.json does not have (stale declaration)")
        elif ent_class[k][0] != "deferred" or not ent_class[k][1].endswith(f"[target {declared_owner[k].get('target')}]"):
            info.append(("R6", k, f"declared deferred owner superseded: the entity is now {ent_class[k][0]}"))

    # ---- per node --------------------------------------------------------------------------------------------
    known = decl.get("knowledgeOnly") or {}
    seen_text = set()
    rows, totals = [], {}
    stacking = []
    for n in nodes:
        nid = n["id"]
        items = []   # (class, detail)
        eff = n.get("effects") or {}
        if eff.get("immediate"):
            fail("R8", nid, f"DA-5: effects.immediate carries {len(eff['immediate'])} entries (retired channel; the loader refuses it)")
        realized_ops = []
        for x in eff.get("realized") or []:
            op = x.get("operand")
            realized_ops.append(op)
            if op not in registry:
                fail("R1", nid, f"realized operand `{op}` is not registered")
                items.append(("UNCLASSIFIED", f"realized {op}"))
                continue
            reader, dead = registry[op]
            if dead:
                fail("R2", nid, f"realized operand `{op}` is not consumed in play ({dead})")
            items.append(("OPERAND", f"{op} → {reader}"))
        for op in sorted({o for o in realized_ops if realized_ops.count(o) > 1}):
            fail("R8", nid, f"DA-1: operand `{op}` appears {realized_ops.count(op)} times in effects.realized")
        rp = n.get("repeatable") or {}
        level_ops = []
        if rp.get("levels"):
            for x in rp.get("effects") or []:
                op = x.get("operand")
                level_ops.append(op)
                if op not in registry:
                    fail("R1", nid, f"level operand `{op}` is not registered")
                    items.append(("UNCLASSIFIED", f"level {op}"))
                    continue
                reader, dead = registry[op]
                if dead:
                    fail("R2", nid, f"level operand `{op}` is not consumed in play ({dead})")
                items.append(("OPERAND", f"level {op} → {reader}"))
            for op in sorted({o for o in level_ops if level_ops.count(o) > 1}):
                fail("R8", nid, f"DA-2: operand `{op}` appears {level_ops.count(op)} times in repeatable.effects")
            for op in sorted(set(level_ops) & set(realized_ops)):
                stacking.append((nid, op))
        for x in eff.get("future") or []:
            system, ms, op = (x.get("system") or "").strip(), x.get("milestone"), x.get("operand")
            ok = True
            if not system or ms not in FUTURE_MILESTONES:
                fail("R4", nid, f"future effect without an owner system or a known milestone (system {system!r}, milestone {ms!r})")
                ok = False
            if op is not None:
                if op not in registry:
                    fail("R4", nid, f"future operand `{op}` is not registered")
                    ok = False
                elif registry[op][1] is None:
                    fail("R4", nid, f"future operand `{op}` is consumed in play by {registry[op][0]} — that effect would be realized, not future")
                    ok = False
                if op in realized_ops or op in level_ops:
                    fail("R8", nid, f"DA-3: future effect on `{op}`, which this node already realizes or levels")
            items.append(("FUTURE" if ok else "UNCLASSIFIED", f"{system} [{ms}]"))
        unlocks = n.get("unlocks") or {}
        for cap in unlocks.get("capabilities") or []:
            items.append(("KNOWLEDGE", "capability text"))
        for lst in TEXT_LISTS:
            for t in unlocks.get(lst) or []:
                if t in ((known.get(lst) or {}).get(nid) or []):
                    seen_text.add((lst, nid, t))
                    items.append(("KNOWLEDGE", f"{lst[:-1]} (declared)"))
                else:
                    fail("R5", nid, f"{lst} item {t!r} is free text not declared knowledge-only")
                    items.append(("UNCLASSIFIED", f"{lst}: {t}"))
        named = []
        for lst in ENTITY_LISTS:
            for eid in unlocks.get(lst) or []:
                named.append(eid)
                if eid not in entities:
                    fail("R9", nid, f"unlocks.{lst} names `{eid}`, which is not an entity")
                    items.append(("UNCLASSIFIED", f"{lst}: {eid}"))
                    continue
                c = ent_class[eid][0]
                items.append(("ENTITY-" + c.upper() if c != "UNCLASSIFIED" else "UNCLASSIFIED", eid))
        for eid in sorted({e for e in named if named.count(e) > 1}):
            fail("R8", nid, f"DA-4: entity `{eid}` is unlocked {named.count(eid)} times by this node")
        for kind, label in sorted(set(gate_of[nid])):
            if kind == "entity":
                eid = label.split("`")[1]
                if eid in named:
                    continue   # already counted as the unlock
                items.append(("GATE", f"{label} ({ent_class[eid][0]})"))
            else:
                items.append(("GATE", f"{kind}: {label}"))
        consequence = [c for c, _ in items if c in ("OPERAND", "FUTURE", "GATE") or c.startswith("ENTITY-")]
        if not consequence:
            fail("R10", nid, "no realized, level or future effect, no entity link and no gate")
        unclassified = sum(1 for c, _ in items if c == "UNCLASSIFIED")
        for c, _ in items:
            totals[c] = totals.get(c, 0) + 1
        rows.append((n, items, unclassified))
    for lst in TEXT_LISTS:
        for nid, texts in sorted((known.get(lst) or {}).items()):
            for t in texts:
                if (lst, nid, t) not in seen_text:
                    fail("R5", nid, f"stale declaration: {lst} item {t!r} is no longer in research.json")
    for nid, op in stacking:
        info.append(("R8", nid, f"completion + level on `{op}`: the completion delta applies once (ADR-047) and each level adds "
                                "cap × k × (1 − k)^(n−1) (ADR-049 §1.6); both are summed into Σ before the registry cap "
                                "(KnowledgeEffects.Improvement) — two declared channels, not a duplicate within one"))
    if totals.get("UNCLASSIFIED"):
        fail("R9", "all nodes", f"{totals['UNCLASSIFIED']} unclassified item(s)")
    return findings, info, render(inp, rows, totals, ent_class, gates, findings, info, register)


def render(inp, rows, totals, ent_class, gates, findings, info, register):
    research = inp["research"]
    L = ["# Research consequence audit (closure grade)", "",
         f"**Generated** by `scripts/{TOOL}` from the canonical content (research.json sha256 `{inp['sha'][:16]}…`), the "
         "operand registry `Sim.Core/State/KnowledgeOperands.cs`, `scripts/research-consequence-declarations.json` and the M7 "
         "building charter §9 register. Do not edit by hand; CI runs `--check`, which fails on a stale file or on any FAIL "
         "below. M6 closure, D-066 R2 (the content/consequence audit) and A5 / A9.", "",
         "## 1. Rules", "", "| rule | name | what it requires | result |", "|---|---|---|---|"]
    for rid, name, what in RULES:
        n = sum(1 for f in findings if f[0] == rid)
        L.append(f"| {rid} | {name} | {what} | {'PASS' if n == 0 else f'**FAIL** ({n})'} |")
    L += ["", f"**Result: {'PASS — zero unclassified items, no rule failed' if not findings else f'FAIL — {len(findings)} finding(s)'}.**", ""]
    if findings:
        L += ["| rule | subject | finding |", "|---|---|---|"]
        for rid, subj, msg in findings:
            L.append(f"| {rid} | `{subj}` | {msg.replace('|', '/')} |")
        L.append("")
    L += ["## 2. Classification totals", "",
          f"{len(rows)} nodes (Technology {len(research['technologies'])}, Civics {len(research['civics'])}); "
          f"{len(ent_class)} entities.", "", "| item class | items |", "|---|---|"]
    for c in sorted(totals):
        L.append(f"| {c} | {totals[c]} |")
    ec = {}
    for c, _ in ent_class.values():
        ec[c] = ec.get(c, 0) + 1
    L += ["", "| entity class | entities |", "|---|---|"] + [f"| {c} | {ec[c]} |" for c in sorted(ec)]
    L += ["", "## 3. Operands → consuming system (the mirror of `KnowledgeOperands.cs`)", "",
          "| operand | consumed by | in play | nodes (realized) | nodes (levels) |", "|---|---|---|---|---|"]
    use_r, use_l = {}, {}
    for n, _, _ in rows:
        for x in (n.get("effects") or {}).get("realized") or []:
            use_r[x["operand"]] = use_r.get(x["operand"], 0) + 1
        rp = n.get("repeatable") or {}
        if rp.get("levels"):
            for x in rp.get("effects") or []:
                use_l[x["operand"]] = use_l.get(x["operand"], 0) + 1
    for op, reader, dead in OPERANDS:
        L.append(f"| `{op}` | {reader} | {'yes' if dead is None else 'NO — ' + dead} | {use_r.get(op, 0)} | {use_l.get(op, 0)} |")
    L += ["", "## 4. Gates → enforcing system", "",
          "The university founding chain is the entity gate of `building.university` and `inst.university` (research.json), "
          "realized by the goods.json projects that found universities (§5).", "",
          "| gate | expression | enforced by |", "|---|---|---|"]
    for kind, label, expr in gates:
        L.append(f"| {label} | `{(expr or '').replace('|', '/')}` | {CONSUMERS.get(kind, '**none**')} |")
    L += ["", "## 5. Entities", "", "| entity | class | realized by / owner |", "|---|---|---|"]
    for eid in sorted(ent_class):
        c, how = ent_class[eid]
        L.append(f"| `{eid}` | {c} | {how.replace('|', '/')} |")
    decl = inp["declarations"]
    L += ["", "## 6. Free text declared knowledge-only", "", decl.get("freeTextRule", ""), "",
          "| node | list | items |", "|---|---|---|"]
    for lst in TEXT_LISTS:
        for nid, texts in sorted((decl.get("knowledgeOnly") or {}).get(lst, {}).items()):
            L.append(f"| `{nid}` | {lst} | {'; '.join(texts)} |")
    L += ["", "## 7. Double application (R8) and recorded observations", ""]
    if info:
        L += ["| rule | subject | observation |", "|---|---|---|"]
        for rid, subj, msg in info:
            L.append(f"| {rid} | `{subj}` | {msg} |")
    else:
        L.append("None.")
    L += ["", "## 8. Every node", "",
          "Counts per item class (O operand, F future, K knowledge text, E entity unlock, G gate, U unclassified).", "",
          "| node | O | F | K | E | G | U |", "|---|---|---|---|---|---|---|"]
    for n, items, unc in rows:
        def c(pred):
            return sum(1 for k, _ in items if pred(k))
        L.append(f"| `{n['id']}` | {c(lambda k: k == 'OPERAND')} | {c(lambda k: k == 'FUTURE')} | {c(lambda k: k == 'KNOWLEDGE')} | "
                 f"{c(lambda k: k.startswith('ENTITY-'))} | {c(lambda k: k == 'GATE')} | {unc} |")
    L.append("")
    return "\n".join(L)


def main(argv):
    findings, _, text = audit(read_inputs())
    rl.write_or_check(OUT, text, argv, TOOL)
    if findings:
        sys.exit(f"{TOOL}: {len(findings)} FAIL finding(s): " + "; ".join(f"{r} {s}: {m}" for r, s, m in findings[:10]))
    print(f"{TOOL}: every rule PASS; zero unclassified items")


if __name__ == "__main__":
    main(sys.argv)
