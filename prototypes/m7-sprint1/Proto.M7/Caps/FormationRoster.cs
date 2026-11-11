using System.Buffers.Binary;
using System.Security.Cryptography;
using Proto.M7.Tactical;
using Proto.M7.War;

namespace Proto.M7.Caps;

/// <summary>[D] One formation of the roster: owner, cap class, identity, people (the conserved stock).</summary>
public sealed record RosterFormation(long Id, int Owner, CapFamily Class, int IdentityKey, long Persons);

/// <summary>[D] Kinds of roster edit. Stable codes.</summary>
public enum RosterEditKind : byte
{
    Create = 1,
    Split = 2,
    Merge = 3,
    Reinforce = 4,
    Detach = 5,
    Transfer = 6,
    Disband = 7,
}

/// <summary>[D] One recorded roster edit, accepted or refused (a refused edit changes nothing): its log index, kind, owner, the
/// formations it names (A, B; -1 when unused), the people it moves and the verdict.</summary>
public readonly record struct RosterEdit(long Index, RosterEditKind Kind, int Owner, long A, long B, long Persons, CapVerdict Verdict);

/// <summary>
/// [D] A deterministic campaign-side formation roster with the DR24 cap check at CREATION and at every EDIT (lane D brief item 1).
///
/// PEOPLE ARE CONSERVED (Law 1). Every person is in exactly one account of a <see cref="WarLedger"/> (commodity People): the
/// owner's DEPOT (people available to form or reinforce formations: the stand-in for the recruitment pool) or one formation.
/// Every edit is all-or-nothing: it is checked completely first (ownership, class, people available, the cap of EVERY formation
/// it changes), and only an accepted edit moves people, by ledger transfers. The ledger total is constant (exact equality).
///
/// THE CAP CHECK (<see cref="DirectorCaps.Check"/>) runs on the formation's people after the edit, on the battle entities those
/// people fight as (<see cref="DirectorCaps.EntitiesOf"/>, the one rule of lanes W and D since D-082 DA5): machine-counted classes on
/// ceil(people / persons per machine); soldier-counted classes on min(people, cap), so a soldier formation is never refused for size
/// (its persons are limited by the economy, not by the cap; before D-082 it was refused one person above the cap). Uncapped classes
/// (lane N's CAP-5 treatment) are never refused for size. One formation is one identity: a merge or transfer across classes or
/// identities is refused (ClassMismatch).
///
/// ORDERING. Formations are kept in ascending id. <see cref="ReinforceToCaps"/> hands recruits one at a time to the formation
/// with the least fill fraction persons / max persons, compared EXACTLY by cross-multiplication (no division), ties to the
/// lowest id (the CLAUDE.md composite key (score, id); tie-dense tests). [CB, D-082 DA5] Only formations whose cap bounds their
/// persons take part (machine classes with a crew rule); soldier formations have no persons cap to fill.
/// [CB, verification round 1, CB-R1-01] That sentence is READING CB-4 (a), applied: this lane's choice, NOT forced by DA5. DA5 makes
/// the cap count entities; it does not say how ReinforceToCaps treats soldier formations. Alternative (b): fill a soldier formation
/// up to its full complement of entities at one person each (<see cref="DirectorCaps.FullStrengthPersons"/>, the cap), persons above
/// it by <see cref="Reinforce"/>; (b) keeps lane D's automatic reinforcement of understrength soldier formations and its three
/// ReinforceToCaps pins, which (a) moved (docs/m7-plan/battle-entity-proposal.md §5, READING CB-4).
/// </summary>
public sealed class FormationRoster
{
    private readonly DirectorCaps _caps;
    private readonly WarLedger _ledger = new();
    private readonly int[] _depot;
    private readonly List<RosterFormation> _forms = new();     // ascending id
    private readonly List<int> _accounts = new();              // parallel to _forms
    private readonly List<RosterEdit> _log = new();
    private readonly Int128 _opening;

