# CONV-1 — TERM NAMESPACING BY DOMAIN

## What a CONV record is, and what it is not

**CONV records hold CONVENTIONS.** A convention is a rule about how we NAME and WRITE things, so
that two domains do not quietly use one word for two things.

- **Not an ADR.** An ADR amends or records a decision against the frozen architecture. A CONV
  record amends **no frozen document** and carries no architectural weight. Nothing in CONV-1 may
  be cited to justify a mechanism change.
- **Not a D-record.** A D-record rules a **design** question — what the simulation does. A CONV
  record rules **no design question**. If applying a convention would change behaviour, the
  convention is wrong and stops at the boundary.

Whoever writes CONV-2: this register exists for rules that are cheap to state, easy to violate by
accident, and worth a test. If your rule changes what the sim computes, it is not a CONV.

---

## 1. THE COLLISIONS — VERIFIED AGAINST THE TREE AT `cadcc83`

All four re-verified here rather than taken from the directing prompt (§7.12). All four hold; no
finding against the brief.

| term | meaning A | meaning B (and C) | verified at |
| --- | --- | --- | --- |
| **grain** | the numeraire food good | the paper fibre texture | `Sim.Data/content/goods.json:5` (`id 1`, `numeraire: true`) vs `docs/style-bible-parchment.md` lines **10, 43, 68** |
| **trade** | the D-034 arbitrage system | the retired M0 toy system | `Sim.Core/Systems/Trade/TradeArbitrageSystem.cs:84` (`Name = "trade"`) vs `TradeSystem.cs:27` (`Name = "toytrade"`) |
| **stock** | a goods inventory | a housing stock — and possibly a conserved population stock | `WorldState.cs:643` (`Table<GoodStockRow> GoodStocks`) vs `d018-classes-and-needs.md:36` / `queue.md:393` vs `m4-pre-spec-dependencies.md:89-98` (notables, OPEN) |
| **source** | a claim source | a need-satisfier source | `docs/d037-emergent-polities.md:177` ("conquest is a claim source") vs `Sim.Data/content/needs.json:15` (`"source": "housingStock"`) |

**Why grain went unnoticed for so long:** both meanings are load-bearing, and they collide ACROSS
domains — sim work and art work rarely read each other's documents, so neither side ever saw both
uses on one page.

**Why trade is the expensive one, and the reason this register exists.** Pipeline presets are
DATA. A preset naming `"trade"` bound ambiguously once a second system claimed the word, and T3.6
had to rename the toy to `toytrade` AND ship a load guard (`PipelineLoader.cs:98`, which errors
with `'toytrade' vs 'trade'`). **That is measured cost, not a hypothetical.**

---

## 2. THE RULE (director ruling)

> **NAMESPACE BY DOMAIN.** A term belongs to ONE domain. The domain that holds it keeps the bare
> word; every other domain must qualify or rename.

**Where a collision already exists, THE CLAIMANT IS THE DOMAIN WITH THE MECHANICAL DEPENDENCY** —
code, data, or a serialized identifier — **not the one that used it first in prose.**

*Rationale:* prose can be reworded at zero risk. A registry id, a config key or a schema field
cannot — renaming one means a data migration, a schema bump, or a golden re-pin. T3.6 already
applied this instinctively, renaming the TOY rather than the shipped system.

**The rejected alternative, recorded so it is not re-proposed:** qualify BOTH sides (`grain-good`
vs `paper-grain`, neither keeping the bare word). Rejected because it renames things that are
currently correct, and it puts the cost on code rather than on prose — exactly backwards from the
claimant rule's reasoning.

---

## 3. THE REGISTRY

### RULED

| term | OWNING DOMAIN | what it means there | what every OTHER domain must say | where the collision was found |
| --- | --- | --- | --- | --- |
| **grain** | **SIM** | the numeraire food good, `goods.json` id 1, `numeraire: true` — serialized, and every price in the world is denominated against it | **"fibre texture", "paper fibre", or "mottling"** — never "grain" | `goods.json:5` vs `style-bible-parchment.md:10,43,68` |
| **trade** | **SIM** | the D-034 arbitrage system, `Name = "trade"`, named in `pipeline.json` | the retired M0 toy is **`toytrade`** — already renamed, guarded at load | `TradeArbitrageSystem.cs:84` vs `TradeSystem.cs:27` |

**`trade` is recorded as ALREADY RESOLVED** (T3.6, director decision 3, 2026-07-28) so it is not
re-litigated. The resolution is the claimant rule in action: the shipped system kept the word, the
toy moved, and `PipelineLoader` fails loudly on the ambiguity rather than binding silently.

### PROPOSED — NOT RULED

Both of these touch questions the director has not settled. **A convention that pre-empts an open
design ruling is worse than no convention**, so these are proposals only, and the test in §5 does
NOT enforce them.

