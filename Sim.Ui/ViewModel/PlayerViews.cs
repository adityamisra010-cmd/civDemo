using System.Globalization;
using Sim.Core.Observability;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;

namespace Sim.Ui.ViewModel;

/// <summary>
/// One line of a player view (M5 polish UR-3): its plain-language <see cref="Text"/> and, when it states a quantity,
/// that <see cref="Figure"/> — which the panel sets in a VALUE COLUMN (right-aligned, in the data face) rather than at
/// the end of the sentence. <see cref="ToString"/> is the line as one string, "text (figure)", as it always read.
/// </summary>
public sealed record ViewLine(string Text, string Figure = "")
{
    public static implicit operator ViewLine(string text) => new(text);

    public override string ToString() => Figure.Length == 0 ? Text : Text + " (" + Figure + ")";
}

/// <summary>One headed block of a player view: a heading and its plain-language rows.</summary>
public sealed record ViewBlock(string Heading, IReadOnlyList<ViewLine> Rows)
{
    /// <summary>A block of figure-less lines.</summary>
    public ViewBlock(string heading, IReadOnlyList<string> lines) : this(heading, Of(lines)) { }

    /// <summary>Each row as one string ("text (figure)").</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            var lines = new string[Rows.Count];
            for (int i = 0; i < lines.Length; i++) lines[i] = Rows[i].ToString();
            return lines;
        }
    }

    private static ViewLine[] Of(IReadOnlyList<string> lines)
    {
        var rows = new ViewLine[lines.Count];
        for (int i = 0; i < rows.Length; i++) rows[i] = new ViewLine(lines[i]);
        return rows;
    }
}

/// <summary>A player-facing view: its SUBJECT (the panel's title — the settlement's name, "Your empire"), a
/// subtitle (whose it is), and its blocks, in reading order.</summary>
public sealed record PlayerView(string Title, IReadOnlyList<ViewBlock> Blocks)
{
    /// <summary>Under the title: whose the subject is ("yours", "free people, under no empire"), or empty.</summary>
    public string Subtitle { get; init; } = "";
}

/// <summary>
/// ADR-033 D9 / audit E37 — THE PLAYER-FACING VIEWS. The record dumps (SETTLEMENT tabs, ECONOMY tables, the
/// TURN audit) were the only home of most M2–M5 state; these views say the same state in plain language so
/// the player can understand the civilization without the developer surfaces. Pure and read-only: every
/// figure is a READ of the live world, a field of the latest SettlementRecord, or a public reader the
/// simulation itself calls (SettlementHappiness, TaxBurdenReading, Governance, LabourActivities,
/// ConstructionQuery, InstitutionsQuery, EmpireQuery). Nothing here is a second formula.
///
/// Information density follows the era theme's <see cref="Sim.Ui.Theme.DensityTokens.Level"/>: every statement
/// carries its figure at every level (M5 polish, Director directive 2026-10-06 §5/§6 — the player needs the numbers
/// in the Age the first hundred turns are played in; density gates only BREAKDOWNS); from level 3 the breakdown
/// lines (births and deaths, the causes' readings, staff and maturity) are added. The words never change with the
/// level — only how much is said — so an era change never contradicts what the player read before.
///
/// Deterministic: table and content order; the one ordering (trade flows by quantity) is a composite key
/// (quantity descending, table index ascending) over longs. No dictionary, no LINQ (law 5).
/// </summary>
public static class PlayerViews
{
    /// <summary>Density level at which statements carry their figures: every level (UR-3; was 2, which left the
    /// A1 Settlement and Empire panels without a single number).</summary>
    public const int FiguresFrom = 1;

    /// <summary>Density level at which the breakdown lines are added.</summary>
    public const int BreakdownFrom = 3;

