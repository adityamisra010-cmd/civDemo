#!/usr/bin/env python3
"""Research → gameplay unlock audit (R1; the Director's unlock-pipeline directive).

Reads the CANONICAL content — research.json (nodes, entities, sectorActivities, researchStage, baseline),
goods.json (recipes[].entity, projects[].entity / founds.entity), sim.json (roads.classes[].entity,
governance.taxationRequires) and ages.json (research milestone facts) — and writes
docs/research-gameplay-unlock-audit.md: for EVERY Technology and Civics node, the knowledge it provides, the
gameplay consequences its completion makes legal (each content-declared link to a realizing domain), its
prerequisites, the class (A–J), whether the relationship is canonical in content, whether a simulation
system enforces it and where the UI exposes it — then the counts per class and every discrepancy.

Nothing here is a progression table. The ONLY mapping in this file is per CONSUMER TYPE (which domain
realizes a recipe entity, a road-class entity, …) and it names no node: every node row is derived from the
content links. The C# loader is the authority on validity; this is an independent re-reading for the report.

Usage:  python3 scripts/research-gameplay-unlock-audit.py            (write the audit)
        python3 scripts/research-gameplay-unlock-audit.py --check    (exit 1 if the audit is stale)
"""
import json
import sys

sys.path.insert(0, str(__import__("pathlib").Path(__file__).resolve().parent))
import research_lib as rl  # noqa: E402

AUDIT = rl.ROOT / "docs" / "research-gameplay-unlock-audit.md"
TOOL = "research-gameplay-unlock-audit.py"
CONTENT = rl.ROOT / "Sim.Data" / "content"

CLASSES = [
    ("A", "Baseline", "exists with zero research (declared in content: a baseline record or a null requirement)"),
    ("B", "Research-gated gameplay capability", "gates a production recipe, a labour activity, the tax edict or an Age milestone"),
    ("C", "Knowledge-only / modifier", "capability strings or immediate effects, no realizing link"),
    ("D", "Institution prerequisite", "gates a REALIZED institution (a founding project) or the research stage"),
    ("E", "Infrastructure prerequisite", "gates a road class RoadDevelopmentSystem builds"),
    ("F", "Military / unit unlock", "gates a unit entity (load-validated; recruitment is M6)"),
    ("G", "Application / realization", "gates a building a construction project builds"),
    ("H", "Repeatable", "a repeatable node (D-044 T6 / D-046 G4)"),
    ("I", "Content-only / descriptive", "no capability string, no effect, no entity"),
    ("J", "Explicitly deferred", "every consequence is an entity no system of this milestone realizes"),
]

# Per CONSUMER TYPE (never per node): the realizing domain, its authoritative predicate, order kind, UI surface,
# and the tests that pin unavailable → available. B/D/E/G are the realized types.
CONSUMERS = {
    "recipe": ("B", "ProductionSystem — CraftingQuery.IsRecipeAvailable", "none (the Crafting labour share runs it)",
               "Production — crafts we know", "ResearchUnlockPipelineTests.T03–T06"),
    "activity": ("B", "LabourActivities (labour identity; ADR-033 D1 — production unchanged)", "3 SectorAllocation (sector baseline)",
                 "Labour — the sector's activity label", "ResearchUnlockPipelineTests.T01–T02"),
    "tax": ("B", "GovernanceSystem — Governance.CanLevyTax", "5 SetTaxRate", "Governance — the tax edict",
            "ResearchUnlockPipelineTests.T07, T08, T15"),
    "age": ("B", "AgeTransitionSystem — AgeQuery.CheckAdvance (milestone fact)", "7 AdvanceAge", "Age — advance",
            "AgeProgressionTests; ResearchUnlockPipelineTests.T15"),
    "stage": ("D", "ResearchSystem — ResearchQuery.StageReached (research stage opens the subtrees)", "6 SetResearchTarget",
              "Research — available nodes", "ResearchEngineTests"),
    "institution": ("D", "ConstructionSystem + InstitutionsSystem — ConstructionQuery.IsProjectAvailable (founds)", "4 EnqueueConstruction",
                    "Construction — found a university", "ResearchUnlockPipelineTests.T11–T15"),
    "road": ("E", "RoadDevelopmentSystem — RoadDevelopmentQuery.IsClassKnown", "8 DevelopRoads", "Roads — develop",
             "ResearchUnlockPipelineTests.T09–T10, T15"),
    "building": ("G", "ConstructionSystem — ConstructionQuery.IsProjectAvailable", "4 EnqueueConstruction", "Construction — build",
                 "ConstructionAvailabilityTests; ResearchUnlockPipelineUiTests (synthetic node)"),
}
PRECEDENCE = "HBEDGFJCI"


