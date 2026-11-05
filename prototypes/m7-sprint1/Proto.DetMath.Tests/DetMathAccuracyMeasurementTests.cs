using System.Diagnostics;
using System.Globalization;
using System.Text;
using Xunit.Abstractions;

namespace Proto.DetMath.Tests;

/// <summary>
/// Spec §7 measurement: ulp distance of DetMath vs the HOST Math.* (glibc libm on Linux) over
/// >= 1e6 inputs per function, drawn from the argument ranges the campaign's call sites use
/// (ranges derived in reports/O17-detmath.md §2). This is a MEASUREMENT of agreement with one libm,
/// not a correctness oracle (that is DetMathCorrectnessTests). Statistics use double — test-only.
///
/// Asserted bound: |ulp| &lt;= 1 everywhere (both libraries are within ~0.5 ulp of the true value, so
/// any larger gap means a DetMath bug). The equal fraction is printed and written to
/// $O17_MEASURE_OUT (if set) for the report; it is NOT asserted because it is a property of glibc.
/// </summary>
public sealed class DetMathAccuracyMeasurementTests
{
    private const int N = 1_000_000;
    private readonly ITestOutputHelper _out;
    public DetMathAccuracyMeasurementTests(ITestOutputHelper output) { _out = output; }

    private sealed record Range(string Name, string Fn, Func<TestRng, (double X, double Y)> Draw);

    private static readonly Range[] Ranges =
    {
        // Exp: decay/survival/hazard factors exp(-rate*dt) and weather lognormals; full domain as a control.
        new("exp campaign [-50, 5]", "exp", r => (r.Lin(-50.0, 5.0), 0.0)),
        new("exp small |x| in [1e-12, 1] (rate*dt)", "exp", r => ((r.U() < 0.5 ? -1 : 1) * r.LogU(1e-12, 1.0), 0.0)),
        new("exp full domain [-745, 709.7]", "exp", r => (r.Lin(-745.0, 709.7), 0.0)),
        // Log: Box-Muller log(u1), u1 = NextDouble() in [1e-300, 1); herd closed form log of ratios; metrics.
        new("log Box-Muller u in (0,1) (53-bit grid)", "log", r => (Math.Max(1e-300, r.U()), 0.0)),
        new("log ratios [0.5, 4]", "log", r => (r.Lin(0.5, 4.0), 0.0)),
        new("log wide [1e-300, 1e300]", "log", r => (r.LogU(1e-300, 1e300), 0.0)),
        // Pow: CES s^rho and acc^(1/rho), s in [1e-12, 1]; RP/CP c*P^beta; costGrowth^(n-1); (1-g)^n.
        new("pow CES s in [1e-12,1], rho in [-10,10]", "pow", r => (r.LogU(1e-12, 1.0), r.Lin(-10.0, 10.0))),
        new("pow P^beta P in [1,1e8], beta in [0.3,1.2]", "pow", r => (r.LogU(1.0, 1e8), r.Lin(0.3, 1.2))),
        new("pow integer exponents: b in [0,2], n in 0..200", "pow", r => (r.Lin(0.0, 2.0), (double)(long)r.Lin(0.0, 201.0))),
        // Cos: Box-Muller cos(2*pi*u2) — the only campaign call site.
        new("cos Box-Muller 2*pi*u", "cos", r => (2.0 * Math.PI * r.U(), 0.0)),
        new("cos wide [-1e5, 1e5]", "cos", r => (r.Lin(-1e5, 1e5), 0.0)),
    };

    [Fact]
    public void UlpDistanceToHostLibm_OverAMillionInputsPerRange_IsAtMostOne()
    {
        var md = new StringBuilder();
        md.AppendLine(CultureInfo.InvariantCulture, $"Host: {Environment.OSVersion} / {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription} / {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        md.AppendLine("| range | inputs | equal | fraction equal | max ulp | mean ulp | DetMath ns/call | Math ns/call |");
        md.AppendLine("|---|---|---|---|---|---|---|---|");
        long worstOverall = 0;
        string? disagreePath = Environment.GetEnvironmentVariable("O17_DISAGREE_OUT");
        StringBuilder? disagree = string.IsNullOrEmpty(disagreePath) ? null : new StringBuilder();
        foreach (var range in Ranges)
        {
            var rng = new TestRng(0x0017_0017UL + (ulong)range.Name.Length);
            var xs = new double[N];
            var ys = new double[N];
            for (int i = 0; i < N; i++) (xs[i], ys[i]) = range.Draw(rng);

            var det = new double[N];
            var lib = new double[N];
            var sw = Stopwatch.StartNew();
            Eval(range.Fn, xs, ys, det, useDet: true);
            double detNs = sw.Elapsed.TotalMilliseconds * 1e6 / N;
            sw.Restart();
            Eval(range.Fn, xs, ys, lib, useDet: false);
            double libNs = sw.Elapsed.TotalMilliseconds * 1e6 / N;

            long equal = 0, maxUlp = 0;
            double sumUlp = 0;
            int dumped = 0;
            for (int i = 0; i < N; i++)
            {
                long d = Ulp.Distance(det[i], lib[i]);
                if (d == 0) equal++;
                else if (disagree != null && dumped < 300)
                {
                    // fn x y detmath libm (hex bits) — classified against the decimal oracle by the report script.
                    disagree.AppendLine(CultureInfo.InvariantCulture,
                        $"{range.Fn} {Ulp.Hex(xs[i])} {Ulp.Hex(ys[i])} {Ulp.Hex(det[i])} {Ulp.Hex(lib[i])}");
                    dumped++;
                }
                if (d > maxUlp) { maxUlp = d; }
                sumUlp += d;
            }
            worstOverall = Math.Max(worstOverall, maxUlp);
            string line = string.Create(CultureInfo.InvariantCulture,
                $"| {range.Name} | {N} | {equal} | {(double)equal / N:F6} | {maxUlp} | {sumUlp / N:E3} | {detNs:F1} | {libNs:F1} |");
            md.AppendLine(line);
            _out.WriteLine(line);
            Assert.True(maxUlp <= 1, $"{range.Name}: max ulp {maxUlp}");
        }

        string? outPath = Environment.GetEnvironmentVariable("O17_MEASURE_OUT");
        if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, md.ToString());
        if (disagree != null) File.WriteAllText(disagreePath!, disagree.ToString());
        Assert.True(worstOverall <= 1);
    }

    private static void Eval(string fn, double[] xs, double[] ys, double[] dst, bool useDet)
    {
        switch (fn)
        {
            case "exp":
                if (useDet) for (int i = 0; i < xs.Length; i++) dst[i] = DetMath.Exp(xs[i]);
                else for (int i = 0; i < xs.Length; i++) dst[i] = Math.Exp(xs[i]);
                break;
            case "log":
                if (useDet) for (int i = 0; i < xs.Length; i++) dst[i] = DetMath.Log(xs[i]);
                else for (int i = 0; i < xs.Length; i++) dst[i] = Math.Log(xs[i]);
                break;
            case "pow":
                if (useDet) for (int i = 0; i < xs.Length; i++) dst[i] = DetMath.Pow(xs[i], ys[i]);
                else for (int i = 0; i < xs.Length; i++) dst[i] = Math.Pow(xs[i], ys[i]);
                break;
            case "cos":
                if (useDet) for (int i = 0; i < xs.Length; i++) dst[i] = DetMath.Cos(xs[i]);
                else for (int i = 0; i < xs.Length; i++) dst[i] = Math.Cos(xs[i]);
                break;
            default: throw new InvalidOperationException(fn);
        }
    }
}
