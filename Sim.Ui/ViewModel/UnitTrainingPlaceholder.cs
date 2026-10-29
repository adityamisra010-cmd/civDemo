using Sim.Core.Systems.Ages;

namespace Sim.Ui.ViewModel;

/// <summary>
/// M6.12 — THE TRAINING PLACEHOLDER on the unit card (D-F option 1, RULED 2026-10-20: "the training panel remains a labelled,
/// non-functional placeholder"; D-066 A2 item 4: the slice's inert promotion entry shown "earned in M7"). ONE TEXT LINE: a
/// pure function of the formation's unit FAMILY (which the card already reads) and the inert unit-progression content. It
/// reads no world state, emits no order and draws no control — the card's only control stays its close button. It is
/// distinct from ADR-055's real recruit / re-equip / disband controls (the Policy surface's roster), which it neither
/// mentions nor replaces. One line, not two: the card fits unscrolled at the 1280x800 design window (P11, measured), and a
/// second row would not. Source guard: UnitTrainingPlaceholderTests.
/// </summary>
public static class UnitTrainingPlaceholder
{
    /// <summary>The labelled entry point (task register M6.12).</summary>
    public const string TrainingText = "Training — planned for M7";

    /// <summary>The promotion half of the line for a family no tree covers.</summary>
    public const string NoTreeText = "no promotions authored";

    /// <summary>Between the training label and the promotion entry.</summary>
    public const string Separator = " · ";

    private static readonly Lazy<UnitProgressionContent> ShippedContent = new(() =>
    {
        using Stream stream = Sim.Data.DataFiles.OpenUnitProgression();
        return UnitProgressionContentLoader.Load(stream, families: null);
    });

    /// <summary>The shipped unit-progression content (loaded once; validated by its loader).</summary>
    public static UnitProgressionContent Shipped => ShippedContent.Value;

    /// <summary>The inert promotion entry: the family tree's first promotion with no prerequisite, "earned in M7".</summary>
    public static string PromotionText(Promotion promotion) => "'" + promotion.Name + "': earned in M7";

    /// <summary>The card's planned-for-M7 line for a formation of <paramref name="family"/>: the training label, then its
    /// tree's inert first promotion (or <see cref="NoTreeText"/>). <paramref name="content"/> null = <see cref="Shipped"/>.</summary>
    public static string Line(UnitFamily? family, UnitProgressionContent? content = null)
    {
        content ??= Shipped;
        Promotion? first = family is null ? null : content.TreeForFamily(family.Id)?.FirstRoot();
        return TrainingText + Separator + (first is null ? NoTreeText : PromotionText(first));
    }
}