    private static string N(long v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static string N(double v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    private static string Pct(double fraction) => (fraction * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";
    private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>A statement and its figure (the figure only from <see cref="FiguresFrom"/> — every level).</summary>
    private static ViewLine Fig(int level, string text, string figure) =>
        level >= FiguresFrom ? new ViewLine(text, figure) : new ViewLine(text);

    // --- words ---------------------------------------------------------------------------------

    /// <summary>The word for a 0..100 happiness reading.</summary>
    public static string HappinessWord(double happiness) =>
        double.IsNaN(happiness) ? "unknown"
        : happiness >= 75.0 ? "thriving"
        : happiness >= 55.0 ? "content"
        : happiness >= 35.0 ? "uneasy"
        : happiness >= 15.0 ? "unhappy"
        : "miserable";

    /// <summary>The word for a 0..1 provision factor (food or housing sufficiency).</summary>
    public static string ProvisionWord(double sufficiency) =>
        double.IsNaN(sufficiency) ? "unknown"
        : sufficiency >= 0.98 ? "fully met"
        : sufficiency >= 0.85 ? "mostly met"
        : sufficiency >= 0.6 ? "short"
        : "badly short";

    /// <summary>The word for a normalized labour share.</summary>
    public static string ShareWord(double share) =>
        share >= 0.5 ? "most hands"
        : share >= 0.25 ? "many hands"
        : share >= 0.1 ? "some hands"
        : share > 0.0 ? "a few hands"
        : "no one";

    /// <summary>The word for a population change over a turn.</summary>
    public static string TrendWord(long delta, long opening)
    {
        if (delta == 0) return "steady";
        double rel = opening > 0 ? (double)delta / opening : 1.0;
        if (delta > 0) return rel >= 0.05 ? "growing fast" : "growing";
        return rel <= -0.05 ? "shrinking fast" : "shrinking";
    }

    /// <summary>The words for a food state (the record's classification of the turn just played).</summary>
    public static string FoodStateWord(FoodStateKind state, FamineReason reason) => state switch
    {
        FoodStateKind.Normal => "no hunger",
        FoodStateKind.Stress => "lean times",
        FoodStateKind.Severe => "severe hunger",
        FoodStateKind.Famine => reason switch
        {
            FamineReason.Disaster => "FAMINE - the harvest was struck by disaster",
            FamineReason.Abandonment => "FAMINE - the fields were abandoned",
            FamineReason.Both => "FAMINE - disaster struck abandoned fields",
            _ => "FAMINE",
        },
        _ => "unknown",
    };

    // --- the settlement ------------------------------------------------------------------------

    /// <summary>
    /// The SETTLEMENT player view of <paramref name="settlementId"/>: people and their trend, food (stores,
    /// harvest, deficit, famine state, the weather), housing, happiness and its causes (food, housing, the tax
    /// burden), migration in and out and why, its labour, structures, institutions and the units stationed.
    /// <paramref name="record"/> is the settlement's latest SettlementRecord (null before the first End Turn —
    /// the turn-flow lines then say so, the stocks still read the live world).
    /// </summary>
    public static PlayerView Settlement(
        IReadOnlyWorldState world, SimConfig cfg, PolityId player, SettlementRecord? record,
        int settlementId, Func<int, string> name, int density)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(name);
        var id = new SettlementId(settlementId);
        bool exists = false;
        for (int i = 0; i < world.Settlements.Count; i++)
            if (world.Settlements[i].Id == id) { exists = true; break; }
        if (!exists) return new PlayerView("No settlement selected", [new ViewBlock("", new[] { "Select a settlement on the map." })]);

        bool controlled = EmpireQuery.TryGetController(world, id, out PolityId owner);
        string ownerWord = !controlled ? "free people, under no empire"
            : owner.Value == player.Value ? "yours"
            : "held by empire " + owner.Value.ToString(CultureInfo.InvariantCulture);
        string title = name(settlementId);

        var blocks = new List<ViewBlock>
        {
            People(world, record, id, density),
            Food(world, record, id, density),
            Housing(world, cfg, record, id, density),
            Happiness(world, cfg, id, density),
            Migration(record, name, density),
            Work(world, cfg, player, id, controlled && owner.Value == player.Value, density),
            Structures(world, cfg, id, density),
            new ViewBlock("Institutions", InstitutionLinesAt(world, cfg, id, name, density, includeSite: false)),
            Units(world, cfg, id, name, player, density),
        };
        return new PlayerView(title, blocks) { Subtitle = ownerWord };
    }

    private static long Population(IReadOnlyWorldState world, SettlementId id)
    {
        long pop = 0;
        for (int i = 0; i < world.Buckets.Count; i++)
            if (world.Buckets[i].Settlement == id) pop += world.Buckets[i].Count.Value;
        return pop;
    }

    private static ViewBlock People(IReadOnlyWorldState world, SettlementRecord? record, SettlementId id, int density)
    {
        long pop = Population(world, id);
        var lines = new List<ViewLine>();
        if (pop == 0) { lines.Add("No one lives here any more."); return new ViewBlock("People", lines); }
        lines.Add(Fig(density, "People live here", N(pop) + " people"));
        if (density >= FiguresFrom)
            lines.Add("children " + N(BandViews.Children(world.Buckets, id)) + ", adults " + N(BandViews.Adults(world.Buckets, id))
                + ", elders " + N(BandViews.Elders(world.Buckets, id)));
        if (record is null) lines.Add("The trend shows after the first turn.");
        else
        {
            long delta = record.Population.Closing - record.Population.Opening;
            lines.Add(Fig(density, "The population is " + TrendWord(delta, record.Population.Opening),
                delta.ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture) + " last turn"));
            if (density >= BreakdownFrom)
                lines.Add("born " + N(record.Population.Births) + ", died " + N(record.Population.Deaths)
                    + ", arrived " + N(record.Population.Inflow) + ", left " + N(record.Population.Outflow)
                    + ", left to found colonies " + N(record.Population.ColonistsDeparted));
        }
        return new ViewBlock("People", lines);
    }

    private static ViewBlock Food(IReadOnlyWorldState world, SettlementRecord? record, SettlementId id, int density)
    {
        long store = 0, harvest = 0;
        for (int i = 0; i < world.GoodStocks.Count; i++)
            if (world.GoodStocks[i].Settlement == id && world.GoodStocks[i].Good == UiGoods.Grain)
            { store = world.GoodStocks[i].Amount.Value; harvest = world.GoodStocks[i].LastProducedUnits; break; }
        double deficit = 0.0;
        long demand = 0;
        for (int i = 0; i < world.ConsumptionDeficits.Count; i++)
            if (world.ConsumptionDeficits[i].Settlement == id)
            { deficit = world.ConsumptionDeficits[i].DeficitRatio; demand = world.ConsumptionDeficits[i].DemandUnits; break; }

        var lines = new List<ViewLine>
        {
            Fig(density, store > 0 ? "Grain is in store" : "The granaries are empty", N(store) + " grain"),
            Fig(density, harvest > 0 ? "The last harvest came in" : "No harvest came in", "+" + N(harvest)),
        };
        if (density >= BreakdownFrom && demand > 0)
            lines.Add("the people need about " + N(demand) + " a turn; the store covers "
                + ((double)store / demand).ToString("0.0", CultureInfo.InvariantCulture) + " turns");
        lines.Add(deficit > 0.0
            ? Fig(density, "Not everyone ate - part of the food the people needed was missing", Pct(deficit) + " short")
            : "Everyone ate their fill.");
        if (record is not null)
        {
            FoodStateSection fs = record.FoodState;
            lines.Add("Food state: " + FoodStateWord(fs.State, fs.Reason) + ".");
            if (fs.HarvestWeatherRowPresent)
            {
                double w = fs.HarvestWeatherApplied;
                string word = w >= 1.05 ? "kind - a good year" : w <= 0.95 ? "poor - the harvest suffered" : "ordinary";
                lines.Add(Fig(density, "The weather was " + word, "harvest x" + F2(w)));
            }
            if (fs.DisasterPendingRowPresent && fs.DisasterPendingMultiplier < 1.0)
                lines.Add(Fig(density, "A crop failure will strike next turn's harvest",
                    "harvest x" + F2(fs.DisasterPendingMultiplier)));
        }
        return new ViewBlock("Food", lines);
    }

    private static ViewBlock Housing(IReadOnlyWorldState world, SimConfig cfg, SettlementRecord? record, SettlementId id, int density)
    {
        var lines = new List<ViewLine>();
        long dwellings = -1;
        for (int i = 0; i < world.Housing.Count; i++)
            if (world.Housing[i].Settlement == id) { dwellings = world.Housing[i].Dwellings.Value; break; }
        if (dwellings < 0) { lines.Add("No homes have been built yet."); return new ViewBlock("Housing", lines); }
        double perDwelling = cfg.Housing?.PersonsPerDwelling ?? double.NaN;
        double sufficiency = cfg.Housing is null ? double.NaN : SettlementHappiness.HousingSufficiency(world, id, cfg);
        lines.Add(Fig(density, "There are homes", N(dwellings) + " dwellings for about " + N(dwellings * perDwelling) + " people"));
        lines.Add(Fig(density, "Shelter is " + ProvisionWord(sufficiency), Pct(sufficiency) + " housed"));
        if (density >= BreakdownFrom && record is not null && record.Housing.HasRow)
            lines.Add("dwellings " + (record.Housing.DwellingsClosing - record.Housing.DwellingsOpening).ToString("+#,0;-#,0;0", CultureInfo.InvariantCulture)
                + " last turn; upkeep materials met " + Pct(record.Housing.LastMaintenanceFraction));
        return new ViewBlock("Housing", lines);
    }

    private static ViewBlock Happiness(IReadOnlyWorldState world, SimConfig cfg, SettlementId id, int density)
    {
        var lines = new List<ViewLine>();
        if (cfg.Housing is null) { lines.Add("Happiness is not measured in this world."); return new ViewBlock("Happiness", lines); }
        double happiness = SettlementHappiness.Of(world, id, cfg);
        Span<double> factors = stackalloc double[SettlementHappiness.FactorCount];
        SettlementHappiness.Factors(world, id, cfg, factors);
        double food = factors[(int)SettlementHappiness.Factor.Food];
        double housing = factors[(int)SettlementHappiness.Factor.Housing];
        TaxBurdenReading tax = TaxBurdenReading.Of(world, cfg, id);

        lines.Add(Fig(density, "The people are " + HappinessWord(happiness), N(happiness) + " of 100"));
        lines.Add(Fig(density, "Food: " + ProvisionWord(food), Pct(food)));
        lines.Add(Fig(density, "Shelter: " + ProvisionWord(housing), Pct(housing)));
        if (tax.PolicyRowPresent && tax.EffectiveRate > 0.0)
            lines.Add(Fig(density, "The tax burden weighs on them",
                "declared " + Pct(tax.NominalRate) + " x reach " + Pct(tax.ControlStrength) + " = " + Pct(tax.EffectiveRate) + " taken"));
        else lines.Add("No tax is taken here.");
        UnrestLines(world, cfg, id, density, lines);

        // The cause that costs the most: the lowest of the three readings (ties: food, then shelter, then tax —
        // the factor order, a stable integer tie-break).
        double taxScale = tax.Scale;
        int worst = 0;
        double worstValue = food;
        if (housing < worstValue) { worst = 1; worstValue = housing; }
        if (taxScale < worstValue) { worst = 2; worstValue = taxScale; }
        if (worstValue < 0.98)
            lines.Add("What weighs most: " + (worst == 0 ? "hunger" : worst == 1 ? "lack of shelter" : "the tax burden") + ".");
        return new ViewBlock("Happiness", lines);
    }

    /// <summary>
    /// H2 (Director 2026-10-05 §4/§18): what the levy has built up here — the accumulated resentment (levy pressure,
    /// which scales happiness and fades only slowly after a cut), protest (part of the levied work withheld) and, per
    /// population segment (class), a RISING: the portion of that segment in open revolt. Read through the public
    /// State.Unrest readers the simulation itself uses; nothing is computed here.
    /// </summary>
    private static void UnrestLines(IReadOnlyWorldState world, SimConfig cfg, SettlementId id, int density, List<ViewLine> lines)
    {
        double pressure = Unrest.LevyPressure(world, id, cfg);
        if (!(pressure > 0.005)) return;
        lines.Add(Fig(density, "Resentment of the levy has built up - it fades only slowly once the levy eases",
            Pct(pressure) + " of their contentment consumed"));
        double protest = Unrest.Protest(world, id, cfg);
        if (protest > 0.0)
            lines.Add(Fig(density, "They protest the levy and withhold part of the levied work", Pct(protest) + " protest"));
        ClassEntry[] classes = cfg.Registries.Classes;
        for (int c = 0; c < classes.Length; c++)
        {
            var cls = new ClassId(classes[c].Id);
            if (!Unrest.IsSegmentRisen(world, id, cls, cfg)) continue;
            double rebels = Unrest.SegmentRebelFraction(world, id, cls, cfg);
            lines.Add(Fig(density, "The " + classes[c].Name.ToLowerInvariant() + " have RISEN against the levy"
                + (rebels > 0.0 ? " - part of them in open revolt, refusing all levied work" : " - at the brink of revolt"),
                Pct(rebels) + " of them in revolt"));
        }
        double rebelShare = Unrest.RebelShare(world, id, cfg);
        if (rebelShare > 0.0 && cfg.Needs?.Unrest is { } u)
            lines.Add(Fig(density, rebelShare > u.UprisingPopulationShare
                    ? "The rebels carry the settlement - it will throw off its ruler"
                    : "The settlement holds while the rebels are fewer than " + Pct(u.UprisingPopulationShare) + " of its people",
                Pct(rebelShare) + " of the people in revolt"));
    }

    private static ViewBlock Migration(SettlementRecord? record, Func<int, string> name, int density)
    {
        var lines = new List<ViewLine>();
        if (record is null) { lines.Add("Movement shows after the first turn."); return new ViewBlock("Migration", lines); }
        long inflow = record.Population.Inflow, outflow = record.Population.Outflow, colonists = record.Population.ColonistsDeparted;
        if (inflow == 0 && outflow == 0 && colonists == 0) lines.Add("No one came or left last turn.");
        if (inflow > 0) lines.Add(Fig(density, "Newcomers arrived", N(inflow)));
        if (outflow > 0) lines.Add(Fig(density, "People left for other settlements", N(outflow)));
        if (colonists > 0) lines.Add(Fig(density, "Settlers left to found a new colony", N(colonists)));

        // Why: the push (hunger), the pull (how this place compares with the most attractive other one), the
        // refusal (no room for newcomers) — each the planner's own reading.
        MigrationSection m = record.Migration;
        if (m.PushDeficitRatio > 0.0)
            lines.Add(Fig(density, "Hunger pushes people to leave", Pct(m.PushDeficitRatio) + " of food short"));
        if (!double.IsNaN(m.PullAttractiveness))
        {
            double best = double.NegativeInfinity;
            int bestId = int.MaxValue;
            foreach (AttractivenessReading a in m.AllAttractiveness)
            {
                if (a.Settlement == record.Settlement || double.IsNaN(a.Value)) continue;
                if (a.Value > best || (a.Value == best && a.Settlement < bestId)) { best = a.Value; bestId = a.Settlement; }
            }
            if (bestId != int.MaxValue)
                lines.Add(m.PullAttractiveness >= best
                    ? "This is the most attractive place to live nearby, so it draws people."
                    : Fig(density, name(bestId) + " looks more attractive, which draws people away",
                        F2(best) + " against " + F2(m.PullAttractiveness)));
        }
        if (record.MigrationPlan.PlanRecorded && record.MigrationPlan.VacancyScale < 1.0 && record.MigrationPlan.DesiredInflowAe > 0.0)
            lines.Add("Some who wanted to come were turned away - there is no room or food for more.");
        return new ViewBlock("Migration", lines);
    }

    private static ViewBlock Work(IReadOnlyWorldState world, SimConfig cfg, PolityId player, SettlementId id, bool ours, int density)
    {
        var lines = new List<ViewLine>();
        if (!ours) { lines.Add("You do not direct the labour here."); return new ViewBlock("Work", lines); }
        foreach (LabourActivity a in LabourActivities.For(world, cfg, player))
        {
            if (a.Settlement != id) continue;
            string goods = a.GoodNames.Length == 0 ? "" : " - " + string.Join(", ", a.GoodNames);
            lines.Add(Fig(density, ShareWord(a.Share) + ": " + a.Label + goods, Pct(a.Share)));
        }
        if (lines.Count == 0) lines.Add("No labour is directed here.");
        return new ViewBlock("Work", lines);
    }

    private static ViewBlock Structures(IReadOnlyWorldState world, SimConfig cfg, SettlementId id, int density)
    {
        var lines = new List<ViewLine>();
        ConstructionProjectEntry[] projects = cfg.Goods?.Projects ?? [];
        for (int i = 0; i < world.Structures.Count; i++)
        {
            StructureRow s = world.Structures[i];
            if (s.Settlement != id || s.Count <= 0) continue;
            string label = ProjectName(projects, s.ProjectId);
            lines.Add(s.Count == 1 ? label : label + " x" + N(s.Count));
        }
        if (lines.Count == 0) lines.Add("Nothing has been built here yet.");
        ConstructionQueueRow[] queue = ConstructionQuery.Queue(world, id);
        for (int q = 0; q < queue.Length; q++)
        {
            string label = ProjectName(projects, queue[q].ProjectId);
            string? blocker = null;
            if (q == 0 && density >= FiguresFrom)
                for (int p = 0; p < projects.Length; p++)
                    if (projects[p].Id == queue[q].ProjectId)
                    { blocker = ConstructionQuery.Blocker(world, cfg, id, projects[p], world.Clock.DtYears); break; }
            lines.Add((q == 0 ? "Being built: " : "Waiting to be built: ") + label + (blocker is null ? "" : " - " + blocker));
        }
        return new ViewBlock("Structures", lines);
    }

    private static string ProjectName(ConstructionProjectEntry[] projects, int projectId)
    {
        for (int p = 0; p < projects.Length; p++) if (projects[p].Id == projectId) return projects[p].Name;
        return "structure " + projectId.ToString(CultureInfo.InvariantCulture);
    }

    private static ViewBlock Units(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId id, Func<int, string> name, PolityId player, int density)
    {
        var lines = new List<ViewLine>();
        UnitFamilyContent? fam = cfg.UnitFamilies;
        for (int i = 0; i < world.MilitaryUnits.Count; i++)
        {
            MilitaryUnitRow u = world.MilitaryUnits[i];
            if (u.Location != id) continue;
            string unit = fam?.IdentityByKey(u.Identity)?.Name ?? fam?.FamilyByKey(u.Family)?.Name ?? "Formation";
            string whose = u.Owner.Value == player.Value ? "Your" : "The empire of " + u.Owner.Value.ToString(CultureInfo.InvariantCulture) + "'s";
            lines.Add(Fig(density, whose + " " + unit + " is stationed here",
                "experience " + u.Experience.ToString("0.0", CultureInfo.InvariantCulture)));
        }
        if (lines.Count == 0) lines.Add("No formations are stationed here.");
        return new ViewBlock("Units", lines);
    }

    // --- the institutions ----------------------------------------------------------------------

    /// <summary>The institutions founded in <paramref name="settlement"/> (InstitutionsQuery.At): type,
    /// stage and maturity, staff, whether the host sustains it, and the effect it produces now.</summary>
    public static IReadOnlyList<ViewLine> InstitutionLinesAt(
        IReadOnlyWorldState world, SimConfig cfg, SettlementId settlement, Func<int, string> name, int density, bool includeSite)
    {
        var lines = new List<ViewLine>();
        InstitutionInstanceView[] here = InstitutionsQuery.At(world, cfg, settlement);
        foreach (InstitutionInstanceView v in here) AddInstitution(lines, world, cfg, v, name, density, includeSite);
        if (lines.Count == 0) lines.Add("No institution has been founded here.");
        return lines;
    }

    private static void AddInstitution(
        List<ViewLine> lines, IReadOnlyWorldState world, SimConfig cfg, InstitutionInstanceView v,
        Func<int, string> name, int density, bool includeSite)
    {
        string stage = v.Stage == InstitutionLifecycle.Mature ? "mature" : "growing";
        string head = v.TypeName + (includeSite ? " at " + name(v.Settlement.Value) : "") + " - " + stage;
        lines.Add(Fig(density, head, Pct(v.Maturity) + " mature"));
        if (density >= BreakdownFrom)
            lines.Add("  employs about " + N(v.Staff) + " adults; founded turn " + v.FoundedTurn.ToString(CultureInfo.InvariantCulture));
        ViewLine effect = EffectNow(world, cfg, v, density);
        lines.Add(effect with { Text = "  " + effect.Text });
        if (!v.Viable)
            lines.Add("  At risk: its town cannot sustain it" + (v.ViabilityBlocker is { Length: > 0 } b ? " (" + b + ")" : "") + ".");
    }

    /// <summary>The effect an institution produces NOW: the healing type its settlement's mortality multiplier
    /// (InstitutionsQuery.Health); every other type the research-cost factor its owner's branch carries
    /// (InstitutionsQuery.Specialties — the stored modifier ResearchSystem reads).</summary>
    public static ViewLine EffectNow(IReadOnlyWorldState world, SimConfig cfg, InstitutionInstanceView v, int density)
    {
        if (v.Heals)
        {
            MedicalCoverageView health = InstitutionsQuery.Health(world, cfg, v.Settlement);
            return health.MortalityMultiplier < 1.0
                ? Fig(density, "Fewer people die here", "deaths x" + F2(health.MortalityMultiplier))
                : "It does not yet lower deaths here.";
        }
        foreach (UniversitySpecialtyView s in InstitutionsQuery.Specialties(world, cfg, v.Owner))
        {
            if (s.TypeKey != v.TypeKey) continue;
            return s.ResearchFactor < 1.0
                ? Fig(density, "Research in " + s.Branch + " is cheaper", "cost x" + F2(s.ResearchFactor))
                : "It does not yet make research in " + s.Branch + " cheaper.";
        }
        return "Its effect is not measured.";
    }

    /// <summary>
    /// The INSTITUTIONS panel for <paramref name="player"/>: every institution the empire owns (type, site,
    /// stage, maturity, staff, viability, the effect now), then each specialty's standing — how many, the
    /// research factor, and what one more would add (diminishing returns made visible).
    /// </summary>
    public static PlayerView Institutions(
        IReadOnlyWorldState world, SimConfig cfg, PolityId player, Func<int, string> name, int density)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        var owned = new List<ViewLine>();
        foreach (InstitutionInstanceView v in InstitutionsQuery.Instances(world, cfg, player))
            AddInstitution(owned, world, cfg, v, name, density, includeSite: true);
        if (owned.Count == 0)
            owned.Add("Your empire has founded no institution yet. A university is founded when its building is "
                + "completed in a town large enough to sustain it.");

        var specialties = new List<ViewLine>();
        foreach (UniversitySpecialtyView s in InstitutionsQuery.Specialties(world, cfg, player))
        {
            if (s.Count == 0)
            {
                if (density >= BreakdownFrom) specialties.Add(s.TypeName + ": none");
                continue;
            }
            string reading = s.Reading switch
            {
                SaturationReading.Saturated => "another would add almost nothing",
                SaturationReading.Diminishing => "another would add less than the first",
                _ => "not yet mature",
            };
            specialties.Add(Fig(density, s.TypeName + " (" + N(s.Count) + "): " + reading,
                "cost x" + F2(s.ResearchFactor) + ", one more cuts " + Pct(s.NextMarginalCut)));
        }
        if (specialties.Count == 0) specialties.Add("No specialty yet.");

        return new PlayerView("Institutions of your empire",
            [new ViewBlock("Founded", owned), new ViewBlock("Specialties", specialties)]);
    }

