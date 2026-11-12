using System.Globalization;
using ImGuiNET;
using Sim.Ui.ViewModel;

namespace Sim.Ui;

/// <summary>
/// D-086 DE2 (the Director, 2026-11-11: "The screen froze and i think the excel didn't come") — THE SAVING SCREEN. While the
/// session closes (<see cref="SessionClose"/>, on a worker thread), the window keeps running its loop and draws this panel
/// alone: what is being saved, each step as it finishes, the workbook step's progress, and that the window closes by itself
/// when the record is complete. When the close ends without a workbook (a refusal, a failure), the panel stays up and says
/// why and what to do, until the player closes the window again. It reads only the close's progress; nothing here touches
/// the session, which the close owns.
/// </summary>
public sealed partial class GameUi
{
    /// <summary>The saving screen's heading while the close runs.</summary>
    public const string ClosingHeading = "Saving the run";

    /// <summary>What the saving screen says about the window while the close runs.</summary>
    public const string ClosingNote = "The window closes by itself when the record is complete.";

    /// <summary>The command, run from the game folder (the folder of the two launchers), that builds the workbook from the
    /// record files in <c>runs\.record</c> — the same command the session export README gives (D-086 DE2). The game is a
    /// window application, so <c>start /wait</c> makes the prompt wait until the workbook is written.</summary>
    public const string RecoveryCommand = "start /wait \"\" \"app\\Sim.Ui.exe\" --export-session runs";

    /// <summary>What the saving screen adds once the player has asked to close more than once while it runs.</summary>
    public const string ClosingAgainNote = "Still saving. If the game is ended by force now, the workbook may not be written; "
        + "the record files stay in runs\\.record, and from the game folder the command " + RecoveryCommand + " builds it from them.";

    /// <summary>The heading when the close ended without a workbook.</summary>
    public const string ClosingProblemHeading = "The run workbook was not written";

    /// <summary>What the saving screen says when the close ended without a workbook.</summary>
    public const string ClosingRecoveryNote = "The record files are in runs\\.record (a hidden folder). From the game folder, the "
        + "command " + RecoveryCommand + " builds the workbook from them. If no workbook appears, send the runs\\.record folder. "
        + "Close the window again to quit.";

    private bool _closeRequestedAfterFinish;

    /// <summary>D-086 DE2: whether the host may let the window close now: the close succeeded (every step ran and the
    /// workbook is written), or it ended without a workbook and the player has asked to close again since.</summary>
    public bool ExitAllowed => _close is { Finished: true } c && (c.Succeeded || _closeRequestedAfterFinish);

    /// <summary>The lines of the saving screen for a close in this state (exposed so a test reads them as drawn).</summary>
    public static IReadOnlyList<string> ClosingLines(SessionClose close, int requests, double elapsedSeconds)
    {
        bool finished = close.Finished, failed = close.HasFailed;
        var lines = new List<string>
        {
            !finished ? ClosingHeading + new string('.', 1 + (int)(elapsedSeconds * 2.0) % 3)
                : close.Succeeded ? "The run is saved" : ClosingProblemHeading,
        };
        int current = close.CurrentStep;
        for (int k = 0; k < SessionClose.StepNames.Length; k++)
        {
            bool done = finished && !failed ? true : k < current;
            string mark = done ? "[done]" : failed && k == current ? "[fail]" : !finished && k == current ? "[now]" : "[ ]";
            string line = mark + " " + SessionClose.StepNames[k];
            if (done || (failed && k == current))
                line += " (" + close.StepSeconds(k).ToString("0.0", CultureInfo.InvariantCulture) + " s)";
            else if (!finished && k == current && k == SessionClose.Workbook)
                line += ": " + ((int)(close.WorkbookProgress * 100.0)).ToString(CultureInfo.InvariantCulture) + "% of the telemetry read";
            lines.Add(line);
        }
        if (!finished)
        {
            lines.Add(ClosingNote);
            if (requests > 1) lines.Add(ClosingAgainNote);
            return lines;
        }
        if (close.Succeeded)
        {
            lines.Add("The workbook: " + close.WorkbookPath);
            return lines;
        }
        if (close.Failure is { } failure) lines.Add(failure);
        else if (close.WorkbookProblem is { } problem) lines.Add(problem);
        lines.Add(ClosingRecoveryNote);
        return lines;
    }

    /// <summary>The saving screen: one centred panel, every line recorded in <see cref="FrameText"/> under
    /// <see cref="FrameSurface.Closing"/>.</summary>
    private void DrawClosing()
    {
        Controls.BeginFrame();
        FrameText.BeginFrame();
        _lastLensCommands = [];
        InfoRegistry.Clear();
        if (_fonts is { } fonts) ImGui.PushFont(fonts.For(_frameTheme).Body);
        _surface = FrameSurface.Closing;
        float s = Scale, margin = PanelLayout.Margin * s;
        float width = Math.Max(1f, Math.Min(_viewportWidth - 2f * margin, 680f * s));
        float height = Math.Max(1f, Math.Min(_viewportHeight - 2f * margin, 360f * s));
        var rect = new PanelRect("##closing", (_viewportWidth - width) / 2f, (_viewportHeight - height) / 2f, width, height);
        BeginChrome(rect);
        DrawPanelFurniture(new ChromeElement(rect, RulePlacement.TopEdge));
        ImGui.SetCursorPos(new System.Numerics.Vector2(2f * margin, 2f * margin));
        ImGui.PushTextWrapPos(width - 2f * margin);
        foreach (string line in ClosingLines(_close!, CloseRequests, _closingSeconds)) Text(line);
        ImGui.PopTextWrapPos();
        ImGui.End();
        if (_fonts is not null) ImGui.PopFont();
        Controls.EndFrame();
        FrameText.EndFrame();
    }
}
