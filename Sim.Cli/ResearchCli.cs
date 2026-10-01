using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Research;

namespace Sim.Cli;

/// <summary>
/// `sim research` — the headless Glass Box for the research engine (ADR-029 §12).
/// It runs the FOUNDED world and prints, for one polity, what
/// <see cref="ResearchQuery"/> answers. It is STRICTLY an observer: every number
/// it prints is a ResearchQuery call on the final world, and the Step loop is the
/// same loop `sim run` drives.
///
/// <c>--auto cheapest</c> is a MEASUREMENT DRIVER, not an AI. Before each step, if
/// the polity has no target and something is available, it appends a
/// SetResearchTarget order stamped with the current turn, choosing the cheapest
/// available node (ResearchQuery.CheapestAvailable: EffectiveCost, then key). The run
/// therefore exercises the real order pathway, and <c>--emit-orders</c> writes the
/// log so that `sim run --founded --orders` replays the same world.
/// Output is invariant-culture and ordered by node key, so it is deterministic.
/// </summary>
internal static class ResearchCli
{
    internal static int Run(string[] args)
    {
        var opts = Options.Parse(args, flags: [],
            valued: ["--seed", "--turns", "--settlements", "--size", "--orders", "--polity", "--node",
                     "--auto", "--emit-orders", "--trace-population"]);
        ulong seed = opts.Seed();
        int turns = opts.Turns();
        int? sizePx = Cli.SizeOpt(opts, founded: true);
        int? settlements = Cli.SettlementsOpt(opts, founded: true);
        var polity = new PolityId((int)opts.LongOr("--polity", 1));
        string? auto = opts.Get("--auto");
        if (auto is not null && auto != "cheapest")
            throw new CliUsageException($"--auto supports only 'cheapest', got '{auto}'");
        if (auto is not null && opts.Get("--orders") is not null)
            throw new CliUsageException("--auto and --orders are exclusive: --auto writes the order log itself");

        ResearchContent content = Cli.SimCfg().Research
            ?? throw new InvalidOperationException("the canonical config carries no research content");
        OrderLog orders = opts.Get("--orders") is { } op ? Cli.LoadOrders(op) : new OrderLog();
        TurnExecutor executor = Cli.Executor(orders, founded: true);
        WorldState world = Cli.StartWorld(seed, true, sizePx, settlements);
        OrderValidation.ValidateAgainstWorld(orders, world);
        if (!EmpireQuery.TryGetCommandSource(world, polity, out _))
            throw new CliUsageException($"--polity {polity.Value} is not a registered Empire in this world");

        var milestones = new List<string>();
        StreamWriter? trace = opts.Get("--trace-population") is { } tp ? new StreamWriter(File.Create(tp)) : null;
        trace?.WriteLine("turn,simYear,dtYears,population,rpPerYear,rpThisTurn");
        int lastCompleted = 0;
        bool stageSeen = false;
        for (int t = 1; t <= turns; t++)
        {
            if (auto is not null && !ResearchQuery.TryGetTarget(world, polity, out _)
                && ResearchQuery.CheapestAvailable(world, content, polity) is { } pick)
            {
                orders.Append(OrderRecord.From(world.Clock.Turn, polity, OrderKind.SetResearchTarget, pick.Value, 0.0));
            }
            world = executor.Step(world);
            if (trace is not null)
            {
                double perYear = ResearchQuery.ResearchPerYear(world, content, polity);
                trace.WriteLine(string.Join(",", world.Clock.Turn.ToString(CultureInfo.InvariantCulture), Year(world),
                    F(world.Clock.DtYears), ResearchQuery.Population(world, polity).ToString(CultureInfo.InvariantCulture),
                    F(perYear), F(perYear * world.Clock.DtYears)));
            }
            int done = ResearchQuery.CompletedNodes(world, content, polity).Length;
            if (done / 50 > lastCompleted / 50)
                milestones.Add($"  {done,4} nodes complete by turn {world.Clock.Turn} (sim-year {Year(world)})");
            lastCompleted = done;
            if (!stageSeen && ResearchQuery.IsStageReached(world, content, polity))
            {
                stageSeen = true;
                milestones.Add($"  research stage reached at turn {world.Clock.Turn} (sim-year {Year(world)}) — the five subtrees open");
            }
        }
        trace?.Dispose();
        if (opts.Get("--emit-orders") is { } emit)
        {
            using var file = File.Create(emit);
            orders.Save(file);
        }

        PrintReport(world, content, polity, seed, orders.Count);
        if (milestones.Count > 0)
        {
            Console.WriteLine("milestones:");
            foreach (string m in milestones) Console.WriteLine(m);
        }
        if (opts.Get("--node") is { } nodeId) PrintNode(world, content, polity, nodeId);
        Console.WriteLine($"world hash {WorldHash.ComputeHex(world)}");
        return 0;
    }