    // --- the empire ----------------------------------------------------------------------------

    /// <summary>
    /// The EMPIRE player view: population, grain, trade in words, legitimacy and the levy (when taxation is
    /// known), roads built and the formations under arms.
    /// </summary>
    public static PlayerView Empire(
        IReadOnlyWorldState world, SimConfig cfg, PolityId player, Func<int, string> name, int density)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(name);
        var mine = new List<SettlementId>();
        for (int s = 0; s < world.Settlements.Count; s++)
        {
            SettlementId id = world.Settlements[s].Id;
            if (EmpireQuery.ControlsSettlement(world, player, id)) mine.Add(id);
        }

        // Realm and people.
        long pop = 0;
        foreach (SettlementId id in mine) pop += Population(world, id);
        var realm = new List<ViewLine>
        {
            Fig(density, "Your empire holds " + (mine.Count == 1 ? "one settlement" : N(mine.Count) + " settlements"), N(pop) + " people"),
        };
        if (EmpireQuery.TryGetCapital(world, player, out SettlementId capital))
            realm.Add("The capital is " + name(capital.Value) + ".");

        // Grain.
        long grain = 0, harvest = 0;
        int hungry = 0;
        foreach (SettlementId id in mine)
        {
            for (int i = 0; i < world.GoodStocks.Count; i++)
                if (world.GoodStocks[i].Settlement == id && world.GoodStocks[i].Good == UiGoods.Grain)
                { grain += world.GoodStocks[i].Amount.Value; harvest += world.GoodStocks[i].LastProducedUnits; break; }
            for (int i = 0; i < world.ConsumptionDeficits.Count; i++)
                if (world.ConsumptionDeficits[i].Settlement == id && world.ConsumptionDeficits[i].DeficitRatio > 0.0) { hungry++; break; }
        }
        var food = new List<ViewLine>
        {
            Fig(density, "Grain in your granaries", N(grain)),
            Fig(density, "Last harvest across the empire", "+" + N(harvest)),
            hungry == 0 ? "No settlement of yours went hungry."
                : (hungry == 1 ? "One settlement" : N(hungry) + " settlements") + " went hungry last turn.",
        };

