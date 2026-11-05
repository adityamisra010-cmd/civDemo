using Gfx.Engine.Cameras;
using Microsoft.Xna.Framework;

namespace Gfx.Tests;

public class CameraTests
{
    static void Near(Vector3 a, Vector3 b, float tol = 1e-3f) => Assert.True(Vector3.Distance(a, b) <= tol, $"{a} vs {b}");

    [Fact]
    public void PoseViewProjectsTargetToScreenCentre()
    {
        var pose = new CameraPose(new Vector3(30, 40, 50), new Vector3(1, 2, 3));
        var clip = Vector4.Transform(new Vector4(pose.Target, 1), pose.ViewProjection(1.5f));
        Assert.True(MathF.Abs(clip.X / clip.W) < 1e-4f && MathF.Abs(clip.Y / clip.W) < 1e-4f);
        Assert.InRange(clip.Z / clip.W, 0f, 1f); // D3D depth range
    }

    [Fact]
    public void TacticalSnapsWithoutSmoothingAndKeepsDistance()
    {
        var c = new TacticalCamera(new Vector3(10, 0, 10), yaw: 0.3f, distance: 100) { Smoothing = 0 };
        c.Update(0.016f);
        Assert.Equal(100f, Vector3.Distance(c.Pose.Position, c.Pose.Target), 3);
        Near(c.Pose.Target, new Vector3(10, 0, 10));
    }

    [Fact]
    public void TacticalPanIsCameraRelative()
    {
        var c = new TacticalCamera(Vector3.Zero, yaw: 0f, distance: 100) { Smoothing = 0 };
        c.Pan(0, 10); c.Update(0.016f);
        // yaw 0: camera sits at +Z looking toward -Z, so "forward" is -Z
        Near(c.Focus, new Vector3(0, 0, -10));
        c.Rotate(MathHelper.PiOver2); c.Update(0.016f);
        c.Pan(0, 10); c.Update(0.016f);
        Near(c.Focus, new Vector3(-10, 0, -10));
        var fwd = c.Pose.Forward; Assert.True(fwd.X < -0.1f && MathF.Abs(fwd.Z) < 1e-3f);
    }

    [Fact]
    public void TacticalZoomAndEdgeClampAreBounded()
    {
        var c = new TacticalCamera(Vector3.Zero, 0, 100) { Smoothing = 0, MinDistance = 10, MaxDistance = 500, Bounds = new BoundingBox(new Vector3(-50), new Vector3(50)) };
        for (int i = 0; i < 50; i++) c.Zoom(0.5f);
        c.Update(0.016f); Assert.Equal(10f, c.Distance, 4);
        for (int i = 0; i < 50; i++) c.Zoom(2f);
        c.Update(0.016f); Assert.Equal(500f, c.Distance, 4);
        c.Pan(1000, -1000); c.Update(0.016f); // right → +X, backward → +Z
        Near(c.Focus, new Vector3(50, 0, 50));
        Assert.True(c.Pitch > MathHelper.ToRadians(60)); // zoomed out = steep
    }

    [Fact]
    public void TacticalPitchFlattensWhenZoomingIn()
    {
        var c = new TacticalCamera(Vector3.Zero, 0, 400) { Smoothing = 0 };
        c.Update(0); float far = c.Pitch;
        c.Zoom(0.05f); c.Update(0); Assert.True(c.Pitch < far);
    }

    [Fact]
    public void TacticalSmoothingConvergesAndFollowTracksTarget()
    {
        var c = new TacticalCamera(Vector3.Zero, 0, 100) { Smoothing = 10 };
        var target = new Vector3(40, 0, 0); c.FollowTarget = () => target;
        for (int i = 0; i < 120; i++) c.Update(1 / 60f);
        Near(c.Focus, target, 0.01f);
        c.Pan(5, 0); Assert.Null(c.FollowTarget); // manual input breaks follow
    }

    [Fact]
    public void CinematicShotHitsEndpointsAndEases()
    {
        var shot = CameraShot.Dolly(new Vector3(0, 5, 0), new Vector3(100, 5, 0), new Vector3(50, 0, 50), 4f);
        Near(shot.Evaluate(0).Position, new Vector3(0, 5, 0));
        Near(shot.Evaluate(4).Position, new Vector3(100, 5, 0));
        Near(shot.Evaluate(2).Position, new Vector3(50, 5, 0), 0.01f);
        Assert.True(shot.Evaluate(0.4f).Position.X < 10f); // ease-in: linear would be at x=10
        Near(shot.Evaluate(1).Target, new Vector3(50, 0, 50));
    }

    [Fact]
    public void OrbitKeepsRadiusAndSequenceSwitchesShots()
    {
        var orbit = CameraShot.Orbit(new Vector3(10, 0, 10), 30, 12, 0, MathHelper.Pi, 6f);
        for (int i = 0; i <= 10; i++)
        {
            var p = orbit.Evaluate(i * 0.6f).Position;
            float r = new Vector2(p.X - 10, p.Z - 10).Length();
            Assert.InRange(r, 29.0f, 30.5f); Assert.Equal(12f, p.Y, 3);
        }
        var seq = new CinematicCamera();
        seq.Shots.Add(orbit); seq.Shots.Add(CameraShot.Dolly(Vector3.Zero, Vector3.One, Vector3.UnitX, 2f));
        Assert.Equal(8f, seq.TotalDuration);
        Assert.Equal(0, seq.Evaluate(5.9f).shot); Assert.Equal(1, seq.Evaluate(6.1f).shot); Assert.Equal(1, seq.Evaluate(99f).shot);
        seq.BlendSeconds = 1f;
        Near(seq.Evaluate(6.0f).pose.Position, orbit.Evaluate(6f).Position, 0.01f); // blend starts from the previous shot's end
    }
}