    public FormationRoster(DirectorCaps caps, long[] depotOpening)
    {
        _caps = caps;
        _depot = new int[depotOpening.Length];
        Int128 t = 0;
        for (int i = 0; i < depotOpening.Length; i++)
        {
            if (depotOpening[i] < 0) throw new ArgumentOutOfRangeException(nameof(depotOpening));
            _depot[i] = _ledger.Open(Commodity.People, depotOpening[i]);
            t += depotOpening[i];
        }
        _opening = t;
    }

    public DirectorCaps Caps => _caps;
    public IReadOnlyList<RosterFormation> Formations => _forms;
    public IReadOnlyList<RosterEdit> Log => _log;
    public long DepotOf(int owner) => _ledger.Balance(_depot[owner]);
    public Int128 TotalPersons => _ledger.Total(Commodity.People);
    public Int128 OpeningPersons => _opening;

    public RosterFormation? Find(long id)
    {
        int i = IndexOf(id);
        return i >= 0 ? _forms[i] : null;
    }

    private int IndexOf(long id)
    {
        int lo = 0, hi = _forms.Count - 1;
        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            long m = _forms[mid].Id;
            if (m == id) return mid;
            if (m < id) lo = mid + 1; else hi = mid - 1;
        }
        return -1;
    }

    private bool ValidOwner(int owner) => owner >= 0 && owner < _depot.Length;

    private CapVerdict Record(RosterEditKind kind, int owner, long a, long b, long persons, CapVerdict v)
    {
        _log.Add(new RosterEdit(_log.Count, kind, owner, a, b, persons, v));
        return v;
    }

    private CapVerdict Own(int owner, long id, out int index)
    {
        index = IndexOf(id);
        if (index < 0) return CapVerdict.Refused(CapRefusal.UnknownFormation, id);
        if (_forms[index].Owner != owner) return CapVerdict.Refused(CapRefusal.NotOwner, id);
        return new CapVerdict(CapRefusal.None, id, 0, 0);
    }

    private void Insert(RosterFormation f, int account)
    {
        int at = 0;
        while (at < _forms.Count && _forms[at].Id < f.Id) at++;
        _forms.Insert(at, f);
        _accounts.Insert(at, account);
    }

    private void SetPersons(int index) => _forms[index] = _forms[index] with { Persons = _ledger.Balance(_accounts[index]) };

    // ------------------------------------------------------------------ creation and edits (each checked completely first)

    /// <summary>Creates formation <paramref name="id"/> of <paramref name="persons"/> people drawn from the owner's depot.</summary>
    public CapVerdict Create(int owner, long id, CapFamily cls, int identityKey, long persons)
    {
        CapVerdict v;
        if (!ValidOwner(owner)) v = CapVerdict.Refused(CapRefusal.NotOwner, id);
        else if (IndexOf(id) >= 0) v = CapVerdict.Refused(CapRefusal.DuplicateId, id);
        else
        {
            v = _caps.Check(id, cls, persons);
            if (v.Accepted && _ledger.Balance(_depot[owner]) < persons) v = CapVerdict.Refused(CapRefusal.DepotShort, id);
        }
        if (v.Accepted)
        {
            int acc = _ledger.Open(Commodity.People, 0);
            _ledger.Transfer(_depot[owner], acc, persons);
            Insert(new RosterFormation(id, owner, cls, identityKey, persons), acc);
        }
        return Record(RosterEditKind.Create, owner, id, -1, persons, v);
    }

    /// <summary>Splits <paramref name="persons"/> people off <paramref name="source"/> into a NEW formation <paramref name="newId"/>
    /// of the same class and identity. Both parts must keep at least one person; both are cap-checked.</summary>
    public CapVerdict Split(int owner, long source, long newId, long persons)
    {
        CapVerdict v = Own(owner, source, out int si);
        if (v.Accepted && IndexOf(newId) >= 0) v = CapVerdict.Refused(CapRefusal.DuplicateId, newId);
        if (v.Accepted && persons <= 0) v = CapVerdict.Refused(CapRefusal.NonPositive, newId);
        if (v.Accepted && _forms[si].Persons < persons) v = CapVerdict.Refused(CapRefusal.InsufficientPersons, source);
        if (v.Accepted && _forms[si].Persons == persons) v = CapVerdict.Refused(CapRefusal.NonPositive, source);
        if (v.Accepted)
        {
            var src = _forms[si];
            // Ascending id: the lower-id part is reported first (a pure function of the edit).
            var a = _caps.Check(Math.Min(source, newId), src.Class, source < newId ? src.Persons - persons : persons);
            var b = _caps.Check(Math.Max(source, newId), src.Class, source < newId ? persons : src.Persons - persons);
            v = !a.Accepted ? a : b;
            if (v.Accepted)
            {
                int acc = _ledger.Open(Commodity.People, 0);
                _ledger.Transfer(_accounts[si], acc, persons);
                SetPersons(si);
                Insert(new RosterFormation(newId, owner, src.Class, src.IdentityKey, persons), acc);
            }
        }
        return Record(RosterEditKind.Split, owner, source, newId, persons, v);
    }

    /// <summary>Merges <paramref name="absorbed"/> into <paramref name="keep"/> (same class and identity); the absorbed formation
    /// ceases to exist. Refused if the merged formation would exceed the cap.</summary>
    public CapVerdict Merge(int owner, long keep, long absorbed)
    {
        CapVerdict v = keep == absorbed ? CapVerdict.Refused(CapRefusal.SameFormation, keep) : Own(owner, keep, out _);
        if (v.Accepted) v = Own(owner, absorbed, out _);
        if (v.Accepted)
        {
            int ki = IndexOf(keep), ai = IndexOf(absorbed);
            var k = _forms[ki]; var x = _forms[ai];
            if (k.Class != x.Class || k.IdentityKey != x.IdentityKey) v = CapVerdict.Refused(CapRefusal.ClassMismatch, absorbed);
            else
            {
                v = _caps.Check(keep, k.Class, checked(k.Persons + x.Persons));
                if (v.Accepted)
                {
                    _ledger.Transfer(_accounts[ai], _accounts[ki], x.Persons);
                    SetPersons(ki);
                    _forms.RemoveAt(ai);
                    _accounts.RemoveAt(ai);
                }
            }
        }
        return Record(RosterEditKind.Merge, owner, keep, absorbed, 0, v);
    }

    /// <summary>Adds <paramref name="persons"/> people from the owner's depot to formation <paramref name="id"/>.</summary>
    public CapVerdict Reinforce(int owner, long id, long persons)
    {
        CapVerdict v = Own(owner, id, out int i);
        if (v.Accepted && persons <= 0) v = CapVerdict.Refused(CapRefusal.NonPositive, id);
        if (v.Accepted) v = _caps.Check(id, _forms[i].Class, checked(_forms[i].Persons + persons));
        if (v.Accepted && _ledger.Balance(_depot[owner]) < persons) v = CapVerdict.Refused(CapRefusal.DepotShort, id);
        if (v.Accepted)
        {
            _ledger.Transfer(_depot[owner], _accounts[i], persons);
            SetPersons(i);
        }
        return Record(RosterEditKind.Reinforce, owner, id, -1, persons, v);
    }

    /// <summary>Returns <paramref name="persons"/> people of formation <paramref name="id"/> to the depot (at least one stays).</summary>
    public CapVerdict Detach(int owner, long id, long persons)
    {
        CapVerdict v = Own(owner, id, out int i);
        if (v.Accepted && persons <= 0) v = CapVerdict.Refused(CapRefusal.NonPositive, id);
        if (v.Accepted && _forms[i].Persons < persons) v = CapVerdict.Refused(CapRefusal.InsufficientPersons, id);
        if (v.Accepted && _forms[i].Persons == persons) v = CapVerdict.Refused(CapRefusal.NonPositive, id);
        if (v.Accepted) v = _caps.Check(id, _forms[i].Class, _forms[i].Persons - persons);
        if (v.Accepted)
        {
            _ledger.Transfer(_accounts[i], _depot[owner], persons);
            SetPersons(i);
        }
        return Record(RosterEditKind.Detach, owner, id, -1, persons, v);
    }

    /// <summary>Moves <paramref name="persons"/> people between two formations of the same class and identity (the source keeps
    /// at least one); the receiving formation is cap-checked.</summary>
    public CapVerdict Transfer(int owner, long from, long to, long persons)
    {
        CapVerdict v = from == to ? CapVerdict.Refused(CapRefusal.SameFormation, from) : Own(owner, from, out _);
        if (v.Accepted) v = Own(owner, to, out _);
        if (v.Accepted && persons <= 0) v = CapVerdict.Refused(CapRefusal.NonPositive, to);
        if (v.Accepted)
        {
            int fi = IndexOf(from), ti = IndexOf(to);
            var f = _forms[fi]; var t = _forms[ti];
            if (f.Class != t.Class || f.IdentityKey != t.IdentityKey) v = CapVerdict.Refused(CapRefusal.ClassMismatch, to);
            else if (f.Persons < persons) v = CapVerdict.Refused(CapRefusal.InsufficientPersons, from);
            else if (f.Persons == persons) v = CapVerdict.Refused(CapRefusal.NonPositive, from);
            else v = _caps.Check(to, t.Class, checked(t.Persons + persons));
            if (v.Accepted)
            {
                _ledger.Transfer(_accounts[fi], _accounts[ti], persons);
                SetPersons(fi);
                SetPersons(ti);
            }
        }
        return Record(RosterEditKind.Transfer, owner, from, to, persons, v);
    }

    /// <summary>Disbands formation <paramref name="id"/>: all its people return to the owner's depot.</summary>
    public CapVerdict Disband(int owner, long id)
    {
        CapVerdict v = Own(owner, id, out int i);
        long persons = 0;
        if (v.Accepted)
        {
            persons = _forms[i].Persons;
            _ledger.Transfer(_accounts[i], _depot[owner], persons);
            _forms.RemoveAt(i);
            _accounts.RemoveAt(i);
        }
        return Record(RosterEditKind.Disband, owner, id, -1, persons, v);
    }

    // ------------------------------------------------------------------ deterministic distribution (tie-dense)

    /// <summary>
    /// Hands up to <paramref name="recruits"/> people of the owner's depot to the owner's CAPPED formations below their cap, one
    /// person at a time, each to the formation with the least fill fraction persons / max persons, ties to the lowest id
    /// (composite key; exact: a/b &lt; c/d iff a·d &lt; c·b in Int128). Stops when no capped formation has room or the depot is
    /// empty. Uncapped formations and machine classes without a crew rule take no part. Returns, per formation of the owner in
    /// ascending id, how many it received. Every person moves by a ledger transfer; each grant is logged as a Reinforce edit.
    /// [CB, D-082 DA5] "Capped" means a formation whose cap bounds its PERSONS: a machine class with a crew rule (cap × persons per
    /// machine). Soldier formations take no part: their cap bounds the entities they fight as, not their persons, so they have no
    /// room to fill (they are reinforced by <see cref="Reinforce"/>, by any number). Before D-082 they took part up to cap persons.
    /// [CB, verification round 1, CB-R1-01] This is READING CB-4 (a), applied, not a consequence of DA5: under alternative (b) a
    /// soldier formation below its cap in persons is below its full complement of entities and takes part up to
    /// <see cref="DirectorCaps.FullStrengthPersons"/> (MEASURED in a scratchpad probe: lane D's three ReinforceToCaps tests of 0d2db74
    /// pass under (b); docs/m7-plan/battle-entity-proposal.md §5).
    /// </summary>
    public long[] ReinforceToCaps(int owner, long recruits)
    {
        if (!ValidOwner(owner)) throw new ArgumentOutOfRangeException(nameof(owner));
        if (recruits < 0) throw new ArgumentOutOfRangeException(nameof(recruits));
        var idx = new List<int>();
        for (int i = 0; i < _forms.Count; i++)
        {
            var f = _forms[i];
            if (f.Owner == owner && DirectorCaps.IsMachineCounted(f.Class) && _caps.MaxPersons(f.Class) > 0) idx.Add(i);
        }
        var pers = new long[idx.Count];
        var max = new long[idx.Count];
        for (int k = 0; k < idx.Count; k++) { pers[k] = _forms[idx[k]].Persons; max[k] = _caps.MaxPersons(_forms[idx[k]].Class); }
        var got = new long[idx.Count];
        long left = Math.Min(recruits, _ledger.Balance(_depot[owner]));
        while (left > 0)
        {
            int best = -1;
            for (int k = 0; k < idx.Count; k++)
            {
                if (pers[k] >= max[k]) continue;
                if (best < 0 || Less(pers[k], max[k], _forms[idx[k]].Id, pers[best], max[best], _forms[idx[best]].Id)) best = k;
            }
            if (best < 0) break;
            pers[best]++;
            got[best]++;
            left--;
        }
        for (int k = 0; k < idx.Count; k++)
            if (got[k] > 0) Reinforce(owner, _forms[idx[k]].Id, got[k]);
        var result = new List<long>();
        for (int i = 0, k = 0; i < _forms.Count; i++)
        {
            if (_forms[i].Owner != owner) continue;
            result.Add(k < idx.Count && idx[k] == i ? got[k++] : 0);
        }
        return result.ToArray();
    }

    /// <summary>Composite key (fill fraction, id): true when a/aMax &lt; b/bMax, or equal and aId &lt; bId. Exact, no division.</summary>
    public static bool Less(long a, long aMax, long aId, long b, long bMax, long bId)
    {
        Int128 l = (Int128)a * bMax, r = (Int128)b * aMax;
        return l < r || (l == r && aId < bId);
    }

    /// <summary>
    /// The sizes of the fewest formations that hold <paramref name="persons"/> people at no more than <paramref name="maxPersons"/>
    /// each, as equal as possible: k = ceil(persons / maxPersons) parts; each gets floor(persons / k) and the first persons mod k
    /// get one more (exact; the parts sum to <paramref name="persons"/>). Used by sandbox force creation (READING S1, both
    /// alternatives in docs/m7-plan/war-sandbox-proposal.md §4).
    /// [CB, D-082 DA5] No longer used by the sandbox: under the one rule a soldier or uncapped force is ONE formation (its cap bounds
    /// its entities, not its persons). Kept as the plan of a machine class, whose people crew one machine each, so a force above
    /// cap × persons per machine still needs several formations (SplitSizes(persons, cap × persons per machine)).
    /// </summary>
    public static long[] SplitSizes(long persons, long maxPersons)
    {
        if (persons <= 0 || maxPersons <= 0) throw new ArgumentOutOfRangeException(nameof(persons));
        long k = persons / maxPersons + (persons % maxPersons == 0 ? 0 : 1);
        if (k > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(persons), "too many formations");
        var s = new long[k];
        long q = persons / k, r = persons % k;
        for (long i = 0; i < k; i++) s[i] = q + (i < r ? 1 : 0);
        return s;
    }

    /// <summary>Canonical hash of the roster: formations (ascending id) and the depots, then the edit log.</summary>
    public string HashHex()
    {
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var b = new byte[8];
        void L(long v) { BinaryPrimitives.WriteInt64LittleEndian(b, v); h.AppendData(b); }
        L(_depot.Length);
        foreach (int d in _depot) L(_ledger.Balance(d));
        L(_forms.Count);
        foreach (var f in _forms) { L(f.Id); L(f.Owner); L((long)f.Class); L(f.IdentityKey); L(f.Persons); }
        L(_log.Count);
        foreach (var e in _log) { L(e.Index); L((long)e.Kind); L(e.Owner); L(e.A); L(e.B); L(e.Persons); L((long)e.Verdict.Refusal); L(e.Verdict.FormationId); L(e.Verdict.Counted); }
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
