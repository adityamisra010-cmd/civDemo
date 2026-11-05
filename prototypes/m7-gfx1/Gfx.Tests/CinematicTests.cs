using Gfx.Avatar;
using Gfx.Cinematics;
using Gfx.Cinematics.Demo;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>Lane A: outcome-driven cinematic planner (rules, truth table, determinism, variation) and player.</summary>
public class CinematicTests
{
    static string OutDir => Path.Combine(AppContext.BaseDirectory, "test-output");

    const string ExecutionPlanHashSeed1 = "7276C8F2A0FC5C9DEE977361D8846BE2BD8F200110CBAB73C4345A95563CE332";

    static WarOutcome Make(OutcomeKind k, LeaderFate f, Side victor, WarDomain d = WarDomain.Land) =>
        new(WarOutcome.Label, k, victor, f, d, 3, 1, 2, new WarRecord(100, 3, 1000, 2000, 1), 99);

    static IEnumerable<string> Clips(CinematicPlan p) => p.Shots.SelectMany(s => new[] { s.AvatarClip, s.OpponentClip }).Where(c => c != "");
    static PlannedShot Outcome(CinematicPlan p) => p.Shots.Single(s => s.Beat == Beat.OutcomeBeat);

    static readonly Beat[] Canonical = { Beat.Aftermath, Beat.AvatarIntro, Beat.OpponentIntro, Beat.Confrontation, Beat.OutcomeBeat, Beat.Banner, Beat.WarRecordCard };

    /// <summary>Every kind × fate × victor × content setting × domain: the validity rules hold for every combination.</summary>
    [Fact]
    public void TruthTableRulesHoldForEveryCombination()
    {
        int n = 0;
        foreach (var k in Enum.GetValues<OutcomeKind>())
        foreach (var f in Enum.GetValues<LeaderFate>())
        foreach (var v in Enum.GetValues<Side>())
        foreach (var allow in new[] { true, false })
        foreach (var d in Enum.GetValues<WarDomain>())
        {
            var o = Make(k, f, v, d); n++;
            var p = CinematicPlanner.Plan(o, 7, new CinematicSettings(allow));
            string ctx = $"{k}/{f}/{v}/allow={allow}/{d}";
            bool inconclusive = k == OutcomeKind.Inconclusive || v == Side.None;
            bool defeat = !inconclusive && (k == OutcomeKind.Defeat || v == Side.Opponent);
            bool surrendered = k == OutcomeKind.Surrender || f == LeaderFate.Surrendered;
            bool execution = !inconclusive && !defeat && f == LeaderFate.Executed && allow && !surrendered;

            // R2/R3: execution only when fate Executed AND content allowed AND no surrender
            Assert.True((p.OutcomeBeat == OutcomeBeatKind.Execution) == execution, ctx);
            Assert.True(Clips(p).Any(c => ClipCatalog.HasTag(c, ClipCatalog.Execute)) == execution, ctx + ": execute clip");
            Assert.True(p.AvatarArmed == execution, ctx + ": armed");
            if (surrendered) Assert.DoesNotContain(Clips(p), c => ClipCatalog.HasTag(c, ClipCatalog.Execute) || ClipCatalog.HasTag(c, ClipCatalog.Fall));
            if (f == LeaderFate.Executed && !execution && !inconclusive && !defeat) Assert.Equal(OutcomeBeatKind.AftermathCut, p.OutcomeBeat);
            // R4: inconclusive → no victory anywhere, no banner
            if (inconclusive)
            {
                Assert.Equal(OutcomeBeatKind.Standoff, p.OutcomeBeat);
                Assert.Equal(BannerSide.None, p.Banner);
                Assert.DoesNotContain(p.Shots, s => s.Beat == Beat.Banner);
                Assert.DoesNotContain(Clips(p), c => ClipCatalog.HasTag(c, ClipCatalog.Victory));
            }
            // R5: defeat → avatar yields on the losing side, opponent's banner, avatar never killed/captured/kneeling
            if (defeat)
            {
                Assert.Equal(OutcomeBeatKind.DefeatYield, p.OutcomeBeat);
                Assert.True(ClipCatalog.HasTag(Outcome(p).AvatarClip, ClipCatalog.Yield), ctx);
                Assert.True(ClipCatalog.HasTag(Outcome(p).OpponentClip, ClipCatalog.Victory), ctx);
                Assert.Equal(BannerSide.Opponent, p.Banner);
            }
            foreach (var s in p.Shots)
                Assert.False(new[] { ClipCatalog.Fall, ClipCatalog.Captive, ClipCatalog.Submit }.Any(t => ClipCatalog.HasTag(s.AvatarClip, t)), ctx + ": avatar in a loser-only clip");
            if (!defeat) Assert.DoesNotContain(p.Shots, s => ClipCatalog.HasTag(s.AvatarClip, ClipCatalog.Yield));
            // R6: sets
            var expectSet = d == WarDomain.Naval ? SetKind.Naval : d == WarDomain.Siege ? SetKind.Fortress
                : k == OutcomeKind.NavalVictory ? SetKind.Naval : k == OutcomeKind.SiegeVictory ? SetKind.Fortress : SetKind.Field;
            Assert.Equal(expectSet, p.Set);
            // R7: mandatory beats, canonical order
            var beats = p.Shots.Select(s => s.Beat).ToArray();
            Assert.Equal(Beat.Aftermath, beats[0]); Assert.Equal(Beat.AvatarIntro, beats[1]); Assert.Equal(Beat.WarRecordCard, beats[^1]);
            Assert.Contains(Beat.Confrontation, beats); Assert.Contains(Beat.OutcomeBeat, beats);
            Assert.Equal(beats, beats.OrderBy(b => Array.IndexOf(Canonical, b)).ToArray());
            Assert.Equal(p.Opponent != OpponentPresence.Absent, beats.Contains(Beat.OpponentIntro));
            Assert.Equal(p.Banner != BannerSide.None, beats.Contains(Beat.Banner));
            // every chosen clip exists in the library
            foreach (var c in Clips(p)) CineClips.Sample(c, 1f, 4f);
        }
        Assert.Equal(9 * 6 * 3 * 2 * 3, n);
    }

