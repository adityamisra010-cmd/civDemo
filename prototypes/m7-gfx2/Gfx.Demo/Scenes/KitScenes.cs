using Gfx.Chars.Animation;
using Gfx.Chars.Rendering;
using Gfx.Engine.Cameras;
using Gfx.Engine.Rendering;
using Gfx.Engine.Scene;
using Gfx.Gear.Items;
using Gfx.Gear.Runtime;
using Microsoft.Xna.Framework;

namespace Gfx.Demo.Scenes;

/// <summary>
/// Stage kit scenes: the era kit generated in code (Gfx.Gear) on the stage-chars meadow. PRESENTATION ONLY: the items
/// and characters read nothing from any simulation (DR39, DR42).
/// </summary>
public abstract class KitSceneBase : CharsSceneBase
{
    protected KitWorld Kit { get; private set; } = null!;
    /// <summary>The character stage (for the costs harness: hiding instances to measure their draw cost).</summary>
    public CharStage KitStage => Stage;

    protected override void Build()
    {
        Kit = new KitWorld(Device, Stage);
        BuildKit();
    }

    protected abstract void BuildKit();
    public override void Dispose() { Kit?.Dispose(); OnLoadDispose(); base.Dispose(); }

    /// <summary>An identity wearing a stage-kit garment outfit ($G2_ASSETS/baked/kit/outfits, scripts/bake-kit-outfits.sh).</summary>
    protected CharInstance SpawnKitOutfit(string identity, string outfit)
    {
        string path = Gfx.Engine.Assets.AssetRoot.Baked($"kit/outfits/{identity}__{outfit}.g2ch");
        if (!File.Exists(path)) throw new FileNotFoundException($"kit outfit bake missing: {path} (run scripts/bake-kit-outfits.sh)");
        var inst = new CharInstance(Stage.Asset(identity));
        var asset = CharAsset.Load(Device, path, Stage.Textures.Get);
        _outfits.Add(asset);
        inst.Wear(Device, asset);
        Stage.Instances.Add(inst);
        Stage.Invalidate();
        return inst;
    }
    readonly List<CharAsset> _outfits = new();

    void OnLoadDispose() { foreach (var a in _outfits) a.Dispose(); _outfits.Clear(); }
}

/// <summary>
/// The hero asset: the bronze-faced aspis (one mesh, one 2048-texel atlas), loaded from its glTF bake
/// ($G2_ASSETS/baked/kit/hero/aspis.glb, written by <c>Gfx.Demo --kit-bake</c>; baked in memory when missing), standing
/// on its rim on the meadow, turned in eight steps, with close-ups; the stage-chars bake's aspis on display beside it
/// for BEFORE/AFTER pairs; and the avatar in its ancient kit holding each (strap grip on the forearm, fist on the
/// hand grip), with the same cameras.
/// </summary>
[DemoScene("kit-hero", "Stage kit: the hero asset, a bronze-faced aspis (glTF bake, 2048 texel PBR atlas, authored wear), turntable, close-ups, before/after")]
public sealed class KitHeroScene : KitSceneBase
{
    KitItem _aspis = null!;
    CharInstance _prop = null!, _before = null!, _avAfter = null!, _avBefore = null!, _held = null!;
    Vector3 _base, _baseBefore;
    AnimDriver _dAfter = null!, _dBefore = null!;
    Gfx.Gear.Rig.KitHold _hold = null!;
    public string Source { get; private set; } = "";
    public float StrapResidual { get; private set; }
    protected override float StageRadius => 12f;

    protected override void BuildKit()
    {
        string glb = Gfx.Engine.Assets.AssetRoot.Baked("kit/hero/" + Gfx.Gear.Bake.HeroBake.FileName);
        if (File.Exists(glb)) { _aspis = Gfx.Gear.Bake.HeroBake.Load(glb); Source = "baked " + glb; }
        else { _aspis = Aspis.Build(Gfx.Gear.Bake.HeroBake.Spec); Source = "generated in memory (no bake at " + glb + ")"; }
        Console.WriteLine("[kit-hero] aspis: " + Source);
        _base = At(0, 0); _baseBefore = At(3.0f, 0);
        _prop = Kit.AddProp(_aspis, Pose(_base, 0));
        // BEFORE: the stage-chars bake's aspis (avatar ancient outfit), moved out of bind space by its own socket
        var outfit = Stage.Asset("avatar__era_ancient");
        var avatar = Stage.Asset("avatar");
        var sock = avatar.File.Sockets.Find(x => x.Name == "shield_l")!;
        var sockBind = Matrix.CreateFromQuaternion(sock.Rotation) * Matrix.CreateTranslation(sock.Position + avatar.Skeleton.Head[sock.Bone]);
        // the chars aspis' face bulges 50 mm along +Z from its socket; its back plane sits 16 mm behind: rest it like ours
        _before = Kit.AddBakedProp(outfit, new[] { "shield_face", "shield_back", "shield_rim" }, Matrix.Invert(sockBind) * Matrix.CreateTranslation(0, 0, 0.016f), Pose(_baseBefore, 0));
        // the avatar in its ancient kit holding each shield, in a guard
        _avAfter = Stage.Spawn("avatar", "era_ancient");
        Gfx.Gear.Runtime.KitAssets.StripBakedKit(_avAfter);
        var sockets = Gfx.Gear.Rig.KitSockets.For(_avAfter.Asset);
        var (wrist, slide, residual) = Gfx.Gear.Rig.KitHold.FitStrap(_avAfter.Asset.Skeleton, sockets, _aspis);
        StrapResidual = residual;
        Console.WriteLine(System.FormattableString.Invariant($"[kit-hero] strap fit: slide {slide * 1000:F1} mm, fist to hand grip {residual * 1000:F1} mm"));
        _hold = new Gfx.Gear.Rig.KitHold(sockets) { Left = _aspis, Mode = Gfx.Gear.Rig.LeftHold.Strap, StrapWrist = wrist, StrapSlide = slide };
        _held = Kit.AddProp(_aspis, Matrix.Identity);
        _dAfter = new AnimDriver(_avAfter, t => new AnimInput { Speed = 0, Stance = "shield_up", LookAt = Cam(0, 1.6f, 6f), Constraint = _hold }, t => Place(-3.2f, -1.0f, 0.35f));
        _avBefore = Stage.Spawn("avatar", "era_ancient");
        _dBefore = new AnimDriver(_avBefore, t => new AnimInput { Speed = 0, Stance = "shield_up", LookAt = Cam(0, 1.6f, 6f) }, t => Place(-6.2f, -1.0f, 0.35f));
        Drivers.Add(_dAfter); Drivers.Add(_dBefore);
        foreach (var d in Drivers) d.Advance(1.5f);
        PlaceHeld();
    }

