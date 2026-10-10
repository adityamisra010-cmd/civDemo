using System.Security.Cryptography;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Disaster;

namespace Sim.Tests.Kernel;

/// <summary>
/// M4 INTEGRATION — THE CONTROL THAT ATTRIBUTES EVERY MOVED PIN.
///
/// Three goldens move on the integrated tree (T4.4 schema v22 + M4 schema v23 +
/// the granary capacity-floor fix), and "schema only" is a claim that has to be
/// MEASURED, not asserted. This is the same control T4.4 used for its own
/// widening, run the other way round.
///
/// The control REMOVES the Empire state from a finished world and re-hashes. If
/// what comes back is the earlier pinned value byte for byte, then the Empire
/// state is the entire delta and no simulation state moved with it.
///
/// TWO LAYERS are separated, because M4 moved these pins twice for different
/// reasons: M4-A's schema v23 LAYOUT (two count prefixes, present even when the
/// tables are empty) and M4-C's founding CONTENT (the rows themselves).
///
/// T4.10 CHANGED WHAT THIS CONTROL CAN CLAIM, and the honesty of that matters more
/// than the convenience of leaving the old numbers in place. Until T4.10 these
/// stripped values equalled main's PRE-M4 pins, which is what licensed the phrase
/// "moved for the M4 schema alone". T4.10 removes the food term from migration
/// attractiveness — a genuine BEHAVIOURAL change — so the three worlds it reaches
/// no longer reduce to any pre-M4 value, and the reference constants below are
/// re-measured on this tree. The control therefore no longer proves "M4 layout is
/// the only delta since pre-M4"; it proves the WEAKER and still-useful thing, that
/// the M4 rows are separable from the simulation output they sit beside.
///
/// THE NO-UNRELATED-MOVEMENT CONTROL MOVED WITH IT, and it is the one that carries
/// the weight now: GoldenHashSeed42Turn200 below is SYNTHETIC and terrain-less, so
/// migration cannot reach it. Its stripped value is still main's pre-M4 pin,
/// untouched by T4.10 — which is the measurement showing this change stayed inside
/// migration instead of leaking somewhere it had no business being.
///
/// M4-C GENERALISED THIS. The original control stripped the two four-byte zero
/// count prefixes off the end of the stream, which was exact while nothing wrote
/// the tables — but M4-C's founding DOES write them, and `Controls` sits
/// mid-stream rather than in the trailer, so byte-stripping no longer expresses
/// the question. Clearing the three tables does, for empty and populated worlds
/// alike, and it degenerates to exactly the old trailer strip when they are empty.
///
/// T4.21-1 (CR-015) ADDED A THIRD LAYER, and it is the one this packet is
/// judged on: schema v25 appends the Disasters table (EMPTY in every canonical
/// world — the packet ships hazardPerYear = 0, so no row is ever written) and
/// DisasterSystem draws two uniforms per settlement per turn UNCONDITIONALLY, so
/// every founded world gains one RngStreamRow per settlement. Those two are the
/// ENTIRE delta the packet may make: <see cref="StripDisaster"/> removes the
/// disaster streams and clears the table, <see cref="HashAtSchemaV24"/> also
/// drops the empty v25 trailer, and the PRE-PACKET pins must return BYTE FOR
/// BYTE on every pinned world. FoodState and FoodHeadroom are statics nothing
/// calls yet, and ProductionSystem multiplies by exactly 1.0 without a strike —
/// this control is the measurement that says so, on the tree, not the argument.
/// The v22/v23 controls below strip the disaster layer too, so every constant
/// they carry is UNMOVED by this packet.
///
/// T4.21-2 (ADR-025, bounded migration) AND T4.21-3 (CR-015 / ADR-026, the
/// effective deficit and the headroom growth cap) ARE BOTH BEHAVIOURAL, and —
/// exactly as T4.10 did — they change what the controls can claim. Both move
/// every founded world (SnapshotTests.FoundedGolden has the joint cause), so
/// the stripped values no longer return any pre-T4.21 pin: every founded /
/// FirstReign / driven constant in this file is RE-MEASURED ON THE MERGED TREE
/// by the agent writing this line (ADR-015 §6), and what the strips prove is
/// the weaker, still-useful thing that the disaster streams and table, and the
/// M4 ladder's tables, are separable from the simulation output they sit
/// beside; "moved for the layout alone" is a historical fact about commit
/// 1735d41, not a property of this tree. The synthetic terrain-less world
/// (GoldenHashSeed42Turn200 — no distances, so no migration; no demand rows, so
/// the growth cap never reads it) is the one that carries the weight now: BOTH
/// its controls are UNMOVED and still return main's pre-M4 (0f94b4ad…) and the
/// pre-T4.21 (eec82711…) values byte for byte, re-measured on the merged tree,
/// which is the measurement showing the two packets stayed inside the founded
/// worlds' migration and demographics instead of leaking anywhere they had no
/// business being.
///
/// ADR-029 (D-044) ADDS A FOURTH LAYER: schema v26 appends the seven research
/// tables — ResearchTargets, ResearchProgress, ResearchCompleted, ResearchEurekas,
/// ResearchCostModifiers and (addendum A) ResearchCredits and ResearchExposures —
/// AFTER Disasters, and the production pipeline gains
/// the ResearchSystem as its last entry. The system writes ONLY those tables. No
/// other system reads them, and the system draws no RNG and records no ledger flow.
/// Under the curated Eurekas of addendum A an order-less world writes NO research
/// row in 300 turns (measured), so each research control also runs a research-DRIVEN
/// arm, a target chosen whenever one is free, which does. <see cref="StripResearch"/> clears
/// the seven tables, and <see cref="HashAtSchemaV25"/> also drops their seven empty
/// count prefixes. The PRE-PACKET pins — the values on main at 93270cd — must
/// return BYTE FOR BYTE on every pinned world. Any leak of research into
/// population, food, trade, migration or anything else would survive the strip and
/// break them. Every older control in this file strips the research layer too and
/// drops its seven prefixes, so every constant it carries is UNMOVED by this packet.
///
/// ADR-035 (P-F1, founding-turn harvest) RE-MEASURED EVERY FOUNDED / DRIVEN / FIRST-REIGN CONSTANT BELOW (BEHAVIOUR, one cause: an absent catchment row is an unmeasured land side, so the founding turn harvests (labour-limited) instead of harvesting zero; turn 2 no longer reads N_lim = 0.).
/// Measured on that commit's tree by the agent writing this line; the strips still separate their layers, but no
/// stripped value returns a pre-ADR-035 (P-F1, founding-turn harvest) pin. Old -> new (16-hex prefixes):
///   02c7f9eb0d08bf0a… -> 685f39c8e87be2da…
///   0460e6e916d1b2bb… -> 27fca538e9b9be22…
///   1368df9fb3d233b1… -> 06fd665d17fedcf9…
///   1588d1c4a9597a5c… -> 4aab29e95f39b3b8…
///   158bdd4cd2ee9cc3… -> a90be19c30924388…
///   15c63d6564ff8092… -> 7f4d6a3ebbab4e4b…
///   23b0db02a8e2cfef… -> 689b69e7702b6c8a…
///   259c13cf27ea3bed… -> 4116a0c5535a1b6a…
///   3613dcc4aa059755… -> dc2ff415a857df6b…
///   364760291d6dd691… -> 0b8268d9949be027…
///   38de16bab469505e… -> d7a3c5ba00c0313e…
///   3a9f007aeeb6dcd9… -> c3f0635213306771…
///   4291b3e15006e3f1… -> a65a24b742237bee…
///   464aac3d5ece3ea9… -> fa8d2cc5811b1a36…
///   481d37170d7f70f3… -> 0af5f3101f59154d…
///   58e4c47cca90b2e4… -> 6183391c6da69699…
///   638d7a0914f0475f… -> 9551e3a1d4b3ea99…
///   64820f83239f005e… -> 8d5657d2043c797c…
///   65d53a01ffe1b3e9… -> e983c416ac52f74e…
///   680a20c5616520f2… -> 31bffbe34abf3d93…
///   68c629b66badfa71… -> c8e22b49a80cdad9…
///   6c901bab51b34819… -> 16c8b2062ab6c965…
///   74306d6a574b6a68… -> db9e0f35a0a54111…
///   74a97abc39d11910… -> 314d42680a0f14bf…
///   75124c12e61f8999… -> de9bf8c08cf55672…
///   7aa20e40f4d3ba9f… -> 93756ec9c8c5ec68…
///   97d89a9afd9ed784… -> d26c53cc94522135…
///   98ee3a7acdcad9a9… -> 1339be6cf6f254a1…
///   b2c0032f9e0a7266… -> 6b7261eacd17c3e3…
///   c20bb5a688c132ad… -> e186250043a1aa43…
///   c805ca10e1e24ae6… -> e9f5f73379cf6f14…
///   dacf3c34824a8667… -> 0d1103ffddac7f15…
///   db7c7a0907ad4335… -> 1f3a31573b05455a…
///   e3d472c6de20e703… -> e901150904d66946…
///   e4279f655c5c25d8… -> 09de018d00555750…
///   efeec45dbb387ddd… -> f40ba5c39e5eecb9…
///   f94b01eb509853e2… -> 70c58da639e83df1…
///   fc5d2646bdb8bf18… -> d4e40301486266c8…
///
/// ADR-035 §6 (P-F0, founding death remainder) RE-MEASURED EVERY FOUNDED / DRIVEN / FIRST-REIGN CONSTANT BELOW (BEHAVIOUR, one cause: a new bucket row's D-004 death accumulator is seeded at 0.5, so the first integer reconciliation rounds instead of flooring (the turn-1 phantom survivors are gone).).
/// Measured on that commit's tree by the agent writing this line; the strips still separate their layers, but no
/// stripped value returns a pre-ADR-035 §6 (P-F0, founding death remainder) pin. Old -> new (16-hex prefixes):
///   06fd665d17fedcf9… -> 63865fe79cb4a61a…
///   09de018d00555750… -> 1b50efabaeba7388…
///   0af5f3101f59154d… -> 6e2583b65d2410f8…
///   0b8268d9949be027… -> f0887401409f5e59…
///   0d1103ffddac7f15… -> 5ce8c62ebc2b9eca…
///   1339be6cf6f254a1… -> 6eb28e4f7de2529d…
///   16c8b2062ab6c965… -> 3982c2f5434fb12b…
///   1f3a31573b05455a… -> 633c778dca6831b6…
///   27fca538e9b9be22… -> ffec140b76cb4de1…
///   314d42680a0f14bf… -> 346a2dafe70e1569…
///   31bffbe34abf3d93… -> 2cdd0faf8cfd6cb3…
///   4116a0c5535a1b6a… -> cfc5c4b639a8555c…
///   4aab29e95f39b3b8… -> 27159f00b3cbbf89…
///   6183391c6da69699… -> 404cc27c06c417ca…
///   685f39c8e87be2da… -> b7d067735340b2bb…
///   689b69e7702b6c8a… -> b71fea551d024798…
///   6b7261eacd17c3e3… -> 2c94049c958c2a8c…
///   70c58da639e83df1… -> 62ac1518b0daf103…
///   7f4d6a3ebbab4e4b… -> 3ff4db18e2fe9205…
///   8d5657d2043c797c… -> 9ebece4c7ed96da3…
///   93756ec9c8c5ec68… -> afc650595e696cf5…
///   9551e3a1d4b3ea99… -> 608e8a527c850a05…
///   a65a24b742237bee… -> e62b53343aad5efd…
///   a90be19c30924388… -> 527385ff349dbb4c…
///   c3f0635213306771… -> 0616a52724cb3685…
///   c8e22b49a80cdad9… -> 90b7815fb63e4f62…
///   d26c53cc94522135… -> 5eee047cba9e4537…
///   d4e40301486266c8… -> c0f319c48831c35d…
///   d7a3c5ba00c0313e… -> 917437d80adcbc64…
///   db9e0f35a0a54111… -> 3f300220a2e81381…
///   dc2ff415a857df6b… -> 788ffe859918263c…
///   de9bf8c08cf55672… -> c4f434a0a1f4361f…
///   e186250043a1aa43… -> 63df2cb846f6146b…
///   e901150904d66946… -> 1716ad2a5eea5154…
///   e983c416ac52f74e… -> 9ceb9c3383372626…
///   e9f5f73379cf6f14… -> 55d28172e13fe19d…
///   f40ba5c39e5eecb9… -> 7b94dbf1b9bf905b…
///   fa8d2cc5811b1a36… -> d988353f11837816…
///
/// ADR-035 §7 (P-F2, founding composition) RE-MEASURED EVERY FOUNDED / DRIVEN / FIRST-REIGN CONSTANT BELOW (BEHAVIOUR, one cause: founding cohort noise at demographic scale (CV ~ 1/sqrt(n_c)) instead of RC-1's per-cohort ±0.69; the settlement-common size factor is unchanged.).
/// Measured on that commit's tree by the agent writing this line; the strips still separate their layers, but no
/// stripped value returns a pre-ADR-035 §7 (P-F2, founding composition) pin. Old -> new (16-hex prefixes):
///   0616a52724cb3685… -> b252a66db4ca2a03…
///   1716ad2a5eea5154… -> 1256f051a0747724…
///   1b50efabaeba7388… -> 8ea1b956ef550ca5…
///   27159f00b3cbbf89… -> 431f2f48a0031017…
///   2c94049c958c2a8c… -> b14899eb23a9704f…
///   2cdd0faf8cfd6cb3… -> 6a6ac746181c47f1…
///   346a2dafe70e1569… -> 62ee79880d95ebeb…
///   3982c2f5434fb12b… -> c4833d844a1c10b2…
///   3f300220a2e81381… -> 9fe68e4ef8cc7327…
///   3ff4db18e2fe9205… -> db52e65e932503dc…
///   404cc27c06c417ca… -> b20df92d2e2ed05a…
///   527385ff349dbb4c… -> 8bc2299e796cae8d…
///   55d28172e13fe19d… -> 7813772b2cee6d5f…
///   5ce8c62ebc2b9eca… -> 0939beb30ae683d3…
///   5eee047cba9e4537… -> 64bb34959bb3d186…
///   608e8a527c850a05… -> 65f5e942fd4b58c9…
///   62ac1518b0daf103… -> aa5ae46d574a1e23…
///   633c778dca6831b6… -> 17464c3f152e89c8…
///   63865fe79cb4a61a… -> 6153222490f6d9a8…
///   63df2cb846f6146b… -> b31684a74926d97f…
///   6e2583b65d2410f8… -> b6013cb54fffaa91…
///   6eb28e4f7de2529d… -> a3a57867172c09a8…
///   788ffe859918263c… -> 653c5c0ece72b390…
///   7b94dbf1b9bf905b… -> d47b7e3d1bb81bce…
///   90b7815fb63e4f62… -> 922ed0c1dfa427cc…
///   917437d80adcbc64… -> 8cd5db52cfc2a4e1…
///   9ceb9c3383372626… -> bce1a3a8a7ab61a4…
///   9ebece4c7ed96da3… -> 6f82a998c06113fe…
///   afc650595e696cf5… -> 6dcc265b9e8d68b6…
///   b71fea551d024798… -> 05da2ccea08c6e91…
///   b7d067735340b2bb… -> 416c0ebe3237b25d…
///   c0f319c48831c35d… -> 4cdad2ddce909e40…
///   c4f434a0a1f4361f… -> 91439b05e8a8c440…
///   cfc5c4b639a8555c… -> 080315066ef7b142…
///   d988353f11837816… -> 320884dd6f6d3d61…
///   e62b53343aad5efd… -> df85224cb7a2ab5f…
///   f0887401409f5e59… -> b94886cfb24df556…
///   ffec140b76cb4de1… -> 811b119acf46f50e…
/// </summary>
public class IntegratedPinAttributionTests
{
    /// <summary>
    /// The driven world at T4.4's schema v22 WITH the capacity-floor fix — i.e.
    /// main's tree plus the behavioural fix and nothing else. Measured on the
    /// integrated tree by <see cref="HashAtSchemaV22"/>; the midpoint that lets
    /// the two causes of the driven pin's movement be reported separately.
    /// </summary>
    /// T4.19 (CR-014 ruled): re-measured on this tree with HashAtSchemaV22 —
    /// the driven world moved for lane C's founding vector (the cap change alone
    /// returns the old vector's pin byte for byte; DrivenGoldenTests has the arms).
    /// OLD 611a1508e650c9b897e3ec3ec0884969ae3add4d8de520fa5a126efbb71926ea.
    /// T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour, both
    /// packets — see the header and SnapshotTests.FoundedGolden).
    /// OLD (pre-packet) 60bd5b208696a25f93469d58d4a4284d8ae8467107aeee3545d3d0f82b61ba14;
    /// OLD (T4.21-2 alone) 789585bed857ae94888ee8edbdc18b1ee5c9222c23695109b86e7002271b194d;
    /// OLD (T4.21-3 alone) 23cdc7042bf2972ba13b52e9e6bb26a8fc526a74ba61284c93208bb73efadffd.
    internal const string CapacityFloorFixAtSchemaV22 =
        "1256f051a074772479b880411bead0433aa662876d282f00ead1566a0ac62c79";

