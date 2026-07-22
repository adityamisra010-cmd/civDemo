namespace Sim.Core.State;

/// <summary>
/// Derived child/adult/elder band views over the cohort Buckets table (T2.1,
/// D-026): the 16-cohort model is the truth; bands are SUMS over cohort ranges
/// (0–2 / 3–11 / 12–15 ⇒ ages 0–14 / 15–59 / 60+, matching the retired M1
/// bands exactly) computed on demand for UI and labor consumers. Sums span
/// every culture/religion/class of the settlement. Pure reads — no caching,
/// no state; checked arithmetic (an overflowing view is itself a failure).
/// </summary>
public static class BandViews
{
    public static bool IsChild(int cohortIdx) => cohortIdx < Cohorts.FirstAdult;
    public static bool IsAdult(int cohortIdx) =>
        cohortIdx >= Cohorts.FirstAdult && cohortIdx < Cohorts.FirstElder;
    public static bool IsElder(int cohortIdx) => cohortIdx >= Cohorts.FirstElder;

    public static long Children(IReadOnlyTable<BucketRow> buckets, SettlementId settlement) =>
        Sum(buckets, settlement, Cohorts.FirstAdult, min: 0);

    public static long Adults(IReadOnlyTable<BucketRow> buckets, SettlementId settlement) =>
        Sum(buckets, settlement, Cohorts.FirstElder, min: Cohorts.FirstAdult);

    public static long Elders(IReadOnlyTable<BucketRow> buckets, SettlementId settlement) =>
        Sum(buckets, settlement, Cohorts.Count, min: Cohorts.FirstElder);

    private static long Sum(
        IReadOnlyTable<BucketRow> buckets, SettlementId settlement, int maxExclusive, int min)
    {
        long total = 0;
        checked
        {
            for (int i = 0; i < buckets.Count; i++)
            {
                BucketRow row = buckets[i];
                if (row.Settlement == settlement && row.CohortIdx >= min && row.CohortIdx < maxExclusive)
                    total += row.Count.Value;
            }
        }
        return total;
    }
}
