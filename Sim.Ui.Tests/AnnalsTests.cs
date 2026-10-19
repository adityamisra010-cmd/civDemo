using Xunit;

namespace Sim.Ui.Tests;

// T2.9: the annals through the REAL UI seam — founding lines exist at session
// start, the export is EXACTLY the panel's lines, the chronicle path twins the
// order log's stamp, and names resolve for every settlement (markers/HUD).
public class AnnalsTests
{
    [Fact]
    public void Annals_FoundingLinesAtStart_ExportMatchesPanel()
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        Assert.Equal(4, session.AnnalLines.Count); // one founding line each
        foreach (string line in session.AnnalLines) Assert.Contains("founded", line);

        for (int t = 1; t <= 10; t++) session.EndTurn();

        string path = Path.Combine(Path.GetTempPath(), $"chronicle-test-{Guid.NewGuid():N}.txt");
        session.ExportChronicle(path);
        try
        {
            // EXPORT == PANEL: the file is the panel's lines, one per line,
            // fixed \n termination — nothing added, nothing dropped.
            string expected = string.Join("\n", session.AnnalLines) + "\n";
            Assert.Equal(expected, File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ChroniclePath_TwinsTheOrderLogStamp()
    {
        Assert.Equal(
            Path.Combine("runs", "chronicle-20260101-120000.txt"),
            Sim.Ui.UiSession.ChroniclePath(
                Path.Combine("runs", "orders-20260101-120000.bin")));
        Assert.Equal(
            Path.Combine("runs", "chronicle-20260101-120000-s256-n4.txt"),
            Sim.Ui.UiSession.ChroniclePath(
                Path.Combine("runs", "orders-20260101-120000-s256-n4.bin")));
    }

    [Fact]
    public void Names_ResolveForEverySettlement_AndFeedTheHudTitle()
    {
        var session = Sim.Ui.UiSession.Start(42, sizeOverridePx: 256, settlementsOverride: 4);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < session.World.Settlements.Count; i++)
        {
            int id = session.World.Settlements[i].Id.Value;
            string name = session.Names.Name(id);
            Assert.True(seen.Add(name), $"duplicate settlement name '{name}'");
            var hud = Sim.Ui.ViewModel.HudModel.From(session.World, id, null, name);
            Assert.Contains(name, hud.TitleLine);
        }
    }

    /// <summary>
    /// M5 playtest (Director, 2026-10-19) — the two defective lines, reproduced
    /// from REAL inputs through the real UI seam: seed 42, one AI Empire,
    /// canonical size, no player orders. Measured on this world before the fix:
    /// "In 60, the stores at Pishemul ran short: 0 of every hundred measures of
    /// need went unmet." (deficit ratio 0.000367) and "In 70, the first artisans
    /// set up their workshops at Pishemul, 1 masters and hands." The events
    /// still fire (the chronicle is UI-side and the event set did not change);
    /// only their wording is now honest and grammatical.
    /// </summary>
    [Fact]
    public void Annals_Seed42OneAiEmpire_ShortfallAndSingleArtisan_ReadHonestly()
    {
        var session = Sim.Ui.UiSession.Start(42, aiEmpiresOverride: 1);
        for (int t = 1; t <= 7; t++) session.EndTurn();
        Assert.Contains("In 60, the stores at Pishemul ran short: fewer than 1 of every hundred measures of need went unmet.",
            session.AnnalLines);
        Assert.Contains("In 70, the first artisan set up a workshop at Pishemul, 1 master.", session.AnnalLines);
        Assert.Contains("In 70, the first artisan set up a workshop at Shekasur, 1 master.", session.AnnalLines);
        foreach (string line in session.AnnalLines)
        {
            Assert.DoesNotContain(" 0 of every hundred measures of need", line, StringComparison.Ordinal);
            Assert.DoesNotContain(" 1 masters", line, StringComparison.Ordinal);
            Assert.DoesNotContain(" 1 souls", line, StringComparison.Ordinal);
        }
    }
}
