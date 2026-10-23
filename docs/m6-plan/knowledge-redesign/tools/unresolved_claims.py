#!/usr/bin/env python3
"""Per-node evidence inventory: what each educational text rests on, and every claim still unresolved.

DESIGN-PACKAGE TOOLING ONLY. Reads corpus/*.json, corpus/plan.json and sources/register.json; writes
data/unresolved-claims.json (one record per corpus entry) and corpus/unresolved-claims.md. Reports only what the
entries and the register say: it never changes a status and never treats a cited URL as evidence that a page was
read (a source counts as READ only when the register records retrieval 'page-read' or 'document-read').

A claim is UNRESOLVED when its recorded result is 'unsupported' (no source found that states it) or 'disputed' (the
sources disagree; the text must present it as disputed). A 'supported' claim whose sources were never opened is
SEARCH-ONLY: corroborated by search-result text, not checked against the page. An 'unverified' entry lists no claims:
every factual statement in it is unresolved. Re-running is idempotent.

Usage: python3 -I tools/unresolved_claims.py
"""
import re
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kr_lib as kr  # noqa: E402

READ = {"page-read", "document-read"}
STATUS_ORDER = ("unverified", "partially-checked", "search-corroborated", "source-checked")
MEANING = {
    "unverified": "no evidence retrieved; every statement unresolved",
    "partially-checked": "some claims match search-result text; the others are unsupported or unchecked",
    "search-corroborated": "every listed claim matches search-result text; no source page opened",
    "source-checked": "every supported claim checked against a page or document that was read",
}
NOTE_FLAG = re.compile(r"not corroborated|uncorroborated|could not|unconfirmed|no source|unverified|"
                       r"general knowledge|conflict|contradict|disput", re.I)


def note_flags(text):
    """Sentences of the free-text notes that record an unconfirmed or contested point."""
    out = []
    for s in re.split(r"(?<=[.;])\s+", text or ""):
        s = s.strip()
        if s and NOTE_FLAG.search(s):
            out.append(s)
    return out


