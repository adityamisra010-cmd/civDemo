using System.Globalization;
using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// M5: the tax-rate control's order, and the reading that explains it.
///
/// THE ORDER IS THE ONLY WAY IN. The panel never writes state — it emits a
/// SetTaxRate order stamped with the CURRENT turn, exactly like the sector and
/// labor controls, and the sim enacts it on the next step. That is the packet's
/// rule and it is also what keeps the session log complete: a rate the director
/// set is in the log, so the replay sets it too.
///
/// WHAT THE PANEL SHOWS, and why it is three numbers rather than one. A declared
/// rate is not what anyone pays. The state collects the declared rate scaled by
/// ADMINISTRATIVE REACH, which decays with travel cost from the capital, so the
/// same edict lands differently on the seat and on the frontier. Showing only the
/// declared number would let the director tax an empire he cannot reach and read
/// the shortfall as a bug. All three come from <see cref="Governance"/> — the
/// sim's own readers, not a second opinion computed here.
///
/// PURE view-model: ints, doubles, strings and OrderRecords — no MonoGame or
/// ImGui types, headless-testable like the other factories.
/// </summary>
public static class TaxOrderFactory
{
    /// <summary>The Empire the human director commands.</summary>
    public static PolityId PlayerEmpire => LaborOrderFactory.PlayerEmpire;

    /// <summary>The legislative range, in percent — the same bounds
    /// <see cref="OrderValidation"/> enforces on the way in.</summary>
    public const int MinPercent = 0;
    public const int MaxPercent = 100;

    /// <summary>True when the typed rate is one the order pipeline will accept.</summary>
    public static bool CanSubmit(int percent) => percent is >= MinPercent and <= MaxPercent;

    /// <summary>
    /// The order. An Empire legislates ITS OWN taxes — actor and target are the
    /// same polity, which is what <see cref="OrderValidation"/> requires — and
    /// the payload is a percentage, converted to a fraction by the system that
    /// enacts it rather than here, so the log records what the director typed.
    /// </summary>
    public static OrderRecord Create(long currentTurn, int percent)
        => Create(currentTurn, PlayerEmpire, percent);

    /// <summary>As above, with the legislating Empire named explicitly.</summary>
    public static OrderRecord Create(long currentTurn, PolityId issuer, int percent)
    {
        if (!CanSubmit(percent))
        {
            throw new ArgumentOutOfRangeException(nameof(percent), percent,
                $"a tax rate is {MinPercent}..{MaxPercent} percent.");
        }

        return OrderRecord.From(
            currentTurn, issuer, OrderKind.SetTaxRate, issuer.Value, percent);
    }

    /// <summary>
    /// The declared rate currently on the books, as a percentage — what the
    /// widget should show when the panel opens, so it reads the world rather
    /// than remembering what was last typed.
    /// </summary>
    public static int DeclaredPercent(IReadOnlyWorldState world, PolityId polity)
        => (int)Math.Round(Governance.NominalTaxRate(world, polity) * 100.0,
            MidpointRounding.AwayFromZero);

    /// <summary>
    /// One line per controlled settlement: the reach the state has there and the
    /// rate it therefore actually collects. Ordered by settlement id — a stable
    /// integer key, never table order.
    /// </summary>
    public static IReadOnlyList<string> BurdenLines(
        IReadOnlyWorldState world, PolityId polity, SimConfig cfg, NameLookup? names = null)
    {
        var places = new List<SettlementId>();
        for (int i = 0; i < world.Controls.Count; i++)
            if (world.Controls[i].Polity == polity) places.Add(world.Controls[i].Place);
        places.Sort(static (a, b) => a.Value.CompareTo(b.Value));

        var lines = new string[places.Count];
        for (int i = 0; i < places.Count; i++)
        {
            SettlementId s = places[i];
            double reach = Governance.AdministrativeReach(world, s, cfg);
            double effective = Governance.EffectiveTaxRate(world, s, cfg);
            string name = names?.Invoke(s) ?? string.Create(CultureInfo.InvariantCulture, $"#{s.Value}");
            lines[i] = string.Create(CultureInfo.InvariantCulture,
                $"{name}: reach {reach * 100.0:F0}% -> collects {effective * 100.0:F0}%");
        }
        return lines;
    }

    /// <summary>How the panel names a settlement. Injected so this file stays
    /// free of the chronicle registry and remains a pure view-model.</summary>
    public delegate string NameLookup(SettlementId settlement);

    /// <summary>
    /// The realm's legitimacy, 0..100 — the population-weighted mean happiness
    /// of what this Empire controls. The number the tax slider is really being
    /// traded against, which is why it sits beside it rather than in a
    /// different panel.
    /// </summary>
    public static string LegitimacyLine(
        IReadOnlyWorldState world, PolityId polity, SimConfig cfg)
        => string.Create(CultureInfo.InvariantCulture,
            $"legitimacy {Governance.Legitimacy(world, polity, cfg):F1} / 100");
}
