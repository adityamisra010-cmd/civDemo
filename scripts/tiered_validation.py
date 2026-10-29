#!/usr/bin/env python3
"""Change-aware tiered validation for civDemo (docs/validation-policy.md).

Usage (from the repository root, or through scripts/validate.sh):
    python3 scripts/tiered_validation.py --tier targeted            # checks for the working-tree changes vs HEAD
    python3 scripts/tiered_validation.py --tier integration --base main
    python3 scripts/tiered_validation.py --tier targeted --paths Sim.Core/Systems/Research/ResearchSystem.cs
    python3 scripts/tiered_validation.py --tier integration --scope research-engine
    python3 scripts/tiered_validation.py --tier full                # milestone / pre-merge: everything runnable here
    add --plan to print the selection and the reasons without running anything.

Selection is data: scripts/validation-map.json maps changed paths to categories and categories to checks per tier.
C# changes also pull in the test classes whose source mentions the changed file's type name (reference inference);
a type referenced by too many classes escalates to the project's full suite at integration tier.

Statuses: PASS, PASS (cached), FAIL, MISSING (a required command or file is absent: counts as a failure), and
NOT RUN (with the reason: CI-only, manual, heavy not requested, blocked by an earlier failure). A NOT RUN check is
never a pass. Exit code: 0 when every check that ran passed; 1 on any FAIL or MISSING; 2 on a usage error;
3 with --strict when anything required was NOT RUN.
"""
from __future__ import annotations

import argparse
import concurrent.futures
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys
import time
from dataclasses import dataclass, field

TIERS = ("targeted", "integration", "full")
TEST_PROJECTS = {"sim": "Sim.Tests", "ui": "Sim.Ui.Tests"}
FULL_LOCAL_CHECKS = [
    "banned-constructs", "read-isolation", "readonly-proof", "selector-selftest", "research-tool-tests",
    "research-audit", "research-calibration-report", "unlock-audit", "consequence-audit", "research-audit-tests",
    "tree-closure", "charters", "doc-links-all", "build", "simtests-full", "uitests-full",
]
FULL_ELSEWHERE_CHECKS = ["ci-xproc-determinism", "ci-platform-smoke", "calibration-nightly", "multi-seed-trial", "bench",
                         "gui-acceptance"]
CHEAP_FIRST = ["selector-selftest", "banned-constructs", "read-isolation", "research-tool-tests", "research-audit",
               "research-calibration-report", "unlock-audit", "consequence-audit", "research-audit-tests", "tree-closure",
               "charters", "doc-links", "doc-links-all", "readonly-proof", "build"]


def repo_root() -> str:
    here = os.path.dirname(os.path.abspath(__file__))
    return os.path.dirname(here)


def glob_to_regex(pattern: str) -> re.Pattern:
    out, i = [], 0
    while i < len(pattern):
        if pattern.startswith("**/", i):
            out.append("(?:.*/)?"); i += 3
        elif pattern.startswith("**", i):
            out.append(".*"); i += 2
        elif pattern[i] == "*":
            out.append("[^/]*"); i += 1
        elif pattern[i] == "?":
            out.append("[^/]"); i += 1
        else:
            out.append(re.escape(pattern[i])); i += 1
    return re.compile("^" + "".join(out) + "$")


def path_matches(path: str, pattern: str) -> bool:
    return glob_to_regex(pattern).match(path) is not None


# ------------------------------------------------------------------ test index

@dataclass
class TestClass:
    project: str      # "sim" | "ui"
    fqn: str          # Namespace.Class
    file: str         # repo-relative path
    traits: tuple