    [Theory]
    [InlineData(OutcomeKind.Execution, LeaderFate.Executed, Side.Player, true, OutcomeBeatKind.Execution)]
    [InlineData(OutcomeKind.Execution, LeaderFate.Executed, Side.Player, false, OutcomeBeatKind.AftermathCut)]
    [InlineData(OutcomeKind.Execution, LeaderFate.Surrendered, Side.Player, true, OutcomeBeatKind.Submission)]
    [InlineData(OutcomeKind.Execution, LeaderFate.Alive, Side.Player, true, OutcomeBeatKind.Victory)]
    [InlineData(OutcomeKind.Surrender, LeaderFate.Executed, Side.Player, true, OutcomeBeatKind.AftermathCut)]
    [InlineData(OutcomeKind.Surrender, LeaderFate.Surrendered, Side.Player, true, OutcomeBeatKind.Submission)]
    [InlineData(OutcomeKind.Surrender, LeaderFate.Alive, Side.Player, true, OutcomeBeatKind.Submission)]
    [InlineData(OutcomeKind.Capture, LeaderFate.Captured, Side.Player, true, OutcomeBeatKind.Capture)]
    [InlineData(OutcomeKind.NegotiatedPeace, LeaderFate.Alive, Side.Player, true, OutcomeBeatKind.Treaty)]
    [InlineData(OutcomeKind.DecisiveVictory, LeaderFate.Alive, Side.Player, true, OutcomeBeatKind.Victory)]
    [InlineData(OutcomeKind.DecisiveVictory, LeaderFate.Fled, Side.Player, true, OutcomeBeatKind.RepresentativeYields)]
    [InlineData(OutcomeKind.DecisiveVictory, LeaderFate.Unknown, Side.Player, true, OutcomeBeatKind.AftermathOnly)]
    [InlineData(OutcomeKind.Inconclusive, LeaderFate.Executed, Side.Player, true, OutcomeBeatKind.Standoff)]
    [InlineData(OutcomeKind.DecisiveVictory, LeaderFate.Alive, Side.None, true, OutcomeBeatKind.Standoff)]
    [InlineData(OutcomeKind.Defeat, LeaderFate.Alive, Side.Opponent, true, OutcomeBeatKind.DefeatYield)]
    [InlineData(OutcomeKind.SiegeVictory, LeaderFate.Captured, Side.Player, true, OutcomeBeatKind.Capture)]
    [InlineData(OutcomeKind.NavalVictory, LeaderFate.Surrendered, Side.Player, true, OutcomeBeatKind.Submission)]
    public void TruthTableExplicitRows(OutcomeKind k, LeaderFate f, Side v, bool allow, OutcomeBeatKind expected) =>
        Assert.Equal(expected, CinematicPlanner.Plan(Make(k, f, v), 3, new CinematicSettings(allow)).OutcomeBeat);

