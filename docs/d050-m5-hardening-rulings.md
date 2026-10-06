# D-050 — M5 hardening rulings (Director, 2026-10-06)

**Status: RATIFIED.** The Director answered the open questions left by the M5 hardening pass
(baseline `docs/m5-playtest-baseline.md` §15, `docs/d049-taxation-and-revolt-model.md` §11/§15,
`docs/adr/adr-034-order-validation-deferral.md`, `docs/m5-hardening-measurements.md` F2).
Build under these rulings: `m5-hardening` @ `6ef1596` (no code change follows from them).

1. **ADR-034 ratified as is.** Order validation defers actor/settlement checks for ids absent at
   turn 0 to delivery time (revolt-founded polities, colonies). ADR-034 status: PROPOSED → RATIFIED.
2. **Forager food stays 4.3 per gatherer / 2.0 per km².** Order-free worlds may starve in bad weather
   (11/20 canonical seeds); a player who neglects food can starve. The 5.0/3.0 alternative is rejected.
3. **Population segments are classes.** Finer segments are deferred to M9 Society.
4. **Rebel count is a deterministic expected fraction; `uprisingGrievance` = 20.**
5. **A last settlement under a permanent 100 % levy may sit in permanent revolt** (food floored at the
   untaxed level). No relief mechanic.
6. **State-capacity factor `taxCapacityOffsetMax` = 0.25** (weak reach makes the same levy feel up to
   25 % heavier).
7. **Capital succession deferred to M8 Politics / Diplomacy.** Revolt-founded civilizations have no
   capital and cannot tax until then.
8. **Playtest item:** the 1080×640 minimum-window snap-back is untested in a real window; the Director
   will check it during the personal playtest.

These turn the corresponding INFERRED entries in d049 §11/§15 and the OPEN entries in the baseline
§15 into RATIFIED. Historical text in those records is not edited.
