using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// ADR-033 D4 (ported from <c>m5-full-build</c>): the tax edict's order, and the readings that
/// explain it. PURE view-model — ints, doubles, strings and OrderRecords; no MonoGame or ImGui
/// types, headless-testable like the other factories. The panel itself is stream U2's.
///
/// THE ORDER IS THE ONLY WAY IN. The UI never writes state: it emits a SetTaxRate order stamped
/// with the CURRENT turn, built by <see cref="Governance.TaxOrder"/> — the one constructor the AI
/// valve uses too — and the sim enacts it in the next step, IF the Empire can levy a tax
/// (<see cref="Governance.CanLevyTax"/>: the Taxation knowledge AND the minimum Age, H2;
/// UiSession.EmitTaxOrder refuses an edict the simulation would ignore).
///
/// WHAT THE PANEL SHOWS, and why it is three numbers rather than one. A declared rate is not what
/// anyone pays: the state collects the declared rate scaled by ADMINISTRATIVE REACH, which decays
/// with travel cost from the capital. The reach shown is the STORED fact,
/// <see cref="ControlRow.Strength"/> (<see cref="Governance.ControlStrength"/>) — the same value
/// production and happiness read — never a recomputation here.
/// </summary>
public static class TaxOrderFactory
{
    /// <summary>The Empire the human director commands.</summary>
    public static PolityId PlayerEmpire => UiPlayer.Empire;

    /// <summary>The legislative range in percent — the bounds the order log enforces at load.</summary>
    public const int MinPercent = 0;
    public const int MaxPercent = 100;

    /// <summary>True when the typed rate is one the order pipeline will accept.</summary>
    public static bool CanSubmit(int percent) => percent is >= MinPercent and <= MaxPercent;

    /// <summary>The player's edict: an Empire legislates ITS OWN rate (actor = target, which
    /// OrderValidation requires); the payload is the percentage as typed.</summary>
    public static OrderRecord Create(long currentTurn, int percent) => Create(currentTurn, PlayerEmpire, percent);

    /// <summary>As above, with the legislating Empire named explicitly.</summary>
    public static OrderRecord Create(long currentTurn, PolityId issuer, int percent)
    {
        if (!CanSubmit(percent))
            throw new ArgumentOutOfRangeException(nameof(percent), percent, $"a tax rate is {MinPercent}..{MaxPercent} percent.");
        // Turn semantics (§3.9): stamped with the CURRENT turn, the edict is delivered to the step
        // executing FROM this turn's state — the very next End Turn — which writes the policy row.
        return Governance.TaxOrder(currentTurn, issuer, percent);
    }

    /// <summary>The declared rate on the books, as a whole percentage — what the widget shows when
    /// the panel opens, so it reads the world rather than remembering what was last typed.</summary>
    public static int DeclaredPercent(IReadOnlyWorldState world, PolityId polity)
        => (int)Math.Round(Governance.NominalTaxRate(world, polity) * 100.0, MidpointRounding.AwayFromZero);

    /// <summary>How the panel names a settlement; injected so this file stays a pure view-model.</summary>
    public delegate string NameLookup(SettlementId settlement);

    /// <summary>One line per controlled settlement: the stored reach there and the rate the state
    /// therefore actually collects. Ordered by settlement id — a stable integer key, never table
    /// order.</summary>
    public static IReadOnlyList<string> BurdenLines(
        IReadOnlyWorldState world, PolityId polity, SimConfig cfg, NameLookup? names = null)
    {
        var places = new List<SettlementId>();
        for (int i = 0; i < world.Controls.Count; i++)
            if (world.Controls[i].Polity == polity && !places.Contains(world.Controls[i].Place)) places.Add(world.Controls[i].Place);
        places.Sort(static (a, b) => a.Value.CompareTo(b.Value));

        var lines = new string[places.Count];
        for (int i = 0; i < places.Count; i++)
        {
            SettlementId s = places[i];
            double reach = Governance.ControlStrength(world, polity, s);
            double effective = Governance.EffectiveTaxRate(world, s, cfg);
            string name = names?.Invoke(s) ?? string.Create(CultureInfo.InvariantCulture, $"#{s.Value}");
            lines[i] = string.Create(CultureInfo.InvariantCulture,
                $"{name}: reach {reach * 100.0:F0}% -> collects {effective * 100.0:F0}%");
        }
        return lines;
    }

    /// <summary>The realm's legitimacy, 0..100 — the population-weighted happiness of what this
    /// Empire controls; the number the rate is really traded against.</summary>
    public static string LegitimacyLine(IReadOnlyWorldState world, PolityId polity, SimConfig cfg)
        => string.Create(CultureInfo.InvariantCulture, $"legitimacy {Governance.Legitimacy(world, polity, cfg):F1} / 100");
}
