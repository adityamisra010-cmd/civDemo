using System.Globalization;
using System.Reflection;
using Xunit;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;
using Sim.Core.Systems.Ages;
using Sim.Ui.Headless;
using Sim.Ui.Render;
using Sim.Ui.ViewModel;
using Sim.Ui.World;

namespace Sim.Ui.Tests;

/// <summary>
/// M6.12 (D-F option 1: "the training panel remains a labelled, non-functional placeholder"; D-066 A2 item 4: the slice's
/// inert promotion entry shown "earned in M7") — THE TRAINING PLACEHOLDER ON THE UNIT CARD.
/// <list type="bullet">
/// <item>the line: "Training — planned for M7", then the family tree's first promotion "earned in M7" (or none authored);</item>
/// <item>PROVENANCE: it moves with the formation's family and nothing else of the row; it is the same for every Age form of
/// one family;</item>
/// <item>SOURCE GUARD: the placeholder's source reads no world state, names no order and draws no control, and no other UI
/// source names the progression content; its public methods take only a family and the content;</item>
/// <item>the real UI: the line is drawn, the card's only control is still close, and showing it changes neither the world
/// hash nor the queued orders; the PlayabilityGate row passes in the A1 state.</item>
/// </list>
/// </summary>
[Collection("ImGui context")]
public class UnitTrainingPlaceholderTests : IDisposable
{
    private static readonly PolityId Me = UiPlayer.Empire;

    private readonly string _root = Path.Combine(Path.GetTempPath(),
        "sim-ui-m612-tests-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private static string Assets() => Path.Combine(AppContext.BaseDirectory, "assets");

    private string WorkDir(string name) => Path.Combine(_root, name);

    private static readonly Lazy<UiSession> Base = new(() => UiSession.Start(42), LazyThreadSafetyMode.ExecutionAndPublication);

    private static SimConfig Cfg => Base.Value.Config;

    private static UnitFamilyContent Families => Cfg.UnitFamilies!;

    private static string Name(int id) => Base.Value.Names.Name(id);

    [Fact]
    public void TheLine_NamesTrainingForM7_AndTheFamilyTreesFirstPromotion_EarnedInM7()
    {
        Assert.Equal("Training — planned for M7 · 'Drilled': earned in M7", UnitTrainingPlaceholder.Line(Families.FamilyById("heavy_infantry")));
        Assert.Equal("Training — planned for M7 · 'Practised volleys': earned in M7", UnitTrainingPlaceholder.Line(Families.FamilyById("ranged_infantry")));
        Assert.Equal("Training — planned for M7 · 'Horse care': earned in M7", UnitTrainingPlaceholder.Line(Families.FamilyById("heavy_cavalry")));
        Assert.Equal("Training — planned for M7 · no promotions authored", UnitTrainingPlaceholder.Line(Families.FamilyById("siege")));
        Assert.Equal("Training — planned for M7 · no promotions authored", UnitTrainingPlaceholder.Line(null));
        // The shipped content the UI loads is the validated file (its loader ran): a tree per land-formation group.
        Assert.Equal(3, UnitTrainingPlaceholder.Shipped.Trees.Count);
    }

    [Fact]
    public void TheCardsPlannedLine_MovesWithTheFamilyOnly_AndIsTheSameForEveryAgeFormOfIt()
    {
        WorldState w = Base.Value.World.Clone();
        int at = -1;
        for (int i = 0; i < w.MilitaryUnits.Count; i++) if (w.MilitaryUnits[i].Owner.Value == Me.Value) at = i;
        Assert.True(at >= 0);
        MilitaryUnitRow row = w.MilitaryUnits[at];
        UnitCard card = UnitCardModel.Build(w, Cfg, row.Id, Me, Name)!;
        Assert.Equal(UnitTrainingPlaceholder.Line(Families.FamilyByKey(row.Family)), card.Planned);
        // Every row field but the family leaves the line where it is.
        Func<MilitaryUnitRow, MilitaryUnitRow>[] others =
        [
            r => r with { Experience = 4.0 }, r => r with { Army = 3 }, r => r with { X = r.X + 7.5 }, r => r with { Y = r.Y + 2.25 },
            r => r with { Location = new SettlementId(-1) }, r => r with { Identity = Families.FamilyById("heavy_infantry")!.Line[3].Key },
        ];
        foreach (Func<MilitaryUnitRow, MilitaryUnitRow> change in others)
        {
            WorldState v = w.Clone();
            v.MilitaryUnits[at] = change(row);
            Assert.Equal(card.Planned, UnitCardModel.Build(v, Cfg, row.Id, Me, Name)!.Planned);
        }
        // The family moves it (ranged infantry: its own tree's first promotion).
        UnitFamily ranged = Families.FamilyById("ranged_infantry")!;
        WorldState f = w.Clone();
        f.MilitaryUnits[at] = row with { Family = ranged.Key, Identity = ranged.Line[0].Key };
        Assert.Equal(UnitTrainingPlaceholder.Line(ranged), UnitCardModel.Build(f, Cfg, row.Id, Me, Name)!.Planned);
        Assert.NotEqual(card.Planned, UnitCardModel.Build(f, Cfg, row.Id, Me, Name)!.Planned);
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repository root not found");
    }

    [Fact]
    public void SourceGuard_ThePlaceholderReadsNoWorld_EmitsNoOrder_DrawsNoControl_AndIsTheOnlyUiReaderOfTheContent()
    {
        string root = RepoRoot();
        string text = File.ReadAllText(Path.Combine(root, "Sim.Ui", "ViewModel", "UnitTrainingPlaceholder.cs"));
        foreach (string banned in new[]
                 {
                     "WorldState", "IReadOnlyWorldState", "MilitaryUnits", "OrderRecord", "OrderKind", "OrderLog", "Enqueue",
                     "QueuedOrders", "UiSession", "Session", "ImGui", "Button", "Controls", "Ledger",
                 })
            Assert.DoesNotContain(banned, text, StringComparison.Ordinal);
        // Its public surface takes a family and the content — nothing that could reach the world.
        foreach (MethodInfo m in typeof(UnitTrainingPlaceholder).GetMethods(BindingFlags.Public | BindingFlags.Static))
            foreach (ParameterInfo p in m.GetParameters())
                Assert.Contains(p.ParameterType, new[] { typeof(UnitFamily), typeof(UnitProgressionContent), typeof(Promotion) });
        // No other UI source names the progression content.
        var readers = new List<string>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "Sim.Ui"), "*.cs", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (rel.Contains("/obj/", StringComparison.Ordinal) || rel.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (rel == "Sim.Ui/ViewModel/UnitTrainingPlaceholder.cs") continue;
            string src = File.ReadAllText(file);
            if (src.Contains("UnitProgression", StringComparison.Ordinal) || src.Contains("PromotionTree", StringComparison.Ordinal)
                || src.Contains("TrainingProfile", StringComparison.Ordinal))
                readers.Add(rel);
        }
        Assert.Empty(readers);
    }

