using Sim.Core.State;

namespace Sim.Core.Observability;

/// <summary>
/// Display names for the ledger's reason ids. <see cref="ReasonIds"/> carries
/// ids only (ADR-001: names never live in sim rows), so a reader of a telemetry
/// line would otherwise see "reason 14" and have to open Ids.cs. This table is
/// the observer's own and is display-only — nothing keys on the string, and an
/// id this table does not know is rendered as its number, never dropped.
/// </summary>
public static class ReasonNames
{
    public static string Of(int reason) => reason switch
    {
        1 => "growth",
        2 => "initialEndowment",
        3 => "harvest",
        4 => "eaten",
        5 => "births",
        6 => "deaths",
        7 => "starvation",
        8 => "produced",
        9 => "inputsConsumed",
        10 => "toolWear",
        11 => "housingBuilt",
        12 => "housingDecayed",
        13 => "housingMaterials",
        14 => "spoilage",
        15 => "granaryOverflow",
        16 => "constructionMaterials",
        _ => "reason-" + reason.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
