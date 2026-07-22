using Sim.Core.State;
using Sim.Core.Systems.ClassMobility;

namespace Sim.Tests.Systems;

// T2.2 (D-020): the emergence-predicate DSL — comparisons and boolean ops over
// registered variables, nothing else. Parse-time rejection is the contract:
// unknown variables name the token AND list the registry; malformed
// expressions name the offending token (T0.4 actionable-error template).
public class PredicateTests
{
    private static Predicate.VariableReader Reader(double surplus, double share) =>
        varId => varId == Variables.FoodSurplusRatio ? surplus
            : varId == Variables.ArtisanShare ? share
            : throw new InvalidOperationException($"unregistered id {varId}");

    [Theory]
    [InlineData("food_surplus_ratio > 1.3", 1.4, 0.0, true)]
    [InlineData("food_surplus_ratio > 1.3", 1.3, 0.0, false)]  // strict
    [InlineData("food_surplus_ratio >= 1.3", 1.3, 0.0, true)]
    [InlineData("food_surplus_ratio < 1.1", 1.0, 0.0, true)]
    [InlineData("food_surplus_ratio <= 1.0", 1.0, 0.0, true)]
    [InlineData("artisan_share == 0", 9.9, 0.0, true)]
    [InlineData("2 < food_surplus_ratio", 3.0, 0.0, true)]     // literal on the left
    [InlineData("food_surplus_ratio > artisan_share", 0.5, 0.4, true)] // var vs var
    public void Comparisons_EvaluateExactly(string src, double surplus, double share, bool expected)
    {
        Assert.Equal(expected, Predicate.Parse(src).Evaluate(Reader(surplus, share)));
    }

    [Fact]
    public void Precedence_NotOverAndOverOr_Pinned()
    {
        // a || b && c parses as a || (b && c): with a=false, b=true, c=false →
        // false. A left-to-right mis-parse ((a || b) && c) also gives false, so
        // pin the discriminating case too: a=true, b=false, c=false → TRUE
        // under correct precedence, false under the mis-parse.
        var p = Predicate.Parse(
            "food_surplus_ratio > 1 || food_surplus_ratio > 2 && artisan_share > 0.5");
        Assert.True(p.Evaluate(Reader(1.5, 0.0)));   // a true, (b && c) false → true
        Assert.False(p.Evaluate(Reader(0.5, 0.9))); // a false, b false → false

        // ! binds tighter than &&: !a && b with a=false, b=true → true.
        var q = Predicate.Parse("!(food_surplus_ratio > 1) && artisan_share > 0.5");
        Assert.True(q.Evaluate(Reader(0.5, 0.9)));
        Assert.False(q.Evaluate(Reader(1.5, 0.9)));

        // Parens override: (a || b) && c.
        var r = Predicate.Parse(
            "(food_surplus_ratio > 1 || food_surplus_ratio > 2) && artisan_share > 0.5");
        Assert.False(r.Evaluate(Reader(1.5, 0.0)));
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("food_surplus_ratio >", "ends where")]
    [InlineData("food_surplus_ratio 1.3", "comparison operator")]
    [InlineData("&& food_surplus_ratio > 1", "unexpected token")]
    [InlineData("(food_surplus_ratio > 1", "never closed")]
    [InlineData("food_surplus_ratio = 1", "equality is '=='")]
    [InlineData("food_surplus_ratio > 1 | artisan_share > 0", "single '|'")]
    [InlineData("food_surplus_ratio > 1.2.3", "not a valid number")]
    [InlineData("food_surplus_ratio > 1 extra > 2", "unexpected token 'extra'")]
    [InlineData("food_surplus_ratio + 1 > 2", "unexpected character '+'")]
    public void Malformed_RejectsNamingTheOffense(string src, string expectedFragment)
    {
        var e = Assert.Throws<PredicateFormatException>(() => Predicate.Parse(src));
        Assert.Contains(expectedFragment, e.Message);
    }

    [Fact]
    public void UnknownVariable_NamesItAndListsTheRegistry()
    {
        var e = Assert.Throws<PredicateFormatException>(() => Predicate.Parse("granary_level > 3"));
        Assert.Contains("unknown variable 'granary_level'", e.Message);
        Assert.Contains("food_surplus_ratio, artisan_share", e.Message); // the full known list
    }

    [Fact]
    public void ConfigLoad_BadClassPredicate_RejectsThroughSimConfig()
    {
        // The D-020 load-time gate end-to-end: a bad predicate in the class
        // registry fails SimConfigLoader.Load with the class named and the
        // parser's actionable message intact.
        string json = TestUtil.TestConfigs.SimJson().Replace(
            "food_surplus_ratio > 1.3", "grain_pile > 1.3");
        var e = Assert.Throws<Sim.Core.Systems.SimConfigException>(
            () => Sim.Core.Systems.SimConfigLoader.Load(json));
        Assert.Contains("Artisans", e.Message);
        Assert.Contains("unknown variable 'grain_pile'", e.Message);
        Assert.Contains("food_surplus_ratio, artisan_share", e.Message);
    }
}