    [Fact]
    public void ConflictsAndCutsAreRecordedAsNotes()
    {
        Assert.Contains(CinematicPlanner.Plan(Make(OutcomeKind.Execution, LeaderFate.Executed, Side.Player), 1, new(false)).Notes, n => n.StartsWith("R3"));
        Assert.Contains(CinematicPlanner.Plan(Make(OutcomeKind.Surrender, LeaderFate.Executed, Side.Player), 1).Notes, n => n.StartsWith("R2"));
        Assert.Contains(CinematicPlanner.Plan(Make(OutcomeKind.Execution, LeaderFate.Alive, Side.Player), 1).Notes, n => n.StartsWith("R1"));
        Assert.Empty(CinematicPlanner.Plan(WarOutcomeFixtures.Execution, 1).Notes);
    }

    [Fact]
    public void PlanIsDeterministicPinnedAndNeverWritesTheOutcome()
    {
        foreach (var (name, o) in WarOutcomeFixtures.All)
        {
            string oh = o.Hash();
            var a = CinematicPlanner.Plan(o, 1); var b = CinematicPlanner.Plan(o with { }, 1);
            Assert.Equal(a.Hash(), b.Hash());
            Assert.Equal(a.CanonicalBytes(), b.CanonicalBytes());
            Assert.Equal(oh, o.Hash());
            if (o.OpponentLeaderFate == LeaderFate.Executed) Assert.NotEqual(a.Hash(), CinematicPlanner.Plan(o, 1, new CinematicSettings(false)).Hash());
            Assert.False(string.IsNullOrEmpty(name));
        }
        string ph = CinematicPlanner.Plan(WarOutcomeFixtures.Execution, 1).Hash(); Assert.True(ph == ExecutionPlanHashSeed1, "hash " + ph);
    }

    [Fact]
    public void SeedsVaryCameraLightWeatherAndClipsButNeverTheBeats()
    {
        foreach (var (_, o) in WarOutcomeFixtures.All)
        {
            var plans = Enumerable.Range(0, 32).Select(s => CinematicPlanner.Plan(o, (ulong)s)).ToArray();
            var beats = plans[0].Shots.Select(s => s.Beat).ToArray();
            foreach (var p in plans)
            {
                Assert.Equal(beats, p.Shots.Select(s => s.Beat).ToArray());
                Assert.Equal(plans[0].OutcomeBeat, p.OutcomeBeat);
                Assert.Equal(plans[0].Banner, p.Banner);
                Assert.Equal(plans[0].Set, p.Set);
            }
            Assert.True(plans.Select(p => string.Join(",", p.Shots.Select(s => $"{s.Move}{s.CameraSide}{s.Framing}"))).Distinct().Count() >= 16, "camera variety");
            Assert.True(plans.Select(p => p.HourTenths).Distinct().Count() >= 3, "lighting variety");
            Assert.True(plans.Select(p => p.Weather).Distinct().Count() >= 2, "weather variety");
            Assert.True(plans.Select(p => p.Hash()).Distinct().Count() >= 30, "plan variety");
        }
        // clip variants: the execution fixture uses both strike variants across seeds
        var strikes = Enumerable.Range(0, 32).Select(s => Outcome(CinematicPlanner.Plan(WarOutcomeFixtures.Execution, (ulong)s)).AvatarClip).Distinct().Count();
        Assert.Equal(2, strikes);
    }

