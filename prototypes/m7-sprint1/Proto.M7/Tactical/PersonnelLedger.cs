using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Proto.M7.Tactical;

/// <summary>Where a person is. Stable codes (hashed).</summary>
public enum PersonnelPlace : byte
{
    /// <summary>A land formation's people on the battlefield (Active or Routing entities).</summary>
    Field = 0,
    /// <summary>Land troops aboard one transport vessel (Carrier = the vessel entity id).</summary>
    Aboard = 1,
    /// <summary>The crew of one vessel or aircraft (Carrier = that entity id).</summary>
    Crew = 2,
    Killed = 3,
    /// <summary>Lost with a sinking vessel or a downed aircraft (dead; kept apart from Killed for inspection).</summary>
    LostWithVessel = 4,
    Captured = 5,
    /// <summary>Left the battle alive (orderly withdrawal, rout off the field, disengagement, shipwreck/bailout survivors).</summary>
    Escaped = 6,
}

/// <summary>Why people moved. Stable codes (hashed).</summary>
public enum PersonnelFlowReason : byte
{
    Embark = 1,
    Disembark = 2,
    KilledInAction = 3,
    LostWithVessel = 4,
    Captured = 5,
    Escaped = 6,
    ShipwreckSurvivor = 7,
}

public readonly record struct PersonnelFlow(long AtMs, int From, int To, long Amount, PersonnelFlowReason Reason);

public readonly record struct PersonnelLine(long FormationId, PersonnelPlace Place, long CarrierId, long Count);

/// <summary>End-of-battle personnel picture: every non-empty account and the ledger's own hash.</summary>
public sealed record PersonnelReport(long Total, long OpeningTotal, PersonnelLine[] Lines, long FlowCount, string Hash)
{
    /// <summary>People of one formation in one place (summed over carriers).</summary>
    public long Count(long formationId, PersonnelPlace place)
    {
        long n = 0;
        foreach (var l in Lines) if (l.FormationId == formationId && l.Place == place) n += l.Count;
        return n;
    }

    public long Count(PersonnelPlace place)
    {
        long n = 0;
        foreach (var l in Lines) if (l.Place == place) n += l.Count;
        return n;
    }
}

/// <summary>
/// The battle's PERSONNEL LEDGER (lane N; CLAUDE.md law 1: people change only through a transfer; stocks are long; exact
/// equality, no tolerance). Every person in the battle is in exactly one account (formation, place, carrier). Accounts are
/// opened at battle start with the people actually there (opening balances); afterwards balances change ONLY through
/// <see cref="Transfer"/>, which is all-or-nothing (negative amount, overdraft or self-transfer throws and changes nothing)
/// and is logged with its instant and reason. Hence the total is constant: <see cref="Total"/> == <see cref="OpeningTotal"/>
/// at every instant (tested at every step). A vessel that sinks with troops aboard moves them Aboard -&gt; LostWithVessel by a
/// transfer: they never vanish. Prototype stand-in for the kernel Ledger (Proto.M7 never references Sim.*); the campaign
/// integration maps Killed / LostWithVessel / Captured onto Ledger flows of real people (O-35, ADR-055) — not done here.
/// </summary>
public sealed class PersonnelLedger
{
    private readonly List<long> _bal = new();
    private readonly List<long> _form = new();
    private readonly List<PersonnelPlace> _place = new();
    private readonly List<long> _carrier = new();
    private readonly List<PersonnelFlow> _flows = new();
    private long _opening;

    public int AccountCount => _bal.Count;
    public long OpeningTotal => _opening;
    public IReadOnlyList<PersonnelFlow> Flows => _flows;

    public int Open(long formationId, PersonnelPlace place, long carrierId, long openingBalance)
    {
        if (openingBalance < 0) throw new ArgumentOutOfRangeException(nameof(openingBalance));
        _bal.Add(openingBalance);
        _form.Add(formationId);
        _place.Add(place);
        _carrier.Add(carrierId);
        _opening = checked(_opening + openingBalance);
        return _bal.Count - 1;
    }

    /// <summary>The account (formation, place, carrier); opened empty on first use (deterministic: first use is in event order).</summary>
    public int Account(long formationId, PersonnelPlace place, long carrierId)
    {
        for (int i = 0; i < _bal.Count; i++)
            if (_form[i] == formationId && _place[i] == place && _carrier[i] == carrierId) return i;
        return Open(formationId, place, carrierId, 0);
    }

    public long Balance(int account) => _bal[account];

    /// <summary>Who an account belongs to (inspection).</summary>
    public (long FormationId, PersonnelPlace Place, long CarrierId) AccountInfo(int account) => (_form[account], _place[account], _carrier[account]);

    public void Transfer(int from, int to, long amount, long atMs, PersonnelFlowReason reason)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "negative transfer");
        if (from == to) throw new InvalidOperationException("transfer to the same account");
        if (_bal[from] < amount) throw new InvalidOperationException($"personnel overdraft: account {from} holds {_bal[from]}, transfer {amount}");
        if (amount == 0) return;
        _bal[from] -= amount;
        _bal[to] += amount;
        _flows.Add(new PersonnelFlow(atMs, from, to, amount, reason));
    }

    /// <summary>Sum over every account (Int128: the check itself cannot overflow).</summary>
    public Int128 Total
    {
        get
        {
            Int128 t = 0;
            for (int i = 0; i < _bal.Count; i++) t += _bal[i];
            return t;
        }
    }

    public long Balance(long formationId, PersonnelPlace place)
    {
        long n = 0;
        for (int i = 0; i < _bal.Count; i++) if (_form[i] == formationId && _place[i] == place) n += _bal[i];
        return n;
    }

    public string HashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendTo(h);
        return Convert.ToHexString(h.GetHashAndReset());
    }

    public void AppendTo(IncrementalHash h)
    {
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(_bal.Count);
        for (int i = 0; i < _bal.Count; i++) { L(_form[i]); L((long)_place[i]); L(_carrier[i]); L(_bal[i]); }
        L(_flows.Count);
        foreach (var f in _flows) { L(f.AtMs); L(f.From); L(f.To); L(f.Amount); L((long)f.Reason); }
    }

    public PersonnelReport Report()
    {
        var lines = new List<PersonnelLine>();
        for (int i = 0; i < _bal.Count; i++)
            if (_bal[i] != 0) lines.Add(new PersonnelLine(_form[i], _place[i], _carrier[i], _bal[i]));
        return new PersonnelReport((long)Total, _opening, lines.ToArray(), _flows.Count, HashHex());
    }
}
