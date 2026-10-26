"""Tranche 2 reports from the runtime research content and the lane-group audit records (D-063).

Writes, next to the tranche-2 contract:
  tranche2-change-report.md     counts, parity, added / merged / dropped nodes, dependency and Age changes, placements
  tranche2-effect-coverage.md   every operand: nodes, summed delta vs cap, saturation scaling; live unlocks; repeatables
  tranche2-civ-comparison.md    Civ VI / Civ VII relation of every node (direct / partial / different / none-by-design /
                                not-reviewed), per tree and Age
  data/tranche2/node-audit.json the node-by-node audit joined to the runtime node (the review artifact's data)

Usage: python3 -I tools/tranche2_reports.py
"""
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

PKG = Path(__file__).resolve().parent.parent
REPO = PKG.parents[2]
T2 = PKG / "data" / "tranche2"
RESEARCH = REPO / "Sim.Data" / "content" / "research.json"
RELATIONS = ["direct", "partial", "different", "none-by-design", "not-reviewed"]
CONTENT = REPO / "Sim.Data" / "content"
UNSCHEDULED = "unscheduled"


def live_entities(d):
    """CR-025 ruling (2026-10-26): the entity ids some system realizes, read from the same content links the research
    card reads (InfoQuery.Consumers): goods.json construction projects (entity / founds.entity) and recipes, the
    research sector activities, sim.json road classes and the trade entity. A placeholder unlock is NOT live. (Before the
    ruling every `building.` / `infra.` / `recipe.` / `activity.` id and inst.university counted, placeholders included.)"""
    goods = load(CONTENT / "goods.json")
    sim = load(CONTENT / "sim.json")
    live = set()
    for p in goods.get("projects", []):
        live.add(p.get("entity"))
        live.add((p.get("founds") or {}).get("entity"))
    for r in goods.get("recipes", []):
        live.add(r.get("entity"))
    for sa in d.get("sectorActivities", []):
        for r in sa.get("researched", []):
            live.add(r.get("entity"))
    for c in (sim.get("roads") or {}).get("classes", []):
        live.add(c.get("entity"))
    live.add((sim.get("trade") or {}).get("entity"))
    live.discard(None)
    return live


def atoms(expr):
    return set(re.findall(r"[a-z][a-z0-9_]*", expr or "")) - {"and", "or", "not"}


def load(p):
    with open(p, encoding="utf-8") as fh:
        return json.load(fh)


