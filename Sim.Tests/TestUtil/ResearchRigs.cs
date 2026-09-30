using System.Globalization;
using System.Text.Json.Nodes;
using Sim.Core;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Research;

namespace Sim.Tests.TestUtil;

/// <summary>
/// Test rigs for the research engine (ADR-029). <see cref="Spec"/> builds a small,
/// VALID research.json in memory — the real loader validates it, so every rig graph
/// passes the same rules as the shipped one — and <see cref="World"/> builds a
/// hand-made world whose CLP is exact: <c>adults</c> adults in cohort 5 of each
/// controlled settlement, and CLP/yr = sqrt(total adults) at the rig tuning. At
/// adults = 10 000 and dt = 10 years that is exactly 1 000 CLP per turn.
/// </summary>
internal static class ResearchRigs
{
    public const int Player = 1;

    public sealed record Eu(string Text, string? When, string Status = "evaluable");

    public sealed record Node(
        int Key, string Id, string? Prereq = null, double Cost = 2500.0, string? Branch = null,
        Eu[]? Eurekas = null, string[]? Institutions = null, string[]? Buildings = null);

    public sealed record Entity(string Id, string Kind, string? Requires, string[]? Unresolved = null);

    /// <summary>A research.json document. Depth, dependents-derived orphan capability
    /// text and the reverse unlock lists are filled in here so the content is valid.</summary>
    public sealed class Spec
    {
        public List<Node> Technologies { get; } = [];
        public List<Node> Civics { get; } = [];
        public List<Entity> Entities { get; } = [];
        public string Stage { get; set; } = "a";
        public double Coefficient { get; set; } = 1.0;
        public double Exponent { get; set; } = 0.5;
        public double EurekaFraction { get; set; } = 0.25;

        public string Json()
        {
            var all = new List<Node>(Technologies);
            all.AddRange(Civics);
            var depth = new Dictionary<string, int>(StringComparer.Ordinal); // test-side builder, not sim logic
            int Depth(Node n)
            {
                if (depth.TryGetValue(n.Id, out int d)) return d;
                int best = 0;
                foreach (string a in Atoms(n.Prereq))
                    if (all.Find(x => x.Id == a) is { } p) best = Math.Max(best, 1 + Depth(p)); // unknown atoms: the loader rejects them
                depth[n.Id] = best;
                return best;
            }

            JsonObject NodeJson(Node n, bool civic)
            {
                var entityLists = new Dictionary<string, JsonArray>(StringComparer.Ordinal)
                {
                    ["buildings"] = [], ["infrastructure"] = [], ["institutions"] = [],
                    ["units"] = [], ["activities"] = [], ["projects"] = [],
                };
                string[] listFor = ["buildings", "infrastructure", "institutions", "units", "activities", "projects"];
                string[] kinds = ["building", "infrastructure", "institution", "unit", "activity", "project"];
                foreach (Entity e in Entities)
                    if (Atoms(e.Requires).Contains(n.Id))
                        entityLists[listFor[Array.IndexOf(kinds, e.Kind)]].Add(e.Id);
                var eurekas = new JsonArray();
                foreach (Eu eu in n.Eurekas ?? [])
                    eurekas.Add(new JsonObject { ["text"] = eu.Text, ["when"] = eu.When, ["status"] = eu.Status });
                var o = new JsonObject
                {
                    ["key"] = n.Key, ["id"] = n.Id, ["name"] = n.Id.ToUpperInvariant(), ["desc"] = "rig node",
                    ["age"] = "A1", ["frontier"] = false, ["emerged"] = null,
                };
                if (!civic) o["branch"] = n.Branch;
                o["domain"] = "science";
                o["secondaryDomains"] = new JsonArray();
                o["depth"] = Depth(n);
                o["cost"] = n.Cost;
                o["prereq"] = n.Prereq;
                o["eurekas"] = eurekas;
                o["unlocks"] = new JsonObject
                {
                    ["capabilities"] = new JsonArray($"{n.Id} capability"),
                    ["units"] = entityLists["units"], ["buildings"] = entityLists["buildings"],
                    ["institutions"] = entityLists["institutions"], ["infrastructure"] = entityLists["infrastructure"],
                    ["activities"] = entityLists["activities"], ["projects"] = entityLists["projects"],
                    ["techniques"] = new JsonArray(), ["applications"] = new JsonArray(),
                };
                o["family"] = null;
                o["generation"] = null;
                o["effects"] = new JsonObject { ["immediate"] = new JsonArray() };
                o["repeatable"] = null;
                return o;
            }

            var techs = new JsonArray();
            foreach (Node n in Technologies) techs.Add(NodeJson(n, civic: false));
            var civs = new JsonArray();
            foreach (Node n in Civics) civs.Add(NodeJson(n, civic: true));
            var ents = new JsonArray();
            foreach (Entity e in Entities)
            {
                var un = new JsonArray();
                foreach (string u in e.Unresolved ?? []) un.Add(u);
                ents.Add(new JsonObject { ["id"] = e.Id, ["kind"] = e.Kind, ["name"] = e.Id, ["requires"] = e.Requires, ["unresolved"] = un });
            }
            var doc = new JsonObject
            {
                ["schema"] = ResearchContentLoader.Schema,
                ["source"] = new JsonObject
                {
                    ["corpus"] = "rig", ["corpusVersion"] = "0", ["corpusSha256"] = new string('0', 64), ["generator"] = "ResearchRigs",
                },
                ["tuning"] = new JsonObject
                {
                    ["clpCoefficient"] = Coefficient, ["clpAdultExponent"] = Exponent, ["eurekaCreditFraction"] = EurekaFraction,
                },
                ["trees"] = new JsonArray(
                    new JsonObject { ["id"] = "technology", ["number"] = "1", ["name"] = "Technology" },
                    new JsonObject { ["id"] = "civics", ["number"] = "2", ["name"] = "Civics" }),
                ["branches"] = new JsonArray(
                    Branch(1, "military", "1.1", "Military"), Branch(2, "medicine", "1.2", "Medicine"),
                    Branch(3, "engineering", "1.3", "Engineering"), Branch(4, "natural_science", "1.4", "Natural Science"),
                    Branch(5, "agriculture", "1.5", "Agriculture")),
                ["researchStage"] = new JsonObject { ["requires"] = Stage },
                ["universityTypes"] = new JsonArray(
                    Uni(1, "military_university", "military"), Uni(2, "medical_university", "medicine"),
                    Uni(3, "engineering_university", "engineering"), Uni(4, "natural_science_university", "natural_science"),
                    Uni(5, "agricultural_university", "agriculture")),
                ["technologies"] = techs,
                ["civics"] = civs,
                ["entities"] = ents,
            };
            return doc.ToJsonString();
        }

