using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Core.Systems.Research;

namespace Sim.Ui.ViewModel;

/// <summary>
/// Audit E24 / item 3 — THE ANNAL EVENTS THE CORE CHRONICLE DOES NOT DETECT, derived from the difference
/// between the world a step began from and the world it produced. No new simulation state: every event is a
/// comparison of two READS of authoritative rows (completed research, the Age state, transport edges,
/// structures, institutions, the control relation, class buckets, the unit-conversion log, tax policies)
/// through the public readers the systems themselves use. UI-side history like the core chronicle: replay
/// through the same session rebuilds it exactly, because it is a pure function of each (prev, next) pair.
///
/// Deterministic: polities, settlements, rows and nodes are scanned in table / content order; no dictionary
/// iteration, no LINQ (law 5). Within one step the events append in the fixed order of <see cref="Observe"/>.
/// </summary>
public sealed class StateChronicle
{
    private static string Year(IReadOnlyWorldState w) =>
        ((long)Math.Round(w.Clock.WorldDateYears)).ToString(CultureInfo.InvariantCulture);

    private static string Pct(double f) => (f * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>"your empire" for the player's polity, "the empire of N" otherwise.</summary>
    public static string Polity(PolityId p) =>
        p.Value == UiPlayer.Empire.Value ? "your empire" : "the empire of " + p.Value.ToString(CultureInfo.InvariantCulture);

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Appends to <paramref name="annals"/> the events of the step <paramref name="prev"/> → <paramref name="next"/>.</summary>
    public void Observe(IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, Func<int, string> name, List<string> annals)
    {
        ArgumentNullException.ThrowIfNull(prev);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(annals);
        string y = "In the year " + Year(next) + ", ";

        for (int pi = 0; pi < next.Polities.Count; pi++)
        {
            PolityId p = next.Polities[pi].Id;
            // Research completed (the player's — rivals' research is not shown, audit §C 9).
            if (p.Value == UiPlayer.Empire.Value && cfg.Research is { } research)
            {
                bool[] before = ResearchQuery.CompletedMask(prev, research, p);
                bool[] after = ResearchQuery.CompletedMask(next, research, p);
                for (int n = 0; n < after.Length && n < research.Nodes.Count; n++)
                    if (after[n] && (n >= before.Length || !before[n]))
                        annals.Add(y + "your scholars mastered " + research.Nodes[n].Name + ".");
            }
            // Age entered.
            if (cfg.Ages is { } ages)
            {
                int a0 = AgeQuery.CurrentAge(prev, ages, p), a1 = AgeQuery.CurrentAge(next, ages, p);
                if (a1 != a0)
                    annals.Add(y + Polity(p) + " entered the " + AgeName(ages, a1) + " age.");
            }
            // Tax levy changed.
            double r0 = Governance.HasPolicy(prev, p) ? Governance.NominalTaxRate(prev, p) : 0.0;
            double r1 = Governance.HasPolicy(next, p) ? Governance.NominalTaxRate(next, p) : 0.0;
            if (r1 != r0)
                annals.Add(y + Cap(Polity(p)) + (r1 == 0.0 ? " abolished its tax." : r0 == 0.0
                    ? " levied a tax of " + Pct(r1) + "." : " changed its tax from " + Pct(r0) + " to " + Pct(r1) + "."));
        }

        // Roads built or modernized: travelled edges new to next, or of a higher class than in prev.
        for (int i = 0; i < next.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = next.TransportEdges[i];
            if (!RoadPerformance.IsTravelled(e) || e.EdgeType == EdgeTypes.DirtPath) continue;
            int oldType = -1;
            if (TransportQuery.TryGetEdge(prev, e.Id, out TransportEdgeRow old) && RoadPerformance.IsTravelled(old)) oldType = old.EdgeType;
            if (oldType == e.EdgeType) continue;
            string cls = Sim.Ui.World.RoadLens.ClassName(cfg, e.EdgeType);
            annals.Add(y + (oldType < 0
                ? "a " + cls.ToLowerInvariant() + " was built between " + name(e.A.Value) + " and " + name(e.B.Value) + "."
                : "the road between " + name(e.A.Value) + " and " + name(e.B.Value) + " became a " + cls.ToLowerInvariant() + "."));
        }

        // Construction completed: a structure count that rose.
        ConstructionProjectEntry[] projects = cfg.Goods?.Projects ?? [];
        for (int i = 0; i < next.Structures.Count; i++)
        {
            StructureRow s = next.Structures[i];
            long was = ConstructionQuery.Built(prev, s.Settlement, s.ProjectId);
            if (s.Count <= was) continue;
            string pname = "a structure";
            for (int k = 0; k < projects.Length; k++) if (projects[k].Id == s.ProjectId) { pname = "a " + projects[k].Name.ToLowerInvariant(); break; }
            annals.Add(y + name(s.Settlement.Value) + " completed " + pname + ".");
        }

        // Institutions founded or matured.
        for (int i = 0; i < next.Institutions.Count; i++)
        {
            InstitutionRow row = next.Institutions[i];
            string type = InstitutionContent.TypeOf(cfg.Research, row.Type)?.Name ?? "an institution";
            int at = -1;
            for (int k = 0; k < prev.Institutions.Count; k++) if (prev.Institutions[k].Id == row.Id) { at = k; break; }
            if (at < 0)
                annals.Add(y + "the " + type + " was founded at " + name(row.Settlement.Value) + ".");
            else if (InstitutionsQuery.Stage(cfg, row.Maturity) == InstitutionLifecycle.Mature
                     && InstitutionsQuery.Stage(cfg, prev.Institutions[at].Maturity) != InstitutionLifecycle.Mature)
                annals.Add(y + "the " + type + " at " + name(row.Settlement.Value) + " came to maturity.");
        }

        // Control lost (revolt or capture) and merchants emerging, per settlement present in both worlds.
        int merchant = MerchantClass(cfg);
        for (int i = 0; i < next.Settlements.Count; i++)
        {
            SettlementId s = next.Settlements[i].Id;
            bool existed = false;
            for (int k = 0; k < prev.Settlements.Count; k++) if (prev.Settlements[k].Id == s) { existed = true; break; }
            if (!existed) continue;
            bool had = EmpireQuery.TryGetController(prev, s, out PolityId was);
            bool has = EmpireQuery.TryGetController(next, s, out PolityId now);
            if (had && (!has || now.Value != was.Value))
                annals.Add(y + name(s.Value) + (has
                    ? " passed from " + Polity(was) + " to " + Polity(now) + "."
                    : " rose up and threw off the rule of " + Polity(was) + "."));
            if (merchant >= 0 && ClassCount(prev, s, merchant) == 0 && ClassCount(next, s, merchant) > 0)
                annals.Add(y + "a merchant class emerged in " + name(s.Value) + ".");
        }

        // Units modernized at an Age transition: the conversion-log rows this step appended.
        int converted = 0;
        PolityId convOwner = default;
        string convTo = "";
        for (int i = prev.UnitConversions.Count; i < next.UnitConversions.Count; i++)
        {
            UnitConversionRow c = next.UnitConversions[i];
            if (c.Outcome != 1 && c.Outcome != 2) continue;
            if (converted > 0 && c.Owner.Value != convOwner.Value) { Flush(); }
            converted++;
            convOwner = c.Owner;
            convTo = cfg.UnitFamilies?.IdentityByKey(c.ToIdentity)?.Name ?? "a new formation";
        }
        Flush();

        void Flush()
        {
            if (converted == 0) return;
            annals.Add(y + Polity(convOwner) + " re-equipped " + (converted == 1 ? "a formation" : converted.ToString(CultureInfo.InvariantCulture) + " formations")
                + " for the new age (" + convTo + ").");
            converted = 0;
        }
    }

    /// <summary>The full name of Age <paramref name="key"/> from the content.</summary>
    public static string AgeName(AgeContent ages, int key)
    {
        for (int i = 0; i < ages.Ages.Count; i++) if (ages.Ages[i].Key == key) return ages.Ages[i].Name;
        return "Age " + key.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The class registry id whose name names merchants (data-driven; -1 when the registry has none).</summary>
    public static int MerchantClass(SimConfig cfg)
    {
        ClassEntry[] classes = cfg.Registries.Classes;
        for (int c = 0; c < classes.Length; c++)
            if (classes[c].Name.StartsWith("Merchant", StringComparison.OrdinalIgnoreCase)) return classes[c].Id;
        return -1;
    }

    private static long ClassCount(IReadOnlyWorldState w, SettlementId s, int cls)
    {
        long n = 0;
        for (int b = 0; b < w.Buckets.Count; b++)
            if (w.Buckets[b].Settlement == s && w.Buckets[b].Class.Value == cls) n += w.Buckets[b].Count.Value;
        return n;
    }
}