    private static void PrintReport(WorldState world, ResearchContent content, PolityId polity, ulong seed, int orderCount)
    {
        Console.WriteLine($"research report: seed {seed}, turn {world.Clock.Turn} (sim-year {Year(world)}), polity {polity.Value}, {orderCount} order(s)");
        long population = ResearchQuery.Population(world, polity);
        double perYear = ResearchQuery.ResearchPerYear(world, content, polity);
        ResearchTuning tu = content.Tuning;
        Console.WriteLine($"research capacity: RP({population}) = {F(ResearchQuery.ResearchCapacity(tu, population))} per reference turn of {F(tu.RpReferenceTurnYears)} years " +
                          $"(PROVISIONAL CALIBRATION: {F(tu.RpAnchorPerTurn)} x (P/{F(tu.RpAnchorPopulation)})^{F(tu.RpExponent)}); " +
                          $"{F(perYear)} per sim-year; {F(perYear * world.Clock.DtYears)} this turn at dt {F(world.Clock.DtYears)}");
        if (ResearchQuery.TryGetTarget(world, polity, out ResearchNodeId target))
        {
            ResearchNode n = Node(content, target);
            ResearchQuery.CostBreakdown cost = ResearchQuery.EffectiveCostBreakdown(world, content, polity, target);
            Console.WriteLine($"active target: {n.Id} ({Where(content, n)}) {F(ResearchQuery.Progress(world, polity, target))} / {F(cost.EffectiveCost)} CLP");
        }
        else
        {
            Console.WriteLine("active target: none — this turn's CLP reaches no node and is not stored (no general bank, D-044 R20-D)");
        }
        bool stage = ResearchQuery.IsStageReached(world, content, polity);
        Console.WriteLine($"research stage (university / research institutional stage): {(stage ? "REACHED — all five subtrees open" : "not reached — subtrees closed")}");

        ResearchNodeId[] completed = ResearchQuery.CompletedNodes(world, content, polity);
        ResearchNodeId[] available = ResearchQuery.AvailableNodes(world, content, polity);
        Console.WriteLine("view                        complete / total   available");
        Row("Tree 1 — Main Technology", content, completed, available, n => n.Tree == ResearchTree.Technology && n.Branch < 0);
        for (int b = 0; b < content.Branches.Count; b++)
        {
            ResearchBranch br = content.Branches[b];
            int bi = b;
            Row($"Tree 1 — {br.Number} {br.Name}", content, completed, available, n => n.Tree == ResearchTree.Technology && n.Branch == bi);
        }
        Row("Tree 2 — Civics", content, completed, available, n => n.Tree == ResearchTree.Civics);

        Console.WriteLine($"available now ({available.Length}): {Ids(content, available, 20)}");
        ResearchProgressRow[] partial = ResearchQuery.PartialProgress(world, polity);
        Console.WriteLine($"partial progress kept on {partial.Length} node(s):");
        foreach (ResearchProgressRow row in partial)
        {
            ResearchNode n = Node(content, row.Node);
            Console.WriteLine($"  {n.Id,-26} {F(row.Progress)} / {F(ResearchQuery.EffectiveCost(world, content, polity, n.Index))}");
        }
        int fired = 0;
        for (int i = 0; i < world.ResearchEurekas.Count; i++) if (world.ResearchEurekas[i].Polity.Value == polity.Value) fired++;
        Console.WriteLine($"Eureka conditions credited: {fired}");
        ResearchQuery.CostTerm[] modifiers = ResearchQuery.CostModifiers(world, content, polity);
        Console.WriteLine(modifiers.Length == 0
            ? "specialized-university cost modifiers: none (no institutions system writes them yet; EffectiveCost = BaseCost)"
            : $"specialized-university cost modifiers: {modifiers.Length}");
        foreach (ResearchQuery.CostTerm term in modifiers)
            Console.WriteLine($"  {term.UniversityId} -> {content.Branches[term.Branch].Name}: x{F(term.Factor)}");
        Console.WriteLine($"knowledge-eligible registry entities: {ResearchQuery.KnowledgeEligibleEntities(world, content, polity).Length} of {content.Entities.Count}");
        Console.WriteLine($"baseline capabilities (outside the research graph, available with zero completed nodes): {content.Baseline.Count}");
        foreach (ResearchBaselineCapability b in content.Baseline)
            Console.WriteLine($"  {b.Id,-34} {b.Name} — {b.ProvidedBy}{(b.Simulated ? "" : " [not yet simulated]")}");
    }

