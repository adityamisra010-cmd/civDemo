# Educational corpus — writing and sourcing brief

Part of the package so the Director can see how every node text was produced. Status: PROPOSED content; nothing is
runtime content until a content packet ships it.

## 1. What to write

One ORIGINAL description per node, **50–60 words** (45–65 only where accuracy needs it), plain educational English for
an adult general reader.

- **Technology nodes** explain: the concept (what the knowledge is), its historical context (where and when it is
  attested, with honest ranges), why it mattered, and one memorable detail or geographic/scientific connection.
- **Civic nodes** explain: the institution, principle or regime; historical context and precedent; the problem it
  addressed; its consequences or limitations.

## 2. Rules that bind every text

1. **Original prose.** Never copy or closely paraphrase an encyclopedia, a website, a textbook or any game (no
   Civilization text, names of game features, or quotes). Quotations ≤ 10 words, only when essential, attributed.
2. **No invention.** No invented dates, inventors, anecdotes, quotations, places or consensus. If a date is uncertain,
   give a range or "by about"; if origins are disputed or multiple, say so ("developed independently in…", "attested
   first in… though earlier use is debated").
3. **No inevitability.** Do not present history as one necessary sequence or one civilization's path as universal.
   Avoid "inevitably", "destined", "naturally led to", "every civilization".
4. **Distinguish** observation, scientific explanation, commercialization and widespread adoption (e.g., noticing a
   phenomenon ≠ explaining it ≠ building with it ≠ its spread).
5. **No game language in the text**: no "unlocks", "bonus", "+%", "research points", "you", "the player". The link to
   simulation mechanics goes in the separate `mechanicLink` field (one sentence, factual, PROPOSED where the mechanic is
   proposed).
6. **Respectful, neutral, non-sensational** — especially for weapons, epidemics, slavery-related institutions, regimes
   and colonial episodes. State facts and consequences; no glorification.
7. **Specific over generic**: prefer a concrete place, artefact, date range or mechanism to adjectives.

## 3. Sources (per node)

- Use credible sources: peer-reviewed scholarship, university pages, academic reference works (e.g., Britannica,
  Stanford Encyclopedia of Philosophy, MacTutor), scientific institutions and official bodies (e.g., IAEA, USGS, NASA,
  WHO, Nobel Prize site), archives, museums (British Museum, Met Heilbrunn Timeline, Smithsonian, Science Museum),
  historical societies and primary sources where useful. Wikipedia may help you find sources; it must not be the sole
  authority for an important or contested claim.
- Every node needs ≥ 1 source; important dates/attributions need a source that states them. Record ONLY sources you
  actually retrieved this session (WebSearch/WebFetch via ToolSearch "select:WebSearch,WebFetch").
- Per node, `sources: [{"sourceId", "claims": [the specific claims it supports]}]`.
- Register entries go in your lane's `sources/<topic>-sources.json`: `{"id", "title", "author_org", "published" (or
  null), "url", "accessed": "2026-10-22", "type": official|archive|academic|museum|university|reference|industry|press|
  community, "claims_supported": [...], "confidence": high|medium|low, "notes"}`. Use the id prefix given to you
  (e.g., `AGR-001`); reuse an existing register entry (from `sources/*.json`) by its id instead of duplicating it.

## 4. Fact-check status (honest, per node)

**Environment limit (MEASURED 2026-10-22):** this session's network policy blocks opening web pages directly
(`WebFetch` fails with DNS errors for non-GitHub hosts, e.g., `www.medievalists.net`); `WebSearch` works and returns
result titles, URLs and summarized content. A source "retrieved" here therefore means a search result whose URL and
summary you saw — not a page you read in full. Record this precisely:

- `source-checked`: every factual claim was compared with the full text of a source you opened (possible only for
  GitHub-hosted material in this environment).
- `search-corroborated`: every factual claim (dates, places, people, mechanisms) matches the summarized content of
  search results from credible sources whose URLs you record; the pages themselves were not opened.
- `partially-checked`: some claims are corroborated; others rest on general knowledge you believe correct but did not
  corroborate — list them in `notes`.
- `unverified`: no claim was corroborated (e.g., search unavailable) — say why in `notes`.
- If a search fails or the search budget is exhausted, keep writing (accuracy first, cautious wording), mark the entry
  `unverified` and do not invent a source.
- `checkedBy`: "drafting lane"; `method`: what was compared against what. An independent check pass follows; a
  citation is not proof that a claim was verified.

## 5. Procedure

1. Read your node list (ids given in your prompt) from `technology-tree-proposal.json` / `civics-tree-proposal.json`:
   name, Age, branch, `emerged`, prerequisites, `nodeKind`, `capability`, `whyDistinct`, `rationale`, civic block.
   The text must fit the node AS PROPOSED (a split node describes only its part).
2. Research, then write. Check the count with `python3 -I tools/wordcount.py corpus/<topic>.json` (the validator uses
   the same counter). Fix anything outside 50–60 unless accuracy needs 45–65.
3. Fill `uncertainty` (disputed origins, ranges, multiple centres) or null; `mechanicLink`; `factCheck`.
4. Validate the JSON parses. Write only your corpus file and your sources file.
