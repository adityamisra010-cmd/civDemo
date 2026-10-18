using System.Diagnostics;

namespace Sim.Tests.TestUtil;

/// <summary>
/// Runs the REAL `sim` executable (Sim.Cli.dll, copied next to the tests by the project reference) in its own process,
/// so a test sees stdout and stderr as a script does — separately, and never mixed with another test's console writes
/// (in-process Console redirection is global to the test host). Bounded by <c>limit</c>: a run that does not finish is
/// killed and reported as non-termination (ADR-015 §7.1), never waited on indefinitely.
/// </summary>
internal static class CliProcess
{
    internal static (int Exit, string Out, string Err) Run(TimeSpan limit, params string[] args)
    {
        string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } h && File.Exists(h) ? h : "dotnet";
        var psi = new ProcessStartInfo(host)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Sim.Cli.dll"));
        foreach (string a in args) psi.ArgumentList.Add(a);
        using Process p = Process.Start(psi)!;
        Task<string> o = p.StandardOutput.ReadToEndAsync(), e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(limit))
        {
            p.Kill(entireProcessTree: true);
            throw new TimeoutException("sim " + string.Join(' ', args) + " did not finish in " + limit + " (non-termination)");
        }
        p.WaitForExit();
        return (p.ExitCode, o.Result, e.Result);
    }

    /// <summary>The non-empty lines of <paramref name="text"/> (either line ending).</summary>
    internal static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
