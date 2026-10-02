using Sim.Core.Systems.Research;
using Rgba = Sim.Ui.Art.ParchmentPalette.Rgba;

namespace Sim.Ui.Progression;

/// <summary>The progression screen's colours: a night-ink field with parchment-gold accents,
/// one hue per lane, one treatment per node state.</summary>
public static class ProgressionPalette
{
    public static Rgba A(uint rgb, double alpha) { Rgba c = Rgba.Hex(rgb); return new(c.R, c.G, c.B, (byte)Math.Round(255 * Math.Clamp(alpha, 0, 1))); }

    public static readonly Rgba Field = Rgba.Hex(0x0E1319);
    public static readonly Rgba FieldBand = Rgba.Hex(0x121922);
    public static readonly Rgba Chrome = Rgba.Hex(0x0A0E13);
    public static readonly Rgba ChromeRaised = Rgba.Hex(0x18202A);
    public static readonly Rgba Hairline = Rgba.Hex(0x2A3442);
    public static readonly Rgba Gold = Rgba.Hex(0xD8B866);
    public static readonly Rgba GoldDim = Rgba.Hex(0x8C7742);
    public static readonly Rgba Text = Rgba.Hex(0xE8E1CF);
    public static readonly Rgba TextSoft = Rgba.Hex(0xA9A390);
    public static readonly Rgba TextDim = Rgba.Hex(0x667080);
    public static readonly Rgba Cyan = Rgba.Hex(0x5FD3E6);
    public static readonly Rgba Amber = Rgba.Hex(0xE0A040);
    public static readonly Rgba Red = Rgba.Hex(0xD0605A);
    public static readonly Rgba Green = Rgba.Hex(0x6CC28A);

    public static readonly Rgba CompletedFill = Rgba.Hex(0x2E2818);
    public static readonly Rgba TargetFill = Rgba.Hex(0x123038);
    public static readonly Rgba AvailableFill = Rgba.Hex(0x1C2733);
    public static readonly Rgba LockedFill = Rgba.Hex(0x131820);
    public static readonly Rgba AvailableEdge = Rgba.Hex(0xA9C3DA);
    public static readonly Rgba LockedEdge = Rgba.Hex(0x2B3440);

    /// <summary>The lane hue for a content subtree id, or the trunk/civics hue.</summary>
    public static Rgba Branch(string laneId) => laneId switch
    {
        "main" => Rgba.Hex(0xD8C48A),
        "military" => Rgba.Hex(0xC8584F),
        "medicine" => Rgba.Hex(0x4FB57E),
        "engineering" => Rgba.Hex(0xD98F3E),
        "natural_science" => Rgba.Hex(0x5E95DC),
        "agriculture" => Rgba.Hex(0xA4C24E),
        "external" => Rgba.Hex(0x7A8494),
        _ => Rgba.Hex(0xB383DA),   // civics domains and any future lane
    };

    public static Rgba BranchOf(ResearchContent content, ResearchNode node) =>
        node.Tree == ResearchTree.Civics ? Branch("civics")
        : node.Branch < 0 ? Branch("main") : Branch(content.Branches[node.Branch].Id);

    public static string StateLabel(NodeState s) => s switch
    {
        NodeState.Completed => "Completed",
        NodeState.CurrentTarget => "Researching",
        NodeState.Available => "Available",
        NodeState.Partial => "Partly researched",
        _ => "Locked",
    };
}
