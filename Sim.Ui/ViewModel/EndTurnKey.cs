namespace Sim.Ui.ViewModel;

/// <summary>
/// T3.9a-b item 1: eligibility of the End-Turn key (Space) — the focus and
/// key-repeat rules as a PURE predicate (no ImGui, no MonoGame types), so the
/// rules are pinned headless (EndTurnKeyTests) while SimUiGame.Update supplies
/// the live inputs and routes a firing through the SAME EndTurn() path the
/// button uses (no second end-turn implementation).
///
/// The two rules, and why each input exists:
/// - FOCUS: <paramref name="imGuiWantsKeyboard"/> (io.WantCaptureKeyboard) is
///   true whenever any ImGui widget owns the keyboard; <paramref
///   name="imGuiWantsTextInput"/> (io.WantTextInput) is the sharper "a text
///   field has focus" signal. Either one vetoes the key — typing a space into
///   a future text box must never end a turn.
/// - REPEAT: the pressed-edge pair (down this frame, up last frame) is the
///   non-repeat key-press query for POLLED input: OS key-repeat re-delivers
///   character events but never re-lowers the polled key state, so a held
///   Space fires exactly once, on the frame it went down. Same idiom as the
///   Tab selection-cycling edge in SimUiGame.Update.
/// </summary>
public static class EndTurnKey
{
    public static bool ShouldFire(
        bool spaceIsDown, bool spaceWasDown,
        bool imGuiWantsKeyboard, bool imGuiWantsTextInput)
        => spaceIsDown && !spaceWasDown && !imGuiWantsKeyboard && !imGuiWantsTextInput;
}
