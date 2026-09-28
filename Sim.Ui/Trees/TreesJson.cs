using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.Ui.Trees.Json;

// The on-disk shape of ui-content/trees/*.json (docs/architecture/the-trees-content-schema.md).
// Plain mutable DTOs: every field nullable so that the LOADER, not the serializer, decides
// what is missing and says so with a path. Unknown members are an error (a typo in a
// field name must not silently drop the Director's edit).

internal static class TreesJsonOptions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
}

internal sealed class TreesFileDto
{
    public string? Schema { get; set; }
    public bool? Placeholder { get; set; }
    public string? Notice { get; set; }
    public List<LensDto>? Lenses { get; set; }
    public List<StateDto>? States { get; set; }
    public List<StateSetDto>? StateSets { get; set; }
    public List<NodeTypeDto>? NodeTypes { get; set; }
    public List<RelationKindDto>? RelationKinds { get; set; }
    public List<NodeDto>? Nodes { get; set; }
    public List<EdgeDto>? Edges { get; set; }
}

internal sealed class LensDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int? Order { get; set; }
    public string? Mark { get; set; }
    public string? ShortDescription { get; set; }
}

internal sealed class StateDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Track { get; set; }
    public string? GlyphState { get; set; }
    public int? Stage { get; set; }
    public string? Wash { get; set; }
    public string? ResearchRole { get; set; }
    public string? Legend { get; set; }
}

internal sealed class StateSetDto
{
    public string? Id { get; set; }
    public string? Description { get; set; }
    public List<string>? States { get; set; }
}

internal sealed class NodeTypeDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Base { get; set; }
    public string? Mark { get; set; }
    public string? StateSet { get; set; }
    public string? Description { get; set; }
}

internal sealed class RelationKindDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Verb { get; set; }
    public string? Flow { get; set; }
    public bool? Layering { get; set; }
    public string? Stroke { get; set; }
    public string? Ink { get; set; }
    public bool? Arrow { get; set; }
    public string? Description { get; set; }
}

internal sealed class IconDto
{
    public string? Base { get; set; }
    public string? Mark { get; set; }
}

internal sealed class VisualStyleDto
{
    public string? Era { get; set; }
    public string? Note { get; set; }
}

internal sealed class AgeRangeDto
{
    public string? From { get; set; }
    public string? To { get; set; }
}

internal sealed class ResearchCostDto
{
    public long? Points { get; set; }
    public string? Note { get; set; }
}

internal sealed class NodeDto
{
    public string? Id { get; set; }
    public string? Domain { get; set; }
    public List<string>? AlsoIn { get; set; }
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public IconDto? Icon { get; set; }
    public VisualStyleDto? VisualStyle { get; set; }
    public AgeRangeDto? AgeRange { get; set; }
    public List<string>? AgeRefs { get; set; }
    public ResearchCostDto? ResearchCost { get; set; }
    public List<string>? Prerequisites { get; set; }
    public List<string>? Enables { get; set; }
    public List<string>? DependsOn { get; set; }
    public List<string>? FeedsInto { get; set; }
    public List<string>? RelatedInstitutions { get; set; }
    public List<string>? RelatedInfrastructure { get; set; }
    public List<string>? RelatedIndustry { get; set; }
    public List<string>? RelatedMilitary { get; set; }
    public List<string>? RelatedApplications { get; set; }
    public List<string>? HistoricalReferences { get; set; }
    public string? DirectorNotes { get; set; }
    public string? MilestoneRef { get; set; }
    public int? Column { get; set; }
    public bool? Placeholder { get; set; }
}

internal sealed class EdgeDto
{
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Kind { get; set; }
    public string? Note { get; set; }
}

// --- ages.json ---

internal sealed class AgesFileDto
{
    public string? Schema { get; set; }
    public bool? Placeholder { get; set; }
    public string? Notice { get; set; }
    public List<MilestoneCategoryDto>? MilestoneCategories { get; set; }
    public List<AgeDto>? Ages { get; set; }
    public List<MilestoneDto>? Milestones { get; set; }
}

internal sealed class MilestoneCategoryDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public bool? Provisional { get; set; }
}

internal sealed class DateRangeDto
{
    public string? StartLabel { get; set; }
    public string? EndLabel { get; set; }
    public long? StartYear { get; set; }
    public long? EndYear { get; set; }
    public string? Note { get; set; }
}

internal sealed class AgeThemeDto
{
    public string? Register { get; set; }
    public string? Accent { get; set; }
    public string? Motif { get; set; }
}

internal sealed class TransitionEffectDto
{
    public string? Animation { get; set; }
    public string? Note { get; set; }
}

internal sealed class DeltaTDto
{
    public bool? Placeholder { get; set; }
    public double? YearsPerTurn { get; set; }
    public string? Note { get; set; }
}

internal sealed class CatchUpDto
{
    public bool? Placeholder { get; set; }
    public bool? PathwayBased { get; set; }
    public string? Note { get; set; }
}

internal sealed class AgeDto
{
    public string? Id { get; set; }
    public string? DisplayName { get; set; }
    public int? Order { get; set; }
    public string? ShortDescription { get; set; }
    public DateRangeDto? DateRange { get; set; }
    public List<string>? Milestones { get; set; }
    public AgeThemeDto? VisualTheme { get; set; }
    public IconDto? Icon { get; set; }
    public TransitionEffectDto? TransitionEffect { get; set; }
    public DeltaTDto? DeltaT { get; set; }
    public CatchUpDto? CatchUp { get; set; }
    public string? HistoricalContext { get; set; }
    public bool? Placeholder { get; set; }
}

