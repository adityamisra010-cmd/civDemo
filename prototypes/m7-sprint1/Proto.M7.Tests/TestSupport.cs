using Proto.M7.Time;

namespace Proto.M7.Tests;

internal static class TestSupport
{
    /// <summary>The prototype's byte copy of Sim.Data/content/era-pacing.json (copied to the test output dir).</summary>
    public static string FixturePath => Path.Combine(AppContext.BaseDirectory, "fixtures", "era-pacing.json");

    public static CampaignCalendar ShippedCalendar() => CampaignCalendar.FromJsonFile(FixturePath);

    /// <summary>The repository's original file, when this prototype sits inside the repo (null when copied elsewhere).</summary>
    public static string? RepoEraPacingPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "Sim.Data", "content", "era-pacing.json");
            if (File.Exists(candidate) && Directory.Exists(Path.Combine(dir.FullName, "prototypes"))) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>Deterministic Fisher-Yates permutation driven by the counter RNG (no System.Random anywhere).</summary>
    public static int[] Permutation(int n, ulong seed)
    {
        var p = new int[n];
        for (int i = 0; i < n; i++) p[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = (int)Rng.CounterRng.NextBelow(seed, 99, i, 0, 0, 0, (ulong)(i + 1));
            (p[i], p[j]) = (p[j], p[i]);
        }
        return p;
    }

    public const long Day = SimInstant.MsPerDay;
}