        public ResearchContent Load() => ResearchContentLoader.Load(Json(), TestConfigs.Sim().Goods);

        private static JsonObject Branch(int key, string id, string number, string name) =>
            new() { ["key"] = key, ["id"] = id, ["number"] = number, ["name"] = name };

        private static JsonObject Uni(int key, string id, string branch) =>
            new() { ["key"] = key, ["id"] = id, ["name"] = id, ["branch"] = branch };
    }

    /// <summary>The atoms of a corpus-grammar expression (ids only).</summary>
    public static List<string> Atoms(string? expr)
    {
        var result = new List<string>();
        if (expr is null) return result;
        foreach (string tok in expr.Replace("(", " ").Replace(")", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (tok is not ("AND" or "OR" or "NOT") && !result.Contains(tok)) result.Add(tok);
        return result;
    }

    /// <summary>
    /// The standard rig graph used across the engine tests:
    /// <code>
    ///  trunk:  a, b (roots) · c = a AND b · d = a OR b · e = (a AND c) OR (b AND d)
    ///  stage:  c
    ///  subtrees (one entry node each, all require a): mil, med, eng, sci, agr
    ///  civics: law (a) · code (law AND c)
    ///  eureka: w (trunk, requires a) "stock_timber &gt; 0" and "c"
    ///  entity: building.hall requires c
    /// </code>
    /// Every cost is 2500 CLP unless stated.
    /// </summary>
    public static Spec Standard()
    {
        var s = new Spec { Stage = "c" };
        s.Technologies.AddRange([
            new Node(1, "a"), new Node(2, "b"),
            new Node(3, "c", "a AND b"), new Node(4, "d", "a OR b"),
            new Node(5, "e", "(a AND c) OR (b AND d)"),
            new Node(6, "mil", "a", Branch: "military"), new Node(7, "med", "a", Branch: "medicine"),
            new Node(8, "eng", "a", Branch: "engineering"), new Node(9, "sci", "a", Branch: "natural_science"),
            new Node(10, "agr", "a", Branch: "agriculture"),
            new Node(11, "w", "a", Eurekas: [new Eu("circumstance: timber", "stock_timber > 0"), new Eu("circumstance: c", "c")]),
        ]);
        s.Civics.AddRange([new Node(1001, "law", "a"), new Node(1002, "code", "law AND c")]);
        s.Entities.Add(new Entity("building.hall", "building", "c"));
        return s;
    }

    /// <summary>A hand-made world: polities (all Ai except <see cref="Player"/>), settlements
    /// 0..n-1 each controlled by the polity at the same position in <paramref name="controllers"/>
    /// with <paramref name="adults"/> adults, and optional good stocks in settlement 0.</summary>
    public static WorldState World(int[] polities, int[] controllers, long adults, (int Good, long Qty)[]? stocks = null)
    {
        var w = new WorldState(5);
        var ledger = new Ledger(w.LedgerFlows);
        foreach (int p in polities)
            w.Polities.Add(new PolityRow(new PolityId(p), p == Player ? CommandSource.Player : CommandSource.Ai));
        for (int s = 0; s < controllers.Length; s++)
        {
            var id = new SettlementId(s);
            w.Settlements.Add(new SettlementRow(id, SiteCell: s, FoundedTurn: 0));
            w.Controls.Add(new ControlRow(new PolityId(controllers[s]), id, 1.0));
            int row = w.Buckets.Add(new BucketRow(id, new CultureId(1), new ReligionId(1), new ClassId(1),
                cohortIdx: 5, Conserved.Zero, 0.0, 0.0, 0.0, 0.0));
            ledger.Flow(ref w.Buckets.Ref(row).Count, ConservedQuantityIds.Population,
                ReasonIds.InitialEndowment, adults, FlowDirection.Source, OverdrawPolicy.Throw);
        }
        foreach ((int good, long qty) in stocks ?? []) Stock(w, 0, good, qty);
        return w;
    }

    /// <summary>Endows settlement <paramref name="settlement"/> with <paramref name="qty"/> of a good (a ledger Source flow).</summary>
    public static WorldState Stock(WorldState w, int settlement, int good, long qty)
    {
        int row = w.GoodStocks.Add(new GoodStockRow(new SettlementId(settlement), new GoodId(good), Conserved.Zero, 0.0, 0.0));
        new Ledger(w.LedgerFlows).Flow(ref w.GoodStocks.Ref(row).Amount, ConservedQuantityIds.OfGood(new GoodId(good)),
            ReasonIds.InitialEndowment, qty, FlowDirection.Source, OverdrawPolicy.Throw);
        return w;
    }

    /// <summary>One polity (the player), one settlement of 10 000 adults: 1 000 CLP per dt-10 turn.</summary>
    public static WorldState PlayerWorld((int Good, long Qty)[]? stocks = null) => World([Player], [Player], 10_000, stocks);

    public static EraTable FlatEra(double dtYears) => EraTableLoader.Load(
        $$"""{ "bands": [ { "name": "flat", "startYear": 0, "endYear": 100000, "dtYears": {{dtYears.ToString(CultureInfo.InvariantCulture)}} } ] }""");

    /// <summary>An executor running ONLY the research system over <paramref name="content"/>.</summary>
    public static TurnExecutor Executor(ResearchContent? content, OrderLog? orders = null, double dtYears = 10.0) =>
        new(FlatEra(dtYears), [SystemCatalog.Research(TestConfigs.Sim() with { Research = content })], orders);

    public static OrderRecord Target(long turn, int key, int polity = Player) =>
        OrderRecord.From(turn, new PolityId(polity), OrderKind.SetResearchTarget, key, 0.0);

    public static ResearchNodeId Key(int key) => new(key);

    public static double Progress(IReadOnlyWorldState w, int key, int polity = Player) =>
        ResearchQuery.Progress(w, new PolityId(polity), new ResearchNodeId(key));

    public static bool Done(IReadOnlyWorldState w, int key, int polity = Player) =>
        ResearchQuery.IsCompleted(w, new PolityId(polity), new ResearchNodeId(key));

    /// <summary>Marks nodes complete directly (a world "as if" they had been researched).</summary>
    public static WorldState WithCompleted(WorldState w, params int[] keys)
    {
        foreach (int k in keys) w.ResearchCompleted.Add(new ResearchCompletedRow(new PolityId(Player), new ResearchNodeId(k)));
        return w;
    }

    /// <summary>Steps <paramref name="turns"/> times.</summary>
    public static WorldState Run(TurnExecutor ex, WorldState w, int turns)
    {
        for (int t = 0; t < turns; t++) w = ex.Step(w);
        return w;
    }
}
