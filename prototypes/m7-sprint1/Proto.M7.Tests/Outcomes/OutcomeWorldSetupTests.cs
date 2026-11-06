using Proto.M7.Events;
using Proto.M7.War.Outcomes;
using static Proto.M7.Tests.Outcomes.OutcomeRig;
using static Proto.M7.Tests.TestSupport;

namespace Proto.M7.Tests.Outcomes;

/// <summary>Setup validation and input-order independence of the outcome world (tie-dense: ids, never list positions, decide).</summary>
public class OutcomeWorldSetupTests
{
    private static T[] Shuffled<T>(T[] a, ulong seed)
    {
        var p = Permutation(a.Length, seed);
        var r = new T[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = a[p[i]];
        return r;
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void InputOrder_DoesNotChangeTheWorld(ulong seed)
    {
        Action<EventScheduler, WarOutcomeWorld> script = (s, _) => { Capture(s, B(1), 0, 1); Capture(s, B(2), 1, 2); Capture(s, B(2), 3, 0); };
        var a = Build(script, ai: new[] { -1, (int)Disposition.Sack, (int)Disposition.Raze });
        var b = Build(script, ai: new[] { -1, (int)Disposition.Sack, (int)Disposition.Raze },
                      polities: p => Shuffled(p, seed), settlements: x => Shuffled(x, seed + 10), formations: Shuffled(Formations(), seed + 20));
        a.R.RunUntil(B(6));
        b.R.RunUntil(B(6));
        Assert.Equal(a.W.StateHashHex(), b.W.StateHashHex());
        Assert.NotEmpty(a.W.Fates);
    }

    [Fact]
    public void Setup_IsValidated()
    {
        Assert.Throws<ArgumentException>(() => Build(polities: p => { p[0] = p[0] with { LeaderId = -1 }; return p; }));
        Assert.Throws<ArgumentException>(() => Build(polities: p => { p[0] = p[0] with { Seat = 2 }; return p; }));          // not its own
        Assert.Throws<ArgumentException>(() => Build(settlements: x => { x[1] = x[1] with { Id = 0 }; return x; }));          // duplicate id
        Assert.Throws<ArgumentException>(() => Build(settlements: x => { x[1] = x[1] with { Installation = 0 }; return x; })); // bound without lane S
        Assert.Throws<ArgumentException>(() => Build(wars: new[] { new OutcomeWar(0, W0, new[] { 1 }, new[] { 1 }) }));       // both sides
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(ai: new[] { -1, 7, -1 }));
    }

    [Fact]
    public void TheCaptureDevice_RefusesAnInconsistentCapture()
    {
        var own = Build((s, _) => Capture(s, B(1), 0, 0));
        Assert.Throws<InvalidOperationException>(() => own.R.RunUntil(B(2)));                                                   // already its own
        var ruin = Build((s, _) => { Capture(s, B(1), 0, 1); Capture(s, B(2), 0, 2); }, ai: new[] { -1, (int)Disposition.Raze, -1 });
        Assert.Throws<InvalidOperationException>(() => ruin.R.RunUntil(B(3)));                                                  // a ruin cannot be taken
    }
}
