using System.Globalization;

namespace Proto.M7.Tactical;

/// <summary>[P] Who commands a side of a battle (resolver-default input).</summary>
public enum Commander : byte
{
    Ai = 0,
    Player = 1,
}

/// <summary>
/// [P] DEFAULT resolver selection (lane P brief; DIR5 §2.1, §2.13; INV-4 of the M7.2/M7.3 revision):
///  - a battle with a PLAYER participant defaults to the TacticalResolver (the AutoResolver is an optional shortcut, never
///    the default);
///  - an AI-vs-AI battle defaults to the AutoResolver (DIR5 §2.13 sandbox; d011:22 campaign) — but ONLY when every formation
///    is inside the AutoResolver's model (<see cref="AutoUnsupportedReason"/>): land formations without domain weapons and
///    not starting embarked. A battle with any vessel or aircraft (naval / air), any crew-served domain weapon or any
///    embarked formation defaults to the TacticalResolver, so NAVAL NEVER DEFAULTS TO AUTO (DIR5 §2.1). The M7.2/M7.3
///    revision's READING RDG-3 (campaign AI-vs-AI naval battles use the AutoResolver) cannot apply while no naval auto model
///    exists; the lane reports it as an open point, it does not decide it.
/// The result is a default only: <see cref="BattleResolution.Resolve"/> keeps the resolver an explicit parameter.
/// </summary>
public static class ResolverPolicy
{
    /// <summary>Null when the AutoResolver can resolve the setup, else why not (the first offending formation by id).</summary>
    public static string? AutoUnsupportedReason(BattleSetup setup)
    {
        var specs = (FormationSpec[])setup.Formations.Clone();
        Array.Sort(specs, static (a, b) => a.Id.CompareTo(b.Id));
        foreach (var f in specs)
        {
            // [P fix r1, P-V9] invariant-culture formatting (CLAUDE.md law 5); the strings are diagnostics, never logic input
            if (f.Domain != Domain.Land) return string.Create(CultureInfo.InvariantCulture, $"formation {f.Id}: {f.Domain} formations have no AutoResolver model");
            if (f.Weapons is { Length: > 0 }) return string.Create(CultureInfo.InvariantCulture, $"formation {f.Id}: domain weapons have no AutoResolver model");
            if (f.EmbarkedOn is not null) return string.Create(CultureInfo.InvariantCulture, $"formation {f.Id}: embarked formations have no AutoResolver model");
        }
        return null;
    }

    public static ResolverKind Default(BattleSetup setup, Commander side0, Commander side1)
    {
        if (side0 == Commander.Player || side1 == Commander.Player) return ResolverKind.Tactical;
        return AutoUnsupportedReason(setup) is null ? ResolverKind.Auto : ResolverKind.Tactical;
    }
}
