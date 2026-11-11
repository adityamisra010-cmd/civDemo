#!/usr/bin/env python3
"""Independent re-measurement of the research-tree audit's content claims (D-084 lane RT).

Reads Sim.Data/content/research.json only (no build). Prints every number the audit's §2, §2.2, §2.3, §4.5 and §7
reports, measured from the file, so each claim can be marked REPRODUCED / REFUTED / PARTLY; and, last, the OR-group
companion rule that research-date-review-2026-11.md proposes (proposal T).

The expression grammar is the research dialect's: orExpr := andExpr (OR andExpr)*; andExpr := unary (AND unary)*;
unary := '(' orExpr ')' | atom (the shipped corpus uses no NOT and no comparison; the script refuses one).
MustHoldAtoms mirrors Predicate.Must: an atom is itself, AND unions its sides, OR intersects them.
The date heuristic mirrors ResearchContentTests.EarliestMarkedYear (the D-047 test's regex).

Usage: python3 -I measure_audit.py [path/to/research.json]
"""
import itertools
import json
import re
import sys
from collections import Counter, defaultdict

PATH = sys.argv[1] if len(sys.argv) > 1 else "Sim.Data/content/research.json"  # run from the repository root
doc = json.load(open(PATH, encoding="utf-8"))
nodes = [dict(n, tree="technology") for n in doc["technologies"]] + [dict(n, tree="civics") for n in doc["civics"]]
by_id = {n["id"]: n for n in nodes}
index = {n["id"]: i for i, n in enumerate(nodes)}


# ---------------------------------------------------------------- the expression
def tokenize(s):
    toks = re.findall(r"\(|\)|[A-Za-z_][A-Za-z0-9_]*|\S", s)
    for t in toks:
        if t in ("NOT", "!") or re.match(r"[<>=!]", t):
            raise SystemExit("unsupported token " + t + " in " + s)
    return toks


def parse(s):
    toks = tokenize(s)
    pos = [0]

    def peek():
        return toks[pos[0]] if pos[0] < len(toks) else None

    def take():
        t = toks[pos[0]]
        pos[0] += 1
        return t

    def p_or():
        left = p_and()
        while peek() == "OR":
            take()
            left = ("or", left, p_and())
        return left

    def p_and():
        left = p_unary()
        while peek() == "AND":
            take()
            left = ("and", left, p_unary())
        return left

    def p_unary():
        t = take()
        if t == "(":
            e = p_or()
            assert take() == ")"
            return e
        return ("atom", t)

    e = p_or()
    assert pos[0] == len(toks), s
    return e


def atoms(e, out=None):
    out = [] if out is None else out
    if e[0] == "atom":
        if e[1] not in out:
            out.append(e[1])
    else:
        atoms(e[1], out)
        atoms(e[2], out)
    return out


def must(e):
    if e[0] == "atom":
        return {e[1]}
    if e[0] == "and":
        return must(e[1]) | must(e[2])
    return must(e[1]) & must(e[2])


def ev(e, true):
    if e[0] == "atom":
        return e[1] in true
    if e[0] == "and":
        return ev(e[1], true) and ev(e[2], true)
    return ev(e[1], true) or ev(e[2], true)


def flat_ev(must_set, others, true):
    """The flat reading the graph and the pane give: every 'required' atom AND (no 'one of' atom, or at least one)."""
    return all(a in true for a in must_set) and (not others or any(a in true for a in others))


exprs = {}
for n in nodes:
    if n.get("prereq"):
        exprs[n["id"]] = parse(n["prereq"])

# ---------------------------------------------------------------- §2 structure
tech = sum(1 for n in nodes if n["tree"] == "technology")
civ = len(nodes) - tech
edges = []  # (prereq id, dependent id, kind)
missing = []
for n in nodes:
    e = exprs.get(n["id"])
    if e is None:
        continue
    m = must(e)
    for a in atoms(e):
        if a not in by_id:
            missing.append((n["id"], a))
            continue
        edges.append((a, n["id"], "and" if a in m else "or"))
in_tree = [x for x in edges if by_id[x[0]]["tree"] == by_id[x[1]]["tree"]]
cross = [x for x in edges if by_id[x[0]]["tree"] != by_id[x[1]]["tree"]]
t2c = [x for x in cross if by_id[x[0]]["tree"] == "technology"]
c2t = [x for x in cross if by_id[x[0]]["tree"] == "civics"]
roots = [n["id"] for n in nodes if not n.get("prereq")]
or_nodes = sorted({x[1] for x in edges if x[2] == "or"})
or_text_nodes = [n["id"] for n in nodes if n.get("prereq") and " OR " in n["prereq"]]