    void PlaceHeld()
    {
        _avAfter.SolvePose();
        var (_, l) = _hold.ItemWorlds(_avAfter);
        if (l is { } w) _held.World = w;
    }

    /// <summary>Standing on its rim, leaning back 14 degrees, face towards the audience, turned by yaw.</summary>
    Matrix Pose(Vector3 at, float yaw)
    {
        var lean = Matrix.CreateRotationX(-MathHelper.ToRadians(14f));
        var lift = Matrix.CreateTranslation(0, (float)(Aspis.Outer * Math.Cos(MathHelper.ToRadians(14f))) + 0.002f, 0);
        return lean * lift * Matrix.CreateRotationY(Facing + yaw) * Matrix.CreateTranslation(at);
    }

    protected override void Animate(float t)
    {
        int step = (int)MathF.Floor(t + 1e-4f);
        _prop.World = Pose(_base, MathHelper.ToRadians(45f * step));
        _before.World = Pose(_baseBefore, MathHelper.ToRadians(45f * step));
        foreach (var d in Drivers) d.Advance(1.5f);
        PlaceHeld();
    }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var l = new List<CaptureShot>();
            var c = _base + new Vector3(0, 0.45f, 0);
            for (int i = 0; i < 8; i++)
                l.Add(new CaptureShot($"turn{i * 45:000}", new CameraPose(Cam(0, 0.85f, 2.1f), c, 0.55f, 0.05f, 4000f), Hour, i));
            l.Add(new CaptureShot("face", new CameraPose(Cam(0.05f, 0.62f, 1.25f), c + new Vector3(0, 0.02f, 0), 0.62f, 0.05f, 4000f), Hour, 0));
            l.Add(new CaptureShot("rim", new CameraPose(Cam(0.28f, 0.98f, 0.55f), Cam(0.05f, 0.82f, 0.04f), 0.40f, 0.02f, 4000f), Hour, 0));
            l.Add(new CaptureShot("device", new CameraPose(Cam(0.15f, 0.62f, 0.62f), Cam(0.1f, 0.58f, 0.0f), 0.42f, 0.02f, 4000f), Hour, 0));
            l.Add(new CaptureShot("back", new CameraPose(Cam(-0.2f, 0.75f, 1.35f), c, 0.62f, 0.05f, 4000f), Hour, 4));
            l.Add(new CaptureShot("fittings", new CameraPose(Cam(-0.35f, 0.62f, 0.62f), Cam(0.0f, 0.46f, -0.05f), 0.55f, 0.02f, 4000f), Hour, 4));
            l.Add(new CaptureShot("dusk", new CameraPose(Cam(0.6f, 0.75f, 1.9f), c, 0.55f, 0.05f, 4000f), 17.6f, 0));
            // BEFORE / AFTER pairs: the same camera relative to each shield
            var cb = _baseBefore + new Vector3(0, 0.45f, 0);
            var d = cb - c;
            l.Add(new CaptureShot("before-face", new CameraPose(Cam(0.05f, 0.62f, 1.25f) + d, cb + new Vector3(0, 0.02f, 0), 0.62f, 0.05f, 4000f), Hour, 0));
            l.Add(new CaptureShot("before-turn045", new CameraPose(Cam(0, 0.85f, 2.1f) + d, cb, 0.55f, 0.05f, 4000f), Hour, 1));
            l.Add(new CaptureShot("before-back", new CameraPose(Cam(-0.2f, 0.75f, 1.35f) + d, cb, 0.62f, 0.05f, 4000f), Hour, 4));
            // in use: the avatar's guard with each shield (same camera offset)
            var ga = At(-3.2f, -1.0f, 1.15f); var gb = At(-6.2f, -1.0f, 1.15f);
            l.Add(new CaptureShot("held-after", new CameraPose(ga + new Vector3(0, 0.2f, 0) + (Cam(0.9f, 0, 2.4f) - Cam(0, 0, 0)), ga, 0.6f, 0.05f, 4000f), Hour, 0));
            l.Add(new CaptureShot("held-before", new CameraPose(gb + new Vector3(0, 0.2f, 0) + (Cam(0.9f, 0, 2.4f) - Cam(0, 0, 0)), gb, 0.6f, 0.05f, 4000f), Hour, 0));
            l.Add(new CaptureShot("held-after-back", new CameraPose(ga + new Vector3(0, 0.25f, 0) + (Cam(-1.6f, 0, -1.4f) - Cam(0, 0, 0)), ga, 0.6f, 0.05f, 4000f), Hour, 0));
            return l;
        }
    }
}

/// <summary>
/// The era kit on display: shields standing on the meadow, helmets on head posts (fitted to the avatar's measured head),
/// weapons leaning on a rack, each generated in code with its own PBR atlas; overview and close-up shots per group.
/// </summary>
[DemoScene("kit-gallery", "Stage kit: the era kit on display (shields, helmets, weapons) with close-ups")]
public sealed class KitGalleryScene : KitSceneBase
{
    readonly List<(CharInstance inst, KitItem item, Vector3 at, float lean)> _shields = new();
    readonly List<(KitItem item, Vector3 at)> _helmets = new();
    readonly List<(KitItem item, Vector3 foot, Vector3 top)> _weapons = new();
    protected override float StageRadius => 14f;
    public IReadOnlyList<KitItem> Items => _shields.Select(x => x.item).Concat(_helmets.Select(x => x.item)).Concat(_weapons.Select(x => x.item)).ToList();

