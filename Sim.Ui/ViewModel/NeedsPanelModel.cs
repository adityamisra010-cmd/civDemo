using System.Globalization;
using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// T3.9a item 3: the needs panel, PER CLASS — D-018's claim is that peasants
/// and artisans want measurably different things, and T3.5's baskets made
/// their satisfactions diverge; this model makes the difference visible.
/// PURE view-model (no MonoGame/ImGui types), read-only over existing tables.
///
/// Honesty rules carried forward from the T2.6/T2.9 gate findings:
/// - unbound needs say "not yet simulated" (they contribute exactly nothing);
/// - a bound need with no published row says "not yet measured" (rows are
///   rebuilt each turn; before the first turn NOTHING is published — a
///   fabricated 0.00 would read as total deprivation on founding day);
/// - a class with zero population shows no numbers at all ("none present"):
///   NeedsGrievanceSystem publishes no rows for an empty class, and a
///   satisfaction for nobody is per-capita-meaningless (T2.13 precedent).
/// </summary>
public sealed record NeedsClassBlock(
    string ClassName, bool Present, IReadOnlyList<string> NeedLines)
{
    public string HeaderLine => Present
        ? ClassName
        : string.Create(CultureInfo.InvariantCulture, $"{ClassName} — none present");
}

public static class NeedsPanelModel
{
    public static IReadOnlyList<NeedsClassBlock> Blocks(
        IReadOnlyWorldState world, int settlementId, NeedsConfig needs,
        IReadOnlyList<ClassEntry> classes)
    {
        var settlement = new SettlementId(settlementId);
        var blocks = new List<NeedsClassBlock>(classes.Count);
        foreach (ClassEntry cls in classes)
        {
            var classId = new ClassId(cls.Id);
            long pop = 0;
            for (int b = 0; b < world.Buckets.Count; b++)
                if (world.Buckets[b].Settlement == settlement && world.Buckets[b].Class == classId)
                    pop += world.Buckets[b].Count.Value;

            if (pop == 0)
            {
                blocks.Add(new NeedsClassBlock(cls.Name, Present: false, NeedLines: []));
                continue;
            }

            var lines = new List<string>(needs.Needs.Length);
            foreach (NeedEntry need in needs.Needs)
            {
                if (!need.Bound)
                {
                    lines.Add(string.Create(CultureInfo.InvariantCulture,
                        $"  {need.Name}: not yet simulated"));
                    continue;
                }
                double? value = null;
                for (int i = 0; i < world.NeedSatisfactions.Count; i++)
                {
                    NeedSatisfactionRow row = world.NeedSatisfactions[i];
                    if (row.Settlement == settlement && row.Class == classId && row.NeedId == need.Id)
                    { value = row.Value; break; }
                }
                lines.Add(value is { } v
                    ? string.Create(CultureInfo.InvariantCulture, $"  {need.Name}: {v:F2}")
                    : string.Create(CultureInfo.InvariantCulture, $"  {need.Name}: not yet measured"));
            }
            blocks.Add(new NeedsClassBlock(cls.Name, Present: true, NeedLines: lines));
        }
        return blocks;
    }
}
