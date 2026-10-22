"""Shared helpers for the M6 knowledge-redesign design package tools.

DESIGN-PACKAGE TOOLING ONLY. Nothing here is imported by, or reads into, the
simulation runtime (Sim.*). The tools read the production content files
read-only and write only inside docs/m6-plan/knowledge-redesign/.

Predicate dialect handled: the research prerequisite dialect (atoms joined by
AND / OR / NOT with parentheses), which is what research.json prereq and
entity `requires` strings use (ADR-029 section 11; D-020). Comparison atoms
(used only by Eureka `when` conditions) are tokenized but treated as opaque.
"""
import json
import re
from pathlib import Path

PKG = Path(__file__).resolve().parent.parent          # docs/m6-plan/knowledge-redesign
ROOT = PKG.parent.parent.parent                         # repository root
CONTENT = ROOT / "Sim.Data" / "content"
DATA = PKG / "data"

AGES = ["A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9"]
BRANCHES = ["trunk", "military", "medicine", "engineering", "natural_science", "agriculture"]

_TOKEN = re.compile(r"\s*(\(|\)|>=|<=|==|!=|>|<|[A-Za-z_][A-Za-z0-9_.]*|-?\d+(?:\.\d+)?)")
_KEYWORDS = {"AND", "OR", "NOT"}


def load_json(path):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def dump_json(obj, path):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(obj, fh, ensure_ascii=False, indent=1, sort_keys=False)
        fh.write("\n")


def tokens(expr):
    out, pos = [], 0
    expr = expr or ""
    while pos < len(expr):
        if expr[pos:].strip() == "":
            break
        m = _TOKEN.match(expr, pos)
        if not m:
            raise ValueError(f"cannot tokenize {expr!r} at {pos}")
        out.append(m.group(1))
        pos = m.end()
    return out


def parse(expr):
    """Parse a prerequisite expression into a tuple AST.

    ('atom', id) | ('and', [..]) | ('or', [..]) | ('not', x) | ('cmp', text) | None
    Precedence: NOT > AND > OR (the D-020 dialect)."""
    toks = tokens(expr)
    if not toks:
        return None
    pos = 0

    def peek():
        return toks[pos] if pos < len(toks) else None

    def take():
        nonlocal pos
        t = toks[pos]
        pos += 1
        return t

    def p_or():
        items = [p_and()]
        while peek() == "OR":
            take()
            items.append(p_and())
        return items[0] if len(items) == 1 else ("or", items)

    def p_and():
        items = [p_not()]
        while peek() == "AND":
            take()
            items.append(p_not())
        return items[0] if len(items) == 1 else ("and", items)

    def p_not():
        if peek() == "NOT":
            take()
            return ("not", p_not())
        return p_primary()

    def p_primary():
        t = take()
        if t == "(":
            e = p_or()
            if take() != ")":
                raise ValueError(f"missing ) in {expr!r}")
            return e
        if t in _KEYWORDS or t == ")":
            raise ValueError(f"unexpected {t!r} in {expr!r}")
        if peek() in (">=", "<=", "==", "!=", ">", "<"):
            op = take()
            rhs = take()
            return ("cmp", f"{t} {op} {rhs}")
        return ("atom", t)

    ast = p_or()
    if pos != len(toks):
        raise ValueError(f"trailing tokens in {expr!r}")
    return ast


def atoms(ast):
    if ast is None:
        return []
    kind = ast[0]
    if kind == "atom":
        return [ast[1]]
    if kind in ("and", "or"):
        out = []
        for x in ast[1]:
            for a in atoms(x):
                if a not in out:
                    out.append(a)
        return out
    if kind == "not":
        return atoms(ast[1])
    return []


def holds(ast, complete):
    """Evaluate against a set of completed ids (NOT and cmp are conservative)."""
    if ast is None:
        return True
    kind = ast[0]
    if kind == "atom":
        return ast[1] in complete
    if kind == "and":
        return all(holds(x, complete) for x in ast[1])
    if kind == "or":
        return any(holds(x, complete) for x in ast[1])
    if kind == "not":
        return not holds(ast[1], complete)
    return False


def word_count(text):
    return len(re.findall(r"[A-Za-z0-9À-ɏ]+(?:[-'’][A-Za-z0-9À-ɏ]+)*", text or ""))