    /// <summary>Bytes one empty table contributes to the stream: its count prefix.</summary>
    private const int EmptyTableBytes = 4;

    /// <summary>
    /// The finished world with EVERY M4 row removed — Polities, Controls,
    /// Capitals (v23) and the construction queue and structures (v24) — then
    /// re-serialized with <paramref name="dropTrailingTables"/> of the trailing
    /// empty count prefixes also removed. Operates on a deep Clone.
    ///
    /// That parameter is what lets one control answer three different questions:
    /// drop 4 tables and the stream is T4.4's v22, before M4 existed; drop the 2
    /// v24 tables and it is v23, the tree as it stood before M4-C; drop none and
    /// it is v24 with M4's content removed but its layout intact. Each is a real
    /// earlier pin, so each comparison is against a measured value rather than a
    /// recomputed one.
    /// </summary>
    private static string HashWithoutM4(WorldState world, int dropTrailingTables)
    {
        WorldState stripped = world.Clone();
        StripLevy(stripped);
        StripInstitutions(stripped);
        StripGovernance(stripped);
        StripRoads(stripped);
        StripAges(stripped);
        StripResearch(stripped);
        StripDisaster(stripped);
        stripped.Polities.Clear();
        stripped.Controls.Clear();
        stripped.Capitals.Clear();
        stripped.ConstructionQueue.Clear();
        stripped.Structures.Clear();

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }

