using System.Diagnostics;
using System.Globalization;

namespace Sim.Ui;

/// <summary>
/// D-086 DE2 (the Director, 2026-11-11: "I've closed the game and runs excel sheet didn't form. The screen froze") —
/// THE SESSION CLOSE, as five named steps run in a fixed order, OFF THE GAME THREAD, while the window keeps drawing a saving
/// screen (DE2-R1 (a)).
///
/// THE STEPS, unchanged in order and in what they write (ADR-059 §2 and its amendments; <see cref="GameUi.SaveOnExit"/>
/// before D-086 ran exactly these on the game thread):
/// <list type="number">
/// <item><b>save</b> — the order log, the chronicle, the trace and the telemetry, as at every End Turn;</item>
/// <item><b>telemetry</b> — the telemetry finalize (forced past the OS buffers);</item>
/// <item><b>forensic close</b> — the close record, which hashes the five companions;</item>
/// <item><b>workbook</b> — <c>session-STAMP.xlsx</c> built from the record files, self-checked, written whole;</item>
/// <item><b>raw log</b> — the raw telemetry log removed once the workbook on disk is verified to hold it.</item>
/// </list>
/// After step 3 the six record files are complete: an interruption in step 4 or 5 (the long ones) leaves them for
/// <c>sim-ui --export-session runs</c> to rebuild the workbook from, and an interruption before it leaves the files of the
/// last End Turn, which the export reads and whose missing close record it states (Forensic sheet <c>NC</c>, README).
///
/// ONE CLOSE PER SESSION: <see cref="Start"/> runs the steps once; a second close request while they run changes nothing
/// (it returns false). A step that throws ends the close there (the later steps would read what it failed to write); the
/// failure is reported on the error writer and appended to the session's export log, and the close counts as finished, so
/// the window can still exit — the record files of the last End Turn stay on disk.
///
/// THREADING: the steps run on one worker thread (not a background thread: a process whose game loop ended stays alive
/// until the record is complete). The game thread reads only <see cref="Finished"/>, <see cref="CurrentStep"/>,
/// <see cref="StepSeconds"/>, <see cref="WorkbookProgress"/> and <see cref="Failure"/>, and stops touching the session for
/// good when the close starts (GameUi draws the saving screen alone and takes no input). Not simulation code: the
/// stopwatch is presentation (ADR-009).
/// </summary>
public sealed class SessionClose
{
    /// <summary>The steps, in order (what the saving screen names).</summary>
    public static readonly string[] StepNames =
    [
        "Saving the order log, chronicle, trace and telemetry",
        "Finishing the telemetry log",
        "Writing the close record",
        "Building and checking the workbook",
        "Removing the raw telemetry log (the workbook holds it)",
    ];

    public const int Save = 0, FinalizeTelemetry = 1, ForensicClose = 2, Workbook = 3, RemoveRawTelemetry = 4;

    private const int NotStarted = 0, Running = 1, Done = 2, Failed = 3;

    private readonly UiSession _session;
    private readonly string _log;
    private readonly Action _save;
    private readonly TextWriter _errors;
    private readonly Action<int>? _beforeStep;
    private readonly Action<double>? _afterProgress;
    private readonly double[] _seconds = new double[StepNames.Length];
    private int _state;
    private int _step = -1;
    private long _progressBits;
    private string? _failure;
    private string? _workbook;
    private string? _workbookProblem;
    private Thread? _thread;

    /// <param name="session">The session closing.</param>
    /// <param name="sessionLogPath">Its order-log path (every record file is twinned from it).</param>
    /// <param name="save">The save step: what End Turn saves (GameUi's SaveSession).</param>
    /// <param name="errors">Where refusals and failures are reported (the game: stderr).</param>
    /// <param name="beforeStep">TEST SEAM: called on the worker before each step with its index; a throw is an interruption
    /// at that point. Null in the game.</param>
    /// <param name="afterProgress">TEST SEAM (verification round 2, RR-R2-03): called on the worker each time the workbook
    /// step reports its progress, after <see cref="WorkbookProgress"/> holds it; blocking in it holds the close at that point
    /// of the telemetry log. Null in the game.</param>
    public SessionClose(UiSession session, string sessionLogPath, Action save, TextWriter errors, Action<int>? beforeStep = null,
        Action<double>? afterProgress = null)
    {
        _session = session;
        _log = sessionLogPath;
        _save = save;
        _errors = errors;
        _beforeStep = beforeStep;
        _afterProgress = afterProgress;
    }

    /// <summary>Whether <see cref="Start"/> has run (the window is closing).</summary>
    public bool Started => Volatile.Read(ref _state) != NotStarted;

    /// <summary>Whether every step has run, or one failed: the window may exit.</summary>
    public bool Finished => Volatile.Read(ref _state) is Done or Failed;