    protected override void BuildKit()
    {
        var specs = new[]
        {
            new ShieldSpec { Kind = "thureos", Seed = 3, Field = "#7B2C24", Charge = "#D8CFB8", Device = "spiral" },
            new ShieldSpec { Kind = "round", Seed = 4, Field = "#56201B", Charge = "#CFC5AE", Device = "wheel" },
            new ShieldSpec { Kind = "kite", Seed = 5, Field = "#2B4373", Charge = "#C29A45", Device = "chevron" },
            new ShieldSpec { Kind = "heater", Seed = 6, Field = "#93301F", Charge = "#DAD3C1", Device = "saltire" },
        };
        for (int i = 0; i < specs.Length; i++)
        {
            var item = Shields.Build(specs[i]);
            var inst = Kit.AddProp(item, Matrix.Identity);
            _shields.Add((inst, item, At(-1.8f + 1.2f * i, 0), 12f));
        }
        // helmets on head posts, fitted to the avatar's head
        var measure = Gfx.Gear.Rig.KitSockets.Measure(Stage.Asset("avatar"));
        string[] kinds = { "corinthian", "montefortino", "nasal", "kettle", "greathelm" };
        for (int i = 0; i < kinds.Length; i++)
        {
            var at = At(-2.4f + 1.2f * i, -1.6f);
            float headY = 1.45f;
            var post = Stands.HeadPost((uint)(10 + i), headY, measure);
            Kit.AddProp(post, Matrix.CreateRotationY(Facing) * Matrix.CreateTranslation(at));
            var helm = Helmets.Build(new HelmetSpec { Kind = kinds[i], Seed = (uint)(20 + i), Crest = kinds[i] == "corinthian" ? "#7E2A20" : kinds[i] == "montefortino" ? "#1F1A16" : null }, measure);
            // the post's head form centre stands at headY; the helmet's frame is the head centre
            Kit.AddProp(helm, Matrix.CreateRotationY(Facing) * Matrix.CreateTranslation(at + new Vector3(0, headY, 0)));
            _helmets.Add((helm, at + new Vector3(0, headY, 0)));
        }
        // weapons leaning on a rack
        var rackAt = At(0, -3.4f);
        Kit.AddProp(Stands.Rack(30, 4.2, 0), Matrix.CreateRotationY(Facing) * Matrix.CreateTranslation(rackAt));
        string[] wk = { "spear", "xiphos", "longsword", "arming", "axe", "bow", "musket", "quiver", "scabbard", "sling" };
        for (int i = 0; i < wk.Length; i++)
        {
            var item = Weapons.Build(new WeaponSpec { Kind = wk[i], Seed = (uint)(40 + i), MapSize = wk[i] is "spear" or "musket" or "bow" ? 1024 : 512 });
            float x = -1.85f + 0.41f * i;
            var mesh = item.Parts[0].Mesh; var bb = mesh.Bounds();
            // stand on its lower end, lean back on the rack's top bar
            var foot = At(x, -3.4f + 0.38f);
            float len = bb.Max.Y - bb.Min.Y;
            float lean = MathF.Asin(Math.Clamp(0.36f / MathF.Max(len, 0.4f), 0f, 0.6f));
            var w = Matrix.CreateTranslation(0, -bb.Min.Y, 0) * Matrix.CreateRotationX(-lean) * Matrix.CreateRotationY(Facing) * Matrix.CreateTranslation(foot);
            if (wk[i] is "quiver" or "scabbard" or "sling")
                w = Matrix.CreateTranslation(0, -bb.Max.Y, 0) * Matrix.CreateRotationY(Facing) * Matrix.CreateTranslation(At(x, -3.4f + 0.04f, 1.22f));
            Kit.AddProp(item, w);
            _weapons.Add((item, foot, Vector3.Transform(new Vector3(0, bb.Max.Y, 0), w)));
        }
        Animate(0);
    }

    Matrix StandPose(KitItem item, Vector3 at, float lean, float yaw)
    {
        var leanM = Matrix.CreateRotationX(-MathHelper.ToRadians(lean));
        float minY = item.Parts[0].Mesh.Positions.Min(p => Vector3.Transform(p, leanM).Y);
        return leanM * Matrix.CreateTranslation(0, -minY + 0.002f, 0) * Matrix.CreateRotationY(Facing + yaw) * Matrix.CreateTranslation(at);
    }

    protected override void Animate(float t)
    {
        int step = (int)MathF.Floor(t + 1e-4f);
        foreach (var (inst, item, at, lean) in _shields) inst.World = StandPose(item, at, lean, MathHelper.ToRadians(45f * step));
    }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var off = new Func<float, float, float, Vector3>((x, y, z) => Cam(x, y, z) - Cam(0, 0, 0));
            var l = new List<CaptureShot>
            {
                new("shields", new CameraPose(Cam(0, 1.1f, 3.6f), Cam(0, 0.55f, 0), 0.62f, 0.05f, 4000f), Hour, 0),
                new("shields-back", new CameraPose(Cam(0, 1.0f, 3.4f), Cam(0, 0.55f, 0), 0.62f, 0.05f, 4000f), Hour, 4),
                new("shields-34", new CameraPose(Cam(1.8f, 1.0f, 2.8f), Cam(0, 0.55f, 0), 0.62f, 0.05f, 4000f), Hour, 1),
                new("helmets", new CameraPose(Cam(0, 1.75f, 1.6f), Cam(0, 1.45f, -1.6f), 0.70f, 0.05f, 4000f), Hour, 0),
                new("weapons", new CameraPose(Cam(0, 1.2f, 0.4f), Cam(0, 0.8f, -3.4f), 0.70f, 0.05f, 4000f), Hour, 0),
            };
            for (int i = 0; i < _shields.Count; i++)
            {
                var c = _shields[i].at + new Vector3(0, 0.55f, 0);
                l.Add(new CaptureShot($"shield{i}", new CameraPose(c + off(0.2f, 0.25f, 1.5f), c, 0.62f, 0.05f, 4000f), Hour, 0));
            }
            for (int i = 0; i < _helmets.Count; i++)
            {
                var c = _helmets[i].at;
                l.Add(new CaptureShot($"helmet{i}", new CameraPose(c + off(0.30f, 0.06f, 0.55f), c + new Vector3(0, 0.0f, 0), 0.55f, 0.02f, 4000f), Hour, 0));
            }
            foreach (var (name, idx, y) in new[] { ("spearhead", 0, 2.35f), ("hilts", 2, 0.75f), ("axe", 4, 0.72f), ("musket", 6, 0.40f) })
            {
                var (item, foot, top) = _weapons[idx];
                var c = foot + (top - foot) * (y / Math.Max(0.01f, (top - foot).Length())) ;
                var eye = c + off(0.25f, 0.05f, 0.75f);
                if (name == "hilts")
                {
                    // the swords stand on their pommels: the three hilts in a row near the ground
                    c = foot + Vector3.Normalize(top - foot) * 0.17f;
                    eye = c + off(0.15f, 0.30f, 1.05f);
                }
                l.Add(new CaptureShot(name, new CameraPose(eye, c, 0.55f, 0.02f, 4000f), Hour, 0));
            }
            return l;
        }
    }
}

