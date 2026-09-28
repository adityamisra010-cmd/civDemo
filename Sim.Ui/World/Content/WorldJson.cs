namespace Sim.Ui.World.Content.Json;

// The on-disk shape of ui-content/world/*.json (docs/architecture/world-visualization.md §5).
// Plain mutable DTOs with every field nullable, so that the LOADER — not the serializer —
// decides what is missing and says so with a path. Unknown members and duplicate keys are
// errors (the shared strict options), so a typo never silently drops an edit.

internal sealed class MorphologyDto
{
    public string? Schema { get; set; }
    public string? Notice { get; set; }
    public SpriteDto? Sprite { get; set; }
    public LodDto? Lod { get; set; }
    public LayoutDto? Layout { get; set; }
    public SettlementStagesDto? SettlementStages { get; set; }
    public AggregationDto? Aggregation { get; set; }
    public List<VisualTypeDto>? VisualTypes { get; set; }
    public List<SpecializationDto>? Specializations { get; set; }
    public List<StateStyleDto>? States { get; set; }
    public List<InfraTypeDto>? InfrastructureTypes { get; set; }
    public List<NodeTypeDto>? NodeTypes { get; set; }
    public List<ResourceTypeDto>? ResourceTypes { get; set; }
    public List<AgentTypeDto>? AgentTypes { get; set; }
    public List<SizeBandDto>? AgentSizeBands { get; set; }
    public List<string>? PolityInks { get; set; }
    public List<LiveProjectDto>? LiveProjects { get; set; }
}

internal sealed class SpriteDto
{
    public double? WorldPerUnit { get; set; }
    public double? MinPxPerUnit { get; set; }
    public double? MaxPxPerUnit { get; set; }
    public string? Note { get; set; }
}

internal sealed class LodDto
{
    public double? MidFromPxPerUnit { get; set; }
    public double? NearFromPxPerUnit { get; set; }
    public string? Note { get; set; }
}

internal sealed class RingDto
{
    public double? Radius { get; set; }
    public int? Lots { get; set; }
    public double? OffsetDeg { get; set; }
}

internal sealed class LayoutDto
{
    public double? PlazaRadius { get; set; }
    public double? SlotSize { get; set; }
    public double? BlockSize { get; set; }
    public List<RingDto>? SlotRings { get; set; }
    public List<RingDto>? BlockRings { get; set; }
    public string? Note { get; set; }
}

internal sealed class DriverDto
{
    public string? Input { get; set; }
    public string? Provenance { get; set; }
    public bool? NamedStages { get; set; }
    public List<double>? Thresholds { get; set; }
    public string? Note { get; set; }
}

internal sealed class SettlementStageDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int? Blocks { get; set; }
}

internal sealed class SettlementStagesDto
{
    public string? Note { get; set; }
    public List<DriverDto>? Drivers { get; set; }
    public List<SettlementStageDto>? Stages { get; set; }
}

internal sealed class AggregationDto
{
    public int? IndividualUpTo { get; set; }
    public string? Provisional { get; set; }
}

internal sealed class GlyphDto
{
    public string? Base { get; set; }
    public string? Mark { get; set; }
}

internal sealed class StageRuleDto
{
    public string? Input { get; set; }
    public string? Label { get; set; }
    public Dictionary<string, double>? Ordinals { get; set; }
}

internal sealed class PartDto
{
    public string? Kind { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? W { get; set; }
    public double? H { get; set; }
}

internal sealed class MorphStageDto
{
    public string? Name { get; set; }
    public double? Min { get; set; }
    public List<PartDto>? Adds { get; set; }
}

internal sealed class VisualTypeDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public GlyphDto? Glyph { get; set; }
    public StageRuleDto? StageBy { get; set; }
    public string? Note { get; set; }
    public List<MorphStageDto>? Stages { get; set; }
}

internal sealed class SpecializationDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Mark { get; set; }
}

internal sealed class StateStyleDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? GlyphState { get; set; }
}

internal sealed class InfraStageDto
{
    public string? Name { get; set; }
    public double? Min { get; set; }
    public double? WidthPx { get; set; }
    public string? Stroke { get; set; }
    public string? Ink { get; set; }
}

internal sealed class InfraTypeDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public StageRuleDto? StageBy { get; set; }
    public List<InfraStageDto>? Stages { get; set; }
}

internal sealed class NodeTypeDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public GlyphDto? Glyph { get; set; }
}

internal sealed class ResourceTypeDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Mark { get; set; }
}

internal sealed class AgentTypeDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public GlyphDto? Glyph { get; set; }
    public string? CountNoun { get; set; }
}

internal sealed class SizeBandDto
{
    public long? Min { get; set; }
    public double? Scale { get; set; }
}

internal sealed class LiveProjectDto
{
    public string? Project { get; set; }
    public string? VisualType { get; set; }
}

// ---------------------------------------------------------------- the demo world

internal sealed class DemoWorldDto
{
    public string? Schema { get; set; }
    public bool? Placeholder { get; set; }
    public string? Notice { get; set; }
    public DemoExtentDto? World { get; set; }
    public List<DemoWaterDto>? Water { get; set; }
    public List<DemoStepDto>? Steps { get; set; }
    public List<DemoPolityDto>? Polities { get; set; }
    public List<DemoEntityDto>? Settlements { get; set; }
    public List<DemoEntityDto>? Structures { get; set; }
    public List<DemoEntityDto>? Nodes { get; set; }
    public List<DemoEntityDto>? Edges { get; set; }
    public List<DemoEntityDto>? Resources { get; set; }
    public List<DemoEntityDto>? Agents { get; set; }
}

internal sealed class DemoExtentDto
{
    public double? Width { get; set; }
    public double? Height { get; set; }
}

/// <summary>Decorative water on the demo's paper (a rectangle in world units) — backdrop only.</summary>
internal sealed class DemoWaterDto
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? W { get; set; }
    public double? H { get; set; }
}

internal sealed class DemoStepDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Caption { get; set; }
}

internal sealed class DemoPolityDto
{
    public string? Key { get; set; }
    public string? Name { get; set; }
    public long? InkSeed { get; set; }
}

/// <summary>One demo entity of any kind. Which fields a kind may carry is checked by the
/// loader (a structure has a settlement, an agent may not, …).</summary>
internal sealed class DemoEntityDto
{
    public string? Key { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Settlement { get; set; }
    public string? Polity { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public string? A { get; set; }
    public string? B { get; set; }
    public List<string>? Members { get; set; }
    public string? Note { get; set; }
    public List<DemoEntryDto>? Timeline { get; set; }
}

/// <summary>One timeline entry: the entity's reported values from <see cref="Step"/> on.
/// Values merge forward; inputs merge BY NAME (a null value deletes that input); an entry
/// with <see cref="Removed"/> ends the entity at that step (exclusive).</summary>
internal sealed class DemoEntryDto
{
    public int? Step { get; set; }
    public bool? Removed { get; set; }
    public Dictionary<string, double?>? Inputs { get; set; }
    public Dictionary<string, string?>? Labels { get; set; }
    public long? Multiplicity { get; set; }
    public string? Specialization { get; set; }
    public string? State { get; set; }
    public string? Visibility { get; set; }
    public string? Polity { get; set; }
    public double? X { get; set; }
    public double? Y { get; set; }
    public string? Edge { get; set; }
    public double? Fraction { get; set; }
    public string? Settlement { get; set; }
    public double? Heading { get; set; }
    public long? Count { get; set; }
}