def main():
    plan = kr.load_json(kr.PKG / "corpus" / "plan.json")
    reg = {s["id"]: s for s in kr.load_json(kr.PKG / "sources" / "register.json")}
    records = []
    for b in plan["batches"]:
        for e in kr.load_json(kr.PKG / "corpus" / b["file"]):
            fc = e.get("factCheck") or {}
            st = fc.get("status", "unverified")
            claims = fc.get("claims") or []
            unresolved, search_only, read = [], [], 0
            for c in claims:
                srcs = c.get("sources") or []
                opened = any((reg.get(s) or {}).get("retrieval") in READ for s in srcs)
                if c.get("result") in ("unsupported", "disputed"):
                    unresolved.append({"claim": c.get("claim"), "result": c.get("result"), "sources": srcs})
                elif c.get("result") == "supported":
                    if opened:
                        read += 1
                    else:
                        search_only.append(c.get("claim"))
            # not itemised: the status rests on the claims each SOURCE is recorded as supporting (sources[].claims);
            # which statements of the text those leave uncovered is recorded, if at all, only in the notes
            itemised = bool(claims)
            source_claims = [c for s in e.get("sources") or [] for c in s.get("claims") or []]
            if not itemised:
                search_only = list(source_claims)
            records.append({
                "id": e["id"], "tree": e.get("tree"), "file": b["file"], "status": st, "meaning": MEANING.get(st, st),
                "sources": len(e.get("sources") or []),
                "sourcesRead": sum(1 for s in e.get("sources") or [] if (reg.get(s.get("sourceId")) or {}).get("retrieval") in READ),
                "claims": dict(sorted(Counter(c.get("result") for c in claims).items())),
                "claimsItemised": itemised,
                "claimsSupportedByReadPage": read, "claimsSearchOnly": len(search_only),
                "unresolvedClaims": unresolved,
                "wholeTextUnresolved": st == "unverified" and not claims,
                "recordedUncertainty": e.get("uncertainty") or None,
                "notesFlags": note_flags(fc.get("notes")),
                "mayShip": st == "source-checked" and all(c.get("result") in ("supported", "corrected", "disputed") for c in claims),
            })
    by_status = Counter(r["status"] for r in records)
    results = Counter()
    for r in records:
        results.update(r["claims"])
    totals = {"entries": len(records), "byStatus": {k: by_status.get(k, 0) for k in STATUS_ORDER},
              "claimsByResult": dict(sorted(results.items())),
              "unresolvedClaims": sum(len(r["unresolvedClaims"]) for r in records),
              "entriesWithUnresolvedClaims": sum(1 for r in records if r["unresolvedClaims"]),
              "entriesWholeTextUnresolved": sum(1 for r in records if r["wholeTextUnresolved"]),
              "claimsSearchOnly": sum(r["claimsSearchOnly"] for r in records),
              "entriesNotItemised": sum(1 for r in records if not r["claimsItemised"] and r["status"] != "unverified"),
              "entriesNotItemisedNoNotes": sum(1 for r in records if not r["claimsItemised"] and r["status"] != "unverified"
                                               and not r["notesFlags"]),
              "claimsSupportedByReadPage": sum(r["claimsSupportedByReadPage"] for r in records),
              "mayShip": sum(1 for r in records if r["mayShip"])}
    kr.dump_json({"_doc": "Generated by tools/unresolved_claims.py from corpus/*.json and sources/register.json. One "
                          "record per corpus entry; see the tool header for the definitions.",
                  "totals": totals, "entries": records}, kr.DATA / "unresolved-claims.json")

    L = ["# Unresolved claims and evidence status, per node (generated)", "",
         "Generated by `tools/unresolved_claims.py` (data: `data/unresolved-claims.json`). It reports what each entry "
         "records; it never promotes a status. **No source page or document has been opened for any entry** "
         f"({totals['claimsSupportedByReadPage']} claims are supported by a page that was read), so no entry is "
         "`source-checked` and none may ship as fact.", "",
         "| Status | Entries | Meaning |", "|---|---|---|"]
    L += [f"| {k} | {by_status.get(k, 0)} | {MEANING[k]} |" for k in STATUS_ORDER]
    L += ["", "| Claim result | Count |", "|---|---|"] + [f"| {k} | {v} |" for k, v in sorted(results.items())]
    L += ["", f"- Unresolved claims (unsupported or disputed): **{totals['unresolvedClaims']}** in "
              f"{totals['entriesWithUnresolvedClaims']} entries.",
          f"- Entries whose whole text is unresolved (unverified, no claim checked): **{totals['entriesWholeTextUnresolved']}**.",
          f"- Supported claims resting on search-result text only: **{totals['claimsSearchOnly']}** (they become "
          "source-checked only when the cited page is opened; for entries without itemised claims these are the "
          "claims recorded per source).",
          f"- Entries above `unverified` whose claims are **not itemised** (status rests on per-source claims): "
          f"**{totals['entriesNotItemised']}**, of which {totals['entriesNotItemisedNoNotes']} record no unconfirmed "
          "point in their notes either — for those, which statements are unchecked is not identified.", ""]
    for b in plan["batches"]:
        rows = [r for r in records if r["file"] == b["file"]]
        L += [f"## {b['title']} — `corpus/{b['file']}`", ""]
        for r in sorted(rows, key=lambda r: (STATUS_ORDER.index(r["status"]) if r["status"] in STATUS_ORDER else 9, r["id"])):
            head = f"- `{r['id']}` — **{r['status']}**"
            if r["wholeTextUnresolved"]:
                L.append(head + " — no evidence retrieved; the whole text is unresolved")
                continue
            if r["claimsItemised"]:
                L.append(head + f" — {r['claimsSearchOnly']} search-only supported, {len(r['unresolvedClaims'])} unresolved")
            else:
                L.append(head + f" — claims not itemised; {r['claimsSearchOnly']} per-source claims (search-only)"
                         + ("" if r["notesFlags"] else "; unchecked statements NOT identified"))
            for u in r["unresolvedClaims"]:
                L.append(f"  - {u['result'].upper()}: {u['claim']}")
            for n in r["notesFlags"]:
                L.append(f"  - note: {n}")
        L.append("")
    (kr.PKG / "corpus" / "unresolved-claims.md").write_text("\n".join(L), encoding="utf-8")
    print("totals:", totals)


if __name__ == "__main__":
    main()
