using Sim.Core.State;
using Sim.Core.Systems.ClassMobility;

namespace Sim.Tests.Systems;

/// <summary>
/// ADR-029 §11 — the research dialect of the ONE D-020 predicate language (D-044 R10:
/// no second condition language). The legacy entry point must behave as it did —
/// same results, same failures, same messages (its code path gained introspection
/// bookkeeping, so it is not byte-for-byte the old code); the dialect adds AND/OR/NOT
/// keywords, boolean atoms and caller-bound quantities, and nothing else — still no
/// functions and no arithmetic.
/// </summary>
public class PredicateResearchDialectTests
{
    // Atoms a=0, b=1, c=2, d=3; quantity stock_timber=0.
    private static readonly string[] AtomNames = ["a", "b", "c", "d"];
    private static readonly PredicateSymbols Symbols = new(
        name => Array.IndexOf(AtomNames, name), name => name == "stock_timber" ? 0 : -1, "an atom a..d or stock_timber");

    private static bool Eval(string src, bool[] atoms, double timber = 0.0, double surplus = 0.0) =>
        Predicate.Parse(src, Symbols).Evaluate(v => v == Variables.FoodSurplusRatio ? surplus : 0.0, a => atoms[a], q => timber);

    [Fact]
    public void LegacyParse_IsUnchanged_KeywordsAndBareNamesStillFail()
    {
        // The shipped predicates' path: AND is just an unknown variable name, and a bare
        // name is still not an expression — exactly the pre-dialect messages.
        Assert.Contains("unknown variable 'cordage'",
            Assert.Throws<PredicateFormatException>(() => Predicate.Parse("cordage AND flax")).Message);
        Assert.Contains("expected a comparison operator",
            Assert.Throws<PredicateFormatException>(() => Predicate.Parse("food_surplus_ratio && artisan_share > 1")).Message);
        Predicate legacy = Predicate.Parse("food_surplus_ratio > 1.3 && population > 520");
        Assert.Empty(legacy.AtomIds);
        Assert.Empty(legacy.QuantityIds);
        Assert.True(legacy.ReadsVariables);
    }

    [Theory]
    [InlineData("a AND b", new[] { true, true, false, false }, true)]
    [InlineData("a AND b", new[] { true, false, false, false }, false)]
    [InlineData("a OR b", new[] { false, true, false, false }, true)]
    [InlineData("a OR b", new[] { false, false, true, true }, false)]
    [InlineData("(a AND c) OR (b AND d)", new[] { true, false, true, false }, true)]
    [InlineData("(a AND c) OR (b AND d)", new[] { true, false, false, true }, false)]
    [InlineData("(a AND c) OR (b AND d)", new[] { false, true, false, true }, true)]
    [InlineData("a AND (b OR (c AND d))", new[] { true, false, true, true }, true)]
    [InlineData("a AND (b OR (c AND d))", new[] { true, false, true, false }, false)]
    [InlineData("NOT a", new[] { false, false, false, false }, true)]
    [InlineData("a && b || c", new[] { false, false, true, false }, true)] // the symbolic operators still work
    public void Dialect_AndOrNested_EvaluateOverAtoms(string src, bool[] atoms, bool expected)
    {
        Assert.Equal(expected, Eval(src, atoms));
    }

    [Fact]
    public void Dialect_Precedence_AndBindsTighterThanOr_AsInTheLegacyGrammar()
    {
        // a OR b AND c == a OR (b AND c): a=true, b=false, c=false -> true (a mis-parse gives false).
        Assert.True(Eval("a OR b AND c", [true, false, false, false]));
        Assert.False(Eval("(a OR b) AND c", [true, false, false, false]));
    }

    [Fact]
    public void Dialect_QuantitiesAndVariablesCompare_AtomsDoNot()
    {
        Assert.True(Eval("stock_timber > 0", [false, false, false, false], timber: 3));
        Assert.False(Eval("stock_timber > 0", [false, false, false, false], timber: 0));
        Assert.True(Eval("stock_timber > 0 OR a", [true, false, false, false]));
        Assert.True(Eval("a AND food_surplus_ratio >= 1.5", [true, false, false, false], surplus: 1.5));
        Predicate p = Predicate.Parse("c AND stock_timber > 0 OR a AND c", Symbols);
        Assert.Equal([2, 0], p.AtomIds);   // distinct, first-appearance order
        Assert.Equal([0], p.QuantityIds);
        Assert.False(p.ReadsVariables);
        Assert.False(p.UsesNot);
        Assert.True(Predicate.Parse("NOT (a OR b)", Symbols).UsesNot);
    }

    [Theory]
    [InlineData("a AND ghost", "unknown name 'ghost'")]
    [InlineData("stock_gold > 0", "unknown operand 'stock_gold'")]
    [InlineData("a AND", "ends where")]
    [InlineData("(a OR b", "never closed")]
    [InlineData("a b", "unexpected token")]
    public void Dialect_MalformedInput_FailsNamingTheToken(string src, string fragment)
    {
        Assert.Contains(fragment, Assert.Throws<PredicateFormatException>(() => Predicate.Parse(src, Symbols)).Message);
    }

    [Theory]
    // CanHold: can the expression be true for SOME truth values of its comparisons, given the atoms?
    [InlineData("a", new[] { false, false, false, false }, false)]
    [InlineData("a", new[] { true, false, false, false }, true)]
    [InlineData("a AND b", new[] { true, false, false, false }, false)]
    [InlineData("a OR b", new[] { false, true, false, false }, true)]
    [InlineData("(a AND c) OR (b AND d)", new[] { false, true, false, true }, true)]
    [InlineData("(a AND c) OR (b AND d)", new[] { true, true, false, false }, false)]
    [InlineData("stock_timber > 0", new[] { false, false, false, false }, true)]              // a comparison may hold
    [InlineData("a AND stock_timber > 0", new[] { false, false, false, false }, false)]
    [InlineData("a OR food_surplus_ratio > 1", new[] { false, false, false, false }, true)]
    [InlineData("NOT a", new[] { true, false, false, false }, true)]                          // NOT: undecided, never "cannot"
    [InlineData("NOT (a OR b) AND c", new[] { false, false, false, false }, true)]
    public void Dialect_CanHold_IsOptimisticOverComparisons_AndUndecidedWithNot(string src, bool[] atoms, bool expected) =>
        Assert.Equal(expected, Predicate.Parse(src, Symbols).CanHold(a => atoms[a]));

    [Fact]
    public void Dialect_EvaluatingWithoutTheNeededReader_Throws()
    {
        Predicate p = Predicate.Parse("a", Symbols);
        Assert.Throws<InvalidOperationException>(() => p.Evaluate(null, null, null));
        Predicate q = Predicate.Parse("stock_timber > 0", Symbols);
        Assert.Throws<InvalidOperationException>(() => q.Evaluate(null, a => true, null));
    }
}
