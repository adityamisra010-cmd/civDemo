using Gfx.Demo.Scenes;
using Gfx.Engine.Diagnostics;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Units;
using Microsoft.Xna.Framework.Graphics;

namespace Gfx.Tests;

/// <summary>Lane I: the combined battlefield scene renders and never writes its frozen inputs.</summary>
public class IntegrationTests
{
    /// <summary>Renderer invariance across lanes: grid hash and battle-trace hash unchanged after shadowed renders of every shot.</summary>
    [GfxFact]
    public void BattlefieldRendersAndNeverWritesGridOrTrace()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new BattlefieldScene();
            scene.Load(gd, TestGfx.Host.Effects);
            var grid0 = scene.Grid.HashHex(); var trace0 = scene.Trace.Hash();
            using var r = new ForwardRenderer(gd, TestGfx.Host.Effects); scene.Configure(r.Settings);
            using var rt = new RenderTarget2D(gd, 320, 180, false, SurfaceFormat.Color, DepthFormat.None);
            Assert.Equal(4, scene.Shots.Count);
            foreach (var shot in scene.Shots)
            {
                scene.Update(shot.SceneTime);
                r.Render(scene, shot.Camera, SunModel.At(shot.HourOfDay), rt, shot.SceneTime);
            }
            var follow = scene.Shots.Single(s => s.Name == "follow-low");
            scene.Update(follow.SceneTime);
            r.Render(scene, follow.Camera, SunModel.At(follow.HourOfDay), rt, follow.SceneTime);
            Assert.True(scene.Crowd.Lod0Count > 20, $"follow cam should see full-detail soldiers, saw {scene.Crowd.Lod0Count}");
            Assert.Equal(grid0, scene.Grid.HashHex());
            Assert.Equal(trace0, scene.Trace.Hash());
            Assert.Equal(grid0, Gfx.Proc.Authoritative.BattleTerrainGenerator.Generate(Gfx.Proc.Scenes.TerrainTemperateScene.FixtureInputs).HashHex());
        });
    }

    /// <summary>Soldiers stand on the terrain, on dry cells: every visible soldier's y equals the terrain height at its x,z.</summary>
    [GfxFact]
    public void SoldiersAreSnappedToTheTerrain()
    {
        TestGfx.Host.Invoke(gd =>
        {
            using var scene = new BattlefieldScene();
            scene.Load(gd, TestGfx.Host.Effects);
            scene.Update(20f);
            int n = 0;
            foreach (var f in scene.Frames)
            {
                if (!f.Visible) continue;
                Assert.Equal(scene.Scenery.HeightAt(f.Position.X, f.Position.Z), f.Position.Y);
                Assert.False(scene.Scenery.Mesh.IsWet(f.Position.X, f.Position.Z));
                n++;
            }
            Assert.Equal(scene.Playback.EntityCount, n);
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var f in scene.Frames) { lo = MathF.Min(lo, f.Position.Y); hi = MathF.Max(hi, f.Position.Y); }
            Assert.True(hi - lo > 0.05f, "terrain relief should show in soldier heights (not a flat plane)");
        });
    }
}
