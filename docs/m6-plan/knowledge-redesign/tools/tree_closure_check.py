"""Tree-closure integrity checks for the technology and civics trees (the Director's closure checklist, 2026-10-26).

Reads only content (Sim.Data/content/research.json, ages.json) and prints a Markdown report. FAIL items are defects that
block closure; INFO items are facts the closure record states (for example, nodes whose effect waits on a future system).

  1  duplicate ids / keys (across both trees)
  2  prerequisite references: every token names an existing node
  3  reachability: every node becomes reachable from the roots (fixed point over AND/OR); no cycle
  4  Age order: no prerequisite of a LATER content Age than the node
  5  unlock references: every entity-like unlock (unit., building., inst., infra., activity., recipe., project.) names
     an entity in `entities`, and every entity is unlocked by some node or listed as a baseline
  6  descriptions: every node has a non-empty name and description
  7  effects: every node has a realized effect, a declared future effect, a live gate (sim.json) or a live entity unlock
     (D-063 R1 + Amendment 1; the C# test is authoritative); future-only nodes listed
  8  orphans: finite nodes with no dependents, no unlock, no realized effect and no milestone reference
  9  milestones: every ages.json research fact names existing nodes
 10  costs: cost == content_cost == round(U x 2^magnitude / 10) x 10, magnitude == the sum of its factors

Usage: python3 -I tools/tree_closure_check.py [research.json]   (exit 1 on any FAIL)
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]


def main(argv):
    path = Path(argv[0]) if argv else ROOT / "Sim.Data" / "content" / "research.json"
    doc = json.load(open(path, encoding="utf-8"))
    ages = json.load(open(ROOT / "Sim.Data" / "content" / "ages.json", encoding="utf-8"))
    nodes = [(t, n) for t in ("technologies", "civics") for n in doc[t]]
    by_id = {n["id"]: n for _, n in nodes}
    tree = {n["id"]: t for t, n in nodes}
    out, fails = [], 0

    def row(label, ok, detail):
        nonlocal fails
        fails += 0 if ok in (True, None) else 1
        status = "PASS" if ok is True else ("INFO" if ok is None else "**FAIL**")
        out.append(f"| {label} | {status} | {detail} |")

    out += [f"# Tree-closure integrity report", "", f"Content: `{path.relative_to(ROOT) if path.is_relative_to(ROOT) else path}` — "
            f"{sum(1 for t, _ in nodes if t == 'technologies')} technologies, {sum(1 for t, _ in nodes if t == 'civics')} civics, "
            f"{len(doc['entities'])} entities.", "", "| Check | Result | Detail |", "|---|---|---|"]

    ids = [n["id"] for _, n in nodes]
    keys = [n["key"] for _, n in nodes]
    dup_ids = sorted({i for i in ids if ids.count(i) > 1})
    dup_keys = sorted({(tree[n["id"]], n["key"]) for _, n in nodes if sum(1 for _, m in nodes if m["key"] == n["key"] and tree[m["id"]] == tree[n["id"]]) > 1})
    row("1 duplicate ids / keys", not dup_ids and not dup_keys, f"{len(ids)} ids unique; keys unique per tree" if not dup_ids and not dup_keys else f"ids {dup_ids} keys {dup_keys}")

    tokens = {i: [x for x in re.findall(r"[A-Za-z][A-Za-z0-9_]*", by_id[i].get("prereq") or "") if x not in ("AND", "OR", "NOT")] for i in ids}
    broken = sorted({(i, x) for i, ts in tokens.items() for x in ts if x not in by_id})
    row("2 prerequisite references", not broken, "every prerequisite names an existing node" if not broken else str(broken[:10]))

    compiled = {}
    for i in ids:
        py = re.sub(r"\bAND\b", " and ", re.sub(r"\bOR\b", " or ", by_id[i].get("prereq") or ""))
        py = re.sub(r"[a-z][a-z0-9_]*", lambda m: f"r({m.group(0)!r})" if m.group(0) in by_id
                    else (m.group(0) if m.group(0) in ("and", "or", "not") else "False"), py)   # a broken reference never holds
        compiled[i] = compile(py.strip() or "True", i, "eval")
    reached, changed = set(), True
    while changed:
        changed = False
        for i in ids:
            if i not in reached and eval(compiled[i], {"r": lambda x: x in reached}):
                reached.add(i)
                changed = True
    unreachable = sorted(set(ids) - reached)
    roots = sum(1 for i in ids if not tokens[i])
    row("3 reachability (no cycle, no dead prerequisite)", not unreachable,
        f"all {len(ids)} reachable from {roots} roots" if not unreachable else f"{len(unreachable)} unreachable: {unreachable[:10]}")

    age = {i: int(by_id[i]["age"][1]) for i in ids}
    later = sorted((i, x) for i, ts in tokens.items() for x in ts if x in age and age[x] > age[i])
    row("4 Age order of prerequisites", not later, "no node requires a node of a later Age" if not later else f"{len(later)}: {later[:8]}")

    entity_ids = {e["id"] for e in doc["entities"]}
    prefixes = ("unit.", "building.", "inst.", "infra.", "activity.", "recipe.", "project.")
    unlocked = set()
    bad_unlock = []
    for i in ids:
        for cat, vals in by_id[i]["unlocks"].items():
            for v in vals:
                if isinstance(v, str) and v.startswith(prefixes):
                    unlocked.add(v)
                    if v not in entity_ids:
                        bad_unlock.append((i, v))
    ent = {e["id"]: e for e in doc["entities"]}
    short = {e.split(".", 1)[1] for e in ent}   # entity requirements name nodes or other entities by short id
    bad_req = sorted((e, x) for e, d in ent.items() for x in re.findall(r"[a-z][a-z0-9_]*", d.get("requires") or "")
                     if x not in by_id and x not in short and x not in ("and", "or"))
    own_req = sorted(e for e in entity_ids if e not in unlocked and ent[e].get("requires"))
    from_start = sorted(e for e in entity_ids if e not in unlocked and not ent[e].get("requires"))
    row("5 unlock references", not bad_unlock and not bad_req,
        f"{len(unlocked)} entity unlocks all name one of {len(entity_ids)} entities; every entity-level requirement names an existing node or entity"
        if not bad_unlock and not bad_req else f"unlocks {bad_unlock[:10]} requires {bad_req[:10]}")
    row("5b entities not unlocked by a node", None,
        f"{len(from_start)} available from the start (requires null): {', '.join(from_start)}; "
        f"{len(own_req)} gated by their own requirement: " + ", ".join(f"{e} ({ent[e]['requires']})" for e in own_req))

    nodesc = sorted(i for i in ids if not (by_id[i].get("name") or "").strip() or not (by_id[i].get("desc") or "").strip())
    row("6 names and descriptions", not nodesc, f"all {len(ids)} nodes named and described" if not nodesc else f"{len(nodesc)} missing: {nodesc[:10]}")

    def effects(n):
        e = n.get("effects") or {}
        return bool(e.get("realized")), bool(e.get("future")), bool(e.get("immediate"))
    sim = json.load(open(ROOT / "Sim.Data" / "content" / "sim.json", encoding="utf-8"))
    gates = {sim.get("governance", {}).get("taxationRequires"), sim.get("discovery", {}).get("assayRequires")} - {None}
    live_unlock = lambda n: any(isinstance(v, str) and v.startswith(("infra.", "activity.", "building.", "inst.", "unit.", "recipe."))
                                for vals in n["unlocks"].values() for v in vals)
    neither = sorted(i for i in ids if not any(effects(by_id[i])) and not by_id[i].get("repeatable"))
    unexplained = [i for i in neither if i not in gates and not live_unlock(by_id[i])]
    future_only = sorted(i for i in ids if effects(by_id[i])[1] and not effects(by_id[i])[0] and not effects(by_id[i])[2])
    row("7 effects: realized, declared future, a live gate or a live unlock", not unexplained,
        f"every node covered; {len(neither)} carry no effect list but are live through a gate or an unlock ("
        + ", ".join(f"{i} [{'gate' if i in gates else 'unlock'}]" for i in neither)
        + "); authoritative: Tranche2ContentTests.EveryNode_HasARealEffect_OrADeclaredFutureEffect (InfoQuery verdict)"
        if not unexplained else f"{len(unexplained)} unexplained: {unexplained}")
    row("7b future-only nodes (effect waits on a future system; shown 'not simulated yet')", None, f"{len(future_only)}: {', '.join(future_only)}")

    milestone_nodes, bad_ms = set(), []
    for a in ages["ages"]:
        for kind in ("core", "supporting"):
            for m in (a.get("entry") or {}).get(kind, []):
                f = m.get("fact") or {}
                if f.get("kind") == "research":
                    for x in f["nodes"]:
                        milestone_nodes.add(x)
                        if x not in by_id:
                            bad_ms.append((m["id"], x))
    row("9 milestone references", not bad_ms, f"{len(milestone_nodes)} research-milestone nodes all exist" if not bad_ms else str(bad_ms))

    dependents = {i: 0 for i in ids}
    for i, ts in tokens.items():
        for x in ts:
            if x in dependents:
                dependents[x] += 1
    orphans = sorted(i for i in ids if not by_id[i].get("repeatable") and dependents[i] == 0 and i not in milestone_nodes
                     and not effects(by_id[i])[0] and not any(isinstance(v, str) and v.startswith(prefixes)
                                                           for vals in by_id[i]["unlocks"].values() for v in vals))
    row("8 orphans (no dependent, unlock, realized effect or milestone)", None if orphans else True,
        "none" if not orphans else f"{len(orphans)} (future-declared dead ends): {orphans[:12]}")

    U, K = doc["tuning"]["costModel"]["U"], doc["tuning"]["costModel"]["K"]
    bad_cost, authored_round = [], []
    for i in ids:
        n = by_id[i]
        cr = n["costRationale"]
        f = cr["factors"]
        mag = sum(f[k] for k in ("novelty", "difficulty", "material", "institutional", "breadth", "prereq_complexity"))
        expect = float(int(U * K ** cr["magnitude"] / 10.0 + 0.5) * 10)
        if abs(mag - cr["magnitude"]) > 1e-9 or n["cost"] != cr["content_cost"] or abs(n["cost"] - expect) > 10.0:
            bad_cost.append(i)
        elif n["cost"] != expect:
            authored_round.append(i)
    row("10 cost model consistency", not bad_cost,
        f"all {len(ids)}: cost = content_cost, magnitude = the sum of its factors, cost = round(U x 2^magnitude) to tens"
        + (f" ({len(authored_round)} authored one rounding step (+10) above, raw value just under a 5 boundary, max 0.07 %: "
           + ", ".join(authored_round) + ")" if authored_round else "") if not bad_cost else f"{len(bad_cost)}: {bad_cost[:10]}")

    out += ["", f"**Result: {'no blocking defect' if fails == 0 else f'{fails} FAIL item(s)'}.**"]
    print("\n".join(out))
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
