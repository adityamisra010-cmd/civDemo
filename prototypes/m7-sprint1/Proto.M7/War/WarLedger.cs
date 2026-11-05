using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Proto.M7.War;

/// <summary>Commodity of a conserved account. A transfer moves one commodity between two accounts of that commodity.</summary>
public enum Commodity : byte
{
    Money = 0,
    Goods = 1,
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
