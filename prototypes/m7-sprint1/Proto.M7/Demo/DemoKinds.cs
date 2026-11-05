namespace Proto.M7.Demo;

/// <summary>Event kinds used by the synthetic demonstration worlds (spec §2.4/§2.5). Synthetic: NO wonders exist in the repo (recon-critic A12).</summary>
public static class DemoKinds
{
    public const int ConstructionTurnStart = 1;
    public const int ConstructionComplete = 2;
    public const int Fire = 10;
    public const int Damage = 11;
    public const int ResolveDeaths = 12;
    public const int PulseEnd = 20;
    public const int PulseStart = 21;
    public const int PulseTick = 22;

    /// <summary>Order kind: add B units/turn to process A from the order instant on.</summary>
    public const int OrderRush = 100;
    /// <summary>Order kind: start a new process (id A, B units/turn, remaining from the world's template) at the order instant.</summary>
    public const int OrderStartProcess = 101;
}