    [Fact]
    public void PlayerStagesTheOutcomeFromThePlan()
    {
        var exec = new CinematicPlayer(CinematicPlanner.Plan(WarOutcomeFixtures.Execution, 1));
        int oi = exec.Plan.Shots.ToList().FindIndex(s => s.Beat == Beat.OutcomeBeat);
        float st = exec.ShotStart(oi), d = exec.Plan.Shots[oi].DurationMs / 1000f;
        var early = exec.Evaluate(st + 0.3f * d); var late = exec.Evaluate(st + 0.95f * d);
        Assert.Equal(Beat.OutcomeBeat, early.Beat);
        Assert.True(early.Opponent.Visible && early.Avatar.Visible);
        // the leader falls only after the strike: the root tilts late, not early
        Assert.True(MathF.Abs(early.Opponent.Pose.Local[0].M23) < 0.05f && MathF.Abs(late.Opponent.Pose.Local[0].M23) > 0.5f);

        var cap = new CinematicPlayer(CinematicPlanner.Plan(WarOutcomeFixtures.Capture, 1));
        oi = cap.Plan.Shots.ToList().FindIndex(s => s.Beat == Beat.OutcomeBeat); st = cap.ShotStart(oi); d = cap.Plan.Shots[oi].DurationMs / 1000f;
        Assert.True(cap.Evaluate(st + 0.9f * d).Opponent.Position.Z > cap.Evaluate(st + 0.2f * d).Opponent.Position.Z + 1f, "captive is led away");

        var inc = new CinematicPlayer(CinematicPlanner.Plan(WarOutcomeFixtures.Inconclusive, 1));
        for (float t = 0; t <= inc.Duration; t += 0.25f) { var f = inc.Evaluate(t); Assert.Equal(0f, f.PlayerBannerRaise); Assert.Equal(0f, f.OpponentBannerRaise); }

        // deterministic and finite
        var x = exec.Evaluate(5.3f); var y = exec.Evaluate(5.3f);
        Assert.Equal(x.Camera, y.Camera);
        Assert.True(float.IsFinite(x.Camera.Position.X) && float.IsFinite(x.Camera.Target.Y));
    }

