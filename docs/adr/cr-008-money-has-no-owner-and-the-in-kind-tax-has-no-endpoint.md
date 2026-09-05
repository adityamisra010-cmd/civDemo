# CR-008 — MONEY HAS NO OWNING MILESTONE, AND M5's IN-KIND TAX HAS NO ENDPOINT

**Status: OPEN — awaiting director ruling. No frozen document has been edited, and
no code, schema, test, golden, band or quarantine was touched.**

Raised under S8 §3 while specifying M5. `docs/queue.md:1237` already named this CR
by number — *"Next blocker is now **CR-008 (money has no owner)**"* — and no file
was ever written for it. This is that file, plus the second half of the problem,
which is the one that actually blocks M5.

---

## §1 THE FROZEN / RATIFIED ITEMS IN CONFLICT

### 1.1 Two ratified records disagree about whether money is M5

| source | says |
| --- | --- |
| `docs/m4-pre-spec-dependencies.md:33-34` — **the ruling** | *"M5 taxes in kind. **Money is NOT folded into M5** and does NOT get an inserted milestone before it; it remains deferred as D-030 states, to a real milestone later in the ladder."* |
| `docs/m4-spec.md:35` | *"**Money: M5, taxes IN KIND (Option B).** M4 ships no currency"* |
| `docs/m4-spec.md:48` | *"**M5 GOVERNS.** Taxes, in kind (1.1). No currency, no fiscal system, no administration at M4."* |
| `docs/m4-spec.md:143`, `:269`, `:420-431` | notable acquisition deferred because *"payment is money, **M5**"*; D-021 valve 5's "bought" *"arrives at **M5 alongside the fiscal system**"* |

**The repository has already adjudicated the surface reading.** `docs/queue.md`
records that *"GOV-2 §1a rules money is NOT at M5, so money is currently UNOWNED
and 'money is M5' in m4-spec is **transcription drift**."*

**So §1.1 is largely self-resolving and is recorded here for completeness:** the
operative ruling is that **M5 taxes IN KIND and ships no currency.** What remains
genuinely open is that **no milestone in the ladder owns money at all** — D-011
§6's table has no money milestone — while three M4 deferrals (`:143`, `:269`,
`:420-431`) are pointed at a fiscal system that, on the operative ruling, M5 does
not build. Those three deferrals are therefore **orphaned**, not merely early.

### 1.2 THE BLOCKING HALF — an in-kind tax has nowhere to go

This is the part that stops M5 being specified, and it is not a documentation
tidy-up.

**Law 1 admits exactly two conserving shapes**: `Ledger.Transfer` between two
`ref Conserved` stocks of the same quantity, and `Ledger.Flow` with a
(quantity, reason) counterweight. A tax is a **Transfer** — the grain already
exists — so it needs **two endpoints**.

The payer endpoint is settled: `GoodStockRow(Settlement, Good, Amount, …)`
(`Sim.Core/State/WorldState.cs:216-245`), the only conserved goods carrier in the
tree, owned strictly per settlement with no polity, class or owner dimension.

**The recipient endpoint does not exist.** Measured on `dbef61a`:

- `PolityRow(Id, CommandSource)` (`WorldState.cs:704`) carries **no quantities**.
- `CapitalRow(Polity, Place)` (`WorldState.cs:724`) is a designation and carries
  **no quantities**; `WorldState.cs:721-723` records that no system writes it.
- There is **no treasury, no state-held stock and no polity-held anything.**
- `ConservedQuantityIds` (`Sim.Core/State/Ids.cs:78-108`) registers Biomass(1),
  ToyGood(2), Population(3), Dwellings(4) and the per-good range 100+goodId —
  nothing state-held.

`docs/queue.md:1219-1221` states the consequence exactly: polity state *"cannot be
postponed past M5 (**Law 1's two-endpoint Transfer gives an in-kind tax nowhere to
go**)"*.

**M5's central schema question is therefore unmade, and it is a design decision
rather than an implementation detail.**

---

## §2 EVIDENCE

**E1 — the shipped precedent proves in-kind extraction is already expressible.**
`AppropriationSystem` (T4.5, D-037 B3) moves grain between two settlements'
`GoodStockRow` stocks via `Ledger.Transfer` and *"publishes no table of its own"*
(`AppropriationSystem.cs:6-7`). An in-kind exaction needs **no new quantity, no
new ReasonId and no new mechanism** — only a decision about the second endpoint.