class TestIndex:
    """The test classes of Sim.Tests and Sim.Ui.Tests, read from source (no build needed)."""

    def __init__(self, root: str):
        self.classes: list[TestClass] = []
        self._text: dict[str, str] = {}
        for key, proj in TEST_PROJECTS.items():
            base = os.path.join(root, proj)
            if not os.path.isdir(base):
                continue
            for dirpath, dirnames, filenames in os.walk(base):
                dirnames[:] = sorted(d for d in dirnames if d not in ("bin", "obj", "Fixtures"))
                for name in sorted(filenames):
                    if not name.endswith(".cs"):
                        continue
                    full = os.path.join(dirpath, name)
                    rel = os.path.relpath(full, root).replace(os.sep, "/")
                    text = open(full, encoding="utf-8", errors="replace").read()
                    self._text[rel] = text
                    ns = re.search(r"^\s*namespace\s+([\w.]+)", text, re.M)
                    namespace = ns.group(1) if ns else proj
                    traits = tuple(sorted(set(f"{k}={v}" for k, v in re.findall(r'Trait\("([^"]+)",\s*"([^"]+)"\)', text))))
                    for cls in re.findall(r"^\s*(?:public\s+|internal\s+)?(?:sealed\s+|static\s+|abstract\s+|partial\s+)*class\s+(\w+)", text, re.M):
                        self.classes.append(TestClass(key, f"{namespace}.{cls}", rel, traits))

    def match(self, project: str, term: str) -> list[TestClass]:
        """Classes a filter term selects (mirrors dotnet's case-insensitive FullyQualifiedName~ contains)."""
        pool = [c for c in self.classes if c.project == project]
        if term.startswith("trait:"):
            return [c for c in pool if term[6:] in c.traits]
        if term.startswith("cls:"):
            return [c for c in pool if c.fqn == term[4:]]
        needle = term.lower()
        return [c for c in pool if needle in (c.fqn + ".").lower()]

    def classes_in_file(self, rel: str) -> list[TestClass]:
        return [c for c in self.classes if c.file == rel]

    def referencing(self, word: str) -> list[TestClass]:
        """Test classes whose source file mentions `word` as a whole word (reference inference)."""
        rx = re.compile(r"(?<![\w])" + re.escape(word) + r"(?![\w])")
        files = sorted(f for f, t in self._text.items() if rx.search(t))
        return [c for c in self.classes if c.file in files]

    def files(self) -> list[str]:
        return sorted(self._text)


# ------------------------------------------------------------------ the plan

@dataclass
class Plan:
    tier: str
    checks: dict = field(default_factory=dict)          # check id -> [reasons]
    terms: dict = field(default_factory=lambda: {"sim": {}, "ui": {}})  # project -> term -> [reasons]
    full: dict = field(default_factory=dict)            # project -> [reasons]
    categories: dict = field(default_factory=dict)      # path -> category id
    notes: list = field(default_factory=list)
    changed_md: list = field(default_factory=list)
    exclude: dict = field(default_factory=lambda: {"sim": [], "ui": []})  # project -> slow test FQNs left out


    def add_check(self, cid: str, reason: str):
        self.checks.setdefault(cid, [])
        if reason not in self.checks[cid]:
            self.checks[cid].append(reason)

    def add_term(self, project: str, term: str, reason: str):
        self.terms[project].setdefault(term, [])
        if reason not in self.terms[project][term]:
            self.terms[project][term].append(reason)

    def add_full(self, project: str, reason: str):
        self.full.setdefault(project, [])
        if reason not in self.full[project]:
            self.full[project].append(reason)


DURATIONS = os.path.join("scripts", "test-durations.json")


def trx_durations(path: str) -> dict:
    """Per-test seconds from a .trx file, keyed by method FQN without Theory arguments (the longest case wins)."""
    with open(path, encoding="utf-8", errors="replace") as fh:
        text = fh.read()
    out = {}
    for name, dur in re.findall(r'testName="([^"]+)"[^>]*duration="([0-9:.]+)"', text):
        h, m, s = dur.split(":")
        secs = int(h) * 3600 + int(m) * 60 + float(s)
        key = re.sub(r"\(.*$", "", name.replace("&quot;", '"'))
        out[key] = max(out.get(key, 0.0), secs)
    return out