# cycles (iterative DFS, sorted ids for a fixed order)
deps = defaultdict(list)
for a, b, _ in edges:
    deps[b].append(a)
color = {}
cycles = 0


def dfs(start):
    global cycles
    stack = [(start, iter(sorted(deps[start])))]
    color[start] = 1
    while stack:
        v, it = stack[-1]
        nxt = next(it, None)
        if nxt is None:
            color[v] = 2
            stack.pop()
            continue
        c = color.get(nxt, 0)
        if c == 1:
            cycles += 1
        elif c == 0:
            color[nxt] = 1
            stack.append((nxt, iter(sorted(deps[nxt]))))


for n in sorted(by_id):
    if color.get(n, 0) == 0:
        dfs(n)

# longest-path depth over ALL prerequisite edges (roots 0), and the in-tree-only variant
memo_all, memo_in = {}, {}


def lp(i, memo, in_only):
    if i in memo:
        return memo[i]
    best = 0
    for a in deps[i]:
        if in_only and by_id[a]["tree"] != by_id[i]["tree"]:
            continue
        best = max(best, lp(a, memo, in_only) + 1)
    memo[i] = best
    return best


sys.setrecursionlimit(10000)
mismatch_all = [n["id"] for n in nodes if lp(n["id"], memo_all, False) != n["depth"]]
mismatch_in = [n["id"] for n in nodes if lp(n["id"], memo_in, True) != n["depth"]]
tier_inv = [(a, b) for a, b, _ in in_tree if by_id[a]["depth"] >= by_id[b]["depth"]]
age_num = lambda n: int(n["age"][1:])
age_inv = [(a, b) for a, b, _ in edges if age_num(by_id[a]) > age_num(by_id[b])]

print("== §2 structure ==")
print("technology nodes", tech, "| civics nodes", civ, "| total", len(nodes))
print("direct prerequisite edges", len(edges), "| in-tree", len(in_tree), "| cross-tree", len(cross))
print("AND/must edges", sum(1 for x in edges if x[2] == "and"), "| OR/alternative edges", sum(1 for x in edges if x[2] == "or"))
print("nodes with an OR-classified edge", len(or_nodes), "| nodes whose text contains OR", len(or_text_nodes))
print("roots", len(roots), roots)
print("missing references", len(missing), missing[:5])
print("back edges found by DFS (cycles)", cycles)
print("stored-depth mismatches (longest path over all edges)", len(mismatch_all), mismatch_all[:8])
print("stored-depth mismatches (longest path over in-tree edges)", len(mismatch_in), mismatch_in[:8])
print("in-tree tier inversions (stored depth)", len(tier_inv), "| Age inversions", len(age_inv), age_inv[:5])
print("technology -> civics", len(t2c), "| civics -> technology", len(c2t), c2t)

# ---------------------------------------------------------------- §2.2 depth gaps
gaps = sorted(((by_id[b]["depth"] - by_id[a]["depth"], by_id[a]["name"], by_id[b]["name"], a, b) for a, b, _ in in_tree),
              key=lambda g: (-g[0], g[3], g[4]))
print("\n== §2.2 in-tree stored-depth gaps ==")
for k in (5, 8, 10, 12):
    print("gap >", k, ":", sum(1 for g in gaps if g[0] > k))
for g in gaps:
    if g[0] > 12:
        print("  %2d  %s -> %s   (%s -> %s)" % g)

# ---------------------------------------------------------------- §2.3 paper / longbow
print("\n== §2.3 ==")
for nid in ("paper", "longbow"):
    n = by_id[nid]
    print(nid, n["age"], "depth", n["depth"], "expr", n["prereq"], "->",
          [(a, by_id[a]["depth"], by_id[a]["name"]) for a in atoms(exprs[nid])])
print("nodes naming longbow as a prerequisite:", [d for (a, d, _) in edges if a == "longbow"])

# ---------------------------------------------------------------- §4.5 mixed expressions
mixed = [nid for nid in exprs if " AND " in by_id[nid]["prereq"] and " OR " in by_id[nid]["prereq"]]
changed = []
for nid, e in exprs.items():
    at = atoms(e)
    m = must(e)
    others = [a for a in at if a not in m]
    differs = False
    for bits in itertools.product((False, True), repeat=len(at)):
        true = {a for a, b in zip(at, bits) if b}
        if ev(e, true) != flat_ev(m, others, true):
            differs = True
            break
    if differs:
        changed.append(nid)
print("\n== §4.5 ==")
print("expressions with both AND and OR", len(mixed))
print("expressions whose flat reading changes the truth table", len(changed), changed)
print("  of which mixed", sum(1 for c in changed if c in mixed))
for c in changed:
    print("  ", c, "|", by_id[c]["name"], "|", by_id[c]["prereq"])
