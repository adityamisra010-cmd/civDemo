using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Proto.M7.War;

/// <summary>Commodity of a conserved account. A transfer moves one commodity between two accounts of that commodity.</summary>
public enum Commodity : byte
{
    Money = 0,
    Goods = 1,
    // ---- Phase C lane E (Proto.M7/WarEcon): further conserved commodities of the per-pulse war economy. ----
    /// <summary>Persons (soldiers, civilians, the dead): a person moves between accounts, never appears or vanishes.</summary>
    People = 2,
    /// <summary>Grain in milli-units of the shipped grain unit (D-015: one unit is one adult's year of food).</summary>
    Grain = 3,
    /// <summary>Riding horses and draft animals (head).</summary>
    Animals = 4,
    /// <summary>Powder and shot (one unit is about one kilogram; gunpowder capability only).</summary>
    Munitions = 5,
    /// <summary>Equipment in milli-kits (one kit is one man's full set of arms, clothing and shoes).</summary>
    Kit = 6,
    /// <summary>Motor fuel in litres (motor transport capability only).</summary>
    Fuel = 7,
    /// <summary>Bunker coal in kilograms (steam-powered vessels only; lane E naval logistics).</summary>
    Coal = 8,
    // ---- Phase C lane S (siege and garrisons): appended after lane E's codes; 0-8 are unchanged. The siege uses lane E's
    // People, Grain (food, gm) and Munitions (gunpowder ammunition, kg); the codes below are what lane E does not model.
    /// <summary>Drinking water in person-days of the full need (cisterns, wells, the outside inflow).</summary>
    Water = 9,
    /// <summary>Missiles of non-gunpowder ranged arms (arrows, bolts, sling shot) in shooter-days of siege fire.</summary>
    Missiles = 10,
    /// <summary>Ordnance in pieces: siege machines (rams, towers, throwers, bombards, modern guns) and installed fortress guns.</summary>
    Ordnance = 11,
    // ---- end lane S
}

/// <summary>
/// Prototype stand-in for the kernel Ledger (Law 1: conserved stocks change ONLY through a transfer; stocks are long).
/// NOT the kernel Ledger: Proto.M7 never references Sim.*. Every conserved quantity lives in an account; the war's
/// consumption goes to explicit SINK accounts (upkeep paid, supply consumed) instead of leaving the system, so the
/// per-commodity total is constant and conservation is checked by exact equality (no tolerance).
/// A transfer is all-or-nothing: an invalid one (negative amount, overdraft, commodity mismatch, same account) throws
/// and changes nothing. Callers that may lack funds decide the amount first (WarTheaterWorld pays min(due, balance)).
/// </summary>
public sealed class WarLedger
{
    private readonly List<long> _balance = new();
    private readonly List<Commodity> _commodity = new();

    public int AccountCount => _balance.Count;
    public long TransferCount { get; private set; }

    public int Open(Commodity commodity, long openingBalance)
    {
        if (openingBalance < 0) throw new ArgumentOutOfRangeException(nameof(openingBalance));
        _balance.Add(openingBalance);
        _commodity.Add(commodity);
        return _balance.Count - 1;
    }

    public long Balance(int account) => _balance[account];
    public Commodity CommodityOf(int account) => _commodity[account];

    public void Transfer(int from, int to, long amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "negative transfer");
        if (from == to) throw new InvalidOperationException("transfer to the same account");
        if (_commodity[from] != _commodity[to]) throw new InvalidOperationException("commodity mismatch");
        if (_balance[from] < amount) throw new InvalidOperationException("overdraft");
        _balance[from] -= amount;
        _balance[to] += amount;
        TransferCount++;
    }

    /// <summary>Sum of every account of one commodity (Int128 so the check itself cannot overflow).</summary>
    public Int128 Total(Commodity c)
    {
        Int128 t = 0;
        for (int i = 0; i < _balance.Count; i++) if (_commodity[i] == c) t += _balance[i];
        return t;
    }

    public long[] Snapshot() => _balance.ToArray();

    public void AppendTo(IncrementalHash h)
    {
        var b = new byte[9];
        for (int i = 0; i < _balance.Count; i++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(b, _balance[i]);
            b[8] = (byte)_commodity[i];
            h.AppendData(b);
        }
    }
}