def load_durations(root: str) -> dict:
    p = os.path.join(root, DURATIONS)
    if not os.path.exists(p):
        return {"slowThresholdSeconds": 15, "slow": {}}
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def record_durations(root: str, trx_files: list, note: str) -> dict:
    data = load_durations(root)
    threshold = data.get("slowThresholdSeconds", 15)
    slow = dict(data.get("slow", {}))
    for trx in trx_files:
        for name, secs in trx_durations(trx).items():
            if secs >= threshold:
                slow[name] = round(secs, 1)
            else:
                slow.pop(name, None)  # re-measured fast: no longer excluded
    data.update({
        "_doc": "Tests measured at or above slowThresholdSeconds (wall seconds per test, from .trx files recorded by "
                "scripts/tiered_validation.py --record-durations). Tier 1 (targeted) excludes them unless their own "
                "file changed; Tiers 2 and 3 and CI run them. Re-record after a full run; a test that becomes fast "
                "is dropped from the list automatically.",
        "recorded": note, "slowThresholdSeconds": threshold, "slow": dict(sorted(slow.items()))})
    with open(os.path.join(root, DURATIONS), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
        f.write("\n")
    return data


def load_map(root: str) -> dict:
    with open(os.path.join(root, "scripts", "validation-map.json"), encoding="utf-8") as f:
        return json.load(f)


def categorize(vmap: dict, path: str) -> dict:
    for cat in vmap["categories"]:
        if cat.get("fallback"):
            continue
        if any(path_matches(path, p) for p in cat["paths"]):
            return cat
    return next(c for c in vmap["categories"] if c.get("fallback"))


def system_token(path: str) -> str | None:
    m = re.match(r"Sim\.Core/Systems/([^/]+)/", path)
    if m:
        return "." + m.group(1)
    m = re.match(r"Sim\.Core/(?:State|Systems)/([^/]+)\.cs$", path)
    if m:
        return "." + re.sub(r"(Query|Config)$", "", m.group(1))
    return None


def is_code_path(path: str) -> bool:
    return path.startswith(("Sim.", "Directory.Build.props", "global.json")) or path.endswith((".cs", ".csproj", ".slnx"))


def select(vmap: dict, index: TestIndex, paths: list[str], tier: str, scope: list[str] | None = None,
           durations: dict | None = None, include_slow: bool = False) -> Plan:
    plan = _select(vmap, index, paths, tier, scope)
    if tier == "targeted" and not include_slow and durations:
        changed_classes = {c.fqn for p in paths for c in index.classes_in_file(p)}
        for name, secs in sorted(durations.get("slow", {}).items()):
            cls = name.rsplit(".", 1)[0]
            if cls in changed_classes:
                continue  # you are editing this test: it runs
            proj = "ui" if name.startswith("Sim.Ui.Tests.") else "sim"
            if plan.terms[proj] and proj not in plan.full and any(
                    cls == c.fqn for t in plan.terms[proj] for c in index.match(proj, t)):
                plan.exclude[proj].append(name)
        n = sum(len(v) for v in plan.exclude.values())
        if n:
            secs = sum(durations["slow"][x] for v in plan.exclude.values() for x in v)
            plan.notes.append(f"{n} slow test(s) left out of Tier 1 (≥{durations.get('slowThresholdSeconds', 15)} s each, "
                              f"≈{secs / 60:.0f} min recorded); they run at --tier integration/full and in CI. "
                              "Use --include-slow to run them now.")
    return plan


def _select(vmap: dict, index: TestIndex, paths: list[str], tier: str, scope: list[str] | None = None) -> Plan:
    plan = Plan(tier)
    checks = vmap["checks"]
    if tier == "full":
        for cid in FULL_LOCAL_CHECKS + FULL_ELSEWHERE_CHECKS:
            plan.add_check(cid, "full validation (milestone / pre-merge checkpoint)")
        plan.add_full("sim", "full validation")
        plan.add_full("ui", "full validation")
        return plan

    cats_by_id = {c["id"]: c for c in vmap["categories"]}
    items: list[tuple[str, dict]] = []
    for s in scope or []:
        if s not in cats_by_id:
            raise SystemExit(f"unknown scope '{s}'; known: {', '.join(cats_by_id)}")
        items.append((f"<scope {s}>", cats_by_id[s]))
    for p in paths:
        items.append((p, categorize(vmap, p)))
    if not items:
        plan.notes.append("No changed files and no --scope: nothing selected. Pass --paths/--scope/--base, or --tier full.")
        return plan

    max_classes = vmap.get("referenceInference", {}).get("maxClasses", 40)
    any_code = False
    escalate = []
    for path, cat in items:
        plan.categories[path] = cat["id"]
        reason = f"{cat['id']}: {path}"
        if cat.get("fallback"):
            plan.notes.append(f"{path}: matched no category; conservative fallback ({cat['desc']}). Add it to "
                              "scripts/validation-map.json if this kind of change recurs.")
        if path.endswith(".md") and not path.startswith("<scope"):
            plan.changed_md.append(path)
        if is_code_path(path) or cat["id"] in ("research-content", "unit-content", "goods-content", "sim-config",
                                               "doc-fixtures", "research-candidate", "tests"):
            any_code = True
        specs = [cat.get("targeted", {})]
        if tier == "integration":
            specs.append(cat.get("integration", {}))
        for spec in specs:
            for cid in spec.get("checks", []):
                plan.add_check(cid, reason)
            for proj in ("sim", "ui"):
                for term in spec.get(proj, []):
                    if term == "{system}":
                        tok = system_token(path)
                        if tok:
                            plan.add_term(proj, tok, reason)
                    elif term == "{inferred-or-all}":
                        inferred = index.referencing(os.path.splitext(os.path.basename(path))[0]) if path.endswith(".cs") else []
                        inferred = [c for c in inferred if c.project == proj]
                        if inferred and len(inferred) <= max_classes:
                            for c in inferred:
                                plan.add_term(proj, "cls:" + c.fqn, reason + " (references the changed type)")
                        else:
                            plan.add_full(proj, reason + (" (no referencing test class found)" if not inferred
                                                          else f" ({len(inferred)} referencing classes)"))
                    else:
                        plan.add_term(proj, term, reason)
        if cat.get("escalate"):
            escalate.append(path)
        # Changed test sources: run exactly the classes they define.
        if path.startswith(("Sim.Tests/", "Sim.Ui.Tests/")) and path.endswith(".cs"):
            for c in index.classes_in_file(path):
                plan.add_term(c.project, "cls:" + c.fqn, f"changed test class: {path}")
        # Reference inference for production C# and for files test sources name.
        elif not path.startswith("<scope"):
            stem = os.path.splitext(os.path.basename(path))[0]
            # .cs: the type name; Markdown: the repo path (a bare 'README.md' would match unrelated readers);
            # anything else (fixtures, json): the file name.
            word = stem if path.endswith(".cs") else (path if path.endswith(".md") else os.path.basename(path))
            refs = index.referencing(word) if len(word) >= 4 else []
            if refs and len(refs) <= max_classes:
                for c in refs:
                    plan.add_term(c.project, "cls:" + c.fqn, f"references {word}: {path}")
            elif refs:
                plan.notes.append(f"{path}: '{word}' is referenced by {len(refs)} test classes (a shared type).")
                if path.endswith(".cs"):
                    if tier == "integration":
                        for proj in sorted({c.project for c in refs}):
                            plan.add_full(proj, f"shared type {word} referenced by {len(refs)} test classes")
                    else:
                        plan.notes.append(f"{path}: targeted tier keeps the category's tests; run --tier integration "
                                          "before calling this change done (shared type).")

    for path in escalate if tier == "integration" else []:
        plan.add_full("sim", f"{plan.categories[path]} is shared infrastructure: {path}")
        plan.add_full("ui", f"{plan.categories[path]} is shared infrastructure: {path}")
        plan.notes.append(f"{path}: shared core/schema/config change; integration tier runs the full suites "
                          "(targeted tests alone are not enough for this kind of change).")
    if escalate and tier == "targeted":
        plan.notes.append("shared core/schema/config change: targeted checks are a first pass only; "
                          "--tier integration (full suites) is required before the change is done.")

    if any(plan.terms[p] or p in plan.full for p in ("sim", "ui")):
        plan.add_check("build", "dotnet tests are selected: they run against a fresh incremental build, never a stale one")
    if any_code:
        if tier == "integration" and any(p.endswith(".cs") for p in paths):
            plan.add_check("banned-constructs", "C# changed (determinism law 5)")
            plan.add_check("read-isolation", "C# changed")
    for proj, cid in (("sim", "simtests-selected"), ("ui", "uitests-selected")):
        if plan.terms[proj] and proj not in plan.full:
            plan.add_check(cid, "selected test filter")
        if proj in plan.full:
            plan.add_check("simtests-full" if proj == "sim" else "uitests-full", "; ".join(plan.full[proj]))
    for cid in list(plan.checks):
        if cid not in checks:
            plan.notes.append(f"check '{cid}' is not defined in validation-map.json (reported as MISSING)")
    return plan


def dotnet_filter(terms: dict, exclude: list | None = None) -> str:
    parts = []
    for term in sorted(terms):
        if term.startswith("trait:"):
            parts.append(term[6:])
        elif term.startswith("cls:"):
            parts.append(f"FullyQualifiedName~{term[4:]}.")
        else:
            parts.append(f"FullyQualifiedName~{term}")
    f = "|".join(parts)
    if exclude:
        f = f"({f})" + "".join(f"&FullyQualifiedName!~{x}" for x in exclude)
    return f


# ------------------------------------------------------------------ fingerprints and cache

def tree_files(root: str) -> list[str]:
    out = subprocess.run(["git", "ls-files", "-co", "--exclude-standard"], cwd=root, capture_output=True, text=True)
    if out.returncode != 0:
        return []
    return sorted(p for p in out.stdout.splitlines() if p and os.path.isfile(os.path.join(root, p)))


def fingerprint(root: str, files: list[str], exclude=lambda p: False) -> str:
    h = hashlib.sha256()
    for p in files:
        if exclude(p):
            continue
        h.update(p.encode())
        with open(os.path.join(root, p), "rb") as f:
            h.update(hashlib.sha256(f.read()).digest())
    return h.hexdigest()


# ------------------------------------------------------------------ execution

@dataclass
class Result:
    cid: str
    status: str
    seconds: float = 0.0
    detail: str = ""
    log: str = ""


def resolve_command(root: str, check: dict, plan: Plan, cid: str, logdir: str):
    kind = check.get("kind", "script")
    if kind == "dotnet-test":
        project = check["project"]
        cmd = ["dotnet", "test", project, "-c", "Release", "--no-build", "--nologo",
               "--logger", f"trx;LogFileName={cid}.trx", "--results-directory", logdir]
        if not check.get("full"):
            proj_key = "sim" if project == "Sim.Tests" else "ui"
            cmd += ["--filter", dotnet_filter(plan.terms[proj_key], plan.exclude[proj_key])]
        return cmd
    cmd = []
    for part in check.get("cmd", []):
        if part == "{changed_md}":
            cmd += plan.changed_md
        else:
            cmd.append(part)
    return cmd


def run_one(root: str, cid: str, check: dict, plan: Plan, logdir: str, cache: dict, fps: dict, use_cache: bool,
            heavy: bool, blocked: bool) -> Result:
    where = check.get("where", "local")
    if where in ("ci", "manual") or (where == "heavy" and not heavy):
        why = {"ci": "CI-only", "manual": "manual", "heavy": "heavy check not requested (--heavy)"}[where]
        return Result(cid, "NOT RUN", detail=f"{why}: {check.get('note', '')}")
    if blocked and (check.get("full") or where == "heavy"):
        return Result(cid, "NOT RUN", detail="blocked: an earlier check failed (expensive checks wait for cheap ones)")
    for req in check.get("requires", []):
        if not os.path.exists(os.path.join(root, req)):
            return Result(cid, "MISSING", detail=f"required path not found: {req}")
    if where == "heavy" and not check.get("cmd") and check.get("kind") != "dotnet-test":
        return Result(cid, "MISSING", detail="heavy check has no command")
    cmd = resolve_command(root, check, plan, cid, logdir)
    if cid == "doc-links" and not plan.changed_md:
        return Result(cid, "NOT RUN", detail="no Markdown file changed")
    kind = check.get("kind", "script")
    key = None
    if use_cache and cid != "build":
        fp = fps["code"] if kind == "dotnet-test" else fps["all"]
        ident = [check.get("project"), cmd[cmd.index("--filter") + 1] if "--filter" in cmd else "*"] \
            if kind == "dotnet-test" else cmd
        key = hashlib.sha256(json.dumps([cid, ident, fp, fps["sdk"]]).encode()).hexdigest()
        hit = cache.get(key)
        if hit:
            return Result(cid, "PASS (cached)", detail=f"inputs unchanged since {hit['when']} ({hit['seconds']:.0f}s run)")
    env = dict(os.environ, **check.get("env", {}))
    log = os.path.join(logdir, f"{cid}.log")
    start = time.monotonic()
    lock = machine_lock() if (check.get("full") or where == "heavy") else None
    try:
        proc = subprocess.run(cmd, cwd=root, capture_output=True, text=True, env=env)
    except FileNotFoundError as e:
        return Result(cid, "MISSING", detail=f"command not found: {e.filename}")
    finally:
        if lock:
            lock.close()
    secs = time.monotonic() - start
    with open(log, "w", encoding="utf-8") as f:
        f.write("$ " + " ".join(cmd) + "\n" + proc.stdout + proc.stderr)
    out = proc.stdout + proc.stderr
    detail = ""
    if kind == "dotnet-test":
        m = re.findall(r"(Passed|Failed)!\s+-\s+Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)", out)
        if m:
            _, failed, passed, skipped, total = m[-1]
            detail = f"{passed} passed, {failed} failed, {skipped} skipped of {total}"
            if int(total) == 0:
                return zero_match(cid, secs, log, plan, check)
        elif "No test matches the given testcase filter" in out:
            return zero_match(cid, secs, log, plan, check)
    status = "PASS" if proc.returncode == 0 else "FAIL"
    if status == "FAIL" and kind == "dotnet-test":
        trx = os.path.join(logdir, f"{cid}.trx")
        if os.path.exists(trx):  # flake capture (scripts/test.sh precedent): name every failed test
            names = sorted(set(re.findall(r'testName="([^"]+)"[^>]*outcome="Failed"', open(trx, encoding="utf-8", errors="replace").read())))
            if names:
                detail = (detail + "; " if detail else "") + "failed: " + ", ".join(names[:8]) + (" ..." if len(names) > 8 else "")
    if status == "FAIL" and not detail:
        tail = [l for l in out.strip().splitlines() if l.strip()][-3:]
        detail = " | ".join(tail)[:300]
    if status == "PASS" and key:
        cache[key] = {"when": datetime.datetime.now().isoformat(timespec="seconds"), "seconds": secs}
    return Result(cid, status, secs, detail, log)


def zero_match(cid, secs, log, plan, check) -> Result:
    proj = "sim" if check.get("project") == "Sim.Tests" else "ui"
    if plan.exclude.get(proj):
        return Result(cid, "NOT RUN", secs, "every selected test is in the slow list: deferred to --tier integration", log)
    return Result(cid, "FAIL", secs, "the filter matched no tests", log)


def execute(root: str, vmap: dict, plan: Plan, use_cache: bool, heavy: bool, jobs: int) -> list[Result]:
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    logdir = os.path.join(root, "TestResults", "validation", f"{stamp}-{plan.tier}")
    os.makedirs(logdir, exist_ok=True)
    cache_path = os.path.join(root, "TestResults", "validation", "cache.json")
    cache = {}
    if use_cache and os.path.exists(cache_path):
        try:
            cache = json.load(open(cache_path))
        except (OSError, ValueError):
            cache = {}
    fps = {}
    if use_cache:
        files = tree_files(root)
        test_text = "\n".join(open(os.path.join(root, f), encoding="utf-8", errors="replace").read()
                              for f in files if f.startswith(("Sim.Tests/", "Sim.Ui.Tests/")) and f.endswith(".cs"))
        pure_doc = lambda p: p.startswith("docs/") and p.endswith(".md") and os.path.basename(p) not in test_text
        fps["all"] = fingerprint(root, files)
        fps["code"] = fingerprint(root, files, pure_doc)
        sdk = subprocess.run(["dotnet", "--version"], capture_output=True, text=True) if shutil_which("dotnet") else None
        fps["sdk"] = sdk.stdout.strip() if sdk and sdk.returncode == 0 else "none"
    checks = vmap["checks"]
    order = sorted(plan.checks, key=lambda c: (CHEAP_FIRST.index(c) if c in CHEAP_FIRST else len(CHEAP_FIRST),
                                               bool(checks.get(c, {}).get("full")), c))
    results: list[Result] = []
    cheap = [c for c in order if c in checks and checks[c].get("kind", "script") == "script" and c != "build"
             and checks[c].get("where", "local") == "local"]
    rest = [c for c in order if c not in cheap]
    with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, jobs)) as pool:
        futs = {c: pool.submit(run_one, root, c, checks[c], plan, logdir, cache, fps, use_cache, heavy, False) for c in cheap}
        for c in cheap:
            results.append(futs[c].result())
    for c in rest:
        if c not in checks:
            results.append(Result(c, "MISSING", detail="not defined in scripts/validation-map.json"))
            continue
        blocked = any(r.status in ("FAIL", "MISSING") for r in results)
        if c.endswith("-selected") or c.endswith("-full"):
            if any(r.cid == "build" and r.status == "FAIL" for r in results):
                results.append(Result(c, "NOT RUN", detail="blocked: build failed"))
                continue
        results.append(run_one(root, c, checks[c], plan, logdir, cache, fps, use_cache, heavy, blocked))
    if use_cache:
        json.dump(cache, open(cache_path, "w"), indent=1, sort_keys=True)
    summary = {"tier": plan.tier, "categories": plan.categories, "notes": plan.notes,
               "filters": {p: dotnet_filter(t) for p, t in plan.terms.items() if t},
               "results": [r.__dict__ for r in results]}
    json.dump(summary, open(os.path.join(logdir, "summary.json"), "w"), indent=1)
    with open(os.path.join(logdir, "summary.md"), "w") as f:
        f.write(render_results(results))
    print(f"\nlogs: {os.path.relpath(logdir, root)}")
    return results