def base_cost(magnitude, u=92.4, k=2.0):
    return u * (k ** magnitude)


class Graph:
    """A prerequisite graph over node records with fields id, cost, prereq."""

    def __init__(self, nodes):
        self.nodes = {n["id"]: n for n in nodes}
        self.order = [n["id"] for n in nodes]
        self.ast = {n["id"]: parse(n.get("prereq")) for n in nodes}
        self.deps = {i: [] for i in self.order}
        for i in self.order:
            for a in atoms(self.ast[i]):
                if a in self.deps:
                    self.deps[a].append(i)
        self._closure = {}
        self._depth = {}

    def missing_refs(self):
        out = []
        for i in self.order:
            for a in atoms(self.ast[i]):
                if a not in self.nodes:
                    out.append((i, a))
        return out

    def find_cycle(self):
        colour = {i: 0 for i in self.order}
        stack = []

        def visit(i):
            colour[i] = 1
            stack.append(i)
            for a in atoms(self.ast[i]):
                if a not in colour:
                    continue
                if colour[a] == 1:
                    return stack[stack.index(a):] + [a]
                if colour[a] == 0:
                    c = visit(a)
                    if c:
                        return c
            colour[i] = 2
            stack.pop()
            return None

        import sys
        sys.setrecursionlimit(10000)
        for i in self.order:
            if colour[i] == 0:
                c = visit(i)
                if c:
                    return c
        return None

    def cost(self, i):
        return float(self.nodes[i].get("cost") or 0.0)

    def _expr_closure(self, ast):
        """Cheapest satisfying ancestor set for an expression (OR: choose the
        alternative whose closure costs least; ties by sorted ids). Greedy over
        shared ancestors, so it is an upper bound on the true minimum."""
        if ast is None:
            return frozenset()
        kind = ast[0]
        if kind == "atom":
            return self.closure(ast[1]) if ast[1] in self.nodes else frozenset()
        if kind == "and":
            s = frozenset()
            for x in ast[1]:
                s = s | self._expr_closure(x)
            return s
        if kind == "or":
            best = None
            for x in ast[1]:
                c = self._expr_closure(x)
                key = (sum(self.cost(j) for j in c), len(c), sorted(c))
                if best is None or key < best[0]:
                    best = (key, c)
            return best[1]
        return frozenset()

    def closure(self, i):
        """Node plus its cheapest satisfying prerequisite closure."""
        if i not in self._closure:
            self._closure[i] = frozenset([i]) | self._expr_closure(self.ast[i])
        return self._closure[i]

    def closure_cost(self, i):
        return sum(self.cost(j) for j in self.closure(i))

    def all_ancestors(self, i):
        seen, todo = set(), list(atoms(self.ast[i]))
        while todo:
            a = todo.pop()
            if a in seen or a not in self.nodes:
                continue
            seen.add(a)
            todo.extend(atoms(self.ast[a]))
        return seen

    def _expr_depth(self, ast):
        if ast is None:
            return 0
        kind = ast[0]
        if kind == "atom":
            return self.depth(ast[1]) if ast[1] in self.nodes else 0
        if kind == "and":
            return max(self._expr_depth(x) for x in ast[1])
        if kind == "or":
            return min(self._expr_depth(x) for x in ast[1])
        return 0

    def depth_longest(self, i):
        """The loader's `depth`: roots 0, else 1 + the longest prerequisite path over every
        atom (OR alternatives included) - MEASURED to match all 433 current nodes."""
        if not hasattr(self, "_dl"):
            self._dl = {}
        if i not in self._dl:
            a = [x for x in atoms(self.ast[i]) if x in self.nodes]
            self._dl[i] = 0 if not a else 1 + max(self.depth_longest(x) for x in a)
        return self._dl[i]

    def depth(self, i):
        """Shortest prerequisite chain length ending at i (roots = 1)."""
        if i not in self._depth:
            self._depth[i] = 1 + self._expr_depth(self.ast[i])
        return self._depth[i]

    def descendants(self, i):
        seen, todo = set(), list(self.deps.get(i, []))
        while todo:
            d = todo.pop()
            if d in seen:
                continue
            seen.add(d)
            todo.extend(self.deps.get(d, []))
        return seen