        // Trade in words: the flows touching the empire, largest first (quantity desc, table index asc).
        var trade = new List<ViewLine>();
        var idx = new List<int>();
        for (int i = 0; i < world.TradeFlows.Count; i++)
        {
            TradeFlowRow f = world.TradeFlows[i];
            if (f.Quantity <= 0) continue;
            if (mine.Contains(f.From) || mine.Contains(f.To)) idx.Add(i);
        }
        idx.Sort((a, b) =>
        {
            int c = world.TradeFlows[b].Quantity.CompareTo(world.TradeFlows[a].Quantity);
            return c != 0 ? c : a.CompareTo(b);
        });
        int shown = density >= BreakdownFrom ? 6 : 3;
        for (int k = 0; k < idx.Count && k < shown; k++)
        {
            TradeFlowRow f = world.TradeFlows[idx[k]];
            trade.Add(Fig(density, GoodName(cfg, f.Good) + " went from " + name(f.From.Value) + " to " + name(f.To.Value), N(f.Quantity)));
        }
        if (idx.Count > shown) trade.Add("... and " + N(idx.Count - shown) + " smaller exchanges.");
        if (trade.Count == 0) trade.Add("No goods were traded last turn - every settlement lived on what it made.");

        // Governance: legitimacy and the levy.
        var rule = new List<ViewLine>();
        double legitimacy = cfg.Housing is null ? double.NaN : Governance.Legitimacy(world, player, cfg);
        rule.Add(Fig(density, "Your people regard your rule as " + LegitimacyWord(legitimacy), N(legitimacy) + " of 100"));
        TaxGate gate = Governance.GateOf(world, cfg, player);
        if (gate == TaxGate.Open)
        {
            double rate = Governance.NominalTaxRate(world, player);
            rule.Add(Governance.HasPolicy(world, player) && rate > 0.0
                ? Fig(density, "You levy a tax", Pct(rate) + " declared; distant towns pay less as your reach thins")
                : "You know how to levy a tax, but have declared none.");
        }
        else if (gate == TaxGate.NeedsAge && cfg.Ages is { } taxAges && cfg.Governance?.TaxationMinAge is { } taxAge)
            // H2 (Director 2026-10-05 §7): the knowledge is held, the capability opens with the Age — say so rather
            // than leave a known civic looking like it does nothing.
            rule.Add("Your scholars know Taxation, but no levy can be raised before the "
                + StateChronicle.AgeName(taxAges, taxAge) + " (Age " + taxAge.ToString(CultureInfo.InvariantCulture) + ").");
        else rule.Add("Your people do not yet know how to levy a tax.");