/// <summary>
/// Two opposing forces of the same period, fully kitted by the era kit: force A, hoplites (linen corselets or, for the
/// commander, a bronze cuirass; Corinthian helmets fitted to each head; aspides, the commander's the baked hero asset;
/// spears held overhand), against force B, Celtic or Galatian warriors (wool tunics and trousers, one in mail;
/// Montefortino helmets, one bare-headed; oval thureoi and a round shield on central grips; spears and a long sword).
/// Garments come from the stage-kit outfit bake; helmets, shields and weapons from Gfx.Gear; everyone stands in the
/// authored guard of their weapon. PRESENTATION ONLY.
/// </summary>
[DemoScene("kit-forces", "Stage kit: two opposing forces fully kitted (hoplites vs Celtic warriors), authored guards")]
public sealed class KitForcesScene : KitSceneBase
{
    protected override float StageRadius => 13f;
    public readonly List<KitSoldier> Soldiers = new();
    readonly Dictionary<string, Vector3> _eyes = new();

    protected override void BuildKit()
    {
        string glb = Gfx.Engine.Assets.AssetRoot.Baked("kit/hero/" + Gfx.Gear.Bake.HeroBake.FileName);
        var hero = File.Exists(glb) ? Gfx.Gear.Bake.HeroBake.Load(glb) : Aspis.Build(Gfx.Gear.Bake.HeroBake.Spec);
        // force A: hoplites
        var a = new (string id, string crest, AspisSpec? shield)[]
        {
            ("a1", "#1E1A17", new AspisSpec { Seed = 11, MapSize = 1024, Device = "crescent", Wear = 0.6, DeviceTurn = 0.2 }),
            ("a2", "#7E2A20", new AspisSpec { Seed = 12, MapSize = 1024, BronzeFace = false, Field = "#2B4373", Device = "sun", Wear = 0.5 }),
            ("a3", "#D8D0BC", new AspisSpec { Seed = 13, MapSize = 1024, Device = "wheel", Paint = "#2A2622", Wear = 0.45, DeviceTurn = -0.3 }),
            ("a4", "#7E2A20", new AspisSpec { Seed = 14, MapSize = 1024, BronzeFace = false, Field = "#273350", Device = "crescent", Wear = 0.7, DeviceTurn = 2.0 }),
            ("commander_a", "#E8E2D2", null),
        };
        for (int i = 0; i < a.Length; i++)
        {
            var (id, crest, shield) = a[i];
            var body = SpawnKitOutfit(id, "anc_a");
            var m = Gfx.Gear.Rig.KitSockets.Measure(body.Asset);
            var kit = new Loadout
            {
                Helmet = Helmets.Build(new HelmetSpec { Kind = "corinthian", Seed = (uint)(100 + i), Crest = crest, Wear = 0.4 + 0.1 * i }, m),
                Right = Weapons.Build(new WeaponSpec { Kind = "spear", Seed = (uint)(200 + i), MapSize = 512, Metal = i % 2 == 0 ? "iron" : "bronze" }),
                RightFrame = "grip_over",
                Left = shield == null ? hero : Aspis.Build(shield), Mode = Gfx.Gear.Rig.LeftHold.Strap,
            };
            Soldiers.Add(Soldier(body, kit, id, "hoplite", i < 4 ? new Vector2(-3.0f, (i - 1.5f) * 1.25f) : new Vector2(-1.75f, 0.35f), MathHelper.PiOver2 + (i < 4 ? 0 : -0.35f), 0.37f * i));
        }
        // force B: Celtic or Galatian warriors
        var b = new (string id, string helmet, string shield, string field, string charge, string device, string weapon)[]
        {
            ("b1", "montefortino", "thureos", "#7B2C24", "#D8CFB8", "spiral", "spear"),
            ("b2", "montefortino", "thureos", "#CFC5AE", "#7B2C24", "wheel", "spear"),
            ("b3", "", "thureos", "#56201B", "#CFC5AE", "lozenges", "longsword"),
            ("b4", "montefortino", "round", "#93301F", "#DAD3C1", "spiral", "spear"),
        };
        for (int i = 0; i < b.Length; i++)
        {
            var (id, helmet, shield, field, charge, device, weapon) = b[i];
            var body = SpawnKitOutfit(id, "anc_b");
            var m = Gfx.Gear.Rig.KitSockets.Measure(body.Asset);
            var kit = new Loadout
            {
                Helmet = helmet.Length == 0 ? null : Helmets.Build(new HelmetSpec { Kind = helmet, Seed = (uint)(110 + i), Crest = i == 1 ? "#1F1A16" : null, Wear = 0.5 }, m),
                Right = Weapons.Build(new WeaponSpec { Kind = weapon, Seed = (uint)(210 + i), MapSize = 512 }),
                RightFrame = weapon == "spear" ? "grip_over" : "grip",
                Left = Shields.Build(new ShieldSpec { Kind = shield, Seed = (uint)(300 + i), Field = field, Charge = charge, Device = device, Wear = 0.55 }),
                Mode = Gfx.Gear.Rig.LeftHold.Central,
            };
            if (weapon == "longsword") kit.Carried.Add((Weapons.Build(new WeaponSpec { Kind = "scabbard", Seed = 400, MapSize = 512 }), "hang", "hip_l"));
            var s = Soldier(body, kit, id, weapon == "spear" ? "hoplite" : "sword", new Vector2(3.0f, (i - 1.5f) * 1.25f), -MathHelper.PiOver2, 0.53f * i + 0.2f);
            int k = i;
            s.FaceDir = _ => Vector3.Transform(Vector3.UnitZ, Matrix.CreateRotationY(Facing - MathHelper.PiOver2 + 0.25f * (k - 1.5f) * 0.2f));
            Soldiers.Add(s);
        }
        _banners.Add(Banner.Create(Kit, Banner.Build(new BannerSpec { Seed = 61, Field = "#2B4373", Charge = "#C29A45", Device = "sun", Fringe = "#C29A45" }),
            Matrix.CreateRotationY(Facing + MathHelper.PiOver2) * Matrix.CreateTranslation(At(-4.6f, -0.9f))));
        _banners.Add(Banner.Create(Kit, Banner.Build(new BannerSpec { Seed = 62, Field = "#7B2C24", Charge = "#D8CFB8", Device = "spiral", Fringe = "#D8CFB8" }),
            Matrix.CreateRotationY(Facing - MathHelper.PiOver2) * Matrix.CreateTranslation(At(4.6f, 0.9f))));
        foreach (var s in Soldiers) s.Update(2.0f);
        foreach (var s in Soldiers) _eyes[s.Body.Asset.Id] = Eyes(s.Body);
        if (Environment.GetEnvironmentVariable("G2_KIT_DEBUG") == "1")
            foreach (var s in Soldiers)
            {
                var w = s.RightProp!.World;
                var axis = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, w));
                var sk = Gfx.Gear.Rig.KitSockets.For(s.Body.Asset)["grip_r"].World(s.Body);
                Console.WriteLine(System.FormattableString.Invariant($"[kit-debug] {s.Body.Asset.Id}: right item axis elevation {MathHelper.ToDegrees(MathF.Asin(axis.Y)):F1} deg; grip socket at {sk.Translation - s.Body.World.Translation}; wrap err {s.Hold.LastRightError * 1000:F1} mm"));
            }
    }

    KitSoldier Soldier(CharInstance body, Loadout kit, string id, string set, Vector2 at, float yaw, float phase)
    {
        Gfx.Gear.Anim.CombatClips.Install(body.Asset);
        var s = new KitSoldier(Kit, body, kit, "kit:" + id);
        bool central = kit.Mode == Gfx.Gear.Rig.LeftHold.Central;
        string guard = (set == "hoplite" ? "kit.hoplite.guard" : "kit.sword.guard") + (central ? ".c" : "");
        // a central-grip spearman uses the hoplite guard for the spear and the shield arm from the clip
        s.Drive(t => new AnimInput { Speed = 0, Action = guard, ActionStart = -phase, HoldLastFrame = true, LookAt = null, FootLock = false }, t => Place(at.X, at.Y, yaw), phase);
        s.Ground = p => Scenery.HeightAt(p.X, p.Z);
        return s;
    }

    readonly List<Banner> _banners = new();

    protected override void Animate(float t)
    {
        foreach (var s in Soldiers) s.Update(2.0f + t);
        for (int i = 0; i < _banners.Count; i++) _banners[i].Pose(2.0f + t + 0.7f * i);
    }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var fwd = Vector3.Normalize(Cam(0, 0, 1) - Cam(0, 0, 0)); var right = Vector3.Cross(fwd, Vector3.Up);
            var e = _eyes.TryGetValue("commander_a", out var ce) ? ce : Cam(-1.75f, 1.65f, 0.35f);
            var l = new List<CaptureShot>
            {
                new("lines-34", new CameraPose(Cam(0.6f, 2.3f, 7.4f), Cam(0f, 0.95f, 0f), 0.62f, 0.1f, 4000f), Hour, 0),
                new("lines-low", new CameraPose(Cam(-0.4f, 1.15f, 4.6f), Cam(0f, 1.15f, 0f), 0.75f, 0.1f, 4000f), Hour, 0),
                new("force-a", new CameraPose(Cam(0.6f, 1.55f, 1.4f), Cam(-3.0f, 1.1f, 0f), 0.62f, 0.1f, 4000f), Hour, 0),
                new("force-b", new CameraPose(Cam(-0.6f, 1.55f, 1.4f), Cam(3.0f, 1.1f, 0f), 0.62f, 0.1f, 4000f), Hour, 0),
                new("commander", new CameraPose(e + fwd * 0.75f - right * 1.05f + new Vector3(0, -0.05f, 0), e + new Vector3(0, -0.22f, 0), 0.55f, 0.05f, 4000f), Hour, 0),
                new("lines-dusk", new CameraPose(Cam(0.6f, 2.3f, 7.4f), Cam(0f, 0.95f, 0f), 0.62f, 0.1f, 4000f), 17.6f, 0),
                new("banner-a", new CameraPose(Cam(-2.6f, 2.0f, 2.2f), Cam(-4.6f, 2.0f, -0.9f), 0.62f, 0.05f, 4000f), Hour, 0),
                new("wide", new CameraPose(Cam(1.5f, 3.4f, 11.5f), Cam(0f, 0.9f, 0f), 0.55f, 0.1f, 4000f), Hour, 0),
            };
            foreach (var id in new[] { "a2", "b2", "b3" })
                if (_eyes.TryGetValue(id, out var ey))
                {
                    var dir = id.StartsWith('a') ? 1f : -1f;
                    var face = Vector3.Normalize(Cam(dir, 0, 0) - Cam(0, 0, 0));
                    l.Add(new CaptureShot("face-" + id, new CameraPose(ey + face * 0.80f + fwd * 0.35f + new Vector3(0, 0.02f, 0), ey + new Vector3(0, -0.08f, 0), 0.5f, 0.05f, 4000f), Hour, 0));
                }
            return l;
        }
    }
}