    /// <summary>Whether a step failed (<see cref="Failure"/> says which and why).</summary>
    public bool HasFailed => Volatile.Read(ref _state) == Failed;

    /// <summary>The step running (0–4), or −1 before the first.</summary>
    public int CurrentStep => Volatile.Read(ref _step);

    /// <summary>The failed step and its reason, or null.</summary>
    public string? Failure => Volatile.Read(ref _failure);

    /// <summary>The workbook written, or null (not yet, or not written).</summary>
    public string? WorkbookPath => Volatile.Read(ref _workbook);

    /// <summary>Why the workbook step wrote no workbook (a named refusal or an I/O failure, as reported), or null.</summary>
    public string? WorkbookProblem => Volatile.Read(ref _workbookProblem);

    /// <summary>Whether the close finished with every step run and a workbook written: the window may close by itself.
    /// Otherwise the saving screen stays up with the reason until the player closes it again.</summary>
    public bool Succeeded => Volatile.Read(ref _state) == Done && WorkbookPath is not null;

    /// <summary>The seconds each finished step took (0 for a step not run). Measurement for the saving screen and the
    /// report, never part of a record.</summary>
    public double StepSeconds(int step) => Volatile.Read(ref _seconds[step]);

    /// <summary>The share (0–1) of the telemetry log the workbook step has read: the long part of the close.</summary>
    public double WorkbookProgress => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _progressBits));

    /// <summary>
    /// Starts the close: on a worker thread when <paramref name="background"/> (the game), else on this thread, returning
    /// when it is done (the headless tools and the tests). Returns false — and does nothing — when the close was already
    /// started: a second close request never runs a step twice.
    /// </summary>
    public bool Start(bool background = true)
    {
        if (Interlocked.CompareExchange(ref _state, Running, NotStarted) != NotStarted) return false;
        if (!background)
        {
            Run();
            return true;
        }
        _thread = new Thread(Run) { Name = "session-close", IsBackground = false };
        _thread.Start();
        return true;
    }

    /// <summary>Waits for a close started on a worker (tests; the game polls <see cref="Finished"/> instead).</summary>
    public bool Wait(TimeSpan timeout) => _thread is null || _thread.Join(timeout);

    private void Run()
    {
        (string Path, Export.SessionExportResult Result)? workbook = null;
        for (int k = 0; k < StepNames.Length; k++)
        {
            Volatile.Write(ref _step, k);
            var watch = Stopwatch.StartNew();
            try
            {
                _beforeStep?.Invoke(k);
                switch (k)
                {
                    case Save:
                        _save();
                        break;
                    case FinalizeTelemetry:
                        _session.FinalizeTelemetry(UiSession.TelemetryPath(_log));
                        break;
                    case ForensicClose:
                        _session.ExportForensicClose(_log);
                        break;
                    case Workbook:
                        var problems = new StringWriter(CultureInfo.InvariantCulture);
                        workbook = _session.ExportWorkbookStep(_log, problems, p =>
                        {
                            Interlocked.Exchange(ref _progressBits, BitConverter.DoubleToInt64Bits(p));
                            _afterProgress?.Invoke(p);
                        });
                        string said = problems.ToString();
                        if (said.Length > 0) _errors.Write(said);
                        if (workbook is null)
                        {
                            // no workbook: say why on the saving screen and, since the Windows launcher discards stderr, in the
                            // session's export log beside the record files (the failure path only: a written workbook adds no line)
                            string why = said.Length > 0 ? said.Trim()
                                : "the session's manifest " + Path.GetFileName(UiSession.ManifestPath(_log)) + " is not in the record folder";
                            Volatile.Write(ref _workbookProblem, why);
                            UiSession.ReportCloseProblem(_log, null, "session close: no workbook written: " + why);
                        }
                        Volatile.Write(ref _workbook, workbook?.Path);
                        break;
                    case RemoveRawTelemetry:
                        if (workbook is { } w) _session.RemoveRawTelemetryStep(_log, w.Path, w.Result, _errors);
                        break;
                }
            }
            catch (Exception e)
            {
                Volatile.Write(ref _seconds[k], watch.Elapsed.TotalSeconds);
                string reason = "session close stopped at step " + (k + 1).ToString(CultureInfo.InvariantCulture) + " ("
                    + StepNames[k] + "): " + e.GetType().Name + ": " + e.Message
                    + " - the record files of the last End Turn stay in the record folder; from the game folder, "
                    + GameUi.RecoveryCommand + " rebuilds the workbook from them";
                Volatile.Write(ref _failure, reason);
                UiSession.ReportCloseProblem(_log, _errors, reason);
                Volatile.Write(ref _state, Failed);
                return;
            }
            Volatile.Write(ref _seconds[k], watch.Elapsed.TotalSeconds);
        }
        Volatile.Write(ref _state, Done);
    }
}
