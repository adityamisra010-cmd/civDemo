#!/usr/bin/env python3
"""Relative-link and anchor check for Markdown (the documentation tier of docs/validation-policy.md).

Usage: python3 scripts/check-doc-links.py FILE.md [FILE.md ...]   check the named files
       python3 scripts/check-doc-links.py --all                     every *.md under docs/ and the repository root
Checks every [text](target) whose target is a relative path: the file must exist and, when a #anchor is given for a
Markdown target, a heading must produce that anchor (GitHub slug rules). External links (http, mailto) are not
fetched. Links inside fenced code blocks are ignored. Exit 1 on any broken link, listing each one.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LINK = re.compile(r"(?<!\!)\[[^\]\n]*\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")


def slug(heading: str) -> str:
    h = re.sub(r"[`*]", "", heading.strip().lower())
    h = re.sub(r"[^\w\- ]", "", h)
    return h.replace(" ", "-")


def anchors(path: str, cache: dict) -> set:
    if path not in cache:
        found, counts, fence = set(), {}, False
        for line in open(path, encoding="utf-8", errors="replace"):
            if line.lstrip().startswith("```"):
                fence = not fence
                continue
            m = None if fence else re.match(r"^#{1,6}\s+(.+?)\s*#*\s*$", line)
            if m:
                s = slug(m.group(1))
                n = counts.get(s, 0)
                found.add(s if n == 0 else f"{s}-{n}")
                counts[s] = n + 1
            for a in re.findall(r'<a\s+(?:name|id)="([^"]+)"', line):
                found.add(a)
        cache[path] = found
    return cache[path]


def check(files):
    broken, cache = [], {}
    for f in files:
        full = f if os.path.isabs(f) else os.path.join(ROOT, f)
        if not os.path.isfile(full):
            continue  # a deleted file has no links to check
        fence = False
        for no, line in enumerate(open(full, encoding="utf-8", errors="replace"), 1):
            if line.lstrip().startswith("```"):
                fence = not fence
                continue
            if fence:
                continue
            for target in LINK.findall(line):
                if re.match(r"^[a-z][a-z0-9+.-]*:", target) or target.startswith("//"):
                    continue
                path, _, anchor = target.partition("#")
                dest = os.path.normpath(os.path.join(os.path.dirname(full), path)) if path else full
                rel = os.path.relpath(full, ROOT)
                if os.path.relpath(dest, ROOT).startswith(".."):
                    continue  # leaves the repository: a GitHub web path such as ../../tags/x, not checked
                if not os.path.exists(dest):
                    broken.append(f"{rel}:{no}: missing target {target}")
                elif anchor and dest.endswith(".md") and anchor not in anchors(dest, cache):
                    broken.append(f"{rel}:{no}: missing anchor #{anchor} in {os.path.relpath(dest, ROOT)}")
    return broken


def main(argv):
    if argv[:1] == ["--all"]:
        files = [os.path.join(dp, n) for dp, dn, fn in os.walk(os.path.join(ROOT, "docs")) for n in fn if n.endswith(".md")]
        files += [os.path.join(ROOT, n) for n in os.listdir(ROOT) if n.endswith(".md")]
    else:
        files = [a for a in argv if a.endswith(".md")]
    broken = check(sorted(files))
    for b in broken:
        print(b)
    print(f"{len(files)} file(s) checked, {len(broken)} broken link(s)")
    return 1 if broken else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