/// <summary>
/// Combat animation authored in code, one performer per program, on the meadow: the hoplite's overhand thrust, block,
/// heavy hit and victory; the Celtic swordsman's overhead and side cuts and a forward death; the archer's draw, hold
/// and loose (the bow's limbs bend and its string follows the hand); surrender (weapons dropped, kneeling, hands
/// raised); a death falling backwards; and a hoplite advancing at a walk with his guard held by the upper-body layer
/// (motion-capture legs, authored arms, the hold constraint keeping spear and shield in hand). PRESENTATION ONLY.
/// </summary>
[DemoScene("kit-combat", "Stage kit: authored combat animation (thrust, cuts, block, bow, hit, deaths, surrender, victory, walk blend)")]
public sealed class KitCombatScene : KitSceneBase
{
    protected override float StageRadius => 13f;
    readonly Dictionary<string, KitSoldier> _p = new();
    readonly Dictionary<string, Vector2> _at = new();
    BowRig _bow = null!;
    CharInstance _arrow = null!;
    KitItem _arrowItem = null!;
    public const float WalkSpeed = 1.2f;

    static readonly (string name, string id, string outfit, string set)[] Cast =
    {
        ("hoplite", "a1", "anc_a", "hoplite"), ("swordsman", "b3", "anc_b", "sword.c"), ("archer", "b1", "anc_b", "bow"),
        ("surrender", "a2", "anc_a", "hoplite"), ("deathback", "a3", "anc_a", "hoplite"), ("walker", "a4", "anc_a", "hoplite"),
    };

