using Proto.M7.Tactical;

namespace Proto.M7.War.Outcomes;

/// <summary>How a battle entity ended, for the collapse. Stable codes; the code is also the apportionment tie-break (lowest first).</summary>
public enum CollapseCategory : byte
{
    /// <summary>Alive and unhurt (Active, orderly escape, still aboard a transport).</summary>
    Fit = 0,
    /// <summary>Alive with less than full hit points at the end.</summary>
    Wounded = 1,
    /// <summary>Unhurt but fled the field in a rout.</summary>
    Routed = 2,
    Dead = 3,
    Surrendered = 4,
}

/// <summary>
/// One campaign formation's part in a battle, as the battle left it: entity counts by <see cref="CollapseCategory"/> (index =
/// code), its end morale and morale scale. Nothing per entity survives this record (DIR5 §2.9: no persistent individual table).
/// </summary>
public sealed record CollapseInput(int CampaignFormation, int Side, long[] EntityCounts, long MoraleEnd, long MoraleMax)
{
    public long Entities
    {
        get
        {
            long n = 0;
            foreach (var c in EntityCounts) n = checked(n + c);
            return n;
        }
    }
}

/// <summary>The persons of one formation after the collapse. Exact: the parts sum to Persons (no epsilon).</summary>
public readonly record struct CollapseSplit(long Persons, long Fit, long Wounded, long RoutedReturned, long Deserted, long Dead, long Captured)
{
    public bool Conserved => Fit + Wounded + RoutedReturned + Deserted + Dead + Captured == Persons;
    /// <summary>People who stay in the formation's ranks (fit and the routed who came back).</summary>
    public long Remaining => Fit + RoutedReturned;
}

/// <summary>
/// The post-battle collapse (DIR5 §2.9; O-10 a): the battle-transient entities of a formation collapse into the counts of the
/// campaign formation they stood for, and the people move through the ledger as explicit flows (dead, wounded, captured,
/// deserted). Pure and integer: the same input gives the same split.
///
/// PERSONS AND ENTITIES. A campaign formation of P persons fights as n entities (n ≤ the cap; one entity per soldier when P = n).
/// The P persons are apportioned over the end categories in proportion to the entity counts by the largest-remainder method
/// (Hamilton): floor(P · n_c / n) each, the leftover persons one each to the largest remainders, ties to the lowest category
/// code. The parts sum to P exactly; with P = n each category gets exactly its entity count.
///
/// WOUNDED. A surviving entity whose hit points are below its maximum at the end of the battle is wounded (derived from the
/// combat state, no coefficient). The wounded leave the ranks for the formation's wounded account; their recovery or death is
/// attrition and belongs to M7.14, which this account hands them to.
///
/// DESERTED (PROPOSAL). An unhurt man who fled in a rout comes back to the colours if his side held the field; on a side that did
/// not, the share (MoraleMax − end morale) / MoraleMax of the routed does not come back (floor) — they desert or straggle. State-
/// derived from the formation's end morale; no free coefficient.
/// </summary>
public static class BattleCollapse
{
    public const int CategoryCount = 5;

    /// <summary>Largest-remainder apportionment of <paramref name="persons"/> over categories weighted by <paramref name="weights"/>.
    /// All weight zero: everyone stays Fit (category 0). Ties of remainder go to the lowest category index.</summary>
    public static long[] Apportion(long persons, ReadOnlySpan<long> weights)
    {
        if (persons < 0) throw new ArgumentOutOfRangeException(nameof(persons));
        int k = weights.Length;
        var share = new long[k];
        Int128 n = 0;
        for (int c = 0; c < k; c++)
        {
            if (weights[c] < 0) throw new ArgumentOutOfRangeException(nameof(weights));
            n += weights[c];
        }
        if (n == 0) { if (k > 0) share[0] = persons; return share; }
        var rem = new Int128[k];
        long given = 0;
        for (int c = 0; c < k; c++)
        {
            Int128 x = (Int128)persons * weights[c];
            share[c] = (long)(x / n);
            rem[c] = x % n;
            given = checked(given + share[c]);
        }
        long left = persons - given;   // < k
        var used = new bool[k];
        for (long i = 0; i < left; i++)
        {
            int best = -1;
            for (int c = 0; c < k; c++)
                if (!used[c] && (best < 0 || rem[c] > rem[best])) best = c;   // strict >: an equal remainder keeps the lower index
            used[best] = true;
            share[best]++;
        }
        return share;
    }

    /// <summary>The split of <paramref name="persons"/> for one formation (see the type summary).</summary>
    public static CollapseSplit Split(long persons, CollapseInput input, bool sideHeldField)
    {
        if (input.EntityCounts.Length != CategoryCount) throw new ArgumentException("one count per category");
        if (input.MoraleMax <= 0 || input.MoraleEnd < 0 || input.MoraleEnd > input.MoraleMax) throw new ArgumentOutOfRangeException(nameof(input), "morale");
        var a = Apportion(persons, input.EntityCounts);
        long routed = a[(int)CollapseCategory.Routed];
        long deserted = sideHeldField ? 0 : (long)((Int128)routed * (input.MoraleMax - input.MoraleEnd) / input.MoraleMax);
        return new CollapseSplit(persons, a[(int)CollapseCategory.Fit], a[(int)CollapseCategory.Wounded], routed - deserted, deserted,
                                 a[(int)CollapseCategory.Dead], a[(int)CollapseCategory.Surrendered]);
    }

