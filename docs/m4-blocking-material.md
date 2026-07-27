# M4 — BLOCKING MATERIAL

Findings that the M4 spec **cannot ship without answering**. Not queue items: the spec is
incomplete until each has a stated resolution. Raised under the S8 §4.1 discipline that a
milestone spec's foundations are audited before its packets are written.

---

## B-1 — Settlement spacing caps the continent at nine sites (T3.4b, director-flagged)

**The finding.** `minSpacingKm` is 480 (T3.2b). The dev continent admits at most **nine**
settlements: `settlement siting could only place 9 of 12 sites at minSpacingKm 480 — terrain too
small or spacing too large`. The 12-settlement world used as the pre-T3.2b migration baseline is
no longer constructible.

**Why this is blocking rather than a queue line.** M4's colonization packet means **founding new
settlements**, and there is nowhere to put the tenth. That leaves three possibilities, and the
spec must choose one:

1. **Spacing becomes colonization-aware** — `minSpacingKm` is a worldgen siting rule today; it
   would become a *founding* rule with different semantics (a daughter colony sited near its
   parent is the normal historical pattern, not a violation). This is the option that makes
   colonization mean what it says.
2. **The continent grows** — a larger terrain raster, which moves every worldgen golden and
   re-opens the T3.2b spatial calibration.
3. **Expansion saturates at nine** — and this is the one the spec must not choose by default,
   because of what it implies below.

**The reason it matters, in the terms CR-003 already settled.** If expansion saturates at nine
sites, population growth eventually presses against a land supply that cannot extend, and the
Malthusian transition arrives by **MAP EXHAUSTION rather than by land filling** — the trap
hardwired by geometry. CR-003's ruling is explicit that the trap must EMERGE when land fills and
"must never be hardwired, and it must never be restored by choosing a constant that reproduces
the old crash." A spacing constant that caps the world at nine settlements is exactly such a
constant, arriving through the back door: it would not look like a hardwired Malthus, it would
look like a full continent, and the distinction would be invisible in every metric the
calibration battery measures.

**What the spec must state.** Which of the three options is taken; if (1), the founding-rule
semantics and how they differ from siting; if (2), the golden and calibration blast radius; if
(3), an explicit argument for why saturation-by-geometry is acceptable and how it is
distinguished, in the metrics, from land genuinely filling.

**Evidence:** `docs/t3.4b-migration-evidence.md` (addendum), `docs/adr/cr-003.md`,
`Sim.Core/Worldgen/SettlementSiting.cs:180`.