    protected override void BuildKit()
    {
        var spots = new Dictionary<string, Vector2> { ["hoplite"] = new(-2.4f, 0), ["swordsman"] = new(0.4f, 0), ["archer"] = new(3.2f, 0),
            ["surrender"] = new(-2.4f, -3.2f), ["deathback"] = new(1.0f, -3.4f), ["walker"] = new(-6.5f, 2.8f) };
        foreach (var (name, id, outfit, set) in Cast)
        {
            var body = SpawnKitOutfit(id, outfit);
            var m = Gfx.Gear.Rig.KitSockets.Measure(body.Asset);
            Gfx.Gear.Anim.CombatClips.Install(body.Asset);
            var kit = new Loadout();
            uint seed = (uint)(500 + _p.Count);
            if (set == "hoplite")
            {
                kit.Helmet = Helmets.Build(new HelmetSpec { Kind = "corinthian", Seed = seed, Crest = "#7E2A20" }, m);
                kit.Right = Weapons.Build(new WeaponSpec { Kind = "spear", Seed = seed, MapSize = 512 }); kit.RightFrame = "grip_over";
                kit.Left = Aspis.Build(new AspisSpec { Seed = seed, MapSize = 1024, Device = "crescent" }); kit.Mode = Gfx.Gear.Rig.LeftHold.Strap;
            }
            else if (set == "sword.c")
            {
                kit.Right = Weapons.Build(new WeaponSpec { Kind = "longsword", Seed = seed, MapSize = 512 });
                kit.Left = Shields.Build(new ShieldSpec { Kind = "thureos", Seed = seed, Field = "#56201B", Charge = "#CFC5AE", Device = "spiral" }); kit.Mode = Gfx.Gear.Rig.LeftHold.Central;
                kit.Carried.Add((Weapons.Build(new WeaponSpec { Kind = "scabbard", Seed = seed, MapSize = 512 }), "hang", "hip_l"));
                kit.Helmet = Helmets.Build(new HelmetSpec { Kind = "montefortino", Seed = seed, Crest = null }, m);
            }
            else
            {
                kit.Left = Weapons.Build(new WeaponSpec { Kind = "bow", Seed = seed, MapSize = 1024 }); kit.Mode = Gfx.Gear.Rig.LeftHold.Hand;
                kit.Carried.Add((Weapons.Build(new WeaponSpec { Kind = "quiver", Seed = seed, MapSize = 512 }), "hang", "hip_r"));
            }
            var s = new KitSoldier(Kit, body, kit, "kit:" + name);
            s.Ground = p => Scenery.HeightAt(p.X, p.Z);
            var at = spots[name]; _at[name] = at;
            float yaw = name == "walker" ? MathHelper.PiOver2 : 0.35f;
            string c = set == "sword.c" ? ".c" : "";
            string guard = set == "hoplite" ? "kit.hoplite.guard" : set == "bow" ? "kit.bow.shoot" : "kit.sword.guard.c";
            Func<float, AnimInput> script = name switch
            {
                "hoplite" => t => Program(t, guard, ("kit.hoplite.thrust", 1.0f), ("kit.hoplite.block", 2.6f), ("kit.hoplite.hit", 4.0f), ("kit.hoplite.victory", 5.2f)),
                "swordsman" => t => Program(t, guard, ("kit.sword.cut_over.c", 1.0f), ("kit.sword.cut_side.c", 2.7f), ("kit.sword.death_fwd.c", 4.5f)),
                "archer" => t => new AnimInput { Speed = 0, Action = "kit.bow.shoot", ActionStart = 0.5f, HoldLastFrame = true, FootLock = false },
                "surrender" => t => Program(t, guard, ("kit.hoplite.surrender", 0.5f)),
                "deathback" => t => Program(t, guard, ("kit.hoplite.death_back", 0.5f)),
                _ => t => new AnimInput { Speed = WalkSpeed, UpperClip = guard, UpperClipTime = t % 2f, UpperWeight = 1f, FootLock = true },
            };
            Func<float, Matrix> place = name == "walker"
                ? t => Matrix.CreateRotationY(Facing + MathHelper.PiOver2) * Matrix.CreateTranslation(AlongX(At(at.X, at.Y), WalkSpeed * t))
                : t => Place(at.X, at.Y, yaw);
            s.Drive(script, place);
            if (name == "surrender") { s.ReleaseRight = 0.5f + 0.30f; s.ReleaseLeft = 0.5f + 0.40f; }
            if (name == "deathback") { s.ReleaseRight = 0.5f + 0.30f; s.ReleaseLeft = 0.5f + 1.0f; }   // the shield slips off the arm as he falls
            if (name == "swordsman") s.ReleaseRight = 4.5f + 0.40f;
            s.FaceDir = name == "swordsman" ? _ => Vector3.Transform(Vector3.UnitZ, Matrix.CreateRotationY(Facing + yaw)) : _ => null;
            if (set == "bow")
            {
                // the bow is drawn by the rig (the left hand holds the rig's instance instead of a plain prop)
                s.LeftProp!.HiddenParts.Add(kit.Left!.Parts[0].Name);
                _bow = BowRig.Create(Kit, kit.Left);
                _arrowItem = Weapons.Build(new WeaponSpec { Kind = "arrow", Seed = seed, MapSize = 256 });
                _arrow = Kit.AddProp(_arrowItem, Matrix.Identity);
            }
            _p[name] = s;
        }
        Animate(0);
    }

    static AnimInput Program(float t, string guard, params (string clip, float start)[] steps)
    {
        string action = guard; float start = 0f;
        foreach (var (clip, st) in steps) if (t >= st) { action = clip; start = st; }
        // authored clips place the feet themselves (leg IK at bake time); the animator's foot lock is for locomotion
        return new AnimInput { Speed = 0, Action = action, ActionStart = start, HoldLastFrame = true, FootLock = false };
    }

