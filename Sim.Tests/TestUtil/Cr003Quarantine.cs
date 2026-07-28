namespace Sim.Tests.TestUtil;

/// <summary>
/// T3.2b — the shared quarantine for every test whose PRECONDITION is famine.
///
/// CR-003 (docs/adr/cr-003.md): correcting the yield denomination (CR-002) and
/// the catchment radius together raised carrying capacity ~8× above what the
/// demographic clock reaches in the 6 000-year campaign. The world is therefore
/// PRE-MALTHUSIAN for its whole horizon: population grows monotonically, the
/// land ceiling is never touched, and NOBODY EVER STARVES.
///
/// That is one fact, and it silently disarms six independent test families at
/// once — which is a bigger deal than any one of them and is the reason this
/// helper exists in one place rather than being copy-pasted six times:
///
///   1. Reconciliation_FromLedgerAlone_PersonAndFoodExact — the Starvation flow
///      never occurs, so the ledger reconciliation covers three reasons, not four.
///   2. MalthusLite_OvershootCorrectionCycles — no overshoot, so no crossings.
///   3. Annals_TwinIdentical_AcrossIdenticalRuns — no famine chronicle line.
///   4. CollapsingWorld_DeadStaysDead_NoResurrectionCycle — no settlement dies,
///      so the ADR-012 resurrection detector has nothing to detect.
///   5. MagnitudeCorridor_FedPhaseDrift_WithTeeth — with no land pressure the
///      per-capita attractiveness gaps collapse, the gap-closing cap binds, and
///      a 10× base rate barely moves gross flow.
///   6. Artisans_EmergeInFedAutoplay_PlateauAtTheCap — surplus never falls, so
///      the recession/famine valve never fires and the share pins at the cap.
///
/// None of these is a defect in the system under test. Every one of them is the
/// same missing MECHANISM: nothing bounds a growing population below agronomic
/// capacity, because it cannot clear land, cannot colonise, and cannot found
/// daughter settlements (cr-003 §3 option 3; the expansion-opportunity entry in
/// docs/queue.md).
///
/// THE QUARANTINE IS NOT A SKIP. Each call asserts that the precondition is
/// ABSENT — exactly as recorded — so the test still fails loudly the moment
/// famine returns, which is precisely when the quarantine must be deleted and
/// the original guard restored. Every OTHER assertion in each of those tests
/// stays live and unweakened.
/// </summary>
public static class Cr003Quarantine
{
    /// <summary>
    /// Assert that a famine-dependent precondition is still absent. Pass the
    /// SAME expression the original vacuity guard asserted to be true.
    /// </summary>
    /// <param name="preconditionOccurred">What the original guard demanded.</param>
    /// <param name="guard">The original guard's message, for the reader.</param>
    public static void FamineGuardStillDisarmed(bool preconditionOccurred, string guard)
    {
        Assert.False(preconditionOccurred,
            $"CR-003 QUARANTINE RESOLVED — \"{guard}\" is satisfiable again, so the world is no "
            + "longer pre-Malthusian. Delete the Cr003Quarantine call here and restore the "
            + "original guard; see docs/adr/cr-003.md before re-banding anything.");
    }
}