def load(name):
    return json.loads((CONTENT / name).read_text(encoding="utf-8"))


def atoms_of(expr):
    return sorted(rl.atoms(rl.parse(expr))) if expr else []


def build():
    research, sha = rl.load()
    goods, sim, ages = load("goods.json"), load("sim.json"), load("ages.json")
    g = rl.Graph(research)
    nodes = g.nodes
    node_ids = set(g.by_id)
    civics = {n["id"] for n in research["civics"]}
    entities = {e["id"]: e for e in research["entities"]}

    def resolve(atom):
        if atom in node_ids:
            return ("node", atom)
        if "inst." + atom in entities:
            return ("inst", "inst." + atom)
        return ("unresolved", atom)

    # ---- consumers: entity id / pseudo-entity -> list of (consumer type, label) -------------------------
    consumers = {}

    def add(key, kind, label):
        consumers.setdefault(key, []).append((kind, label))

    for r in goods["recipes"]:
        if r.get("entity"):
            add(r["entity"], "recipe", f"recipe `{r['name']}`")
    for p in goods.get("projects") or []:
        if p.get("entity"):
            add(p["entity"], "building", f"project `{p['name']}`")
        if p.get("founds"):
            add(p["founds"]["entity"], "institution", f"project `{p['name']}` founds")
    for c in sim["roads"]["classes"]:
        if c.get("entity"):
            add(c["entity"], "road", f"road class `{c['entity']}`")
    for sa in research["sectorActivities"]:
        for rr in sa["researched"]:
            add(rr["entity"], "activity", f"sector `{sa['sector']}` → {rr['mode']}")

    pseudo = {}   # pseudo requirement → (consumer type, label, expression)
    pseudo["tax"] = ("tax", "sim.json governance.taxationRequires", sim["governance"]["taxationRequires"])
    pseudo["stage"] = ("stage", "research.json researchStage.requires", research["researchStage"]["requires"])
    for ag in ages["ages"]:
        for group, items in (ag.get("entry") or {}).items():
            if not isinstance(items, list):
                continue
            for m in items:
                f = m["fact"]
                if f["kind"] == "research":
                    pseudo["age:" + m["id"]] = ("age", f"Age {ag['key']} milestone `{m['id']}` ({group})", " OR ".join(f["nodes"]))

    # ---- per node: what it gates directly, and through institution atoms ---------------------------------
    gates = {n["id"]: [] for n in nodes}   # node -> [(entity or pseudo, consumer kind or None, via)]

    def requirement_nodes(expr, seen=None):
        """Node atoms of an expression, institution atoms expanded through their own requirements."""
        seen = seen or set()
        out = []
        for a in atoms_of(expr):
            k, v = resolve(a)
            if k == "node":
                out.append((v, None))
            elif k == "inst" and v not in seen:
                seen.add(v)
                for nid, via in requirement_nodes(entities[v]["requires"], seen):
                    out.append((nid, via or v))
        return out

    for eid, e in entities.items():
        for nid, via in requirement_nodes(e["requires"]):
            gates[nid].append((eid, via))
    for key, (_, _, expr) in pseudo.items():
        for nid, via in requirement_nodes(expr):
            gates[nid].append((key, via))

    def consumer_of(key):
        if key in pseudo:
            kind, label, _ = pseudo[key]
            return [(kind, label)]
        return consumers.get(key, [])

    # ---- classify ------------------------------------------------------------------------------------------
    rows, counts = [], {c[0]: 0 for c in CLASSES}
    for n in nodes:
        nid = n["id"]
        flags, cons = set(), []
        for key, via in sorted(set(gates[nid]), key=lambda kv: (kv[0], kv[1] or "")):
            cs = consumer_of(key)
            if cs:
                for kind, label in cs:
                    flags.add(CONSUMERS[kind][0])
                    cons.append((key, label, kind, via))
            else:
                kind = entities[key]["kind"]
                flags.add("F" if kind == "unit" else "J")
                cons.append((key, "no realizing system", None, via))
        if n.get("repeatable"):
            flags.add("H")
        caps = n["unlocks"]["capabilities"]
        effects = (n.get("effects") or {}).get("immediate") or []
        if not flags:
            flags.add("C" if caps or effects else "I")
        primary = next(c for c in PRECEDENCE if c in flags)
        counts[primary] += 1
        rows.append((n, primary, sorted(flags, key=PRECEDENCE.index), cons, caps))

    # ---- baseline (class A) --------------------------------------------------------------------------------
    baseline = [(b["id"], b["name"], "baseline record", "simulated" if b["simulated"] else "not simulated")
                for b in research["baseline"]]
    for sa in research["sectorActivities"]:
        b = sa["baseline"]
        baseline.append((f"sector.{sa['sector']}.{b['id']}", b["name"], "sector baseline identity", "simulated (LabourActivities)"))
    for eid, e in entities.items():
        if e["requires"] is None:
            realized = ", ".join(lbl for _, lbl in consumers.get(eid, [])) or "no realizing system"
            baseline.append((eid, e["name"], "null-requirement entity", realized))
    for c in sim["roads"]["classes"]:
        if not c.get("entity"):
            baseline.append(("road class (no entity)", "the free baseline road class (DirtPath)", "sim.json roads.classes", "simulated"))
    counts["A"] = len(baseline)

    # ---- discrepancies -------------------------------------------------------------------------------------
    disc = []
    reach = g.reachable()
    known = set(reach)   # every reachable node + every institution short id whose requirement can hold (fixpoint)
    changed = True
    while changed:
        changed = False
        for eid, e in entities.items():
            short = eid[5:] if eid.startswith("inst.") else None
            if short and short not in known and (e["requires"] is None or rl.holds(rl.parse(e["requires"]), known)):
                known.add(short)
                changed = True
    for eid, e in entities.items():
        if e["requires"] and not consumers.get(eid):
            disc.append(("deferred", eid, f"{e['kind']} entity requires `{e['requires']}`; no system of this milestone realizes it",
                         "DEFERRED — " + ("recruitment is M6 (ADR-033 D7)" if e["kind"] == "unit"
                                          else "no owning system yet; knowledge eligibility is shown in the trees and lenses only")))
        if e.get("unresolved"):
            disc.append(("unresolved", eid, f"requirement names declared-unresolved {e['unresolved']} (reads false)", "content (ADR-029)"))
        if e["requires"] and not rl.holds(rl.parse(e["requires"]), known):
            disc.append(("unreachable", eid, "requirement can never hold on the graph", "content defect"))
    for r in goods["recipes"]:
        if not r.get("entity"):
            disc.append(("unlinked", f"recipe {r['name']}", "goods.json recipe without a knowledge entity", "OPEN"))
            continue
        e = entities[r["entity"]]
        if e["requires"] is None:
            out = r["output"]["good"]
            claims = [n["id"] for n in nodes if any(out in s.lower() for s in n["unlocks"]["capabilities"])]
            if claims:
                disc.append(("baseline-claim", r["entity"],
                             f"declared BASELINE, but node capability strings mention `{out}`: {', '.join('`'+c+'`' for c in claims)}",
                             "KEPT BASELINE — ownership matrix classifies it A; D-047 left recipe gating to the Director "
                             "(\"no blanket gate\"); the directive named pottery and bronze casting only. Director decision."))
    for p in goods.get("projects") or []:
        if not p.get("entity"):
            disc.append(("unlinked", f"project {p['name']}", "construction project without a knowledge entity", "OPEN"))
    # fixed by R1 (recorded so the history reads in one place)
    disc.append(("fixed", "goods.json recipes", "E15: crafting recipes ran with no knowledge requirement — pottery and bronze before research",
                 "FIXED (R1): recipe entities + CraftingQuery; ProductionSystem enforces"))
    disc.append(("fixed", "ActionSurface", "university-founding descriptors (Institutions domain) were never rendered by the action surface",
                 "FIXED (R1): they join the construction block (same EnqueueConstruction button)"))

    # ---- write ---------------------------------------------------------------------------------------------
    L = []
    L.append("# Research → gameplay unlock audit")
    L.append("")
    L.append(f"**Generated** by `scripts/{TOOL}` from the canonical content (research.json sha256 `{sha[:16]}…`). "
             "Do not edit by hand; CI runs `--check`. Every row is derived from content links; the only mapping in "
             "the script is per consumer type (§1), which names no node.")
    L.append("")
    L.append("## 1. How a node's consequence is realized (per consumer type)")
    L.append("")
    L.append("| consumer type | class | enforcing system — predicate | order kind | UI surface (AvailableActionsQuery) | tests |")
    L.append("|---|---|---|---|---|---|")
    for k, (c, sys_, order, ui, tests) in CONSUMERS.items():
        L.append(f"| {k} | {c} | {sys_} | {order} | {ui} | {tests} |")
    L.append("")
    L.append("A consequence is CANONICAL when content declares it (an entity requirement, a consumer's entity link, a "
             "requirement expression). ENFORCED means the realizing system calls the knowledge evaluator "
             "(`ResearchQuery.IsKnowledgeEligible` / `RequirementMet`) through the predicate named above; the UI lists the "
             "action only when the query does (one predicate, every caller). Unrealized entities are DEFERRED (§4).")
    L.append("")
    L.append("## 2. Counts per class")
    L.append("")
    L.append("Each node is counted once, under its primary class (precedence H, B, E, D, G, F, J, C, I); A counts the "
             "baseline rows of §3.")
    L.append("")
    L.append("| class | name | rule | count |")
    L.append("|---|---|---|---|")
    for c, name, rule in CLASSES:
        L.append(f"| {c} | {name} | {rule} | {counts[c]} |")
    L.append(f"| | **nodes** | Technology {len(research['technologies'])} + Civics {len(research['civics'])} | **{len(nodes)}** |")
    L.append("")
    kinds = {}
    for _, _, flags, cons, _ in rows:
        for _, _, kind, _ in cons:
            kinds[kind or "deferred"] = kinds.get(kind or "deferred", 0) + 1
    L.append("Node→consequence links by consumer type: " + ", ".join(f"{k} {kinds[k]}" for k in sorted(kinds)) + ".")
    L.append("")
    L.append("## 3. Baseline (class A)")
    L.append("")
    L.append("| id | name | declared as | realized |")
    L.append("|---|---|---|---|")
    for bid, name, how, realized in baseline:
        L.append(f"| `{bid}` | {name} | {how} | {realized} |")
    L.append("")
    L.append("## 4. Discrepancies")
    L.append("")
    L.append("| type | subject | finding | resolution |")
    L.append("|---|---|---|---|")
    order = {"fixed": 0, "baseline-claim": 1, "unlinked": 2, "unresolved": 3, "unreachable": 4, "deferred": 5}
    for t, subj, finding, res in sorted(disc, key=lambda d: (order[d[0]], d[1])):
        L.append(f"| {t} | `{subj}` | {finding} | {res} |")
    L.append("")
    L.append("## 5. Every node")
    L.append("")
    L.append("Columns: class (primary, then all), prerequisites, knowledge (capability strings), consequences "
             "(entity or requirement → consumer; `via inst.x` when the node acts through an institution's requirement), "
             "canonical / enforced / UI.")
    L.append("")
    L.append("| node | tree | age | class | prereq | knowledge | consequences | canonical | enforced | UI |")
    L.append("|---|---|---|---|---|---|---|---|---|---|")
    for n, primary, flags, cons, caps in rows:
        tree = "civics" if n["id"] in civics else "tech"
        cs = []
        realized = deferred = False
        uis = []
        for key, label, kind, via in cons:
            v = f" via `{via}`" if via else ""
            cs.append(f"`{key}` → {label}{v}")
            if kind:
                realized = True
                ui = CONSUMERS[kind][3].split(" — ")[0]
                if ui not in uis:
                    uis.append(ui)
            else:
                deferred = True
        enforced = ("yes" if realized and not deferred else "partly (rest deferred)" if realized
                    else "deferred" if deferred else "n/a")
        L.append("| `{}` | {} | {} | {} | {} | {} | {} | {} | {} | {} |".format(
            n["id"], tree, n["age"], primary + (" (" + "".join(flags) + ")" if len(flags) > 1 else ""),
            (n.get("prereq") or "—").replace("|", "/"), "; ".join(caps).replace("|", "/") or "—",
            "<br>".join(cs) or "—", "yes" if cons else "—", enforced, ", ".join(uis) or "—"))
    L.append("")
    return "\n".join(L)


if __name__ == "__main__":
    rl.write_or_check(AUDIT, build(), sys.argv, TOOL)