    Vector3 AlongX(Vector3 start, float metres)
    {
        var p = start + Vector3.Transform(new Vector3(metres, 0, 0), Matrix.CreateRotationY(Facing));
        return new Vector3(p.X, Scenery.HeightAt(p.X, p.Z), p.Z);
    }

    protected override void Animate(float t)
    {
        foreach (var s in _p.Values) s.Update(t);
        // the archer: the bow follows the left hand; its string's nock follows the right hand from the draw to the loose
        var archer = _p["archer"];
        var (_, lw) = archer.Hold.ItemWorlds(archer.Body);
        var bw = lw ?? Matrix.Identity;
        _bow.Instance.World = bw;
        float clipT = t - 0.5f;
        var nock = Gfx.Gear.Rig.KitSockets.For(archer.Body.Asset)["grip_r"].World(archer.Body).Translation;
        bool drawing = clipT >= 0.55f && clipT < 1.66f;
        _bow.Draw(drawing ? nock : null);
        // the arrow lies on the bow hand from the nock; after the loose it flies along its axis
        var grip = bw.Translation;
        var from = drawing ? nock : Vector3.Transform(BowRig.Brace, bw);
        var axis = Vector3.Normalize(grip - from + Vector3.TransformNormal(new Vector3(0, 0, 0.02f), bw));
        if (clipT >= 1.66f) from += axis * MathF.Min(30f * (clipT - 1.66f), 40f);
        var side = Vector3.Normalize(Vector3.Cross(axis, Vector3.Up));
        var up = Vector3.Cross(side, axis);
        _arrow.World = new Matrix(side.X, side.Y, side.Z, 0, axis.X, axis.Y, axis.Z, 0, up.X, up.Y, up.Z, 0, from.X, from.Y, from.Z, 1);
        if (clipT < 0.25f || clipT > 1.66f + 1.2f) _arrow.HiddenParts.Add("arrow"); else _arrow.HiddenParts.Remove("arrow");
    }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var l = new List<CaptureShot>();
            CameraPose Side(string who, float dx, float dz, float h = 1.15f, float fov = 0.62f)
            {
                var a = _at[who]; var c = At(a.X, a.Y, h - 0.15f);
                return new CameraPose(At(a.X + dx, a.Y + dz, h + 0.25f), c, fov, 0.05f, 4000f);
            }
            foreach (var (n, t) in new[] { ("guard", 0.6f), ("thrust-wind", 1.35f), ("thrust-strike", 1.56f), ("block", 2.85f), ("hit", 4.12f), ("victory", 6.0f) })
                l.Add(new CaptureShot("hoplite-" + n, Side("hoplite", -2.6f, 1.6f), Hour, t));
            foreach (var (n, t) in new[] { ("guard", 0.6f), ("over-raise", 1.45f), ("over-strike", 1.74f), ("side-wind", 3.1f), ("side-strike", 3.34f), ("death-1", 4.85f), ("death-2", 5.5f), ("death-3", 6.9f) })
                l.Add(new CaptureShot("sword-" + n, Side("swordsman", -1.2f, 2.2f), Hour, t));   // front-right: the sword arm, not the shield's face; inside the walker's path (z = 2.8)
            foreach (var (n, t) in new[] { ("rest", 0.4f), ("raise", 1.05f), ("draw", 1.75f), ("hold", 2.1f), ("loose", 2.24f), ("after", 2.6f) })
                l.Add(new CaptureShot("bow-" + n, Side("archer", -3.0f, 1.3f, 1.35f), Hour, t));   // from the drawing side, chest and face in view
            foreach (var (n, t) in new[] { ("drop", 1.0f), ("kneel", 1.9f), ("hands", 3.0f) })
                l.Add(new CaptureShot("surrender-" + n, Side("surrender", 2.2f, 2.2f, 0.95f), Hour, t));
            foreach (var (n, t) in new[] { ("hit", 0.66f), ("buckle", 1.05f), ("fall", 1.45f), ("down", 2.8f) })
                l.Add(new CaptureShot("deathback-" + n, Side("deathback", -1.7f, 2.3f, 0.95f), Hour, t));
            // the walk: a camera that keeps pace beside the walker
            for (int i = 0; i < 4; i++)
            {
                float t = 3.0f + 0.25f * i;
                var p = AlongX(At(_at["walker"].X, _at["walker"].Y), WalkSpeed * t);
                var fwd = Vector3.Normalize(Cam(0, 0, 1) - Cam(0, 0, 0));
                l.Add(new CaptureShot($"walk-{i}", new CameraPose(p + fwd * 3.4f + new Vector3(0, 1.1f, 0), p + new Vector3(0, 0.95f, 0), 0.6f, 0.05f, 4000f), Hour, t));
            }
            return l;
        }
    }
}

/// <summary>
/// The era kit's later forms on the stage-chars medieval soldiers: their baked helmets, shields and weapons replaced by
/// the generated great helm, nasal helmets and kettle hats, kite and heater shields on enarmes, arming swords, an axe,
/// spears and an archer's bow with a quiver; a standard for each side. Same garments as stage chars (mail, gambesons,
/// surcoats). PRESENTATION ONLY.
/// </summary>
[DemoScene("kit-medieval", "Stage kit: later era kit (great helm, nasal, kettle, kite, heater, arming sword, axe, bow) on the medieval soldiers")]
public sealed class KitMedievalScene : KitSceneBase
{
    protected override float StageRadius => 12f;
    public readonly List<KitSoldier> Soldiers = new();
    readonly List<Banner> _banners = new();
    readonly Dictionary<string, Vector3> _eyes = new();