        return HashDroppingTrailer(buffer.ToArray(), dropTrailingTables + AgeTableCount + RoadTableCount + GovernanceTableCount + InstitutionTableCount + LevyTableCount);
    }

    private static string HashDroppingTrailer(byte[] full, int dropTrailingTables)
    {
        int drop = dropTrailingTables * EmptyTableBytes;
        for (int i = full.Length - drop; i < full.Length; i++)
        {
            Assert.Equal(0, full[i]);   // what is dropped really is empty tables
        }

        return Convert.ToHexStringLower(SHA256.HashData(full.AsSpan(0, full.Length - drop).ToArray()));
    }

    /// <summary>
    /// T4.21-1: remove the packet's two layout contributions IN PLACE — the
    /// Disasters rows (none exist at hazard 0; cleared regardless so a populated
    /// world strips the same way) and DisasterSystem's RngStreams rows, which
    /// the registry appended lazily on turn 1 AFTER HarvestWeather's and which
    /// therefore sit in the stream exactly where removing them restores the
    /// pre-packet row sequence. Returns how many stream rows were removed, so a
    /// caller can assert the strip was not vacuous.
    /// </summary>
    private static int StripDisaster(WorldState stripped)
    {
        stripped.Disasters.Clear();
        var kept = new List<RngStreamRow>(stripped.RngStreams.Count);
        int removed = 0;
        for (int i = 0; i < stripped.RngStreams.Count; i++)
        {
            RngStreamRow row = stripped.RngStreams[i];
            if (row.System == DisasterSystem.WellKnownId) { removed++; continue; }
            kept.Add(row);
        }
        stripped.RngStreams.Clear();
        for (int i = 0; i < kept.Count; i++) stripped.RngStreams.Add(kept[i]);
        return removed;
    }

    /// <summary>The stream as T4.4's v22 — before M4 touched the schema at all.
    /// Ten trailing empty tables since v26: Polities, Capitals (v23),
    /// ConstructionQueue, Structures (v24), Disasters (v25) and the seven research
    /// tables (v26).</summary>
    private static string HashAtSchemaV22(WorldState world) => HashWithoutM4(world, 5 + ResearchTableCount);

    /// <summary>The stream as v23 — M4-A's tables present but empty, i.e. the
    /// tree exactly as it stood before M4-C's founding wrote them.</summary>
    private static string HashAtSchemaV23(WorldState world) => HashWithoutM4(world, 3 + ResearchTableCount);

    /// <summary>ADR-029: the seven v26 research tables, appended after Disasters (two of
    /// them, ResearchCredits and ResearchExposures, by addendum A).</summary>
    private const int ResearchTableCount = 7;

    /// <summary>ADR-031: the five v28 Age/military tables, appended after ResearchExposures.
    /// Every pin in this file predates v28, so every control strips them and drops their
    /// five count prefixes first (the two Age systems draw no RNG and write no ledger flow;
    /// founding formations are tokens), then asks its original question unchanged.</summary>
    private const int AgeTableCount = 5;

    private static int StripAges(WorldState stripped)
    {
        int removed = stripped.AgeStates.Count + stripped.AgeEligibility.Count + stripped.AgeTransitions.Count
            + stripped.MilitaryUnits.Count + stripped.UnitConversions.Count;
        stripped.AgeStates.Clear();
        stripped.AgeEligibility.Clear();
        stripped.AgeTransitions.Clear();
        stripped.MilitaryUnits.Clear();
        stripped.UnitConversions.Clear();
        return removed;
    }

    /// <summary>The stream as v26 — the tree exactly as it stood BEFORE ADR-031: the Age/military
    /// rows removed, the five empty v28 prefixes dropped, nothing else touched.</summary>
    internal static string HashAtSchemaV26(WorldState world, out int ageRowsRemoved)
    {
        WorldState stripped = world.Clone();
        StripLevy(stripped);
        StripInstitutions(stripped);
        StripGovernance(stripped);
        StripRoads(stripped);
        ageRowsRemoved = StripAges(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), AgeTableCount + RoadTableCount + GovernanceTableCount + InstitutionTableCount + LevyTableCount);
    }

    /// <summary>ADR-032: the two v29 transport tables, appended after UnitConversions. Every pin in
    /// this file predates v29, so every control strips them and drops their two count prefixes
    /// first, then asks its original question unchanged.</summary>
    private const int RoadTableCount = 2;

    private static int StripRoads(WorldState stripped)
    {
        int removed = stripped.TransportEdges.Count + stripped.RoadDevelopments.Count;
        stripped.TransportEdges.Clear();
        stripped.RoadDevelopments.Clear();
        return removed;
    }

    /// <summary>The stream as v28 — the tree exactly as it stood BEFORE ADR-032: the transport rows
    /// removed, the two empty v29 prefixes dropped, nothing else touched.</summary>
    internal static string HashAtSchemaV28(WorldState world, out int roadRowsRemoved)
    {
        WorldState stripped = world.Clone();
        StripLevy(stripped);
        StripInstitutions(stripped);
        StripGovernance(stripped);
        roadRowsRemoved = StripRoads(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), RoadTableCount + GovernanceTableCount + InstitutionTableCount + LevyTableCount);
    }

    /// <summary>ADR-033 D4: the one v30 governance table (TaxPolicies), appended after
    /// RoadDevelopments. Every pin in this file predates v30, so every control strips the
    /// governance layer and drops its count prefix first, then asks its original question
    /// unchanged.</summary>
    private const int GovernanceTableCount = 1;

    /// <summary>
    /// ADR-033 D4: remove the governing loop's ENTIRE footprint in the stream, IN PLACE — the
    /// TaxPolicies rows (none exist in an order-less or tax-less world; cleared regardless so a
    /// populated world strips the same way) and the reach GovernanceSystem writes into
    /// ControlRow.Strength, restored to the 1.0 every control row carried before v30 (founding and
    /// colonization write exactly 1.0 and, before the port, nothing else ever wrote the field).
    /// GovernanceSystem draws no RNG and writes no ledger flow, and Strength is read only by the
    /// tax readers, so with no tax levied this IS the whole delta. Returns how many Strength
    /// fields were restored, so a caller can tell a populated strip from a vacuous one.
    /// </summary>
    private static int StripGovernance(WorldState stripped)
    {
        stripped.TaxPolicies.Clear();
        int restored = 0;
        for (int i = 0; i < stripped.Controls.Count; i++)
        {
            ControlRow row = stripped.Controls[i];
            if (BitConverter.DoubleToInt64Bits(row.Strength) == BitConverter.DoubleToInt64Bits(1.0)) continue;
            stripped.Controls[i] = row with { Strength = 1.0 };
            restored++;
        }
        return restored;
    }

    /// <summary>The stream as v29 — the tree exactly as it stood BEFORE ADR-033 D4: the tax rows
    /// removed, every Strength back at 1.0, the empty v30 prefix dropped, nothing else touched.</summary>
    internal static string HashAtSchemaV29(WorldState world, out int strengthsRestored)
    {
        WorldState stripped = world.Clone();
        StripLevy(stripped);
        StripInstitutions(stripped);
        strengthsRestored = StripGovernance(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), GovernanceTableCount + InstitutionTableCount + LevyTableCount);
    }

    /// <summary>ADR-033 D6 + D10: the two v31 tables (Institutions, ConstructionLabor), appended after
    /// TaxPolicies. Every pin in this file predates v31, so every control strips the institutions layer and drops
    /// its two count prefixes first, then asks its original question unchanged.</summary>
    private const int InstitutionTableCount = 2;

    /// <summary>
    /// ADR-033 D6 + D10: remove the institutions layer's ENTIRE footprint in the stream, IN PLACE — the
    /// Institutions rows, the ConstructionLabor rows (D10, rebuilt every step; a row only where a project was
    /// built) and the ResearchCostModifiers rows InstitutionsSystem now rebuilds every step (empty in any world
    /// with no university, exactly as the table was empty before it had a writer). InstitutionsSystem draws no
    /// RNG and writes no ledger flow; with no institution the labour reader returns the raw adult count and the
    /// mortality seam the literal 1.0, and with no completed project PathBuild subtracts nothing — so in a world
    /// with neither, these rows (none) and two count prefixes are the whole delta. Returns how many rows were
    /// removed.
    /// </summary>
    private static int StripInstitutions(WorldState stripped)
    {
        int removed = stripped.Institutions.Count + stripped.ConstructionLabor.Count + stripped.ResearchCostModifiers.Count;
        stripped.Institutions.Clear();
        stripped.ConstructionLabor.Clear();
        stripped.ResearchCostModifiers.Clear();
        return removed;
    }

    /// <summary>The stream as v30 — the tree exactly as it stood BEFORE ADR-033 D6/D10: the institutions layer
    /// removed, the two empty v31 prefixes dropped, nothing else touched.</summary>
    internal static string HashAtSchemaV30(WorldState world, out int institutionRowsRemoved)
    {
        WorldState stripped = world.Clone();
        StripLevy(stripped);
        institutionRowsRemoved = StripInstitutions(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), InstitutionTableCount + LevyTableCount);
    }

    /// <summary>H2 (2026-10-05): the one v32 table (TaxGrievances — the levy's grievance per population segment),
    /// appended after ConstructionLabor. Every pin in this file predates v32, so every control strips it and drops its
    /// count prefix first, then asks its original question unchanged.</summary>
    private const int LevyTableCount = 1;

    /// <summary>H2: clear the TaxGrievances rows IN PLACE (the layer's entire footprint in the stream: rows exist only
    /// where a levy has been felt, so an untaxed world holds none). Returns how many rows were removed.</summary>
    private static int StripLevy(WorldState stripped)
    {
        int removed = stripped.TaxGrievances.Count;
        stripped.TaxGrievances.Clear();
        return removed;
    }

    /// <summary>The stream as v31 — the tree exactly as it stood BEFORE H2's schema change: the levy-grievance rows
    /// removed, the empty v32 prefix dropped, nothing else touched.</summary>
    internal static string HashAtSchemaV31(WorldState world, out int levyRowsRemoved)
    {
        WorldState stripped = world.Clone();
        levyRowsRemoved = StripLevy(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), LevyTableCount);
    }

    /// <summary>A pre-v32 pin's comparison form: the v31 stream of an UNTAXED run (no levy-grievance row to strip —
    /// asserted, so the comparison can never hide a behavioural change behind the strip).</summary>
    private static string V31(WorldState world)
    {
        string hash = HashAtSchemaV31(world, out int removed);
        Assert.Equal(0, removed);
        return hash;
    }

    /// <summary>
    /// ADR-029: clear the seven research tables IN PLACE. It returns how many rows
    /// were removed, so a caller can tell a populated strip from a vacuous one. The
    /// research system owns nothing else, draws no RNG and writes no ledger flow, so
    /// these tables are its entire footprint in the stream.
    /// </summary>
    private static int StripResearch(WorldState stripped)
    {
        int removed = stripped.ResearchTargets.Count + stripped.ResearchProgress.Count
            + stripped.ResearchCompleted.Count + stripped.ResearchEurekas.Count
            + stripped.ResearchCostModifiers.Count + stripped.ResearchCredits.Count
            + stripped.ResearchExposures.Count;
        stripped.ResearchTargets.Clear();
        stripped.ResearchProgress.Clear();
        stripped.ResearchCompleted.Clear();
        stripped.ResearchEurekas.Clear();
        stripped.ResearchCostModifiers.Clear();
        stripped.ResearchCredits.Clear();
        stripped.ResearchExposures.Clear();
        return removed;
    }

    /// <summary>
    /// The stream as v25 — the tree exactly as it stood BEFORE the research packet
    /// (main at 93270cd): the research rows removed, the seven empty v26 prefixes
    /// dropped, and NOTHING ELSE touched. The pre-packet pin must return byte for
    /// byte, or research changed something outside its own tables.
    /// </summary>
    private static string HashAtSchemaV25(WorldState world, out int researchRowsRemoved)
    {
        WorldState stripped = world.Clone();
        StripLevy(stripped);
        StripInstitutions(stripped);
        StripGovernance(stripped);
        StripRoads(stripped);
        StripAges(stripped);
        researchRowsRemoved = StripResearch(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), ResearchTableCount + AgeTableCount + RoadTableCount + GovernanceTableCount + InstitutionTableCount + LevyTableCount);
    }

    /// <summary>
    /// The stream as v24 — the tree exactly as it stood BEFORE T4.21-1: the
    /// disaster streams and table removed, the empty v25 trailer dropped, and
    /// NOTHING ELSE touched (M4's rows stay). The pre-packet pin must return
    /// byte for byte, or the packet changed behaviour.
    /// </summary>
    private static string HashAtSchemaV24(WorldState world, out int disasterStreamsRemoved)
    {
        WorldState stripped = world.Clone();
        StripLevy(stripped);
        StripInstitutions(stripped);
        StripGovernance(stripped);
        StripRoads(stripped);
        StripAges(stripped);
        StripResearch(stripped);
        disasterStreamsRemoved = StripDisaster(stripped);
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            CanonicalSchema.Write(stripped, writer);
        }
        return HashDroppingTrailer(buffer.ToArray(), 1 + ResearchTableCount + AgeTableCount + RoadTableCount + GovernanceTableCount + InstitutionTableCount + LevyTableCount);
    }

    /// <summary>
    /// T4.21-4 — THE λ = 0 TWIN THE LAYOUT CONTROLS RUN ON, and why; KEPT BY
    /// T4.21-7 NOW THAT λ = 0 IS THE SHIPPED VALUE AGAIN.
    ///
    /// T4.21-4 armed the famine-class disaster (sim.json
    /// disaster.hazardPerYear 0.0 -> 0.01). That is a BEHAVIOUR change and it
    /// moved the behavioural goldens, which were re-pinned where they live
    /// (SnapshotTests.FoundedGolden, DrivenGoldenTests.DrivenGolden). The
    /// controls in this file are LAYOUT controls: their question is whether a
    /// stream layer is SEPARABLE from the stream, not what the world did.
    /// Re-measuring them against the armed world would answer a different
    /// question and would quietly drop the attribution.
    ///
    /// So they run the λ = 0 twin, which — because DisasterSystem draws both
    /// its uniforms UNCONDITIONALLY (the stated RNG contract) — is bit-identical
    /// to the tree as it stood before the arming commit. Every constant in this
    /// file therefore returns BYTE FOR BYTE, and that was itself the
    /// attribution T4.21-4 owed: the arming was the ENTIRE cause of the
    /// behavioural goldens' movement, because nothing else in that packet
    /// touched code that runs.
    ///
    /// T4.21-7 DISARMS the shipped value (CR-016: the mechanism ships complete
    /// and tested but INERT; the RATE is the director's). The twin is therefore
    /// now equal to the shipped config — and is KEPT EXPLICIT anyway, for the
    /// same reason it was introduced: these controls must answer the LAYOUT
    /// question whatever λ the director rules, and a control that silently
    /// inherits the shipped value would stop being one the moment the rate is
    /// ruled non-zero. The shipped value is asserted alongside, so a silent
    /// RE-ARMING fails here too — and if it is ruled, these constants stay put
    /// while the behavioural goldens move, which is the whole point.
    /// </summary>
    /// <summary>R1: the canonical config with the recipe-knowledge links removed (TestConfigs.PreRecipeKnowledge).
    /// EVERY layer control in this file runs on it, so each constant it carries is UNMOVED by R1 (the T4.21-4
    /// precedent: a layout control must not be asked to absorb a behaviour change); R1's own controls at the
    /// end of the file prove the twin returns the pre-R1 pins byte for byte.
    /// R2a: the twin is also PRE-R2a (TestConfigs.PreTradeKnowledge: no trade node/entity, trade ungated, no
    /// city-state research), and M5 R2b: with every R2b layer stripped too (TestConfigs.PreR2b — weather
    /// geography, unrest/Dignity, Age military realization), so every older control stays unmoved; the R2a, R2b
    /// and R2c (merged) controls are at the end of the file.</summary>
    private static Sim.Core.Systems.SimConfig PreR1() =>
        TestUtil.TestConfigs.PreRecipeKnowledge(TestUtil.TestConfigs.PreTradeKnowledge(TestUtil.TestConfigs.PreR2b(TestUtil.TestConfigs.Sim())));

    /// <summary>M5 R2b: the canonical config with only the R2b layers stripped — on the merged tree, the R2a world.</summary>
    private static Sim.Core.Systems.SimConfig PreR2b() => TestUtil.TestConfigs.PreR2b(TestUtil.TestConfigs.Sim());

    /// <summary>R2a: the canonical config with only the R2a trade-knowledge layer stripped — on the merged tree, the R2b world.</summary>
    private static Sim.Core.Systems.SimConfig PreR2a() => TestUtil.TestConfigs.PreTradeKnowledge(TestUtil.TestConfigs.Sim());

    /// <summary>R2c: both R2 layers stripped — the R1 world (m5-integration @ f1fe76f).</summary>
    private static Sim.Core.Systems.SimConfig PreR2() =>
        TestUtil.TestConfigs.PreTradeKnowledge(TestUtil.TestConfigs.PreR2b(TestUtil.TestConfigs.Sim()));

    private static Sim.Core.Systems.SimConfig Unarmed()
    {
        Sim.Core.Systems.SimConfig shipped = PreR1();
        Assert.Equal(0.0, shipped.Disaster.HazardPerYear);   // inert, T4.21-7 / CR-016
        return shipped with { Disaster = shipped.Disaster with { HazardPerYear = 0.0 } };
    }

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheM4SchemaAlone()
    {
        // main (070f05b) carried this value, and T4.4 deliberately left it
        // UNMOVED as its own no-unrelated-movement control. If it reappears with
        // the v23 trailer removed, M4's tables are the entire cause here too.
        const string mainValue = "0f94b4ad95b8821d19b24d208d56ecc1d2be755ced2d89c539249855ebc23745";

        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(mainValue, HashAtSchemaV22(world));
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheM4SchemaAlone()
    {
        // T4.21-2 (ADR-025): re-measured — OLD f886efbd159f5717848534efe3af61b8
        // 26fa599d742e5e244d7afafa067bce22 (v22), e48d9bcd8883204bb2efa4843923c49a
        // 30de86fae9269e7354e6d2018bf8e1f7 (v23); bounded migration moved this world.
        // main's post-T4.4 pin. Reappearing under the control proves the
        // capacity-floor fix does NOT reach this world — consistent with the
        // pre-integration measurement, which found the fix's blast radius to be
        // the driven golden only.
        // T4.19 lane C: re-measured on the corrected founding vector (tuning
        // data; SnapshotTests.FoundedGolden carries the record and the 41-table
        // turn-0 control). OLD f25c5dd3947a53827c1d9615a7e351108c05258bb0ffe0b1ab1a269e9a4626c6.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) f886efbd159f5717848534efe3af61b826fa599d742e5e244d7afafa067bce22;
        // OLD (T4.21-2 alone) 05eb271cfec6226716d58eb6ca10859ae2f7a91273b0978574509d96fbb806c3;
        // OLD (T4.21-3 alone) c870354f478551a199617e7525e51fd06bffa22f9ccaeb2015956f117c6f4828.
        const string mainValue = "b94886cfb24df55650553f0857916c88b8e3ec80e620dfcad10f88b31176feb7";

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                Unarmed(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), Unarmed(), 42), 300);

        Assert.Equal(mainValue, HashAtSchemaV22(world));

        // M4-C LAYER. This world is FOUNDED, so it now carries real Empire rows.
        // Emptying just those rows — leaving the v23 prefixes in place — must
        // return the pre-M4-C pin byte for byte. That is what proves founding's
        // Empire state is the WHOLE delta: no population, food, terrain, deposit,
        // path, production, demographic, migration or economic state moved with
        // it, because any such drift would survive the strip and break this.
        // T4.19 lane C: OLD 16a1c17150f210b90a8c4d866f16a1767bdc13f218f880304f2449437625e015.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) e48d9bcd8883204bb2efa4843923c49a30de86fae9269e7354e6d2018bf8e1f7;
        // OLD (T4.21-2 alone) f49815f0d222a8a269a042611bc110486527d5112e3262fe591f7524266872ab;
        // OLD (T4.21-3 alone) bbbb9aece1b981f74fba310d0b0e40a3764bee0192a357018a64f8e7ed1e5e63.
        const string beforeM4C = "64bb34959bb3d186c0a389c18535b8754c216ad54bd27e011139db24e6d406fe";
        Assert.Equal(beforeM4C, HashAtSchemaV23(world));

        // ...and the rows really are there, so the strip is not vacuous.
        Assert.Equal(1, world.Polities.Count);
        Assert.Equal(world.Settlements.Count, world.Controls.Count);
        Assert.Equal(1, world.Capitals.Count);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheM4SchemaAlone()
    {
        // T4.21-2 (ADR-025): re-measured — OLD 69d6cf178fa536e0582874eacf7adec9
        // fbcbc686c5e14a12292f935aa2694550 (v22), 4e7d2e69e7c5ed72444501bed84c341b
        // 50a0d77c25d123ead36498ce9b280d7b (v23); bounded migration moved this world.
        // The fourth pinned world, and the one that most needs a control: T4.4's
        // own history records an earlier revision of it moving this pin
        // BEHAVIOURALLY (the lone settlement colonising its way out of the
        // director's 0%-farm order). So "schema only" here is exactly the claim
        // that must not be taken on trust.
        // T4.19 lane C: re-measured on the corrected founding vector (tuning
        // data; FirstReignTests carries the record). OLD a64a6cf62eb63a4e5c46297fca4e146a543e13cb0f49a53c3687b47da63001e6.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) 69d6cf178fa536e0582874eacf7adec9fbcbc686c5e14a12292f935aa2694550;
        // OLD (T4.21-2 alone) 7cb35cbdc92e3bf0419dcd091343126acb801eb0a7bc0576627c561af47ca2e1;
        // OLD (T4.21-3 alone) f424daa7da78568c23f475dd0b24dce31ee3b15f8ccf7d50fbf2713e0300d325.
        const string mainValue = "05da2ccea08c6e919368ba29bf84e5737d52740e691940d7ff1ad5dfe12236bd";

        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(mainValue, HashAtSchemaV22(world));

        // M4-C LAYER — this world is FOUNDED too, so it also carries Empire rows.
        // T4.19 lane C: OLD f79714f955c31cf0f25d323c045a0c1935345e92908fa78758bc8266c6b8ef0b.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) 4e7d2e69e7c5ed72444501bed84c341b50a0d77c25d123ead36498ce9b280d7b;
        // OLD (T4.21-2 alone) e877c79683265a6dd09055861541c731ecfdfed2eee0188cb6939974541cf6f5;
        // OLD (T4.21-3 alone) 89bb4b64a8c7d664c6d89d843c0255463ee49171c4d12707b4cabac181e537ee.
        const string beforeM4C = "4cdad2ddce909e4069ea4a859234997b3da5b256a3e665664ceee49104fcb63f";
        Assert.Equal(beforeM4C, HashAtSchemaV23(world));
        Assert.Equal(1, world.Polities.Count);
        Assert.Equal(world.Settlements.Count, world.Controls.Count);
        Assert.Equal(1, world.Capitals.Count);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_SeparatesTheSchemaMoveFromTheBehaviouralOne()
    {
        // This is the one golden with TWO causes, so the control has to show them
        // apart. With the v23 trailer removed the stream is at T4.4's v22 — and it
        // must NOT equal main's pin, because the capacity-floor fix genuinely
        // changes this world. What the control establishes is that the difference
        // between the integrated pin and this value is the M4 schema alone, and
        // the difference between this value and main's pin is the behavioural fix
        // alone.
        const string mainPinBeforeTheFix = "5b204b455cc5d0ef03031f7b0606af9d491ecc3d2d2c0d68bdb60a3bbd0b69cb";

        // T4.19 — RE-PINNED under the CR-014 ruling. Lane C's founding vector
        // moved this world AND carried it into a latent Craft overdraw at turn
        // 213 (weaving, fiber, settlement 10: stock 66, cap 22 x 3 = 66, banked
        // ConsumeRemainder 0.9999999999999929, exactIn 67.0, Throw). The cap now
        // includes the bank (option 1) and the world completes. The two causes
        // are measured apart in DrivenGoldenTests: the cap change on the OLD
        // vector returns the old pin byte for byte at turn 300 (a three-turn
        // transient at 273-275, closed by 276), so the movement of every
        // constant here is the founding vector; the cap change is what lets it
        // be measured at all. Both constants below re-measured on this tree via
        // HashAtSchemaV22 / HashAtSchemaV23; the founded, FirstReign and
        // synthetic controls in this file are UNMOVED (run, not assumed).
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, Unarmed());
        string atV22 = HashAtSchemaV22(world);

        Assert.NotEqual(mainPinBeforeTheFix, atV22);
        Assert.Equal(CapacityFloorFixAtSchemaV22, atV22);

        // M4-C LAYER — founded world, so Empire rows land here as well. Three
        // causes now compose in this one pin, and each is measured separately.
        // T4.19: OLD e2f3c0426f504077c8536f51f7784a7fa2b5925bc85c95ef715b7931f64851ab.
        // T4.21-2 ∥ T4.21-3 MERGE: re-measured on the merged tree (behaviour,
        // both packets; header). OLD (pre-packet) cf93e0fed26a3e28e9e971498f8240c1534a5a8aa97391fe030adeaf76d4fa75;
        // OLD (T4.21-2 alone) bdf29a88dcec3385d9bd6ccb8938877ef88459629b10eccf33707e45dcdb0c15;
        // OLD (T4.21-3 alone) edb812c3b2940498e779c0f81002f69db74b7b5023e5f78a72cb8e35d152a9c0.
        const string beforeM4C = "c4833d844a1c10b2029704760cb7f3010eb636ed24b14315e46e4584bfbb3cd4";
        Assert.Equal(beforeM4C, HashAtSchemaV23(world));
        Assert.Equal(1, world.Polities.Count);
        Assert.Equal(world.Settlements.Count, world.Controls.Count);
        Assert.Equal(1, world.Capitals.Count);
    }

    // ======================================================================
    // T4.21-1 — THE LAYOUT-ONLY CONTROL FOR SCHEMA v25 + THE DISASTER STREAMS
    // ======================================================================

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV25TrailerAlone()
    {
        // The toy pipeline has no disaster system, so this synthetic world gains
        // NO stream rows — its whole movement is the empty Disasters prefix.
        // The pre-packet pin (SnapshotTests.GoldenHash at 45046eb). UNMOVED by
        // T4.21-2 (bounded migration): no distances, no migration — this is the
        // no-unrelated-movement control for that packet too.
        const string beforeT421 = "eec82711bbb257ea4ad2a6537ae31945cede7008f1c512b99af936831e3afe69";

        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.Equal(0, removed);
        Assert.Equal(0, world.Disasters.Count);
        Assert.Equal(32, CanonicalSchema.Version);   // v31: ADR-033 D6/D10 Institutions + ConstructionLabor (v32: H2 TaxGrievances)
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheDisasterLayoutAlone()
    {
        // T4.21-2 (ADR-025): the pre-packet pin no longer returns — bounded
        // migration moved this world; re-measured on this tree (OLD
        // 917993b2b5367cd6141c46f4b0d2d81bfd74516198b87209a82be6a643637d62 —
        // the pre-T4.21 pin, which this control returned byte for byte at
        // 1735d41). What survives is separability: the strip is non-vacuous.
        // The pre-packet pin (SnapshotTests.FoundedGolden and ci.yml's
        // FOUNDED_GOLDEN at 45046eb). hazardPerYear = 0 ⇒ no row is ever
        // written, ProductionSystem multiplies by 1.0 exactly, and the ONLY
        // thing in the stream that is not in the pre-packet stream is one
        // RngStreamRow per settlement plus the empty v25 prefix.
        // T4.21-2 ∥ T4.21-3 MERGE: BEHAVIOUR moved this world twice (header;
        // SnapshotTests.FoundedGolden), so the stripped value can no longer
        // return the pre-T4.21 pin
        // (917993b2b5367cd6141c46f4b0d2d81bfd74516198b87209a82be6a643637d62);
        // it is re-measured ON THE MERGED TREE and the control now proves the
        // disaster rows are separable from the output. The difference between
        // this value and the pre-T4.21 pin IS the two packets' behaviour,
        // attributed there.
        // OLD (T4.21-2 alone) 7ee73714b054cd3250a0c9a37a59677581cb74a0469e20b29e9bcde65fc05ed6;
        // OLD (T4.21-3 alone) 776616ce4e78ca8cff586a891cf495840b9e48df1e9d29bc9b5e5ecede21b566.
        const string beforeT421 = "431f2f48a00310178c663a549c910022096dd7e3d491621d8418c99222968f19";

        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                Unarmed(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), Unarmed(), 42), 300);

        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.Equal(world.Settlements.Count, removed);   // one disaster stream per settlement — not vacuous
        Assert.Equal(0, world.Disasters.Count);           // hazard 0: no row, ever
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheDisasterLayoutAlone()
    {
        // T4.21-2 (ADR-025): re-measured on this tree (OLD 5ee8119e365ad04bdfc45f
        // 791a8962bb0fb616016ad1616c67b9c74c2d81e9a returned byte for byte at
        // 1735d41); bounded migration moved this world.
        // The pre-packet pin (FirstReignTests at 45046eb). This is the world
        // whose lone settlement dies under the director's 0%-farm order — an
        // ABANDONMENT famine under the new classification, and this control is
        // what proves the classification's existence moved nothing: FoodState
        // is a static nothing in the pipeline calls yet.
        // T4.21-2 ∥ T4.21-3 MERGE: both packets moved this world (FirstReignTests
        // has the joint record) — the classification is now READ (FAMINE/
        // Abandonment feeds the kernel), the turn-2 headroom hold applies, and the
        // flight is the bounded hazard. Re-measured ON THE MERGED TREE.
        // OLD (pre-packet) 5ee8119e365ad04bdfc45f791a8962bb0fb616016ad1616c67b9c74c2d81e9a;
        // OLD (T4.21-2 alone) 8e23b4b32e3a1e0257df6af8fd482d8f5cb177345bb3378f2b4061ac8a561e72;
        // OLD (T4.21-3 alone) 77454c587a98f8fadb2ad055ca00509612f32a2a7aa73762e2418e0da30ebdfb.
        const string beforeT421 = "b31684a74926d97fb71eec21e8868dccbfa92cb4ab3d62e47d41a22ba35d9488";

        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.True(removed >= 1, "no disaster stream rows to strip — control vacuous");
        Assert.Equal(0, world.Disasters.Count);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheDisasterLayoutAlone()
    {
        // The pre-packet pin (DrivenGoldenTests at 45046eb).
        // T4.21-2 ∥ T4.21-3 MERGE: both packets moved this world (DrivenGoldenTests
        // has the joint record); re-measured ON THE MERGED TREE.
        // OLD (pre-packet) 76f82629abbffbc3c0897d2cfab7933e890a5441697dfdb82a59cd64d74163a6;
        // OLD (T4.21-2 alone) cee0c2c0… (see docs/t4.21-2-record.md §2);
        // OLD (T4.21-3 alone) d6a0554e6c419cc6b8f3bd364b30f9721fb9b1ad1fb00675f265ae309ab1f1a1.
        const string beforeT421 = "8cd5db52cfc2a4e16748e2d4e930ead0daa76dcc48dfb9224893736c3ab8bf79";

        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, Unarmed());
        Assert.Equal(beforeT421, HashAtSchemaV24(world, out int removed));
        Assert.Equal(world.Settlements.Count, removed);
        Assert.Equal(0, world.Disasters.Count);
    }

    // ======================================================================
    // ADR-029 — THE LAYOUT CONTROL FOR SCHEMA v26 + THE RESEARCH ROWS
    // ======================================================================
    // Each constant below is the value the world's own golden carried on main at
    // 93270cd, before the research packet. The strip returning it BYTE FOR BYTE is
    // the measurement that the research layer is the ENTIRE delta.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV26ResearchTrailerAlone()
    {
        // The toy pipeline runs no research system: its whole movement is seven empty
        // count prefixes (SnapshotTests.GoldenHash on main).
        const string beforeResearch = "b6df7edd362e15de908526c6343f50f920f3a344b7dac703aaad7671c41adaa1";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeResearch, HashAtSchemaV25(world, out int removed));
        Assert.Equal(0, removed);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheResearchLayerAlone()
    {
        // SnapshotTests.FoundedGolden and ci.yml's FOUNDED_GOLDEN on main.
        const string beforeResearch = "17464c3f152e89c8a8d53445fb57db744a61340def1dacf938811ac632d39c9a";
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(
                Unarmed(), TestUtil.TestConfigs.Worldgen())));
        WorldState world = executor.Run(
            Sim.Core.Worldgen.WorldFounding.Found(
                TestUtil.TestConfigs.Worldgen(), Unarmed(), 42), 300);
        Assert.Equal(beforeResearch, HashAtSchemaV25(world, out _));
        Assert.Equal(0, world.ResearchTargets.Count);    // nobody chose a target
        Assert.Equal(0, world.ResearchCompleted.Count);
        // MEASURED (research finalization, addendum A): with no order, none of the 18 evaluable
        // curated Eurekas holds on an available node within 300 turns, so the order-less arm
        // writes NO research row and its strip is vacuous on its own. The research-DRIVEN arm
        // below supplies the teeth: the same world with a target chosen every time one is free.
        WorldState researched = RunWithResearchDriver(
            Sim.Core.Worldgen.WorldFounding.Found(TestUtil.TestConfigs.Worldgen(), Unarmed(), 42),
            new OrderLog(), Unarmed(), 300, researchFrom: 0);
        Assert.Equal(beforeResearch, HashAtSchemaV25(researched, out int removed));
        Assert.True(removed > 0, "no research rows to strip — control vacuous");
        Assert.True(researched.ResearchCompleted.Count > 0, "research completed nothing — control vacuous");
    }

    /// <summary>
    /// Runs a founded world with the measurement driver of ResearchDeterminismTests appending a
    /// SetResearchTarget for the cheapest available node whenever the player has no target,
    /// from turn <paramref name="researchFrom"/> on, on top of whatever <paramref name="orders"/>
    /// already holds. Research writes only its own
    /// tables, so the stripped stream must equal the pre-research pin byte for byte.
    /// </summary>
    private static WorldState RunWithResearchDriver(WorldState world, OrderLog orders, Sim.Core.Systems.SimConfig cfg, int turns, int researchFrom)
    {
        Sim.Core.Systems.Research.ResearchContent content = TestUtil.TestConfigs.Research();
        var player = new PolityId(1);
        using var eraStream = Sim.Data.DataFiles.OpenEraPacing();
        using var pipeStream = Sim.Data.DataFiles.OpenPipeline();
        var executor = new TurnExecutor(
            EraTableLoader.Load(eraStream),
            PipelineLoader.Load(pipeStream, SystemCatalog.All(cfg, TestUtil.TestConfigs.Worldgen())), orders);
        for (int t = 0; t < turns; t++)
        {
            if (world.Clock.Turn >= researchFrom && !ResearchQuery.TryGetTarget(world, player, out _)
                && ResearchQuery.CheapestAvailable(world, content, player) is { } pick)
                orders.Append(OrderRecord.From(world.Clock.Turn, player, OrderKind.SetResearchTarget, pick.Value, 0.0));
            world = executor.Step(world);
        }
        return world;
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheResearchLayerAlone()
    {
        // FirstReignTests' golden on main.
        const string beforeResearch = "0939beb30ae683d3b901ce773b221cb73a30f23ce4c0b145ef5b57a09699c3d3";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeResearch, HashAtSchemaV25(world, out _));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheResearchLayerAlone()
    {
        // DrivenGoldenTests' golden on main.
        const string beforeResearch = "a3a57867172c09a832af5b7074ad01829e6ca07f4d9c94ec9ed0fc14478db0c3";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, Unarmed());
        Assert.Equal(beforeResearch, HashAtSchemaV25(world, out _));
        // The order-less research arm writes no research row (measured; see the founded control),
        // so the teeth are the same driven world with the research driver added to its orders.
        WorldState founded = Sim.Core.Worldgen.WorldFounding.Found(TestUtil.TestConfigs.Worldgen(), Unarmed(), 42);
        WorldState researched = RunWithResearchDriver(founded,
            DrivenGoldenTests.DrivingOrders(founded.Settlements.Count), Unarmed(), 300,
            researchFrom: 2); // the log is append-only in turn order, and its batch is stamped turn 2
        Assert.Equal(beforeResearch, HashAtSchemaV25(researched, out int removed));
        Assert.True(removed > 0, "no research rows to strip — control vacuous");
        Assert.True(researched.ResearchCompleted.Count > 0, "research completed nothing — control vacuous");
    }
    // ======================================================================
    // ADR-031 — THE LAYOUT CONTROL FOR SCHEMA v28 (Ages and military formations)
    // ======================================================================
    // Each constant is the pin as it stood on this branch at 3e5647f, BEFORE ADR-031.
    // Stripping the five Age/military tables and dropping their count prefixes must return
    // it BYTE FOR BYTE: the Age systems draw no RNG, write no ledger flow, and the founding
    // formations are tokens — so the v28 tables are the ENTIRE cause of every re-pin.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV28AgeTrailerAlone()
    {
        const string beforeAges = "c7bb78dc2164b335c87a819e6e2084ab4d0ddc5f4932597cece5eaac9c6052e8";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeAges, HashAtSchemaV26(world, out int removed));
        Assert.Equal(0, removed); // the toy pipeline runs no Age system and founds no formation
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheAgeLayerAlone()
    {
        const string beforeAges = "8ea1b956ef550ca5e3d92ced8794917502e44448bf54ee5dcc140520fd06c7b5";
        WorldState world = SnapshotTests.RunFoundedGolden(PreR1());
        Assert.Equal(beforeAges, HashAtSchemaV26(world, out int removed));
        Assert.True(removed > 0, "no Age/military rows to strip — control vacuous");
        Assert.Equal(1, world.MilitaryUnits.Count); // the founding warband, unconverted: nobody advanced
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheAgeLayerAlone()
    {
        const string beforeAges = "df85224cb7a2ab5f3d2aa5d4850914d06af44baf5a26dd9c9a6f48665ef6d176";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeAges, HashAtSchemaV26(world, out int removed));
        Assert.True(removed > 0, "no Age/military rows to strip — control vacuous");
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheAgeLayerAlone()
    {
        const string beforeAges = "320884dd6f6d3d618cec4499043bc38e1b33f068f778836daa7c05f546d6ab6d";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(beforeAges, HashAtSchemaV26(world, out int removed));
        Assert.True(removed > 0, "no Age/military rows to strip — control vacuous");
    }

    // ======================================================================
    // ADR-032 — THE LAYOUT CONTROL FOR SCHEMA v29 (the inter-city transport graph)
    // ======================================================================
    // Each constant is the pin as it stood on this branch at c8ceb5f, BEFORE ADR-032.
    // Stripping the two transport tables and dropping their count prefixes must return it
    // BYTE FOR BYTE. None of these runs issues a DevelopRoads order, so the tables are EMPTY
    // (removed == 0 is the point, not a vacuity): RoadDevelopmentSystem returns at its first
    // loop, draws no RNG and writes no ledger flow, and the research content change (motor_road,
    // the dry-dock requirement, the track_road wording) moved no research choice either — the
    // two empty prefixes are the ENTIRE cause of every re-pin.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV29TransportTrailerAlone()
    {
        const string beforeRoads = "498635bf3c2673b9b544582e17774381b7a785903d7ed320e2dd8e44ddfd4296";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeRoads, HashAtSchemaV28(world, out int removed));
        Assert.Equal(0, removed);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheTransportLayoutAlone()
    {
        const string beforeRoads = "db52e65e932503dc36e347a773211a2144fd8c232070e53b38c1e8cb124cd9df";
        WorldState world = SnapshotTests.RunFoundedGolden(PreR1());
        Assert.Equal(beforeRoads, HashAtSchemaV28(world, out int removed));
        Assert.Equal(0, removed); // no order log: no road is ever developed
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheTransportLayoutAlone()
    {
        const string beforeRoads = "080315066ef7b142d1147a2e753ad1184cfd062bb3759f91a62200c16c56dc61";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeRoads, HashAtSchemaV28(world, out int removed));
        Assert.Equal(0, removed); // the first-reign log carries no DevelopRoads order
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheTransportLayoutAlone()
    {
        const string beforeRoads = "aa5ae46d574a1e2362e7ddc138bf2095e3ec56d5dc2977fd85f079440859db98";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(beforeRoads, HashAtSchemaV28(world, out int removed));
        Assert.Equal(0, removed); // the driven log carries no DevelopRoads order
    }

    // ======================================================================
    // ADR-033 D4 — THE LAYER CONTROL FOR SCHEMA v30 (the M5 governing loop)
    // ======================================================================
    // Each constant is the pin as it stood on m5-integration at ffbb6f8, BEFORE the governing loop
    // was ported (schema v29). Stripping the governance layer — the TaxPolicies rows, the reach
    // GovernanceSystem writes into ControlRow.Strength (restored to the 1.0 founding and colonization
    // wrote), and the empty v30 count prefix — must return it BYTE FOR BYTE. None of these runs
    // levies a tax (no SetTaxRate order; and none could pass the research gate), so the effective tax
    // rate is exactly 0 everywhere, production multiplies by exactly 1.0, happiness by exactly 1.0,
    // and the new table plus the Strength writes are the ENTIRE cause of every re-pin. GovernanceSystem
    // draws no RNG and writes no ledger flow. A leak of the governing loop into population, food,
    // migration, trade or anything else would survive the strip and break these.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV30GovernanceTrailerAlone()
    {
        // The toy pipeline runs no governance system and the synthetic world has no control rows:
        // its whole movement is one empty count prefix.
        const string beforeGovernance = "4c051fd40e9b86610ea7e2245daaff55d6503074ace491a986b41959f4f73151";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeGovernance, HashAtSchemaV29(world, out int restored));
        Assert.Equal(0, restored);
        Assert.Equal(0, world.TaxPolicies.Count);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheGovernanceLayerAlone()
    {
        const string beforeGovernance = "b14899eb23a9704f41374726172d16ff26fe6bd5c64dfabe780d31478d751450";
        WorldState world = SnapshotTests.RunFoundedGolden(PreR1());
        Assert.Equal(beforeGovernance, HashAtSchemaV29(world, out int restored));
        // Non-vacuous: the founded world's non-capital settlements carry a computed reach < 1.
        Assert.True(restored > 0, "no Strength differed from 1.0 — the reach computation is invisible and the control vacuous");
        Assert.Equal(0, world.TaxPolicies.Count);   // no order log: no tax is ever levied
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheGovernanceLayerAlone()
    {
        const string beforeGovernance = "7813772b2cee6d5f78b5acb55dc643f808f27741752a6cc938be5bd5fb67967f";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeGovernance, HashAtSchemaV29(world, out _));
        Assert.Equal(0, world.TaxPolicies.Count);   // the first-reign log carries no SetTaxRate order
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheGovernanceLayerAlone()
    {
        const string beforeGovernance = "811b119acf46f50e805cb62ad3b90c639444277648aadb9a1fe306562bc21205";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(beforeGovernance, HashAtSchemaV29(world, out int restored));
        Assert.True(restored > 0, "no Strength differed from 1.0 — control vacuous");
        Assert.Equal(0, world.TaxPolicies.Count);   // the driven log carries no SetTaxRate order
    }

    // ======================================================================
    // ADR-033 D6 + D10 — THE LAYER CONTROL FOR SCHEMA v31 (institutions; construction spent once)
    // ======================================================================
    // Each constant is the pin as it stood on m5-integration at 5e7fa35, BEFORE the institutions layer (schema
    // v30). Stripping it — the Institutions and ConstructionLabor rows, the ResearchCostModifiers rows its system
    // now rebuilds, and the two empty v31 count prefixes — must return it BYTE FOR BYTE. None of these runs
    // founds a university (no EnqueueConstruction order of a university project; none could pass the knowledge
    // gate) and none completes any construction project (no EnqueueConstruction at all), so: the labour reader
    // returns the raw adult count (production, housing, paths and capacity unchanged bit for bit), the mortality
    // seam returns the literal 1.0 (demographics unchanged), PathBuild subtracts no construction labour (D10 null
    // arm), and the cost-modifier table stays empty. removed == 0 is the point, not a vacuity (the ADR-032
    // precedent): a leak of the layer into population, food, paths, research or anything else would survive the
    // strip and break these.

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV31InstitutionsTrailerAlone()
    {
        // The toy pipeline runs no institutions or construction system: its whole movement is two empty prefixes.
        const string beforeInstitutions = "bbcac0469b61ff494fee410179f937e62505fa23ad183afce00d89b8c3f8333c";
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(beforeInstitutions, HashAtSchemaV30(world, out int removed));
        Assert.Equal(0, removed);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheInstitutionsLayoutAlone()
    {
        const string beforeInstitutions = "6f82a998c06113fe17fbc26bf5919a62f03b0b447602bda6618a2f803d7f0668";
        WorldState world = SnapshotTests.RunFoundedGolden(PreR1());
        Assert.Equal(beforeInstitutions, HashAtSchemaV30(world, out int removed));
        Assert.Equal(0, removed);   // no order log: nothing is built, no university is founded
        Assert.Equal(0, world.Structures.Count);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheInstitutionsLayoutAlone()
    {
        const string beforeInstitutions = "653c5c0ece72b3909c8faf3921f67d37b654cb14be5f57e36b6c8578deaf14e8";
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1());
        Assert.Equal(beforeInstitutions, HashAtSchemaV30(world, out int removed));
        Assert.Equal(0, removed);   // the first-reign log carries no EnqueueConstruction order
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheInstitutionsLayoutAlone()
    {
        const string beforeInstitutions = "65f5e942fd4b58c902bc195a0789bd0d748151aad6984246d74532280b691f13";
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(beforeInstitutions, HashAtSchemaV30(world, out int removed));
        Assert.Equal(0, removed);   // the driven log carries only SectorAllocation orders
    }
    // ======================================================================
    // R1 (research → gameplay unlock pipeline, 2026-10-03) — THE RECIPE-KNOWLEDGE LAYER
    // ======================================================================
    // R1 links each goods.json recipe to a research.json recipe entity, and ProductionSystem runs a recipe only
    // when its entity is knowledge-eligible for the settlement's controller (CraftingQuery). pottery-firing
    // requires pottery_open_fired and bronze-casting tin_bronze; none of these runs completes either node, so
    // from turn 1 no settlement fires pottery or casts bronze (and toolmaking, which reads bronze, has none).
    // That is BEHAVIOUR, so no strip of state can return the old pins: the control is the CONTENT twin. Removing
    // the four recipe links (TestConfigs.PreRecipeKnowledge — a pure data change, the T4.21-4 precedent) must
    // return each pre-R1 pin BYTE FOR BYTE: the links are the entire delta, and nothing else in the R1 commit
    // set moves simulation output. Each OLD constant is the pin on m5-integration at ac13c3d.

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheRecipeKnowledgeLayerAlone()
    {
        const string preR1 = "9fe68e4ef8cc73270c68bddc6b8075c09d91303ebb27ad663e9a25c013f80d8c";
        Assert.Equal(preR1, V31(SnapshotTests.RunFoundedGolden(PreR1())));
        string now = V31(SnapshotTests.RunFoundedGolden(PreR2()));
        Assert.True(now == PostR1FoundedGolden, "R1 founded hash " + now);
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheRecipeKnowledgeLayerAlone()
    {
        const string preR1 = "b6013cb54fffaa91905f47370690167289c123b8962792b957e416a3424b6f6d";
        Assert.Equal(preR1, V31(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR1())));
        string now = V31(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR2()));
        Assert.True(now == PostR1FirstReignGolden, "R1 first-reign hash " + now);
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheRecipeKnowledgeLayerAlone()
    {
        const string preR1 = "bce1a3a8a7ab61a41c5237decfff452239f786e237bdec565cf680bc754145fb";
        (WorldState twin, _) = DrivenGoldenTests.RunDriven(300, PreR1());
        Assert.Equal(preR1, V31(twin));
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, PreR2());
        string now = V31(world);
        Assert.True(now == PostR1DrivenGolden, "R1 driven hash " + now);
    }

    // ======================================================================
    // M5 R2b (governance and weather fixes, 2026-10-03) — THREE BEHAVIOURAL LAYERS
    // ======================================================================
    // (1) WEATHER GEOGRAPHY: the harvest-weather kernel reads straight-line site distance, not road-aware travel
    //     cost; (2) UNREST: Dignity bound to the tax burden (D-035-D) and grievance → protest/discharge/uprising
    //     (D-021 unrest-lite); (3) AGE MILITARY REALIZATION: formations facts count only identities realized at the
    //     Age being entered. All three are BEHAVIOUR (or a published eligibility row), so the control is the
    //     CONTENT/CONFIG twin (TestConfigs.PreR2b), which must return each R1 pin BYTE FOR BYTE: the three layers
    //     are the entire delta. Each OLD constant is the pin on m5-integration at f1fe76f.

    internal const string PostR1FoundedGolden = "922ed0c1dfa427cc6ae3ce7f717955864ea2cb4d9fe0eb22c5d7e30414505c92";
    internal const string PostR1FirstReignGolden = "8bc2299e796cae8d9b59aa378a0364c46962c2cee0c2d6b0d49f65a4def0a607";
    internal const string PostR1DrivenGolden = "6dcc265b9e8d68b65d8d48a0bfbd031ce4d37db26b586112a91d6bce0feb00f2";

    // R2c (merge of R2a + R2b, 2026-10-03): on the merged tree each stream's layer is stripped separately and
    // jointly. MEASURED: R2a moves only the driven pin; R2b moves all three; the two layers compose without
    // interaction on these pins (stripping one returns exactly the other stream's own pin).
    internal const string R2aOnlyDrivenGolden = "91439b05e8a8c4409739d7c43c2cdb70f9afa0c3743d3117bd465467dba7cba0";
    internal const string R2bOnlyFoundedGolden = "6153222490f6d9a8f0317a67e9ebe16b4c2517634b59bffd4786ef4955c2d68b";
    internal const string R2bOnlyFirstReignGolden = "6a6ac746181c47f1e14b783efb74311baca2319a7e4c74411c4c31b289cd6a65";
    internal const string R2bOnlyDrivenGolden = "b20df92d2e2ed05aaac4bf75f8452620ae778307e5fc9a41535c2be4cd9ed510";

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheR2bLayersAlone()
    {
        Assert.Equal(PostR1FoundedGolden, V31(SnapshotTests.RunFoundedGolden(PreR2b())));
        Assert.Equal(PostR1FoundedGolden, V31(SnapshotTests.RunFoundedGolden(PreR2())));
        Assert.Equal(SnapshotTests.FoundedGoldenHash, WorldHash.ComputeHex(SnapshotTests.RunFoundedGolden()));
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheR2bLayersAlone()
    {
        Assert.Equal(PostR1FirstReignGolden, V31(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR2b())));
        Assert.Equal(PostR1FirstReignGolden, V31(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR2())));
        Assert.Equal(Sim.Tests.Systems.FirstReignTests.PostR1Golden, WorldHash.ComputeHex(Sim.Tests.Systems.FirstReignTests.Replay(40, out _)));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheR2bLayersAlone()
    {
        (WorldState twin, _) = DrivenGoldenTests.RunDriven(300, PreR2b());
        Assert.Equal(R2aOnlyDrivenGolden, V31(twin));
        (WorldState both, _) = DrivenGoldenTests.RunDriven(300, PreR2());
        Assert.Equal(PostR1DrivenGolden, V31(both));
    }

    [Fact]
    public void FoundedAndFirstReign_UnmovedByTheTradeKnowledgeLayer()
    {
        Assert.Equal(R2bOnlyFoundedGolden, V31(SnapshotTests.RunFoundedGolden(PreR2a())));
        Assert.Equal(R2bOnlyFirstReignGolden, V31(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, PreR2a())));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheTradeKnowledgeLayerAlone()
    {
        (WorldState twin, _) = DrivenGoldenTests.RunDriven(300, PreR2a());
        Assert.Equal(R2bOnlyDrivenGolden, V31(twin));
        // Which part of the layer: the TRADE GATE alone (node, entity and city-state research kept; only
        // sim.json trade.entity removed) returns the R2b-only pin too — the gate is the entire R2a cause here.
        Sim.Core.Systems.SimConfig ungated = TestUtil.TestConfigs.PreForager(TestUtil.TestConfigs.Sim());
        (WorldState gateOnly, _) = DrivenGoldenTests.RunDriven(300, ungated with { Trade = ungated.Trade with { Entity = null } });
        Assert.Equal(R2bOnlyDrivenGolden, V31(gateOnly));
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300, TestUtil.TestConfigs.PreForager(TestUtil.TestConfigs.Sim()));
        Assert.Equal(R3DrivenGolden, V31(world));
    }

    // ======================================================================
    // R4 (M5 closure, 2026-10-04) — THE FORAGER LAYER
    // ======================================================================
    // sim.json farming.preCultivation ships ON (2.0 / 4.3). BEHAVIOUR, so the control is the CONFIG twin
    // TestConfigs.PreForager (switch OFF), which must return each R3 pin BYTE FOR BYTE: the switch is the entire
    // delta on these pins. The same tree's two other R4 changes are proven inert here by the same equality: the
    // consumption substitution-credit fix (only a negative staple request differs, and none occurs) and the tax
    // burden offset (no run levies a tax, so Dignity reads exactly 1.0). Every older control strips the forager
    // layer too (TestConfigs), so its constant is unmoved. OLD constants are the pins on m5-integration @ 12754e3.

    internal const string R3FoundedGolden = "6153222490f6d9a8f0317a67e9ebe16b4c2517634b59bffd4786ef4955c2d68b";
    internal const string R3FirstReignGolden = "6a6ac746181c47f1e14b783efb74311baca2319a7e4c74411c4c31b289cd6a65";
    internal const string R3DrivenGolden = "d47b7e3d1bb81bce0459455435e9d89205d53e51fc8ae5006951070c81456655";

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheForagerLayerAlone()
    {
        Assert.Equal(R3FoundedGolden, V31(SnapshotTests.RunFoundedGolden(TestUtil.TestConfigs.PreForager(TestUtil.TestConfigs.Sim()))));
        Assert.Equal(SnapshotTests.FoundedGoldenHash, WorldHash.ComputeHex(SnapshotTests.RunFoundedGolden()));
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheForagerLayerAlone()
    {
        Assert.Equal(R3FirstReignGolden, V31(Sim.Tests.Systems.FirstReignTests.Replay(40, out _, TestUtil.TestConfigs.PreForager(TestUtil.TestConfigs.Sim()))));
        Assert.Equal(Sim.Tests.Systems.FirstReignTests.PostR1Golden, WorldHash.ComputeHex(Sim.Tests.Systems.FirstReignTests.Replay(40, out _)));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheForagerLayerAlone()
    {
        (WorldState twin, _) = DrivenGoldenTests.RunDriven(300, TestUtil.TestConfigs.PreForager(TestUtil.TestConfigs.Sim()));
        Assert.Equal(R3DrivenGolden, V31(twin));
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300);
        Assert.Equal(DrivenGoldenTests.Golden, WorldHash.ComputeHex(world));
    }

    // ======================================================================
    // H2 (Director 2026-10-05, docs/d049-taxation-and-revolt-model.md) — THE LAYER CONTROL FOR SCHEMA v32
    // (taxation as accumulated pressure per population segment; segment revolt)
    // ======================================================================
    // Each constant is the pin as it stood on m5h-h2-tax-revolt at ece2a7b (= main's pins at 9bb7423: the Age half
    // of the tax gate and the revolt-Age inheritance move none of these runs — none levies, none advances an Age).
    // The H2 model REPLACES the old revolt rule (a declared 100 % levy at full reach read happiness 0 and was
    // revolt-ready) with a levy-grievance stock per (settlement, class), and reads it in happiness, legitimacy,
    // output and revolt. None of these runs levies a tax (no SetTaxRate order; the toy and founded runs have no order
    // log, the first-reign and driven logs carry none), so on every one of them the old rule and the new one are the
    // SAME FUNCTION: the effective rate is exactly 0, no TaxGrievance row is ever written, the levy pressure reads
    // exactly 0, happiness multiplies by exactly 1.0, the protest/rebel output factor is exactly 1.0, the Dignity
    // satisfaction is exactly 1.0 (as before), and revolt's provision reading IS the old happiness reading. So the
    // old-rule twin of each run is the run itself, and the entire movement must be the one EMPTY v32 count prefix:
    // stripping the (asserted absent) rows and dropping that prefix returns each OLD pin BYTE FOR BYTE. Any leak of
    // the new model into an untaxed world — a row written, a reading moved, a revolt fired — survives the strip and
    // breaks these. The model's behaviour under a levy is pinned semantically (TaxPressureTests A–H, UnrestTests,
    // GovernanceTests, TaxAgeGateTests) and is outside every golden: no golden run taxes.

    internal const string V31ToyGolden = "0af7143fb69809fc58653ae99178ff11c8d020137b78443ac1bae46b21c8b269";
    internal const string V31FoundedGolden = "416c0ebe3237b25d3d6c1a14483739c87e1dcaa14f154f2baae28b3fd25f8d49";
    internal const string V31FirstReignGolden = "62ee79880d95ebeb2ad4ac2cd8c7fe745deb181160fcde0c27bc269bcd638bb6";
    internal const string V31DrivenGolden = "b252a66db4ca2a03b34deb62489841f504602130f0b18920ef124d0475977542";

    [Fact]
    public void GoldenHashSeed42Turn200_MovedForTheV32LevyTrailerAlone()
    {
        WorldState world = SnapshotTests.CanonicalExecutor().Run(SnapshotTests.Genesis(42), 200);
        Assert.Equal(V31ToyGolden, V31(world));
        Assert.Equal(0, world.TaxPolicies.Count);
        Assert.Equal(32, CanonicalSchema.Version);
    }

    [Fact]
    public void FoundedGoldenSeed42Turn300_MovedForTheV32LevyLayerAlone()
    {
        WorldState world = SnapshotTests.RunFoundedGolden();
        Assert.Equal(V31FoundedGolden, V31(world));
        Assert.Equal(0, world.TaxPolicies.Count);   // no order log: no levy, so no levy grievance anywhere
        Assert.Equal(SnapshotTests.FoundedGoldenHash, WorldHash.ComputeHex(world));
    }

    [Fact]
    public void FirstReignTurn40_MovedForTheV32LevyLayerAlone()
    {
        WorldState world = Sim.Tests.Systems.FirstReignTests.Replay(40, out _);
        Assert.Equal(V31FirstReignGolden, V31(world));
        Assert.Equal(0, world.TaxPolicies.Count);   // the first-reign log carries no SetTaxRate order
        Assert.Equal(Sim.Tests.Systems.FirstReignTests.PostR1Golden, WorldHash.ComputeHex(world));
    }

    [Fact]
    public void DrivenGoldenSeed42Turn300_MovedForTheV32LevyLayerAlone()
    {
        (WorldState world, _) = DrivenGoldenTests.RunDriven(300);
        Assert.Equal(V31DrivenGolden, V31(world));
        Assert.Equal(0, world.TaxPolicies.Count);   // the driven log carries only SectorAllocation orders
        Assert.Equal(DrivenGoldenTests.Golden, WorldHash.ComputeHex(world));
    }
}