def machine_lock(path: str | None = None):
    """One full suite or heavy run at a time per machine (policy §5): a blocking flock on a file in the system temp
    directory, shared by every worktree and session. Released when the returned file is closed."""
    import fcntl
    import tempfile
    path = path or os.environ.get("CIVDEMO_LOCK_PATH") or os.path.join(tempfile.gettempdir(), "civdemo-full-suite.lock")
    f = open(path, "w")
    try:
        fcntl.flock(f, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        print("waiting: another full suite or heavy run holds the machine lock (docs/validation-policy.md §5)", flush=True)
        fcntl.flock(f, fcntl.LOCK_EX)
    return f


def shutil_which(name: str) -> bool:
    import shutil
    return shutil.which(name) is not None


# ------------------------------------------------------------------ reporting

def render_plan(vmap: dict, index: TestIndex, plan: Plan) -> str:
    lines = [f"Tier: {plan.tier}"]
    if plan.categories:
        lines.append("Changed paths -> category:")
        for p, c in plan.categories.items():
            lines.append(f"  {p} -> {c}")
    lines.append("Selected checks:")
    for cid, reasons in plan.checks.items():
        chk = vmap["checks"].get(cid, {})
        where = chk.get("where", "local")
        tag = "" if where == "local" else f" [{where}]"
        lines.append(f"  - {cid}{tag}: {chk.get('desc', '?')}")
        for r in reasons[:6]:
            lines.append(f"      because {r}")
        if len(reasons) > 6:
            lines.append(f"      ... and {len(reasons) - 6} more")
    for proj in ("sim", "ui"):
        if plan.terms[proj] and proj not in plan.full:
            n = len({c.fqn for t in plan.terms[proj] for c in index.match(proj, t)})
            ex = f", {len(plan.exclude[proj])} slow tests excluded" if plan.exclude[proj] else ""
            lines.append(f"  {TEST_PROJECTS[proj]} filter ({n} classes{ex}): {dotnet_filter(plan.terms[proj])}")
    for n in plan.notes:
        lines.append(f"NOTE: {n}")
    if not plan.checks:
        lines.append("  (none)")
    return "\n".join(lines)


def render_results(results: list[Result]) -> str:
    lines = ["| Check | Status | Time | Detail |", "|---|---|---|---|"]
    for r in results:
        lines.append(f"| {r.cid} | {r.status} | {r.seconds:.0f}s | {r.detail} |")
    passed = sum(r.status.startswith("PASS") for r in results)
    failed = sum(r.status in ("FAIL", "MISSING") for r in results)
    notrun = sum(r.status == "NOT RUN" for r in results)
    lines.append(f"\n{passed} passed, {failed} failed or missing, {notrun} NOT RUN (not passes).")
    return "\n".join(lines) + "\n"


def changed_paths(root: str, base: str | None) -> list[str]:
    ref = base or "HEAD"
    diff = subprocess.run(["git", "diff", "--name-only", ref], cwd=root, capture_output=True, text=True)
    if diff.returncode != 0:
        raise SystemExit(f"git diff against '{ref}' failed: {diff.stderr.strip()}")
    untracked = subprocess.run(["git", "ls-files", "--others", "--exclude-standard"], cwd=root, capture_output=True, text=True)
    return sorted(set(p for p in (diff.stdout + untracked.stdout).splitlines() if p))


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--tier", choices=TIERS)
    ap.add_argument("--paths", nargs="*", help="explicit changed paths (default: working tree vs --base)")
    ap.add_argument("--base", help="git ref to diff the working tree against (default HEAD)")
    ap.add_argument("--scope", nargs="*", help="explicit category ids from validation-map.json")
    ap.add_argument("--plan", action="store_true", help="print the selection and reasons; run nothing")
    ap.add_argument("--no-cache", action="store_true", help="never reuse a cached pass")
    ap.add_argument("--use-cache", action="store_true", help="allow cached passes in --tier full (off by default)")
    ap.add_argument("--heavy", action="store_true", help="also run heavy checks (multi-seed trial, bench)")
    ap.add_argument("--jobs", type=int, default=min(4, os.cpu_count() or 1), help="parallel cheap validators")
    ap.add_argument("--strict", action="store_true", help="exit 3 if a selected check was NOT RUN")
    ap.add_argument("--include-slow", action="store_true", help="Tier 1: also run the tests recorded as slow")
    ap.add_argument("--record-durations", nargs="+", metavar="TRX",
                    help="update scripts/test-durations.json from .trx files and exit")
    args = ap.parse_args(argv)
    root = repo_root()
    if args.record_durations:
        data = record_durations(root, args.record_durations, f"{datetime.date.today().isoformat()} from "
                                + ", ".join(os.path.basename(t) for t in args.record_durations))
        print(f"{len(data['slow'])} slow tests recorded in {DURATIONS}")
        return 0
    if not args.tier:
        ap.error("--tier is required (targeted, integration or full)")
    vmap = load_map(root)
    index = TestIndex(root)
    paths = args.paths if args.paths is not None else ([] if args.scope else changed_paths(root, args.base))
    plan = select(vmap, index, [p.replace(os.sep, "/") for p in paths], args.tier, args.scope,
                  load_durations(root), args.include_slow)
    print(render_plan(vmap, index, plan))
    if args.plan:
        return 0
    if not plan.checks:
        print("Nothing to run.")
        return 0
    use_cache = not args.no_cache and (args.tier != "full" or args.use_cache)
    results = execute(root, vmap, plan, use_cache, args.heavy, args.jobs)
    print(render_results(results))
    if any(r.status in ("FAIL", "MISSING") for r in results):
        return 1
    if args.strict and any(r.status == "NOT RUN" for r in results):
        return 3
    return 0


if __name__ == "__main__":
    sys.exit(main())