**`stock` — PROPOSED: owned by SIM's goods inventory; every other use qualifies.**

Three live meanings: the goods inventory (`GoodStocks`, a serialized table), the housing stock
(T3.8, D-018's Shelter line), and a possible conserved POPULATION stock. Under the claimant rule
the goods inventory wins — it is a schema field and the other two are prose.

*Proposed wording:* bare **"stock"** = goods inventory; **"dwelling stock"** for housing (it is
already what `HousingSystem` counts, and "housing stock" invites the collision); population
qualified explicitly if it ever becomes one.

**Why this is NOT ruled:** the third meaning is genuinely open — GOV-2 carries *"is a notable a
person?"*, and whether notables are conserved population decides whether there IS a population
stock to name. Ruling now would either bless a term for a thing that may not exist, or quietly
constrain how that ruling can be phrased. **Left for the director alongside the notables
question.**

**`source` — PROPOSED: owned by the NEEDS domain; claims say "claim origin".**

Two meanings, both in ratified documents: D-037 C1's claim source, and `needs.json`'s
`"source": "housingStock"`. Under the claimant rule **needs.json wins** — `source` there is a
SERIALIZED JSON KEY read by the needs binding, while D-037's is prose in a design document. That
is the whole rule applied cleanly.

*Proposed wording:* bare **"source"** = a need satisfier's binding; claims say **"claim origin"**
or **"claim provenance"**.

**Why this is NOT ruled:** D-037 is a ratified director document and the polity layer it describes
is unbuilt. Renaming a term inside a ratified design before its implementing packet exists risks
ruling on vocabulary the M4 spec may want to choose deliberately. The claimant rule points
clearly; the timing is the director's.

**Two ruled, two proposed — the honest split.** Rule only what is safely rulable now.

---

## 4. THE FIX APPLIED

`docs/style-bible-parchment.md` amended so the paper texture is not called grain. All three
instances found by grepping the file, not by trusting a list: §1 substrate line, §4 item 2
heading, §5 prompt-skeleton line. A rename note is recorded IN that file.

**Not touched, deliberately:**

- **`Sim.Data/content/goods.json`** — the sim keeps the bare word. That is the ruling.
- **`docs/d038-visual-target.md:15`** ("grain overlay") — a director ruling filed verbatim, and
  `docs/art-gate-defects.md` — a closed gate record. **Closed records describe what was said at
  the time and are not retroactively edited** (S8 §5). The style bible's rename note is what
  keeps a reader of those documents from being confused by the older phrasing.

This is a RENAME, not a supersession, so the style bible's wording is edited in place rather than
struck through — nothing there became false, it is simply called something else now.

---

## 5. THE CHECK — AND ITS LIMIT, STATED PLAINLY

**A convention nobody verifies decays exactly like a stale document.** This project has recorded
THREE instances of that shape already: CLAUDE.md's merge-loop line false for eleven merges,
ADR-015 §7.7 carrying a refuted mechanism, and the Spine's system inventory stale by one milestone
from M6 onward. All three survived *because they sat in documents people read TO find out what is
true.* CONV-1 therefore ships with a test.

`Sim.Tests/Conventions/Conv1TermNamespacingTests.cs`:

1. **grain** — `docs/style-bible-parchment.md` contains no case-insensitive `grain`.
2. **trade** — the shipped arbitrage system is named `trade` and the toy is named `toytrade`,
   asserted against the constants themselves.

**WHAT THIS TEST CANNOT DO, said plainly** (the T3.11 4a precedent — *"no test can enforce that an
agent enumerated two projects; what it enforces is that the rule stays performable"*):

- It **cannot enforce English usage.** Someone may write "grain" in a new art document, a commit
  message, or a comment, and no grep scoped to one file will see it.
- It **cannot police the historical record**, and must not: D-038 and the art gate records keep
  their original phrasing by ruling.
- It **does not check the PROPOSED terms.** Enforcing an unruled convention would bind a decision
  the director has not made.

**What it DOES enforce** is that the one file this convention renamed STAYS renamed, and that the
`trade`/`toytrade` split that already cost a packet cannot silently revert. A regression on either
fails the suite instead of being rediscovered.

**Anchoring** — the T3.11 4a red proof caught its own guard passing on an unrelated substring
elsewhere in the file, so the anchoring is stated: the grain check is a **whole-file
case-insensitive substring search over one named file**, which cannot pass on a match elsewhere
because there is nowhere else in scope. The trade check compares against the `Name` **constants**,
not against file text, so it cannot be satisfied by a comment or a doc mentioning either word.

**RED PROOF (§7.4):** both arms proven red by reintroducing the defect and reverting — transcript
in §6.

---

## 6. RED PROOF TRANSCRIPT

Filled in by the run; see the handback. Both arms applied ALONE and reverted; neither committed.
