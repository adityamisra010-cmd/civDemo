using System.Globalization;
using Sim.Core.Chronicle;
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
///
/// P7 — FILTERED BY SUBJECT: with a <see cref="PlayerKnowledge"/> (UiSession passes the player's, built from the world
/// the step produced), an event is appended only when the player knows EVERY settlement and polity it names (a
/// settlement the player does not know is never visited, its rows never read), and a re-equipment counts only the
/// formations the player knows. All-known (or no knowledge given): every event, exactly as before.
///
/// ADR-059 amendment 2026-10-29 — THE CODED RECORD. With a <see cref="ChronicleCode.Record"/> (UiSession passes it for the
/// session record only), every event appended to the annals is also appended there as its coded line, built from the
/// same values the prose prints (never from the prose): <c>Y&lt;year&gt; &lt;EV&gt; &lt;fields&gt;</c>, a settlement as
/// ST&lt;id&gt;, a polity as PL&lt;id&gt;, a content thing by its key (RN research node, AG Age, RC road class, PJ project,
/// IN institution type, CL class, ID unit identity), each named once in the record's name table. The prose is unchanged.
/// </summary>
public sealed class StateChronicle
{
    private static string Year(IReadOnlyWorldState w) =>
        ((long)Math.Round(w.Clock.WorldDateYears)).ToString(CultureInfo.InvariantCulture);

    private static string Pct(double f) => Whole(f) + "%";

    /// <summary>A rate as the prose prints it, without the % (the coded record's TB / TN value).</summary>
    private static string Whole(double f) => (f * 100.0).ToString("0", CultureInfo.InvariantCulture);