    /// <summary>The category of one entity at the end of a battle.</summary>
    public static CollapseCategory CategoryOf(EntityState state, long hp, long maxHp, bool routedEscape) => state switch
    {
        EntityState.Dead => CollapseCategory.Dead,
        EntityState.Surrendered => CollapseCategory.Surrendered,
        EntityState.Active or EntityState.Escaped or EntityState.Embarked =>
            hp < maxHp ? CollapseCategory.Wounded : routedEscape ? CollapseCategory.Routed : CollapseCategory.Fit,
        _ => throw new InvalidOperationException($"entity state {state} cannot be collapsed (the battle is not over)"),
    };

    /// <summary>
    /// Collapse inputs of a finished tactical battle. <paramref name="campaignOf"/> maps each tactical formation id to the campaign
    /// formation it stood for (pairs in any order; one-to-one: a campaign formation mapped twice is refused). Land formations: one entity per soldier (EntityView.Hp against the profile's
    /// MaxHp). Vessel and aircraft formations: their people from the battle's personnel ledger (killed and lost with the vessel →
    /// Dead, captured → Surrendered, everyone else → Fit; a hull's crew has no per-person hit points, so no wounded).
    /// </summary>
    public static CollapseInput[] FromTactical(TacticalBattle battle, BattleSetup setup, (long TacticalId, int CampaignFormation)[] campaignOf)
    {
        var result = battle.Result ?? throw new InvalidOperationException("the battle is not over");
        var views = battle.Entities();
        var specs = SortedSpecs(setup.Formations);
        var inputs = new CollapseInput[specs.Length];
        for (int i = 0; i < specs.Length; i++)
        {
            var f = specs[i];
            int camp = -1;
            foreach (var (t, c) in campaignOf) if (t == f.Id) { if (camp >= 0) throw new ArgumentException("formation mapped twice"); camp = c; }
            if (camp < 0) throw new ArgumentException($"tactical formation {f.Id} has no campaign formation");
            // fix r1 (W-R1-2): one tactical formation per campaign formation (the collapse splits a formation's whole balance by one
            // input's entity counts; a campaign formation fielded as several battle formations is refused, not mis-split)
            for (int j = 0; j < i; j++)
                if (inputs[j].CampaignFormation == camp)
                    throw new ArgumentException($"campaign formation {camp} is mapped from two tactical formations ({specs[j].Id} and {f.Id})");
            var counts = new long[CategoryCount];
            if (f.Domain == Domain.Land)
            {
                foreach (var v in views)
                    if (v.FormationId == f.Id) counts[(int)CategoryOf(v.State, v.Hp, f.Profile.MaxHp, v.RoutedEscape)]++;
            }
            else
            {
                var p = result.Personnel ?? throw new InvalidOperationException("a multi-domain battle without a personnel report");
                counts[(int)CollapseCategory.Dead] = p.Count(f.Id, PersonnelPlace.Killed) + p.Count(f.Id, PersonnelPlace.LostWithVessel);
                counts[(int)CollapseCategory.Surrendered] = p.Count(f.Id, PersonnelPlace.Captured);
                counts[(int)CollapseCategory.Fit] = p.Count(f.Id, PersonnelPlace.Field) + p.Count(f.Id, PersonnelPlace.Aboard)
                                                   + p.Count(f.Id, PersonnelPlace.Crew) + p.Count(f.Id, PersonnelPlace.Escaped);
            }
            long morale = 0;
            foreach (var r in result.Formations) if (r.FormationId == f.Id) morale = r.MoraleEnd;
            inputs[i] = new CollapseInput(camp, f.Side, counts, morale, setup.Rules.MoraleMax);
        }
        return inputs;
    }

    /// <summary>The setup's formations ordered by id (insertion sort on the integer id: deterministic, no comparer over scores).</summary>
    private static FormationSpec[] SortedSpecs(FormationSpec[] specs)
    {
        var a = (FormationSpec[])specs.Clone();
        for (int i = 1; i < a.Length; i++)
        {
            var x = a[i];
            int j = i - 1;
            while (j >= 0 && a[j].Id > x.Id) { a[j + 1] = a[j]; j--; }
            a[j + 1] = x;
        }
        return a;
    }
}

/// <summary>
/// A finished field battle handed to the outcome world at its end instant (the stand-in for the M7.15 battle → campaign hand-off):
/// the war, the two sides' polities, the deciding mechanism and its end code, the side that held the field (-1 none) and one
/// collapse input per campaign formation that fought (exactly one: a repeated campaign formation is refused at scheduling).
/// </summary>
public sealed record FieldBattle(int WarId, long StartMs, int Side0Polity, int Side1Polity, BattleMechanism Mechanism, byte EndCode, int HoldsField,
                                 CollapseInput[] Formations);