internal sealed class MilestoneDto
{
    public string? Id { get; set; }
    public string? Age { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool? Mandatory { get; set; }
    public List<string>? Prerequisites { get; set; }
    public string? EvidenceNotes { get; set; }
    public string? HistoricalSource { get; set; }
    public List<string>? NodeRefs { get; set; }
    public bool? Placeholder { get; set; }
}

// --- gallery.json ---

internal sealed class GalleryFileDto
{
    public string? Schema { get; set; }
    public bool? Placeholder { get; set; }
    public string? Notice { get; set; }
    public List<MaturityStageDto>? MaturityStages { get; set; }
    public List<StatusGlyphDto>? OperationalStatuses { get; set; }
    public List<StatusGlyphDto>? UnitStates { get; set; }
    public List<VeterancyLevelDto>? VeterancyLevels { get; set; }
    public List<GalleryItemDto>? Buildings { get; set; }
    public List<GalleryItemDto>? Units { get; set; }
}

internal sealed class MaturityStageDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int? Stage { get; set; }
    public string? Legend { get; set; }
}

internal sealed class StatusGlyphDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? GlyphState { get; set; }
    public string? Legend { get; set; }
}

internal sealed class VeterancyLevelDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int? Chevrons { get; set; }
}

internal sealed class GalleryItemDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Base { get; set; }
    public string? Mark { get; set; }
    public string? Era { get; set; }
    public string? Category { get; set; }
    public string? Role { get; set; }
    public bool? Placeholder { get; set; }
}

// --- animations.json ---

internal sealed class AnimationsFileDto
{
    public string? Schema { get; set; }
    public string? Notice { get; set; }
    public List<AnimationDto>? Animations { get; set; }
}

internal sealed class AnimationDto
{
    public string? Id { get; set; }
    public string? Kind { get; set; }
    public string? AppliesTo { get; set; }
    public string? Trigger { get; set; }
    public double? PeriodSeconds { get; set; }
    public double? DurationSeconds { get; set; }
    public double? Amplitude { get; set; }
    public int? Dots { get; set; }
    public string? Description { get; set; }
}

// --- demo-state.json (the PLACEHOLDER state source) ---

internal sealed class DemoStateFileDto
{
    public string? Schema { get; set; }
    public bool? Placeholder { get; set; }
    public string? Notice { get; set; }
    public DemoCivilizationDto? Civilization { get; set; }
    public List<DemoStepDto>? Steps { get; set; }
}

internal sealed class DemoCivilizationDto
{
    public string? Name { get; set; }
    public bool? Placeholder { get; set; }
}

internal sealed class DemoStepDto
{
    public string? Label { get; set; }
    public DemoResearchDto? Research { get; set; }
    public List<DemoNodeDto>? Nodes { get; set; }
    public DemoAgesDto? Ages { get; set; }
    public List<DemoMilestoneDto>? Milestones { get; set; }
    public List<DemoBuildingDto>? Buildings { get; set; }
    public List<DemoUnitDto>? Units { get; set; }
    public DemoDiffusionDto? Diffusion { get; set; }
}

internal sealed class DemoResearchDto
{
    public long? PointsPerTurn { get; set; }
    public string? CurrentTarget { get; set; }
    public string? Note { get; set; }
}

internal sealed class DemoNodeDto
{
    public string? Id { get; set; }
    public string? State { get; set; }
    public double? ResearchProgress { get; set; }
    public long? ResearchPoints { get; set; }
    public double? RealizationProgress { get; set; }
    public string? Note { get; set; }
}

internal sealed class DemoAgesDto
{
    public string? Current { get; set; }
    public double? Progress { get; set; }
    public DemoTransitionDto? Transition { get; set; }
}

internal sealed class DemoTransitionDto
{
    public bool? Pending { get; set; }
    public string? To { get; set; }
    public string? Note { get; set; }
}

internal sealed class DemoMilestoneDto
{
    public string? Id { get; set; }
    public string? Completion { get; set; }
    public double? Progress { get; set; }
    public string? Evidence { get; set; }
}

internal sealed class DemoPersonnelDto
{
    public long? Current { get; set; }
    public long? Capacity { get; set; }
}

internal sealed class DemoBuildingDto
{
    public string? Building { get; set; }
    public string? Maturity { get; set; }
    public double? MaturityProgress { get; set; }
    public double? ConstructionProgress { get; set; }
    public DemoPersonnelDto? Personnel { get; set; }
    public string? Capacity { get; set; }
    public string? Specialization { get; set; }
    public string? AgeBuilt { get; set; }
    public string? Status { get; set; }
}

internal sealed class DemoUnitDto
{
    public string? Unit { get; set; }
    public string? State { get; set; }
    public double? Strength { get; set; }
    public long? Experience { get; set; }
    public string? Veterancy { get; set; }
    public double? ExperienceProgress { get; set; }
    public string? Training { get; set; }
    public string? Recovery { get; set; }
    public string? Doctrine { get; set; }
    public double? Cohesion { get; set; }
}

internal sealed class DemoDiffusionDto
{
    public int? Stage { get; set; }
    public string? Note { get; set; }
}
