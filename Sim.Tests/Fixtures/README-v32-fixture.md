# The schema-v32 snapshot fixture (D-054 / ADR-039)

`v32-seed42-turn40.civsnap` is a REAL schema-v32 save, written by the batch-2 build (de86ace plus the
uncommitted batch-2 tree, `scratchpad/base-b2.tar`; its manifest verified by `mkclone.sh`) before any
v33 code existed. It is the input of the explicit v32 -> v33 migration test
(`Sim.Tests/State/ConstructionSchemaV33Tests.cs`).

Command (run with that build's `Sim.Cli`):

    sim run --founded --seed 42 --turns 40 --ai-empires 1 --orders v32-fixture-orders.bin \
            --save-at 40 --save v32-seed42-turn40.civsnap

`v32-fixture-orders.bin` (3 records, all by the player, polity 1):

| turn | kind | target | amount | meaning |
|---|---|---|---|---|
| 0 | 3 SectorAllocation | 4 (settlement 0, construction) | 0 | settlement 0's construction share to 0 |
| 1 | 4 EnqueueConstruction | 0 | 1 | a granary at settlement 0 |
| 2 | 4 EnqueueConstruction | 0 | 1 | a second granary behind it |

With no builders at settlement 0 the v32 (M4-D) rule never builds either, so the save holds a NON-EMPTY
queue: two entries at settlement 0 (slots 0 and 1, project 1). The v32 build reported:

- `run complete: seed 42, 40 turns, hash b000b2ee26cc02310e6e379f9b7c4bad2b9568e41e6080be85056b3ebdb92361`
- `sim hash v32-seed42-turn40.civsnap` -> `b000b2ee26cc02310e6e379f9b7c4bad2b9568e41e6080be85056b3ebdb92361`
- file SHA-256 `4bbe50c6f5ed11ce80fa092b68f31ef8f622b0e3ca3e09ddbe6e173dbd7baf1a`, orders SHA-256
  `c1ef798243965b6952c0da041027bd055628ba7382c84f212a47c4232b83ccde`.

The v33 build loads it (the two v33 tables empty, no topology trailer, a flat world) and its v32
projection (`SchemaProjection.HashAtSchemaV32`) equals `b000b2ee…`.