print("largest atom count in any expression", max(len(atoms(e)) for e in exprs.values()))

# ---------------------------------------------------------------- §7 date heuristic
RX = re.compile(r"(\d[\d,]*(?:\.\d+)?)(?:\s*[-–/]\s*(\d[\d,]*(?:\.\d+)?))?\s*(BCE|CE|kya|Mya)\b")


def earliest(s):
    if s is None:
        return None
    best = None
    for m in RX.finditer(s):
        for g in (1, 2):
            if m.group(g) is None:
                continue
            v = float(m.group(g).replace(",", ""))
            u = m.group(3)
            y = v if u == "CE" else -v if u == "BCE" else -v * 1000.0 if u == "kya" else -v * 1e6
            if best is None or y < best:
                best = y
    return best


cands = []
for a, b, kind in edges:
    ya, yb = earliest(by_id[a].get("emerged")), earliest(by_id[b].get("emerged"))
    if ya is None or yb is None:
        continue
    if ya > yb:
        cands.append((b, a, kind, yb, ya))


def group_of(e, atom, parent=None):
    """The innermost OR group holding atom (as a list of its atoms), or None."""
    if e[0] == "atom":
        return parent if e[1] == atom else None
    if e[0] == "or":
        # an OR chain is one group: collect its direct alternatives (atoms and sub-expressions)
        alts, stack = [], [e]
        while stack:
            x = stack.pop()
            if x[0] == "or":
                stack.extend([x[2], x[1]])
            else:
                alts.append(x)
        for alt in alts:
            if alt[0] == "atom" and alt[1] == atom:
                return [atoms(a) for a in alts]
            g = group_of(alt, atom, None)
            if g is not None:
                return g
        return None
    for side in (e[1], e[2]):
        g = group_of(side, atom, None)
        if g is not None:
            return g
    return None


def valid_alternative(dep, atom):
    """For an OR candidate: an alternative of the same group whose every atom is dated no later than the dependent
    (or undated), named, else None."""
    yd = earliest(by_id[dep].get("emerged"))
    g = group_of(exprs[dep], atom)
    if g is None:
        return None
    for alt in g:
        if atom in alt:
            continue
        ys = [earliest(by_id[x].get("emerged")) for x in alt]
        if all(y is None or y <= yd for y in ys):
            return "+".join(alt) + " (" + ", ".join("undated" if y is None else "%.0f" % y for y in ys) + ")"
    return None
print("\n== §7 ==")
print("candidates", len(cands), "| must", sum(1 for c in cands if c[2] == "and"), "| alternative",
      sum(1 for c in cands if c[2] == "or"))
print("Age inversions among them", sum(1 for c in cands if age_num(by_id[c[1]]) > age_num(by_id[c[0]])))
print("dependents dated after 3000 BCE among the must candidates",
      [c[:2] for c in cands if c[2] == "and" and c[3] > -3000])
for c in sorted(cands, key=lambda c: (index[c[0]], index[c[1]])):
    alt = valid_alternative(c[0], c[1]) if c[2] == "or" else None
    print("  %-26s <- %-24s %-4s dep %9.0f  pre %9.0f" % c + ("   valid alternative: " + alt if alt else ""))


# ---------------------------------------------------------------- §7.3 a companion rule for OR groups (proposal T)
def or_groups(e, out):
    """Every OR group of the expression (an OR chain is one group), as its list of direct alternatives."""
    if e[0] == "atom":
        return
    if e[0] == "or":
        alts, stack = [], [e]
        while stack:
            x = stack.pop()
            if x[0] == "or":
                stack.extend([x[2], x[1]])
            else:
                alts.append(x)
        out.append(alts)
        for a in alts:
            or_groups(a, out)
        return
    or_groups(e[1], out)
    or_groups(e[2], out)


print("\n== §7.3 OR-group rule (dependents dated after 3000 BCE: one alternative dated no later) ==")
for undated_ok in (True, False):
    viol, checked = [], 0
    for nid, e in sorted(exprs.items()):
        yd = earliest(by_id[nid].get("emerged"))
        if yd is None or yd <= -3000:
            continue
        groups = []
        or_groups(e, groups)
        for alts in groups:
            checked += 1
            if not any(all((y is None and undated_ok) or (y is not None and y <= yd)
                           for y in (earliest(by_id[x].get("emerged")) for x in atoms(alt))) for alt in alts):
                viol.append(nid)
    print("undated alternative counts as", "satisfied" if undated_ok else "unsatisfied", "| OR groups", checked,
          "| violations", len(viol), viol)