    protected override void BuildKit()
    {
        var cast = new (string id, string outfit, string helmet, string shield, string field, string charge, string device, string weapon, Vector2 at, float yaw)[]
        {
            ("commander_a", "kit_a", "greathelm", "heater", "#2B4373", "#C29A45", "chevron", "arming", new(-1.9f, 0.3f), MathHelper.PiOver2 - 0.35f),
            ("a2", "kit_a", "nasal", "kite", "#2B4373", "#C29A45", "cross", "spear", new(-3.0f, -0.9f), MathHelper.PiOver2),
            ("a3", "kit_a", "kettle", "heater", "#2B4373", "#C29A45", "bend", "arming", new(-3.0f, 0.5f), MathHelper.PiOver2),
            ("b2", "kit_b", "nasal", "kite", "#93301F", "#DAD3C1", "saltire", "spear", new(3.0f, -0.9f), -MathHelper.PiOver2),
            ("b3", "kit_b", "kettle", "heater", "#93301F", "#DAD3C1", "pale", "axe", new(3.0f, 0.5f), -MathHelper.PiOver2),
            ("b4", "kit_b", "", "", "", "", "", "bow", new(3.6f, 1.9f), -MathHelper.PiOver2 + 0.3f),
        };
        for (int i = 0; i < cast.Length; i++)
        {
            var c = cast[i];
            var body = Stage.Spawn(c.id, c.outfit);
            KitAssets.StripBakedKit(body);
            Gfx.Gear.Anim.CombatClips.Install(body.Asset);
            var m = Gfx.Gear.Rig.KitSockets.Measure(body.Asset);
            var kit = new Loadout();
            if (c.helmet.Length > 0) kit.Helmet = Helmets.Build(new HelmetSpec { Kind = c.helmet, Seed = (uint)(700 + i), Crest = null, Wear = 0.5 }, m);
            string set;
            if (c.weapon == "bow")
            {
                kit.Left = Weapons.Build(new WeaponSpec { Kind = "bow", Seed = (uint)(710 + i), MapSize = 1024 }); kit.Mode = Gfx.Gear.Rig.LeftHold.Hand;
                kit.Carried.Add((Weapons.Build(new WeaponSpec { Kind = "quiver", Seed = (uint)(720 + i), MapSize = 512 }), "hang", "hip_r"));
                set = "bow";
            }
            else
            {
                kit.Right = Weapons.Build(new WeaponSpec { Kind = c.weapon, Seed = (uint)(730 + i), MapSize = 512 });
                kit.RightFrame = c.weapon == "spear" ? "grip_over" : "grip";
                kit.Left = Shields.Build(new ShieldSpec { Kind = c.shield, Seed = (uint)(740 + i), Field = c.field, Charge = c.charge, Device = c.device, Wear = 0.45 });
                kit.Mode = Gfx.Gear.Rig.LeftHold.Strap;
                kit.SleeveThickness = 0.028f;   // mail over a quilted gambeson on the shield arm
                if (c.weapon == "arming") kit.Carried.Add((Weapons.Build(new WeaponSpec { Kind = "scabbard", Seed = (uint)(750 + i), MapSize = 512 }), "hang", "hip_l"));
                set = c.weapon == "spear" ? "hoplite" : "sword";
            }
            var s = new KitSoldier(Kit, body, kit, "kitm:" + c.id);
            s.Ground = p => Scenery.HeightAt(p.X, p.Z);
            string clip = set == "bow" ? "kit.bow.shoot" : set == "hoplite" ? "kit.hoplite.guard" : "kit.sword.guard";
            float phase = 0.41f * i;
            var at = c.at; float yaw = c.yaw;
            // the archer stands at rest with the bow (the clip's first frame); the others in their guards
            s.Drive(t => new AnimInput { Speed = 0, Action = clip, ActionStart = set == "bow" ? 100f : -phase, HoldLastFrame = true, FootLock = false, Idle = "idle" },
                t => Place(at.X, at.Y, yaw), phase);
            if (set == "bow") s.Drive(t => new AnimInput { Speed = 0, Action = clip, ActionStart = 0f, HoldLastFrame = true, FootLock = false }, t => Place(at.X, at.Y, yaw), phase);
            Soldiers.Add(s);
        }
        _banners.Add(Banner.Create(Kit, Banner.Build(new BannerSpec { Seed = 63, Field = "#2B4373", Charge = "#C29A45", Device = "chevron", Fringe = "#C29A45" }),
            Matrix.CreateRotationY(Facing + MathHelper.PiOver2) * Matrix.CreateTranslation(At(-4.4f, -0.2f))));
        _banners.Add(Banner.Create(Kit, Banner.Build(new BannerSpec { Seed = 64, Field = "#93301F", Charge = "#DAD3C1", Device = "saltire", Fringe = "#DAD3C1" }),
            Matrix.CreateRotationY(Facing - MathHelper.PiOver2) * Matrix.CreateTranslation(At(4.4f, -0.4f))));
        Animate(0);
        foreach (var s in Soldiers) _eyes[s.Body.Asset.Id] = Eyes(s.Body);
    }

    protected override void Animate(float t)
    {
        foreach (var s in Soldiers) s.Update(2.0f + t);
        for (int i = 0; i < _banners.Count; i++) _banners[i].Pose(2.0f + t + 0.9f * i);
    }

    public override IReadOnlyList<CaptureShot> Shots
    {
        get
        {
            var fwd = Vector3.Normalize(Cam(0, 0, 1) - Cam(0, 0, 0));
            var l = new List<CaptureShot>
            {
                new("lines-34", new CameraPose(Cam(0.6f, 2.3f, 7.4f), Cam(0f, 0.95f, 0f), 0.62f, 0.1f, 4000f), Hour, 0),
                new("force-a", new CameraPose(Cam(0.6f, 1.55f, 1.6f), Cam(-2.6f, 1.1f, 0f), 0.62f, 0.1f, 4000f), Hour, 0),
                new("force-b", new CameraPose(Cam(-0.6f, 1.55f, 1.6f), Cam(3.1f, 1.1f, 0.3f), 0.62f, 0.1f, 4000f), Hour, 0),
            };
            foreach (var id in new[] { "commander_a", "a2", "a3", "b3" })
                if (_eyes.TryGetValue(id, out var ey))
                {
                    var dir = id.StartsWith('b') ? -1f : 1f;
                    var face = Vector3.Normalize(Cam(dir, 0, 0) - Cam(0, 0, 0));
                    l.Add(new CaptureShot("face-" + id, new CameraPose(ey + face * 0.85f + fwd * 0.40f + new Vector3(0, 0.03f, 0), ey + new Vector3(0, -0.10f, 0), 0.55f, 0.05f, 4000f), Hour, 0));
                }
            return l;
        }
    }
}
