using Sim.Core.State;
using Sim.Core.Systems;

namespace Sim.Ui.ViewModel;

/// <summary>
/// M5 resources view, Director playtest 2026-10-20 — THE GOODS THE PLAYER HAS UNLOCKED IN THIS GAME: UI-side history,
/// never sim state (the <see cref="OwnershipHistory"/> shape). A good is unlocked once the player's empire has held some of
/// it — in the founding world or after any step of this game — and stays unlocked for the rest of the game, so a good whose
/// stores run down to 0 keeps its row in the strip, its card and RESOURCES and reads 0. Holding a good is how the player
/// comes to use it (extraction is endowment-bound, a recipe needs its knowledge; <see cref="EmpireStores"/>), so no research
/// rule is restated here.
///
/// <para><b>Game-scoped.</b> Each <see cref="UiSession"/> owns one, filled by its founding capture and its one Step path, so
/// a resumed session's replay rebuilds it and a new game starts with an empty one. Nothing is written to any file.</para>
///
/// <para><b>Own empire only.</b> A capture reads the player's controlled settlements' stores
/// (<see cref="EmpireStockpile.Available"/> over <see cref="EmpireStores.OwnSettlements"/>) — never a rival's. Indexed in
/// goods.json order; no dictionary, no LINQ.</para>
/// </summary>
public sealed class ResourceCatalogue
{
    private readonly List<int> _unlocked = [];

    /// <summary>The unlocked goods' ids, ascending.</summary>
    public IReadOnlyList<int> Unlocked => _unlocked;

    /// <summary>Whether <paramref name="good"/> has been unlocked in this game.</summary>
    public bool IsUnlocked(int good) => _unlocked.BinarySearch(good) >= 0;

    /// <summary>Unlocks every good the player's empire holds in <paramref name="world"/>. Monotone: nothing is ever locked
    /// again.</summary>
    public void Capture(IReadOnlyWorldState world, SimConfig cfg, PolityId player)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cfg);
        if (cfg.Goods is not { } gc) return;
        int[] own = EmpireStores.OwnSettlements(world, player);
        var ids = new SettlementId[own.Length];
        for (int i = 0; i < own.Length; i++) ids[i] = new SettlementId(own[i]);
        foreach (GoodEntry g in gc.Goods)
            if (EmpireStockpile.Available(world.GoodStocks, ids, new GoodId(g.Id)) > 0) Unlock(g.Id);
    }

    private void Unlock(int good)
    {
        int at = _unlocked.BinarySearch(good);
        if (at < 0) _unlocked.Insert(~at, good);
    }

    /// <summary>A catalogue with <paramref name="goods"/> unlocked (tests).</summary>
    public static ResourceCatalogue Of(params int[] goods)
    {
        var c = new ResourceCatalogue();
        foreach (int g in goods) c.Unlock(g);
        return c;
    }
}
