using Sim.Core.Kernel;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.Tests;

/// <summary>
/// H2 (Director 2026-10-05 §7): the tax edict is operational only from sim.json governance.taxationMinAge (A3).
/// A fixture that grants the Taxation civic to make the edict usable must ALSO put the polity in that Age — this
/// rig upserts the AgeStateRow AgeTransitionSystem would have written for an AdvanceAge order on eligibility.
/// </summary>
internal static class TaxAgeRig
{
    /// <summary>The Age the edict needs (1 = no Age requirement).</summary>
    public static int TaxAge(SimConfig cfg) => cfg.Governance?.TaxationMinAge ?? 1;

    /// <summary><paramref name="polity"/> enters the edict's Age (upsert; the founding Age writes nothing).</summary>
    public static WorldState EnterTaxAge(WorldState w, SimConfig cfg, PolityId polity)
    {
        int age = TaxAge(cfg);
        for (int i = 0; i < w.AgeStates.Count; i++)
        {
            if (w.AgeStates[i].Polity.Value != polity.Value) continue;
            if (w.AgeStates[i].Age < age) w.AgeStates[i] = new AgeStateRow(polity, age, w.Clock.Turn, 1, w.Clock.Turn);
            return w;
        }
        if (cfg.Ages is { } ages && age == ages.FoundingAge) return w;
        w.AgeStates.Add(new AgeStateRow(polity, age, w.Clock.Turn, 1, w.Clock.Turn));
        return w;
    }
}