    private static string Id(int v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>"your empire" for the player's polity, "the empire of N" otherwise.</summary>
    public static string Polity(PolityId p) =>
        p.Value == UiPlayer.Empire.Value ? "your empire" : "the empire of " + p.Value.ToString(CultureInfo.InvariantCulture);

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Appends to <paramref name="annals"/> the events of the step <paramref name="prev"/> → <paramref name="next"/>
    /// that <paramref name="knowledge"/> admits (null: every event).
    /// D-060: with <paramref name="playerBooks"/> (the in-game ANNALS view; UiSession's session record passes false), an
    /// event that tells another empire's BOOKS is left out — a rival's tax levy, an institution maturing, a merchant class
    /// emerging or a segment rising or subsiding at a settlement whose books the player may not read
    /// (<see cref="PlayerKnowledge.ReadsInternals"/>). Its PUBLIC events stay: an Age entered (the banner on the map), a
    /// road built, a structure completed and an institution founded (each drawn on the map), a change of ruler, a
    /// re-equipment of formations the player knows.</summary>
    public void Observe(IReadOnlyWorldState prev, IReadOnlyWorldState next, SimConfig cfg, Func<int, string> name, List<string> annals,
        PlayerKnowledge? knowledge = null, bool playerBooks = false, ChronicleCode.Record? coded = null)
    {
        ArgumentNullException.ThrowIfNull(prev);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(annals);
        string y = "In the year " + Year(next) + ", ";
        string cy = "Y" + Year(next) + " ";
        // One event: its prose to the annals and, for the session record, its coded line (same admission, same order).
        void Tell(string prose, Func<string> code)
        {
            annals.Add(prose);
            coded?.Add(cy + code());
        }
        // A named thing's code, its name noted in the record's name table.
        string N(string code, string thing)
        {
            coded?.Note(code, thing);
            return code;
        }
        string St(SettlementId s) => N(ChronicleCode.Settlement(s.Value), name(s.Value));
        string Pl(PolityId p) => "PL" + Id(p.Value);
        // P7: the subjects an event names must all be known to the player.
        bool S(SettlementId s) => knowledge is null || !knowledge.IsHiddenSettlement(s.Value);
        bool P(PolityId p) => knowledge is null || knowledge.KnowsPolity(p.Value);
        // D-060: the books of a settlement / a polity's own policy, told only in the player's view when they are its own.
        PlayerKnowledge? reader = playerBooks ? knowledge ?? PlayerKnowledge.Of(next) : null;
        bool Books(SettlementId s) => reader is null || reader.ReadsInternals(s.Value);
        bool OwnPolicy(PolityId p) => reader is null || p.Value == reader.Player.Value;

        for (int pi = 0; pi < next.Polities.Count; pi++)
        {
            PolityId p = next.Polities[pi].Id;
            if (!P(p)) continue;   // P7: nothing is told of a polity the player has no contact with
            // Research completed (the player's — rivals' research is not shown, audit §C 9).
            if (p.Value == UiPlayer.Empire.Value && cfg.Research is { } research)
            {
                bool[] before = ResearchQuery.CompletedMask(prev, research, p);
                bool[] after = ResearchQuery.CompletedMask(next, research, p);
                for (int n = 0; n < after.Length && n < research.Nodes.Count; n++)
                    if (after[n] && (n >= before.Length || !before[n]))
                    {
                        ResearchNode node = research.Nodes[n];
                        Tell(y + "your scholars mastered " + node.Name + ".",
                            () => "RM " + Pl(p) + " " + N("RN" + Id(node.Key.Value), node.Name));
                    }
            }
            // Age entered.
            if (cfg.Ages is { } ages)
            {
                int a0 = AgeQuery.CurrentAge(prev, ages, p), a1 = AgeQuery.CurrentAge(next, ages, p);
                if (a1 != a0)
                    Tell(y + Polity(p) + " entered the " + AgeName(ages, a1) + " age.",
                        () => "AE " + Pl(p) + " " + N("AG" + Id(a1), AgeName(ages, a1)));
            }
            // Tax levy changed (D-060: a rival's levy is its own policy — not told in the player's view).
            if (!OwnPolicy(p)) continue;
            double r0 = Governance.HasPolicy(prev, p) ? Governance.NominalTaxRate(prev, p) : 0.0;
            double r1 = Governance.HasPolicy(next, p) ? Governance.NominalTaxRate(next, p) : 0.0;
            if (r1 != r0)
                Tell(y + Cap(Polity(p)) + (r1 == 0.0 ? " abolished its tax." : r0 == 0.0
                    ? " levied a tax of " + Pct(r1) + "." : " changed its tax from " + Pct(r0) + " to " + Pct(r1) + "."),
                    () => "TL " + Pl(p) + " TB" + Whole(r0) + " TN" + Whole(r1));
        }

        // Roads built or modernized: travelled edges new to next, or of a higher class than in prev.
        for (int i = 0; i < next.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = next.TransportEdges[i];
            if (!RoadPerformance.IsTravelled(e) || e.EdgeType == EdgeTypes.DirtPath) continue;
            if (!S(e.A) || !S(e.B)) continue;
            int oldType = -1;
            if (TransportQuery.TryGetEdge(prev, e.Id, out TransportEdgeRow old) && RoadPerformance.IsTravelled(old)) oldType = old.EdgeType;
            if (oldType == e.EdgeType) continue;
            string cls = Sim.Ui.World.RoadLens.ClassName(cfg, e.EdgeType);
            Tell(y + (oldType < 0
                ? "a " + cls.ToLowerInvariant() + " was built between " + name(e.A.Value) + " and " + name(e.B.Value) + "."
                : "the road between " + name(e.A.Value) + " and " + name(e.B.Value) + " became a " + cls.ToLowerInvariant() + "."),
                () => (oldType < 0 ? "RD " : "RU ") + St(e.A) + " " + St(e.B) + " " + N("RC" + Id(e.EdgeType), cls));
        }

        // Construction completed: a structure count that rose.
        ConstructionProjectEntry[] projects = cfg.Goods?.Projects ?? [];
        for (int i = 0; i < next.Structures.Count; i++)
        {
            StructureRow s = next.Structures[i];
            if (!S(s.Settlement)) continue;
            long was = ConstructionQuery.Built(prev, s.Settlement, s.ProjectId);
            if (s.Count <= was) continue;
            string pname = "a structure";
            string? proper = null;
            for (int k = 0; k < projects.Length; k++) if (projects[k].Id == s.ProjectId) { pname = "a " + projects[k].Name.ToLowerInvariant(); proper = projects[k].Name; break; }
            string pj = "PJ" + Id(s.ProjectId);
            Tell(y + name(s.Settlement.Value) + " completed " + pname + ".",
                () => "SC " + St(s.Settlement) + " " + (proper is null ? pj : N(pj, proper)));
        }

        // Institutions founded or matured.
        for (int i = 0; i < next.Institutions.Count; i++)
        {
            InstitutionRow row = next.Institutions[i];
            if (!S(row.Settlement)) continue;
            string? typeName = InstitutionContent.Info(cfg, row.Type)?.Name;
            string type = typeName ?? "an institution";
            string Inst() => typeName is null ? "IN" + Id(row.Type) : N("IN" + Id(row.Type), typeName);
            int at = -1;
            for (int k = 0; k < prev.Institutions.Count; k++) if (prev.Institutions[k].Id == row.Id) { at = k; break; }
            if (at < 0)
                Tell(y + "the " + type + " was founded at " + name(row.Settlement.Value) + ".",
                    () => "IF " + Inst() + " " + St(row.Settlement));
            else if (Books(row.Settlement) && InstitutionsQuery.Stage(cfg, row.Type, row.Maturity) == InstitutionLifecycle.Mature
                     && InstitutionsQuery.Stage(cfg, row.Type, prev.Institutions[at].Maturity) != InstitutionLifecycle.Mature)
                Tell(y + "the " + type + " at " + name(row.Settlement.Value) + " came to maturity.",
                    () => "IM " + Inst() + " " + St(row.Settlement));
        }

        // Control lost (revolt or capture) and merchants emerging, per settlement present in both worlds.
        int merchant = MerchantClass(cfg);
        for (int i = 0; i < next.Settlements.Count; i++)
        {
            SettlementId s = next.Settlements[i].Id;
            if (!S(s)) continue;
            bool existed = false;
            for (int k = 0; k < prev.Settlements.Count; k++) if (prev.Settlements[k].Id == s) { existed = true; break; }
            if (!existed) continue;
            bool had = EmpireQuery.TryGetController(prev, s, out PolityId was);
            bool has = EmpireQuery.TryGetController(next, s, out PolityId now);
            if (had && (!has || now.Value != was.Value) && P(was) && (!has || P(now)))
                Tell(y + name(s.Value) + (has
                    ? " passed from " + Polity(was) + " to " + Polity(now) + "."
                    : " rose up and threw off the rule of " + Polity(was) + "."),
                    () => has ? "CP " + St(s) + " PF" + Id(was.Value) + " PT" + Id(now.Value) : "RV " + St(s) + " PF" + Id(was.Value));
            if (!Books(s)) continue;   // D-060: its classes and their risings are its books
            if (merchant >= 0 && ClassCount(prev, s, merchant) == 0 && ClassCount(next, s, merchant) > 0)
                Tell(y + "a merchant class emerged in " + name(s.Value) + ".", () => "ME " + St(s));

            // H2 (Director 2026-10-05 §4/§18): a population SEGMENT (class) rising against the levy, or its rising
            // subsiding — the segment's own tipping point, read through the public Unrest reader on both worlds.
            ClassEntry[] classes = cfg.Registries.Classes;
            for (int c = 0; c < classes.Length; c++)
            {
                var cls = new ClassId(classes[c].Id);
                bool rose = Unrest.IsSegmentRisen(prev, s, cls, cfg), risen = Unrest.IsSegmentRisen(next, s, cls, cfg);
                if (risen == rose) continue;
                string who = "the " + classes[c].Name.ToLowerInvariant() + " of " + name(s.Value);
                ClassEntry entry = classes[c];
                Tell(y + (risen ? who + " rose against the levy." : "the rising of " + who + " subsided."),
                    () => (risen ? "SR " : "SS ") + N("CL" + Id(entry.Id), entry.Name) + " " + St(s));
            }
        }

        // Units modernized at an Age transition: the conversion-log rows this step appended.
        int converted = 0;
        PolityId convOwner = default;
        string convTo = "";
        int convKey = 0;
        bool convNamed = false;
        for (int i = prev.UnitConversions.Count; i < next.UnitConversions.Count; i++)
        {
            UnitConversionRow c = next.UnitConversions[i];
            if (c.Outcome != 1 && c.Outcome != 2) continue;
            if (knowledge is not null && !KnowsConverted(next, knowledge, c)) continue;   // P7: a formation the player knows
            if (converted > 0 && c.Owner.Value != convOwner.Value) { Flush(); }
            converted++;
            convOwner = c.Owner;
            string? identity = cfg.UnitFamilies?.IdentityByKey(c.ToIdentity)?.Name;
            convTo = identity ?? "a new formation";
            convKey = c.ToIdentity;
            convNamed = identity is not null;
        }
        Flush();

        void Flush()
        {
            if (converted == 0) return;
            int count = converted;
            PolityId owner = convOwner;
            string to = convTo, idCode = "ID" + Id(convKey);
            bool named = convNamed;
            Tell(y + Polity(convOwner) + " re-equipped " + (converted == 1 ? "a formation" : converted.ToString(CultureInfo.InvariantCulture) + " formations")
                + " for the new age (" + convTo + ").",
                () => "RQ " + Pl(owner) + " FC" + Id(count) + " " + (named ? N(idCode, to) : idCode));
            converted = 0;
        }
    }

    /// <summary>P7: whether the player knows the formation a conversion row is about — its owner (contact) and the
    /// formation itself as it stands in <paramref name="next"/>.</summary>
    private static bool KnowsConverted(IReadOnlyWorldState next, PlayerKnowledge knowledge, UnitConversionRow c)
    {
        if (!knowledge.KnowsPolity(c.Owner.Value)) return false;
        for (int u = 0; u < next.MilitaryUnits.Count; u++)
            if (next.MilitaryUnits[u].Id == c.Unit) return knowledge.KnowsUnit(next.MilitaryUnits[u]);
        return knowledge.AllKnown;
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
