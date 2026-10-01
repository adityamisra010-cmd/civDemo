"""Shared reading of Sim.Data/content/research.json for the research scripts.

The content is AUTHORED (ADR-029 addendum A): no script generates it. These helpers only
read it — the expression grammar, knowledge reachability and the finite / recursive sets —
so that the content audit and the calibration report derive every number from the one file
the engine loads. The C# loader (Sim.Core/Systems/Research/ResearchContent.cs) is the
authority on validity; this is an independent re-reading for the reports, not a validator.
"""
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONTENT = ROOT / "Sim.Data" / "content" / "research.json"
AGES = ["A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9"]

# The D-020 research dialect: node ids as atoms, AND / OR / NOT, parentheses, and
# comparisons "name op number" (quantities and variables, which no script can evaluate).
TOKEN = re.compile(r"\s*(\(|\)|>=|<=|==|!=|>|<|[A-Za-z_][A-Za-z0-9_.]*|-?\d+(?:\.\d+)?)")
OPS = (">=", "<=", "==", "!=", ">", "<")


def fail(msg):
    sys.exit(f"research_lib: {msg}")


def load():
    raw = CONTENT.read_bytes()
    return json.loads(raw), hashlib.sha256(raw).hexdigest()


def nodes_of(content):
    return content["technologies"] + content["civics"]


def tokens(expr):
    toks, pos = [], 0
    while pos < len(expr):
        m = TOKEN.match(expr, pos)
        if not m or m.end() == pos:
            if expr[pos:].strip() == "":
                break
            fail(f"expression {expr!r}: unexpected text at {pos}")
        toks.append(m.group(1))
        pos = m.end()
    return toks


def parse(expr):
    """('atom', id) | ('cmp', text) | ('not', x) | ('and'|'or', [children])."""
    toks = tokens(expr)
    pos = 0

    def parse_or():
        nonlocal pos
        items = [parse_and()]
        while pos < len(toks) and toks[pos] == "OR":
            pos += 1
            items.append(parse_and())
        return items[0] if len(items) == 1 else ("or", items)

    def parse_and():
        nonlocal pos
        items = [parse_unary()]
        while pos < len(toks) and toks[pos] == "AND":
            pos += 1
            items.append(parse_unary())
        return items[0] if len(items) == 1 else ("and", items)

    def parse_unary():
        nonlocal pos
        if pos >= len(toks):
            fail(f"expression {expr!r} ends early")
        t = toks[pos]
        if t == "NOT":
            pos += 1
            return ("not", parse_unary())
        if t == "(":
            pos += 1
            inner = parse_or()
            if pos >= len(toks) or toks[pos] != ")":
                fail(f"expression {expr!r}: unclosed parenthesis")
            pos += 1
            return inner
        if t in ("AND", "OR", ")") or t in OPS:
            fail(f"expression {expr!r}: unexpected {t}")
        pos += 1
        if pos < len(toks) and toks[pos] in OPS:
            op, rhs = toks[pos], toks[pos + 1]
            pos += 2
            return ("cmp", f"{t} {op} {rhs}")
        return ("atom", t)

    node = parse_or()
    if pos != len(toks):
        fail(f"expression {expr!r}: trailing tokens")
    return node


def atoms(node):
    if node[0] == "atom":
        return {node[1]}
    if node[0] == "cmp":
        return set()
    if node[0] == "not":
        return atoms(node[1])
    return set().union(*(atoms(c) for c in node[1]))


def holds(node, complete, cmp_value=False):
    """Evaluate with exactly `complete` complete; every comparison reads `cmp_value`."""
    kind = node[0]
    if kind == "atom":
        return node[1] in complete
    if kind == "cmp":
        return cmp_value
    if kind == "not":
        return not holds(node[1], complete, cmp_value)
    vals = [holds(c, complete, cmp_value) for c in node[1]]
    return all(vals) if kind == "and" else any(vals)


def must_direct(node):
    """Atoms true in EVERY satisfying assignment (AND unions, OR intersects; a comparison
    or a NOT contributes nothing) — the same rule as Predicate.MustHoldAtoms."""
    kind = node[0]
    if kind == "atom":
        return {node[1]}
    if kind in ("cmp", "not"):
        return set()
    parts = [must_direct(c) for c in node[1]]
    return set().union(*parts) if kind == "and" else set.intersection(*parts)


class Graph:
    def __init__(self, content):
        self.content = content
        self.nodes = nodes_of(content)
        self.by_id = {n["id"]: n for n in self.nodes}
        self.prereq = {n["id"]: (parse(n["prereq"]) if n.get("prereq") else None) for n in self.nodes}
        sets = content.get("researchSets") or {}
        self.recursive = set(sets.get("recursive") or [])
        self.speculative = set(sets.get("speculative_finite") or [])
        self.order = self._topological()
        self.must = {}
        for nid in self.order:
            p = self.prereq[nid]
            direct = must_direct(p) if p is not None else set()
            closed = set(direct)
            for a in direct:
                closed |= self.must[a]
            self.must[nid] = closed

    def _topological(self):
        deps = {nid: (atoms(p) if p is not None else set()) for nid, p in self.prereq.items()}
        order, done = [], set()
        pending = [n["id"] for n in self.nodes]
        while pending:
            progressed = False
            rest = []
            for nid in pending:
                if deps[nid] <= done:
                    order.append(nid)
                    done.add(nid)
                    progressed = True
                else:
                    rest.append(nid)
            if not progressed:
                fail(f"prerequisite cycle among {rest[:5]}")
            pending = rest
        return order

    def finite(self):
        return [n for n in self.nodes if n["id"] not in self.recursive]

    def reachable(self, excluded=()):
        """The nodes that can ever complete when every node in `excluded` never does
        (a fixpoint over the prerequisites; recursive availability is ignored — this asks
        about knowledge paths only)."""
        excluded = set(excluded)
        done = set()
        changed = True
        while changed:
            changed = False
            for nid in self.order:
                if nid in done or nid in excluded:
                    continue
                p = self.prereq[nid]
                if p is None or holds(p, done):
                    done.add(nid)
                    changed = True
        return done


def fmt_int(x):
    return f"{int(round(x)):,}"


def stats(values):
    vs = sorted(values)
    if not vs:
        return dict(n=0, total=0, median=0, mean=0, min=0, max=0)
    n = len(vs)
    median = vs[n // 2] if n % 2 else (vs[n // 2 - 1] + vs[n // 2]) / 2
    return dict(n=n, total=sum(vs), median=median, mean=sum(vs) / n, min=vs[0], max=vs[-1])


def rp_per_turn(tuning, population):
    """ADR-030: Research Points per strategic turn — never multiplied by dtYears."""
    rp = tuning["rpPerTurn"]
    return rp["coefficient"] * population ** rp["exponent"] if population > 0 else 0.0


def write_or_check(path, text, argv, tool):
    if "--check" in argv[1:]:
        current = path.read_text(encoding="utf-8") if path.exists() else None
        if current != text:
            sys.exit(f"{tool}: {path.relative_to(ROOT)} is stale — run python3 scripts/{tool}")
        print(f"{tool}: {path.relative_to(ROOT)} is current")
        return
    path.write_text(text, encoding="utf-8")
    print(f"{tool}: wrote {path.relative_to(ROOT)} ({len(text)} bytes)")