def main():
    d = load(RESEARCH)
    pre = load(T2 / "research.pre-tranche2.json")
    rep = load(T2 / "import-report.json")
    review = load(T2 / "review-new-nodes.json")["verdicts"]
    dec = load(T2 / "decisions.json")
    nodes = [("technology", n) for n in d["technologies"]] + [("civics", n) for n in d["civics"]]
    pre_ids = {n["id"] for n in pre["technologies"] + pre["civics"]}
    audit, newrec = {}, {}
    for p in sorted((T2 / "out").glob("*.json")):
        o = load(p)
        for r in o.get("audit", []):
            audit[r["id"]] = r
        for r in o.get("newNodes", []):
            newrec[r["id"]] = r
    caps = {}
    gates = {"taxation": "order.tax_edict (sim.json governance.taxationRequires)",
             "ore_prospecting": "order.assay (sim.json discovery.assayRequires)"}
    live = live_entities(d)
    # Node -> the entity ids whose knowledge requirement names it (ResearchNode.UnlockedEntities).
    unlocks = defaultdict(list)
    for e in d["entities"]:
        for a in atoms(e.get("requires")):
            unlocks[a].append(e["id"])
    # The other consequences the research card counts as simulated: Age milestone facts, the research stage, the tax edict.
    ages = load(CONTENT / "ages.json")
    milestone_nodes = set()
    def walk(o):
        if isinstance(o, dict):
            f = o.get("fact")
            if isinstance(f, dict) and f.get("kind") == "research":
                milestone_nodes.update(f.get("nodes") or [])
            for v in o.values():
                walk(v)
        elif isinstance(o, list):
            for v in o:
                walk(v)
    walk(ages)
    stage_nodes = atoms(d["researchStage"].get("requires"))
    tax_nodes = atoms(load(CONTENT / "sim.json")["governance"].get("taxationRequires"))

    # ---------------------------------------------------------------- node-by-node audit data
    rows = []
    for tree, n in nodes:
        r = audit.get(n["id"]) or newrec.get(n["id"]) or {}
        rep_d = n.get("repeatable") if isinstance(n.get("repeatable"), dict) else None
        ents = sorted(set(unlocks.get(n["id"], [])))
        rows.append({
            "key": n["key"], "id": n["id"], "name": n["name"], "tree": tree, "age": n["age"],
            "branch": n.get("branch"), "cost": n["cost"], "prereq": n.get("prereq"), "depth": n["depth"],
            "origin": "pre-tranche" if n["id"] in pre_ids else ("engine" if n["key"] >= 1990 else "tranche-2"),
            "realized": (n.get("effects") or {}).get("realized", []),
            "liveUnlocks": [e for e in ents if e in live],
            "future": (n.get("effects") or {}).get("future", []),
            "otherLive": [k for k, on in (("age milestone fact", n["id"] in milestone_nodes),
                                          ("research stage", n["id"] in stage_nodes),
                                          ("tax edict", n["id"] in tax_nodes)) if on],
            "orderGate": gates.get(n["id"]),
            "levels": rep_d.get("effects") if rep_d and rep_d.get("levels") else None,
            "costGrowth": rep_d.get("costGrowth") if rep_d else None,
            "auditAction": r.get("action"), "auditRationale": r.get("rationale") or r.get("prereqRationale"),
            "prereqAssessment": r.get("prereqAssessment"), "effectRationale": r.get("effectRationale"),
            "civ6": r.get("civ6") or {"relation": "not-reviewed", "analog": None},
            "civ7": r.get("civ7") or {"relation": "not-reviewed", "analog": None},
        })
    json.dump({"nodes": rows}, open(T2 / "node-audit.json", "w", encoding="utf-8"), indent=1, ensure_ascii=False)

    # ---------------------------------------------------------------- change report
    fin = lambda arr: sum(1 for n in arr if not isinstance(n.get("repeatable"), dict))
    tf, cf = fin(d["technologies"]), fin(d["civics"])
    by_age = defaultdict(Counter)
    for tree, n in nodes:
        by_age[n["age"]][(tree, "new" if n["id"] not in pre_ids else "old")] += 1
    L = ["# Tranche 2 — change report", "",
         "Generated by `tools/tranche2_reports.py` from `Sim.Data/content/research.json` (runtime, authoritative) and the "
         "lane-group audit records. Re-run after any content change.", "",
         "## Counts", "",
         "| | Pre-tranche | Now | Finite now | Repeatable |", "|---|---:|---:|---:|---:|",
         f"| Technology | {len(pre['technologies'])} | {len(d['technologies'])} | {tf} | {len(d['technologies']) - tf} |",
         f"| Civics | {len(pre['civics'])} | {len(d['civics'])} | {cf} | {len(d['civics']) - cf} |", "",
         f"Civics : Technology (finite) = {cf} : {tf} = **{100.0 * cf / tf:.1f} %** (D-063 R5 asks 90–100 %).", "",
         "The pre-tranche runtime held 496 technologies; the 494 in the planning workbook counted two deferred merges as "
         "retired. Runtime is authoritative and the 496 are all kept.", "",
         "## Nodes per Age (old / new)", "", "| Age | Tech old | Tech new | Civics old | Civics new |", "|---|---:|---:|---:|---:|"]
    for a in sorted(by_age):
        c = by_age[a]
        L.append(f"| {a} | {c[('technology', 'old')]} | {c[('technology', 'new')]} | {c[('civics', 'old')]} | {c[('civics', 'new')]} |")
    verdicts = Counter(v["verdict"] for v in review)
    L += ["", "## Review of proposed new nodes", "",
          "Every proposed node was reviewed for duplication across lanes and against existing nodes:", ""]
    L += [f"- **{k}**: {v}" for k, v in sorted(verdicts.items())]
    L += ["", "### Merged into an existing node", "", "| Proposed | Merged into | Why |", "|---|---|---|"]
    L += [f"| {v['id']} | {v.get('target')} | {(v.get('why') or '').replace('|', '/')} |" for v in review if v["verdict"] == "merge-into"]
    L += ["", "### Dropped", "", "| Proposed | Why |", "|---|---|"]
    L += [f"| {v['id']} | {(v.get('why') or '').replace('|', '/')} |" for v in review if v["verdict"] == "drop"]
    L += ["", "## Dependency and Age changes applied", "", "| Node | Action | Before | After | Rationale |", "|---|---|---|---|---|"]
    for a in rep.get("applied", []):
        L.append(f"| {a['id']} | {a.get('action')} | {a['old'].get('prereq')} {a['old'].get('age') or ''} | "
                 f"{a['new'].get('prereq')} {a['new'].get('age') or ''} | {(a.get('rationale') or '').replace('|', '/')} |")
    L += ["", "## Subtree placement (loader rule: Main-tree knowledge never sits behind the research stage)", "",
          "| Node | Placed in | Because it requires |", "|---|---|---|"]
    L += [f"| {p['id']} | {p['branch']} | {', '.join(p['because'])} |" for p in rep.get("subtreePlacement", [])]
    L += ["", "## Eurekas dropped because an audit edge made the precursor a hard prerequisite", ""]
    L += [f"- {e['id']}: `{e['when']}`" for e in rep.get("impliedEurekasDropped", [])]
    (PKG / "tranche2-change-report.md").write_text("\n".join(L) + "\n", encoding="utf-8")

    # ---------------------------------------------------------------- effect coverage
    reg = (REPO / "Sim.Core" / "State" / "KnowledgeOperands.cs").read_text(encoding="utf-8")
    # CR-025: a row may carry a seventh argument, the NotConsumed reason (no system consumes the operand in play).
    for m in re.finditer(r'new\(KnowledgeOperand\.\w+, "([^"]+)", OperandDirection\.(\w+), ([\d.]+), (true|false), [^,]+, '
                         r'"([^"]+)", "([^"]+)"(?:,\s*"([^"]+)")?\)', reg):
        caps[m.group(1)] = (m.group(2), float(m.group(3)), m.group(5), m.group(6), m.group(7))
    per_op = defaultdict(list)
    for tree, n in nodes:
        for e in (n.get("effects") or {}).get("realized", []):
            per_op[e["operand"]].append((n["id"], e["delta"]))
    consumed = {op for op, v in caps.items() if v[4] is None}
    real_eff = lambda r: any(e["operand"] in consumed for e in r["realized"])
    n_real = sum(1 for r in rows if real_eff(r))
    n_dead = sum(1 for r in rows for e in r["realized"] if e["operand"] not in consumed)
    n_live_only = sum(1 for r in rows if not real_eff(r) and r["liveUnlocks"])
    n_gate = sum(1 for r in rows if not real_eff(r) and not r["liveUnlocks"] and r["orderGate"])
    n_other = sum(1 for r in rows if not real_eff(r) and not r["liveUnlocks"] and not r["orderGate"] and not r["levels"] and r["otherLive"])
    n_lev = sum(1 for r in rows if r["levels"])
    is_real = lambda r: real_eff(r) or r["liveUnlocks"] or r["orderGate"] or r["levels"] or r["otherLive"]
    future_only = [r["id"] for r in rows if not is_real(r) and r["future"]]
    n_future = sum(len(r["future"]) for r in rows)
    none = [r["id"] for r in rows if not is_real(r) and not r["future"]]
    L = ["# Tranche 2 — effect coverage (D-063 R1, as amended by the CR-025 ruling of 2026-10-26)", "",
         "Generated by `tools/tranche2_reports.py` from `Sim.Data/content/research.json`. A REAL effect is live in this "
         "build: a realized operand effect on an operand some system consumes in play, a level rule, a live entity unlock "
         "(an entity some system realizes — goods.json projects and recipes, the sector activities, the sim.json road classes "
         "and trade entity; a placeholder unlock does not count), an order gate, or what the research card also counts "
         "(an Age milestone fact, the research stage, the tax edict). A FUTURE effect (`effects.future`) is a declaration, "
         "never counted as realized (D-063 R1 amendment).", "",
         f"- Nodes: **{len(rows)}**. With a realized operand effect on a consumed operand: **{n_real}**. Live unlock only: "
         f"{n_live_only}. Order gate only: {n_gate} ({', '.join(gates)}). Age milestone, stage or tax edict only: {n_other}. "
         f"Repeatables with level effects: {n_lev}.",
         f"- Realized effects on an operand no system consumes: **{n_dead}** (the loader refuses them since the CR-025 ruling).",
         f"- Nodes whose only declared effect is a FUTURE one (not simulated in this build): **{len(future_only)}**"
         f"{(' — ' + ', '.join(future_only)) if future_only else ''}; future declarations in all: {n_future}.",
         f"- Nodes with neither a real effect nor a future declaration: **{len(none)}**{(' — ' + ', '.join(none)) if none else ''}. "
         "They are the known gaps `Tranche2ContentTests.KnownGapsWithoutARealEffect` lists (CR-025 §7.3).",
         "- A realized effect is read by its owning system (ADR-047); the per-operand sum is clamped to its cap. Where the "
         "authored sum exceeded 90 % of the cap, every delta on that operand was scaled down proportionally so no node is "
         "made inert by the clamp (the saturation column).", "",
         "| Operand | Direction | Cap Σ | Nodes | Σ deltas | Σ / cap | Saturation scale | Read by |", "|---|---|---:|---:|---:|---:|---:|---|"]
    for op, (dirn, cap, reader, meaning, not_consumed) in caps.items():
        lst = per_op.get(op, [])
        s = sum(x for _, x in lst)
        sat = rep.get("saturation", {}).get(op, {}).get("scaledBy")
        read = reader if not_consumed is None else f"none in play: {not_consumed} (the {reader} hook)"
        L.append(f"| `{op}` | {dirn} | {cap:g} | {len(lst)} | {s:.3f} | {100 * s / cap:.0f} % | {sat if sat else ''} | {read} |")
    L += ["", "## Declared future effects (not simulated in this build)", "",
          "| Node | Tree | Milestone | System | Operand | Description |", "|---|---|---|---|---|---|"]
    for r in rows:
        for f in r["future"]:
            L.append(f"| {r['id']} | {r['tree']} | {f['milestone']} | {f['system']} | "
                     f"{('`' + f['operand'] + '`') if f.get('operand') else '—'} | {f['description']} |")
    L += ["", "## Repeatables (level effects)", "", "| Node | Tree | Cost growth | Level effects (operand, gap fraction) |", "|---|---|---:|---|"]
    for r in rows:
        if r["levels"]:
            L.append(f"| {r['id']} | {r['tree']} | {r['costGrowth']} | " +
                     ", ".join(f"`{e['operand']}` {e['gapFraction']}" for e in r["levels"]) + " |")
    (PKG / "tranche2-effect-coverage.md").write_text("\n".join(L) + "\n", encoding="utf-8")

    # ---------------------------------------------------------------- Civ comparison
    L = ["# Tranche 2 — Civilization VI / VII comparison", "",
         "Every node carries a relation to its nearest Civilization VI (base game) and Civilization VII node: **direct** "
         "(same concept and role), **partial** (overlapping concept), **different** (same name, different role), "
         "**none-by-design** (deliberately absent there — finer-grained history or simulation-specific), "
         "**not-reviewed**. Analogs come from `data/civ6-civics-base.json`, `data/civ7-civics.json` and the lane audits.", ""]
    for game in ("civ6", "civ7"):
        L += [f"## {'Civilization VI' if game == 'civ6' else 'Civilization VII'}", "",
              "| Tree | " + " | ".join(RELATIONS) + " |", "|---|" + "---:|" * len(RELATIONS)]
        for tree in ("technology", "civics"):
            c = Counter(r[game]["relation"] for r in rows if r["tree"] == tree)
            L.append(f"| {tree} | " + " | ".join(str(c.get(k, 0)) for k in RELATIONS) + " |")
        L.append("")
    L += ["## Direct and partial analogs", "", "| Node | Tree | Age | Civ VI | Civ VII |", "|---|---|---|---|---|"]
    for r in rows:
        a6, a7 = r["civ6"], r["civ7"]
        if a6["relation"] in ("direct", "partial") or a7["relation"] in ("direct", "partial"):
            f = lambda a: f"{a['relation']}: {a['analog']}" if a.get("analog") else a["relation"]
            L.append(f"| {r['id']} | {r['tree']} | {r['age']} | {f(a6)} | {f(a7)} |")
    (PKG / "tranche2-civ-comparison.md").write_text("\n".join(L) + "\n", encoding="utf-8")
    print(f"finite tech {tf}, finite civics {cf} ({100.0 * cf / tf:.1f} %); realized {n_real}; future only {len(future_only)}; "
          f"no effect {len(none)}")


if __name__ == "__main__":
    main()
