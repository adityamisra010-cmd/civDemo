# CR-011 — the founded Empire's PolityId collides with the entire existing order corpus

**Status: RULED (director, 2026-09-01). Option 1 accepted: the initial player Empire is `PolityId 1`.**

**THE RULING, so nobody "corrects" the id back to 0.** M4-C §7 prescribed `PolityId 0` *only if*
that was consistent with existing row-table conventions. It is not. The convention that actually
governs an order's actor is the order corpus — the UI factories, ~8 in-code order logs, and both
BINARY replay fixtures — which has stamped `ActorId = 1` since M1. M4-B's actor-existence check
makes that convention operational the moment a roster exists. **The id is arbitrary; the frozen
replay pin is not**, so the id moved rather than the fixtures. The frozen replay artifacts were
preserved unmodified and now resolve naturally. `WorldFounding.FoundInitialEmpire` carries this
reasoning in its header.

Ids 0 and 2.. remain free for later AI Empires. No translation layer and no allocator framework
was introduced.

---

## 1. THE FROZEN / PRESCRIBED ITEMS IN CONFLICT

| | |
| --- | --- |
| **A** | **M4-C packet §7**: *"initial founded Polity = PolityId 0"*, qualified as *"the smallest deterministic rule consistent with existing row-table conventions"*. |
| **B** | **The existing order corpus**: every order log in the repository stamps `ActorId = 1` — the UI factories, ~8 test order logs, and **both binary replay fixtures**. |
| **C** | **M4-B (certified)**: `OrderRecord.ActorId` IS the issuing Empire's `PolityId`, and `OrderValidation` rejects an order whose actor is not a registered Empire — a check that was **dormant only because no world had a roster**. |

A and B were compatible for exactly as long as no founded world had a polity roster. **M4-C populates the roster, which makes C live, and A and B then contradict each other.** This is not a latent disagreement that implementation revealed cosmetically; it is the two rules producing opposite answers about the same order.

## 2. EVIDENCE — MEASURED, NOT ARGUED

Full `Sim.Tests` on the M4-C tree (571 tests): **23 failed**, of which 6 are the pre-existing mainline quarantine and **17 are new**. Categorised by reading each failure's actual message:

| Cause | Count |
| --- | --- |
| **Actor rejected — `"is issued by polity 1, which is not a registered Empire"`** | **14** |
| Golden moved (founded worlds now carry Empire rows) | 2 |
| Error-message assertion (actor check now fires before the settlement check) | 1 |

**The controlled probe.** Changing ONLY the founding id from 0 to 1 and re-running the affected set: **29 of 30 pass**; the single remainder is `FirstReign_PostFix`, a genuine golden movement unrelated to the id. So the 14 failures are caused by the id choice alone, and nothing else about M4-C is implicated. The probe was reverted; the tree implements §7's 0.

**The part that cannot be fixed by editing code.** Both binary replay fixtures carry `ActorId = 1` in their bytes:

```
Sim.Tests/Fixtures/first-reign-orders.bin : 4 records, ActorIds = [1]
Sim.Tests/Fixtures/t38-director-orders.bin: 6 records, ActorIds = [1]
```

These are frozen replay records — the T1.9 trajectory pin and the T3.9a gate session. Under `PolityId 0` they name an Empire that no founded world contains, so they are permanently unreplayable without **restamping a frozen replay artifact**, which is exactly what a replay fixture exists to prevent.

## 3. OPTIONS (≤3, minimal)

**Option 1 — the founded Empire is `PolityId 1`.** One character in `WorldFounding`. Every existing order log, both binary fixtures, and the UI factories become correct as they stand; the 14 failures clear, measured. **Cost:** contradicts §7's literal `0`, and `PolityId 1` as the first-allocated id is inconsistent with every other id table in the repo, which starts at 0.

**Option 2 — keep `PolityId 0` and restamp the corpus.** Editing ~8 in-code logs is trivial; the two BINARY fixtures must be regenerated. **Cost:** rewrites two frozen replay records, which needs its own ruling and destroys their value as untouched historical pins. The T1.9 fixture is the director's own first reign.

**Option 3 — keep `PolityId 0` and make the actor check tolerate legacy logs** (e.g. accept an unregistered actor when the log predates the roster). **Cost:** reintroduces exactly the "actor id means nothing" looseness M4-B was written to remove, and does so permanently rather than as a stated dormancy. **Not recommended.**

## 4. BLAST RADIUS

Option 1: `WorldFounding` only; no fixture, no golden, no contract touched. Option 2: two binary fixtures + ~8 test logs + the UI constant; the goldens do NOT move either way, because `ActorId` is not part of serialized world state. Option 3: `OrderValidation` semantics, permanently.

## 5. RECOMMENDATION

**Option 1.** §7's own wording conditions `0` on being *"consistent with existing row-table conventions"* — and the convention that actually governs order actors is the corpus that has stamped `1` since M1, including two frozen fixtures. `0` is the more elegant id in isolation and the wrong one in context. The id is arbitrary; the frozen replay record is not.

If the director prefers `0` on principle, Option 2 is coherent but the restamping of `first-reign-orders.bin` and `t38-director-orders.bin` needs to be authorised explicitly and separately — an agent must not rewrite a frozen replay pin on its own judgement.

## 6. GOLDEN MOVEMENT — AUDITED, THEN PARTLY REPINNED

**Three** founded-world pins move, not two: `FoundedGolden_Seed42Turn300`, `FirstReign` and
`DrivenGolden_Seed42Turn300`. `GoldenHash_Seed42Turn200` does NOT — it is synthetic and never
founded, which is the no-unrelated-movement control holding.

**The audit, run before any repin.** `IntegratedPinAttributionTests` now separates TWO layers,
because M4 moved these pins twice for different reasons: M4-A's schema-v23 LAYOUT (count prefixes,
present even when the tables are empty) and M4-C's founding CONTENT (the rows). Emptying
Polities/Controls/Capitals on each finished world returns its pre-M4-C pin **byte for byte** — for
all three. Any drift in population, food, terrain, deposits, paths, production, demography,
migration or the economy would survive that strip and break the control, so it is *measured* that
nothing else moved.

| pin | old | new |
| --- | --- | --- |
| `FoundedGolden_Seed42Turn300` | `9fc45cc7…ff686` | `5a64109a…c5c4c` |
| `FirstReign` turn 40 | `28247419…75d74` | `c6f28f8a…91dc7` |
| `DrivenGolden_Seed42Turn300` | `ca1d8329…767a4` | `1768b254…860bf` |

All three were repinned, each only after the control proved its delta: `FoundedGolden` under the
explicit ruling, `FirstReign` under §6's condition that it may be touched if it moves for the same
intended founding-state reason, and `DrivenGolden` under a separate director approval given once the
audit was on the table. `DrivenGolden` is the one pin carrying FOUR composed causes, and its comment
records the whole chain rather than just the last step.

**CLOSED. M4-C certified at this tree.**