    private static void PrintNode(WorldState world, ResearchContent content, PolityId polity, string nodeId)
    {
        int index = content.IndexOfId(nodeId);
        if (index < 0) throw new CliUsageException($"--node '{nodeId}' is not a research node id");
        ResearchNode n = content.Nodes[index];
        ResearchQuery.PrerequisiteState pre = ResearchQuery.Prerequisites(world, content, polity, n.Key);
        ResearchQuery.CostBreakdown cost = ResearchQuery.EffectiveCostBreakdown(world, content, polity, n.Key);
        Console.WriteLine($"node {n.Id} (key {n.Key.Value}): {n.Name} — {Where(content, n)}, domain {n.Domain}, age {n.Age}{(n.Frontier ? " (frontier)" : "")}");
        Console.WriteLine($"  state: {(pre.Completed ? "COMPLETED" : pre.Available ? "AVAILABLE" : "LOCKED")}; subtree open: {pre.SubtreeOpen}");
        Console.WriteLine($"  prerequisites: {pre.Expression ?? "(none — a root)"} -> {(pre.Satisfied ? "satisfied" : "not satisfied")}");
        foreach (ResearchQuery.PrerequisiteAtom a in pre.Atoms)
            Console.WriteLine($"    {a.Id,-26} {(a.Completed ? "complete" : "missing")}");
        Console.WriteLine($"  cost: base {F(cost.BaseCost)} -> effective {F(cost.EffectiveCost)} CLP; progress {F(ResearchQuery.Progress(world, polity, n.Key))}");
        foreach (ResearchQuery.CostTerm term in cost.Terms)
            Console.WriteLine($"    x{F(term.Factor)} from {term.UniversityId} (modifier row {term.ModifierRow})");
        ResearchQuery.EurekaProgress ep = ResearchQuery.EurekaProgressOf(world, content, polity, n.Key);
        Console.WriteLine($"  eureka: {ep.Credited}/{ep.Evaluable} evaluable conditions credited; {F(ep.CreditedShare * 100)}% of the full Eureka " +
                          $"({F(content.Tuning.EurekaFullCreditFraction * 100)}% of base cost when fully satisfied); evaluable share {F(ep.EvaluableShare * 100)}%");
        foreach (ResearchQuery.EurekaState e in ResearchQuery.Eurekas(world, content, polity, n.Key))
        {
            Console.WriteLine($"  eureka[{e.Index}] {e.Category} {e.Status} share {F(e.Share * 100)}%: \"{e.Text}\"" +
                              (e.Reason is null ? "" : $" — {e.Reason}") + (e.FutureSystem is null ? "" : $" [future system: {e.FutureSystem}]"));
            foreach (ResearchQuery.EurekaConditionState c in e.Conditions)
                Console.WriteLine($"      condition {c.Condition} `{c.Source}` credit {F(c.Credit)} holds-now={c.HoldsNow} credited={c.Credited}");
        }
        Console.WriteLine($"  unlocks: {n.Capabilities.Count} capabilities, {n.UnlockedEntities.Count} entities, {n.Dependents.Count} dependent nodes");
    }

    private static void Row(string label, ResearchContent content, ResearchNodeId[] completed, ResearchNodeId[] available,
        Func<ResearchNode, bool> inView)
    {
        int total = 0, done = 0, avail = 0;
        foreach (ResearchNode n in content.Nodes) if (inView(n)) total++;
        foreach (ResearchNodeId id in completed) if (inView(Node(content, id))) done++;
        foreach (ResearchNodeId id in available) if (inView(Node(content, id))) avail++;
        Console.WriteLine($"  {label,-30} {done,4} / {total,-4}      {avail,4}");
    }

    private static string Ids(ResearchContent content, ResearchNodeId[] ids, int max)
    {
        var parts = new List<string>();
        for (int i = 0; i < ids.Length && i < max; i++) parts.Add(Node(content, ids[i]).Id);
        if (ids.Length > max) parts.Add($"… +{ids.Length - max}");
        return string.Join(", ", parts);
    }

    private static string Where(ResearchContent content, ResearchNode n) =>
        n.Tree == ResearchTree.Civics ? "Tree 2 Civics"
        : n.Branch < 0 ? "Tree 1 Main"
        : $"Tree 1 {content.Branches[n.Branch].Number} {content.Branches[n.Branch].Name}";

    private static ResearchNode Node(ResearchContent content, ResearchNodeId id) => content.Nodes[content.IndexOf(id)];

    /// <summary>Sim-years elapsed since the campaign start (era-pacing.json's first band);
    /// the clock stores elapsed days, not a calendar year.</summary>
    private static string Year(WorldState world) =>
        Math.Floor(world.Clock.WorldDateYears).ToString("0", CultureInfo.InvariantCulture);

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
