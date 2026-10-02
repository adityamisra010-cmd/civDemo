using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems.Ages;

namespace Sim.Ui.Theme;

/// <summary>
/// THE NINE VISUAL ERAS of the interface — one per ratified Age (ages.json keys 1..9). An era is a
/// PRESENTATION of the player's Age, never a state of its own (ADR-033 D8): it is not stored in the
/// simulation, not serialized, and recomputed from the world after every step and every load.
/// </summary>
public enum UiEra
{
    Prehistoric = 1,
    Neolithic = 2,
    Bronze = 3,
    Iron = 4,
    Classical = 5,
    Medieval = 6,
    EarlyModern = 7,
    Industrial = 8,
    Modern = 9,
}

/// <summary>
/// THE DERIVATION — the one pure function from authoritative Age state to the visual era:
/// <c>UiEra = f(AgeQuery.CurrentAge(world, cfg.Ages, player))</c> (ADR-033 D8). Research changes
/// WHAT is shown; only the Age changes HOW it is shown, so nothing here reads research, a date, a
/// turn number or an era band (law 4). The live renderer and the headless previews both call it.
/// </summary>
public static class UiEras
{
    public const int Count = 9;

    public static IReadOnlyList<UiEra> All { get; } =
    [
        UiEra.Prehistoric, UiEra.Neolithic, UiEra.Bronze, UiEra.Iron, UiEra.Classical,
        UiEra.Medieval, UiEra.EarlyModern, UiEra.Industrial, UiEra.Modern,
    ];

    /// <summary>The player's visual era: the era of <see cref="AgeQuery.CurrentAge"/>. With no Age
    /// content loaded there is no Age state, and the interface presents the first era (the shipped
    /// content founds every polity in Age 1).</summary>
    public static UiEra Of(IReadOnlyWorldState world, AgeContent? ages, PolityId player) =>
        ages is null ? UiEra.Prehistoric : FromAge(AgeQuery.CurrentAge(world, ages, player));

    /// <summary>The era of an Age key (1..9; out-of-range keys clamp to the nearest era).</summary>
    public static UiEra FromAge(int age) => (UiEra)Math.Clamp(age, 1, Count);

    /// <summary>The era's ordinal, 1 (Prehistoric) … 9 (Modern) — the Age key it presents.</summary>
    public static int Ordinal(this UiEra era) => (int)era;
}
