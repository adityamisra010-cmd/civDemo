namespace Proto.M7.Events;

/// <summary>
/// The set of event KINDS that interrupt a campaign turn (spec §2.4). It is DATA, not a hard-coded list:
/// which events are "significant" is an OPEN Director question (recon-critic G1; only the Taj Mahal example
/// is given). Stored as a sorted, de-duplicated int array; membership by binary search.
/// </summary>
public sealed class SignificantKinds
{
    private readonly int[] _kinds;

    public SignificantKinds(params int[] kinds)
    {
        var k = (int[])kinds.Clone();
        Array.Sort(k);
        int n = 0;
        for (int i = 0; i < k.Length; i++)
            if (n == 0 || k[n - 1] != k[i]) k[n++] = k[i];
        Array.Resize(ref k, n);
        _kinds = k;
    }

    public static readonly SignificantKinds None = new();

    public bool Contains(int kind) => Array.BinarySearch(_kinds, kind) >= 0;
    public IReadOnlyList<int> Kinds => _kinds;
}