        // Roads: travelled routes touching the empire, by class.
        var roads = new List<ViewLine>();
        int routes = 0;
        double km = 0.0;
        for (int i = 0; i < world.TransportEdges.Count; i++)
        {
            TransportEdgeRow e = world.TransportEdges[i];
            if (!RoadPerformance.IsTravelled(e)) continue;
            if (!mine.Contains(e.A) && !mine.Contains(e.B)) continue;
            routes++;
            km += e.LengthKm;
            if (density >= BreakdownFrom)
                roads.Add(Sim.Ui.World.RoadLens.ClassName(cfg, e.EdgeType) + ": " + name(e.A.Value) + " - " + name(e.B.Value)
                    + (e.TargetClass != e.EdgeType && e.Modernization > 0.0
                        ? " (becoming " + Sim.Ui.World.RoadLens.ClassName(cfg, e.TargetClass) + ", " + Pct(e.Modernization) + ")" : ""));
        }
        roads.Insert(0, routes == 0 ? "No roads built yet - travellers use dirt paths."
            : Fig(density, (routes == 1 ? "One road" : N(routes) + " roads") + " link your settlements", N(km) + " km"));

        // Arms.
        int units = 0;
        for (int i = 0; i < world.MilitaryUnits.Count; i++) if (world.MilitaryUnits[i].Owner.Value == player.Value) units++;
        var arms = new List<ViewLine> { units == 0 ? "You have no formations under arms." : Fig(density, "Formations under arms", N(units)) };

        return new PlayerView("Your empire",
        [
            new ViewBlock("Realm", realm), new ViewBlock("Grain", food), new ViewBlock("Trade", trade),
            new ViewBlock("Rule", rule), new ViewBlock("Roads", roads), new ViewBlock("Arms", arms),
        ]);
    }

    /// <summary>The word for a 0..100 legitimacy reading.</summary>
    public static string LegitimacyWord(double legitimacy) =>
        double.IsNaN(legitimacy) ? "unknown"
        : legitimacy >= 70.0 ? "just"
        : legitimacy >= 50.0 ? "acceptable"
        : legitimacy >= 30.0 ? "doubtful"
        : "illegitimate";

    private static string GoodName(SimConfig cfg, GoodId good)
    {
        if (cfg.Goods is { } goods)
            foreach (GoodEntry g in goods.Goods) if (g.Id == good.Value) return Capital(g.Name);
        return "Good " + good.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
