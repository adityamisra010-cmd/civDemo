# The schema-v33 fixtures (D-055, schema v34)

`v33-seed42-turn2.civsnap` (a save) and `v33-session-q17/` (a session record) are REAL schema-v33 records, written
on 2026-10-16 by the BATCH-3 build before any v34 code existed. They are the input of the v33 -> v34 migration
tests (`Sim.Tests/State/ConstructionHoldsV33FixtureTests.cs`, `ConstructionHoldsSchemaV34Tests.cs`,
`ConstructionSchemaV33Tests.cs`) and of the UI resume of a v33 session (`Sim.Ui.Tests/V33SessionResumeTests.cs`).

## The build that wrote them

- Tree: base-b3 = the integration worktree's HEAD `de86ace16574206d8597ce9534251746a2f377c6` plus the batch-3 final
  patch `b3-final-v9` (sha256 `0f8f45c9ac57fca86499d7140fcfb341f5208d366cde52a4e7c2885fa040f2e9`), as the tar
  `base-b3.tar` (sha256 `bbdc3082a507420e7e186fd6199c31fa716de2c62ace8ba97c0ee75849a4cd33`). The scratch clone
  was made with `mkclone4.sh`, which printed "clone OK: matches batch-4 base manifest (batch-3 final)". No source
  file was changed.
- `Sim.Cli` Release, .NET 10.0.112, built with
  `dotnet build Sim.Cli/Sim.Cli.csproj -c Release -p:BuildSha=b3-final-v9-0f8f45c9 -p:BuildDate=2026-10-15
  -p:CustomAfterMicrosoftCommonTargets=<stamp-cli.targets>`. base-b3's `Sim.Cli.csproj` does not consume
  `-p:BuildSha`/`-p:BuildDate` (only `Sim.Ui.csproj` declares `AssemblyMetadata`), so an OUT-OF-TREE targets file,
  imported through `CustomAfterMicrosoftCommonTargets`, added the two `AssemblyMetadata` items `CliBuildInfo` reads
  (metadata only). The manifest therefore names the build: `"buildSha": "b3-final-v9-0f8f45c9"`,
  `"buildDate": "2026-10-15"`.

## The commands

`q.bin` (here `v33-fixture-orders.bin`) was written by a small Python script in the OrderLog format
(`magic "CIVORDR\0" | int32 version 1 | int32 count | records: int64 turn, int32 actor, int32 kind, int32 target,
float64 amount`, little-endian; `scripts/gen-sample-orders.py` writes the same format). Then, with that build's CLI:

    sim run --founded --seed 42 --size 256 --settlements 4 --ai-empires 1 --turns 6 --orders q.bin \
            --save-at 2 --save v33-seed42-turn2.civsnap --emit-session session

The four player records (polity 1):

| turn | kind | target | amount | meaning |
|---|---|---|---|---|
| 0 | 12 SetResourceAllocation | 1 (its own Empire) | 30 | building 30 shares in 100, road works 70 |
| 0 | 4 EnqueueConstruction | 0 | 1 | a granary at settlement 0 |
| 0 | 4 EnqueueConstruction | 2 | 1 | a granary at settlement 2 |
| 1 | 3 SectorAllocation | (0 << 3) \| 4 | 0 | settlement 0's construction share to 0 (no builders) |

The session's run log (`orders-f1cc70bce302a9a0.bin`) holds those four and the AI Empire's own order, written by the
run: `(0, 2, 6 SetResearchTarget, 2, 0)`.

The fourth record forms the D-054 Q17 shape. In the turn-2 world (the save) s0's granary is part-built at
p = 700,000 and s2's is at p = 0; both were ordered at turn 0 (EnqueuedTurn 0), and the player's split row is
(1, 30). With s0's builders gone, s2's granary, younger in the building order, progresses while s0 waits.

## What the batch-3 build printed

- `run complete: seed 42, 6 turns, hash 910f7b75bd6e61d384ef41874568c5e5f0e6f1831e8f9953eb5de86a79eed2f9`.
- The same run without `--emit-session` wrote a save `cmp`-identical to this one.
- `sim hash v33-seed42-turn2.civsnap --size 256` -> `705fb72b0c7d224f43d21b21783c4d35ebf72d648bcf7bea74ed3cc424484ce1`.
  This equals the trace's turn-2 row (the save is that run's turn-2 world).
- `sim inspect --manifest session/session-f1cc70bce302a9a0.json` -> `reproduction VERIFIED: 7 turns, hash-for-hash.`,
  with the recipe `sim replay --founded --seed 42 --size 256 --settlements 4 --ai-empires 1 --orders
  orders-f1cc70bce302a9a0.bin --turns 6`.
- That recipe, run from the session directory, printed
  `replay complete: seed 42, 6 turns, hash 910f7b75bd6e61d384ef41874568c5e5f0e6f1831e8f9953eb5de86a79eed2f9`.
- The trace (`trace-f1cc70bce302a9a0.csv`) records turns 0 to 6; its hash column is the batch-3 world hash of each
  turn.

## Files and SHA-256

| file | sha256 |
|---|---|
| `v33-seed42-turn2.civsnap` | `def9cdf2137c80bae261969a926989bf02127168834242756feb5be1dc608a88` |
| `v33-fixture-orders.bin` (`q.bin`) | `7e08f18372f80368e4242d2219d6491e40c4a197f7a6c958b5d1f08d00e3366f` |
| `v33-session-q17/session-f1cc70bce302a9a0.json` | `d4ce6e0315e484de9ee7110d21406d0b85c760dfd002a2861967378ec1c53877` |
| `v33-session-q17/orders-f1cc70bce302a9a0.bin` | `0a67362f9aee2dfed508aab51db89e544f80f7d19791b90e60a7ba2625ad19ca` |
| `v33-session-q17/trace-f1cc70bce302a9a0.csv` | `411eeaccdac61aa1f7d6116e018cd1f2bdfb8299ca81dc34615421acc18c8a62` |

The run also wrote a telemetry file (sha256 `e107d143…`) and a forensic record (sha256 `19496a1f…`). They are not
checked in: resume and `sim inspect` without `--answer` read only the manifest, the orders and the trace. The manifest
names a chronicle file that a headless run does not write.

## What the v34 build checks

- (a) The loaded save's v33 projection (`SchemaProjection.V33`: ConstructionHolds, section 61, dropped and asserted
  empty) is `705fb72b…`.
- (b) The loaded world is the world an in-process `TestConfigs.PreReservations` run of the session's orders reaches at
  turn 2.
- (c), (d) Loaded, and re-saved as v34 and reloaded, it continues under the shipped executor exactly as that run's
  turn-2 world does. The v34 re-save is the v33 file with header 34 and one empty count prefix before the trailer.
- Premise (i): the session replayed under `PreReservations` reproduces every trace hash through `SchemaProjection.V33`.
- Premise (iii) (Sim.Ui.Tests): `UiSession.Resume` gives the four-line notice, the cylinder, and the fresh replay's hash.
- Premise (iv): `sim inspect --manifest` prints `SessionSchemaNotice.NotChecked(33)` and exits 0.
