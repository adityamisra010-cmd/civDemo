using Sim.Core.State;

namespace Sim.Core.Kernel;

/// <summary>Raised when an order log is inconsistent with the world it targets.</summary>
public sealed class OrderValidationException(string message) : Exception(message);

/// <summary>
/// World-dependent order validation (T1.6): payload RANGES are checked at
/// OrderLog.Load; target EXISTENCE needs the world and is checked here, before
/// turn 1 (the CLI calls it right after world construction). Rejection is
/// actionable and up-front — never a silent mid-turn skip.
/// </summary>
public static class OrderValidation
{
    public static void ValidateAgainstWorld(OrderLog orders, IReadOnlyWorldState world)
    {
        for (int i = 0; i < orders.Count; i++)
        {
            OrderRecord record = orders[i];
            if (record.Kind != OrderKind.LaborAllocation) continue;

            bool found = false;
            for (int s = 0; s < world.Settlements.Count; s++)
            {
                if (world.Settlements[s].Id.Value == record.TargetId) { found = true; break; }
            }
            if (!found)
                throw new OrderValidationException(
                    $"order[{i}] (turn {record.Turn}): LaborAllocation targets settlement " +
                    $"{record.TargetId}, which does not exist in this world " +
                    $"({world.Settlements.Count} settlement(s)). Toy worlds have none — " +
                    "labor orders need a founded world.");
        }
    }
}