**E2 — minting the tax would be a conservation defect, not a shortcut.** Using
`Ledger.Flow(Source)` with a new "Taxed" reason would create grain that already
exists and double-count it against the world audit. **Introducing a Tax ReasonId
at all is the signal that the design has gone wrong.**

**E3 — the M4 ruling on localized resources binds the answer.** The M4 exit record
forbids *"magical Empire-wide pooled resources"* and requires that resources stay
physically localized. Any recipient that is a placeless per-polity pool is in
tension with that ruling and must be ruled on explicitly rather than inherited.

**E4 — historical warrant is already on record and points at a place, not a
pool.** `m4-pre-spec-dependencies.md` records the deciding criterion as historical
accuracy: Mesopotamia ran on barley accounting, Old Kingdom Egypt was in-kind and
corvée throughout. **Those states stored grain in central granaries — at a
location, hauled there at a cost** — not in an abstract national account.

---

## §3 OPTIONS (≤3, minimal; none implemented)

**Option A — THE CAPITAL IS THE TREASURY.** The tax is a `Ledger.Transfer` from
each controlled settlement's `GoodStockRow` into the **capital settlement's own
`GoodStockRow`**, located by the existing `CapitalRow`.
*No new row type, no new table, no new conserved quantity, no new ReasonId, and no
schema version bump.* Resources stay physically localized — the grain is really in
a place. Hauling distance becomes a natural, already-modelled cost surface for
authority/administrative reach. Losing the capital means losing the stores, which
gives `CapitalRow` its first real mechanical consequence. **Cost:** a capital-less
Empire (representable under M4-A) has nowhere to tax into and must be defined as
collecting nothing; and the capital's stock is then a mixture of its own
production and the realm's revenue, which no reader currently distinguishes.

**Option B — A PER-POLITY IN-KIND STORE.** A new field-based row,
`PolityStockRow(Polity, Good, Conserved Amount)`, serialized, with a populated
round-trip test and a schema bump. Clean separation of state revenue from local
production, and it survives capital loss. **Cost:** it is a placeless pool, which
is the shape the M4 ruling on localized resources was written against; it needs an
explicit ownership semantics statement; and it adds a table to the canonical
stream, moving every world hash.

**Option C — A PER-(POLITY, SETTLEMENT) STORE.** `PolityStockRow(Polity,
Settlement, Good, Conserved Amount)` — state-owned goods that are nevertheless
*somewhere*. Keeps localization honestly and separates state from local
ownership. **Cost:** the largest table of the three and the most bookkeeping, and
it duplicates the (settlement, good) key that `GoodStockRow` already carries.

---

## §4 BLAST RADIUS

| item | A | B | C |
| --- | --- | --- | --- |
| new row type / table | **none** | one | one |
| `CanonicalSchema.Version` | **unchanged (v24)** | v25 | v25 |
| world hashes / four goldens | **unmoved by the endpoint choice** | **all move** | **all move** |
| new `ConservedQuantityId` | none | none | none |
| new `ReasonId` | **none in any option — a tax is a Transfer** | none | none |
| M4 "resources stay localized" ruling | **honoured** | **in tension — needs explicit ruling** | honoured |
| `CapitalRow` gains mechanical meaning | **yes** | no | no |
| capital-less Empire | must be defined as collecting nothing | unaffected | unaffected |

**No option requires money, currency, or a fiscal system**, and none touches the
10-year turn, D-020, D-021, migration, Happiness, revolt or any M4 mechanism.

---

## §5 RECOMMENDATION

**Option A**, and not merely because it is cheapest.

It is the only option that makes the tax a *physical* act — grain leaves a
settlement and arrives at a place that can be reached, besieged and lost — which
is what the project's own historical warrant describes and what the M4 ruling on
localized resources asks for. It gives `CapitalRow` the mechanical consequence it
has lacked since M4-A, and it makes administrative reach a real constraint rather
than an abstract score, because distance to the capital is already computable from
the shipped network. It also keeps all four M4 goldens still, which matters while
the director is playtesting that baseline.

The stated cost is real and should be ruled on with eyes open: **the capital's
grain stock becomes a mixture of local production and realm revenue.** If the
director wants those distinguishable — for UI, for legitimacy, or for a later
fiscal milestone — Option C is the honest choice and B is the convenient one.

**Separately, and independently rulable: does money get an owning milestone?**
Three M4 deferrals point at a fiscal system no milestone currently builds. That
does not block M5 — M5 taxes in kind under the operative ruling — but it leaves
notable purchase and D-021 valve 5's "bought" permanently orphaned until a
milestone claims them.

**No option is implemented. Awaiting ruling.**
