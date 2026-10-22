#!/usr/bin/env python3
"""Merge every lane's source file into the consolidated register.

DESIGN-PACKAGE TOOLING ONLY. Reads sources/*-sources.json (every file except
register.json) and the corpus files; writes sources/register.json with one
record per source id, a `file` field naming where it was recorded, and `usedBy`
(the corpus entries citing it). Duplicate URLs across files are kept as
separate ids (each lane cites its own) but flagged in `duplicateOf` so the
register shows them. Deterministic (sorted by id).

Usage: python3 -I tools/merge_sources.py
"""
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402


def main():
    src_dir = kr.PKG / "sources"
    records, seen_url = {}, {}
    problems = []
    for f in sorted(src_dir.glob("*.json")):
        if f.name == "register.json":
            continue
        for s in kr.load_json(f):
            sid = s.get("id")
            if not sid:
                problems.append(f"{f.name}: a record without id")
                continue
            if sid in records:
                problems.append(f"{f.name}: duplicate id {sid} (first in {records[sid]['file']})")
                continue
            rec = dict(s, file=f"sources/{f.name}")
            url = (s.get("url") or "").strip().rstrip("/").lower()
            if url and url in seen_url:
                rec["duplicateOf"] = seen_url[url]
            elif url:
                seen_url[url] = sid
            records[sid] = rec
    used = defaultdict(set)
    for f in sorted((kr.PKG / "corpus").glob("*.json")):
        if f.name in ("index.json", "plan.json"):
            continue
        for e in kr.load_json(f):
            for s in e.get("sources") or []:
                used[s.get("sourceId")].add(e["id"])
    for sid, rec in records.items():
        rec["usedBy"] = sorted(used.get(sid, []))
    missing = sorted(sid for sid in used if sid not in records)
    for sid in missing:
        problems.append(f"corpus cites {sid}, which no source file records")
    out = [records[k] for k in sorted(records)]
    kr.dump_json(out, src_dir / "register.json")
    print(f"register: {len(out)} sources from {len(list(src_dir.glob('*-sources.json')))} files; "
          f"{sum(1 for r in out if r['usedBy'])} cited by the corpus; {len(problems)} problem(s)")
    for p in problems[:40]:
        print("PROBLEM:", p)
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