    [Fact]
    public void TheLiveCard_DrawsTheLine_ItsOnlyControlIsClose_AndShowingItChangesNothing()
    {
        using UiFrameHarness h = UiFrameHarness.Start(UiSession.Start(42), WorkDir("card"), Assets());
        MilitaryUnitRow unit = MilitaryQuery.Units(h.Ui.World, UiPlayer.Empire)[0];
        CameraFocus.CenterOn(h.Ui.Camera, h.Ui.World, unit.Location.Value, h.Ui.MapRect);
        h.Idle(2);
        string hash = WorldHash.ComputeHex(h.Ui.World);
        int orders = h.Ui.Session.Orders.Count;
        int queued = h.Ui.Session.QueuedOrders().Count;
        UnitPlacement at = h.Ui.LensFrame!.UnitPlacements.Single(p => p.Id == unit.Id);
        h.Click(at.X, at.Y);
        h.Idle(2);
        UnitCard card = h.Ui.UnitCard!;
        Assert.Equal("Training — planned for M7 · 'Drilled': earned in M7", card.Planned);
        Assert.Contains(card.Planned, h.Ui.FrameText.Texts(FrameSurface.UnitCard));
        var onCard = new List<string>();
        foreach (UiControl c in h.Ui.Controls.Last)
            if (c.X0 >= h.Ui.UnitCardRect.X && c.X1 <= h.Ui.UnitCardRect.X + h.Ui.UnitCardRect.Width
                && c.Y0 >= h.Ui.UnitCardRect.Y && c.Y1 <= h.Ui.UnitCardRect.Y + h.Ui.UnitCardRect.Height)
                onCard.Add(c.Name);
        Assert.Equal(["unit-close"], onCard);
        Assert.Equal(0f, h.Ui.UnitCardScroll.Max);   // still unscrolled at the 1280x800 design window
        Assert.Equal(hash, WorldHash.ComputeHex(h.Ui.World));
        Assert.Equal(orders, h.Ui.Session.Orders.Count);
        Assert.Equal(queued, h.Ui.Session.QueuedOrders().Count);
        Assert.Empty(h.Problems);
    }

    [Fact]
    public void TheGateRow_Passes_InTheA1State()
    {
        GateReport r = PlayabilityGate.Run(new GateOptions(Assets(), WorkDir("gate"), MonkeySteps: 0, OnlyStates: ["A1 turn 1"]));
        GateRow row = Assert.Single(r.Rows, x => x.Control == "unit card training placeholder is inert (M6.12)");
        Assert.True(row.Result == GateResult.Pass, row.Detail);
    }
}