    /// <summary>Lowest and highest world-space Y of a character's mesh exactly as the scene draws it (<see cref="PosedAvatar.SkinPlaced"/>).</summary>
    static (float min, float max) WorldYRange(AvatarModel m, CharacterFrame c)
    {
        var dst = new VertexPNC[m.Mesh.Vertices.Length];
        var w = PosedAvatar.SkinPlaced(m, c.Pose, c.World, dst);
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var v in dst) { float y = Microsoft.Xna.Framework.Vector3.Transform(v.Position, w).Y; lo = MathF.Min(lo, y); hi = MathF.Max(hi, y); }
        return (lo, hi);
    }

    [Fact]
    public void StagedCharactersStayAboveTheGroundAndTheExecutedLeaderLiesFallen()
    {
        const float Tol = 0.04f; // skin/cloth sag below the sole; anything deeper is a body through the ground
        var bad = new List<string>();
        void Check(bool ok, string msg) { if (!ok) bad.Add(msg); }
        foreach (var (name, o) in WarOutcomeFixtures.All)
        {
            var plan = CinematicPlanner.Plan(o, 1); var cine = new CinematicPlayer(plan);
            var pl = AvatarProfile.PlayerFixture();
            var op = AvatarProfile.Sample(0xC0FFEEUL + (ulong)o.OpponentCultureId, "Opposing leader");
            var am = AvatarMesh.Build(pl, EraDress.Select(pl, plan.EraCapabilityTier, plan.AvatarRole), new AvatarBuildOptions(Sword: plan.AvatarArmed));
            var om = AvatarMesh.Build(op, EraDress.Select(op, plan.EraCapabilityTier, plan.OpponentRole));
            for (float t = 0; t <= cine.Duration; t += 0.2f)
            {
                var f = cine.Evaluate(t);
                if (f.Avatar.Visible) Check(WorldYRange(am, f.Avatar).min > -Tol, $"{name} avatar {f.Avatar.Clip} t={t:0.0} minY {WorldYRange(am, f.Avatar).min:0.000}");
                if (f.Opponent.Visible) Check(WorldYRange(om, f.Opponent).min > -Tol, $"{name} opponent {f.Opponent.Clip} t={t:0.0} minY {WorldYRange(om, f.Opponent).min:0.000}");
            }
            if (plan.OutcomeBeat != OutcomeBeatKind.Execution) continue;
            int oi = plan.Shots.ToList().FindIndex(s => s.Beat == Beat.OutcomeBeat);
            float st = cine.ShotStart(oi), d = plan.Shots[oi].DurationMs / 1000f;
            foreach (float u in new[] { 0.3f, 0.92f, 0.99f })
            {
                var r = WorldYRange(om, cine.Evaluate(st + u * d).Opponent);
                Check(r.min > -Tol, $"{name} u={u} minY {r.min:0.000}");
                if (u > 0.9f) Check(r.max > 0.15f && r.max < 0.75f, $"{name} u={u}: fallen body lies on the ground, maxY {r.max:0.000}");
            }
        }
        Assert.True(bad.Count == 0, string.Join("\n", bad));
    }

    /// <summary>
    /// The plan and profile hashes are pinned exact, so the planner, the outcome record and the profile must stay
    /// integer-only: run the shared determinism scan on them. String literals are blanked first (hex colours and GUID
    /// text like "#8D5A3B" are not numbers); everything else is scanned as written.
    /// </summary>
    [Fact]
    public void PlannerOutcomeAndProfileSourcesPassTheIntegerOnlyScan()
    {
        if (!OperatingSystem.IsLinux()) return;
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string dir = Path.Combine(Path.GetTempPath(), "gfx-scan-laneA-" + Environment.ProcessId);
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var rel in new[] { "Gfx.Cinematics/CinematicPlanner.cs", "Gfx.Cinematics/WarOutcome.cs", "Gfx.Avatar/AvatarProfile.cs" })
            {
                string src = File.ReadAllText(Path.Combine(repo, rel));
                src = System.Text.RegularExpressions.Regex.Replace(src, "\"(?:[^\"\\\\\n]|\\\\.)*\"", "\"\"");
                File.WriteAllText(Path.Combine(dir, Path.GetFileName(rel)), src);
            }
            var psi = new System.Diagnostics.ProcessStartInfo("bash", new[] { Path.Combine(repo, "scripts/check-gfx-determinism.sh"), dir }) { RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = System.Diagnostics.Process.Start(psi)!;
            string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit();
            Assert.True(p.ExitCode == 0, o);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void WritePlanDumpsForEveryFixture()
    {
        string dir = Path.Combine(OutDir, "plans"); Directory.CreateDirectory(dir);
        foreach (var (name, o) in WarOutcomeFixtures.All)
        {
            var p = CinematicPlanner.Plan(o, 1);
            File.WriteAllText(Path.Combine(dir, $"plan-{name}.txt"), $"fixture: {o.FixtureLabel}\noutcome: {o.Kind} victor={o.Victor} fate={o.OpponentLeaderFate} domain={o.Domain} tier={o.EraCapabilityTier} outcomeHash={o.Hash()}\n" + p.Dump());
        }
        var noExec = CinematicPlanner.Plan(WarOutcomeFixtures.Execution, 1, new CinematicSettings(AllowExecutionContent: false));
        File.WriteAllText(Path.Combine(dir, "plan-execution-content-off.txt"), noExec.Dump());
        Assert.Equal(WarOutcomeFixtures.All.Count + 1, Directory.GetFiles(dir, "plan-*.txt").Length);
    }

    [GfxFact]
    public void RenderingACinematicNeverWritesItsFrozenInputs()
    {
        TestGfx.Host.Invoke(gd =>
        {
            var outcome = WarOutcomeFixtures.Execution; var player = AvatarProfile.PlayerFixture(); var opp = AvatarProfile.Sample(77, "Opponent");
            string oh = outcome.Hash(), ph = player.Hash(), qh = opp.Hash();
            using var scene = new CinematicScene(outcome, 1, player, opp);
            string planHash = scene.Plan.Hash();
            scene.Load(gd, TestGfx.Host.Effects);
            using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(r.Settings);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            var shots = scene.Shots;
            var picks = new[] { shots.First(s => s.Name.Contains("outcomebeat-62")), shots.First(s => s.Name.Contains("banner")), shots[^1] };
            foreach (var s in picks)
            {
                scene.Update(s.SceneTime);
                r.Render(scene, s.Camera, SunModel.At(s.HourOfDay), rt);
                var px = Capture.ReadPixels(rt);
                Assert.True(Capture.DistinctColors(px) > 150, s.Name);
                Capture.SavePng(rt, Path.Combine(OutDir, $"cine-test-{s.Name}.png"));
            }
            Assert.True(scene.Frame!.CardVisible);
            Assert.Equal(oh, outcome.Hash()); Assert.Equal(ph, player.Hash()); Assert.Equal(qh, opp.Hash());
            Assert.Equal(planHash, scene.Plan.Hash());
        });
    }
}
