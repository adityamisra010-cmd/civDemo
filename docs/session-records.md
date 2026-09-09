# WHAT A PLAYED SESSION LEAVES BEHIND

Four files in `runs\`, all sharing one stamp so a session's files sort together
and cannot be confused with another's:

```
runs\session-20260909-143022-s256-n4.json     the manifest — how to rebuild this world
runs\orders-20260909-143022-s256-n4.bin       the order log — what you ordered, and when
runs\trace-20260909-143022-s256-n4.csv        the turn trace — what happened, turn by turn
runs\chronicle-20260909-143022-s256-n4.txt    the annals, as the panel showed them
```

## The defect this closed

The order log recorded what the director ordered but **not which world he ordered
it in**. The seed lived only in the argv of a process that had since exited, so a
session log was, strictly, unreplayable — the one input needed to rebuild the
world was the one input nobody wrote down. `sim replay` could always reproduce a
session; there was no way to know what to hand it. Every analysis tool in the
repo, `ReplayReport` included, sat behind that missing number.

The manifest is written **once, at launch, before the window opens**, so a
session that ends in a crash or a force-quit is still reproducible.

## Answering "around turn 85 something went wrong"

```
sim inspect --manifest runs\session-20260909-143022-s256-n4.json --turn 85
```

Which prints the world it rebuilt, whether the session reproduced, the orders
that landed on that turn, and the turns either side with the change in each
column beside it:

```
reproduction VERIFIED: 91 turns, hash-for-hash.

--- turn 85 (+/- 3) ---
  ORDER  SectorAllocation target 0 = 50  (Empire 1)
  ORDER  SectorAllocation target 1 = 10  (Empire 1)
  ...
   turn     year   population            food   settlements
      84      840       2,235       +22         2,437          +3       4
 >    85      850       2,253       +18         2,457         +20       4
      86      860       2,267       +14         1,859        -598       4
      87      870       2,286       +19         2,499        +640       4
```

The order and its consequence are on the same screen. The delta column is the
point: "food 1,859" says little; "food 1,859 (−598)" is the thing you saw.

Other forms:

- `sim inspect --manifest ...` with no `--turn` reports **every turn an order was
  issued on** — the turns you made a decision are the turns worth looking at.
- `--window K` widens or narrows the turns shown either side (default 3).
- `--report-jsonl out.jsonl` additionally dumps the FULL per-turn state through
  the existing `ReplayReport` — stocks, prices, per-class needs, grievance, class
  counts, sector mixes — for when the six trace columns are not enough.

## Why the trace is written live when replay could derive it

So the two can be **compared**. Each trace line ends in the world hash the
director's machine actually computed. Replaying the log recomputes them; if any
turn disagrees, the session did not reproduce, and `sim inspect` names the first
turn where it stopped:

```
REPRODUCTION FAILED at turn 63: the session recorded 9d474ec2a587…,
this replay computed c4a55eb3d575…. Every turn before it matches, so
that turn is where the two diverge.
```

That is a **determinism finding**, not a reporting nicety. Without a live record
there would be nothing to compare a replay against, and "it replays fine" would
be a claim about the replay only.

## What this is not

It is **not a savegame**. You still cannot quit at turn 60 and resume at turn 60;
you replay from turn 0, which is fast but is not the same thing. Snapshot
save/load exists in the kernel (`Snapshot`, `sim hash`) and remains unwired to
the UI — that gap is unchanged and is still recorded under missing player agency
in the M4 playtest record.

Nothing here is state. The manifest is the session's argv plus the identity of
the build that ran it; the trace is six columns read off the world. Neither is
consulted by any system, and no schema version, golden, corridor or quarantine
moved for either.
