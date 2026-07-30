using Sim.Ui.ViewModel;
using Xunit;

namespace Sim.Ui.Tests;

// T3.9a-b item 1: the End-Turn key's eligibility predicate, headless. The
// packet's three requirements each get a named pin: fires on a clean press;
// never re-fires while held (the key-repeat rule); never while ImGui wants
// the keyboard or a text field has focus (the focus rule). Discoverability
// (the "[Space]" on the button label) is a render-site concern, not a
// predicate concern, and is judged at the visual gate.
public class EndTurnKeyTests
{
    [Fact]
    public void Fires_OnCleanPress_WhenImGuiWantsNothing() =>
        Assert.True(EndTurnKey.ShouldFire(
            spaceIsDown: true, spaceWasDown: false,
            imGuiWantsKeyboard: false, imGuiWantsTextInput: false));

    [Fact]
    public void DoesNotRefire_WhileHeld_TheKeyRepeatRule() =>
        Assert.False(EndTurnKey.ShouldFire(
            spaceIsDown: true, spaceWasDown: true,
            imGuiWantsKeyboard: false, imGuiWantsTextInput: false));

    [Fact]
    public void DoesNotFire_WhileImGuiWantsTheKeyboard() =>
        Assert.False(EndTurnKey.ShouldFire(
            spaceIsDown: true, spaceWasDown: false,
            imGuiWantsKeyboard: true, imGuiWantsTextInput: false));

    [Fact]
    public void DoesNotFire_WhileATextFieldHasFocus()
    {
        // WantTextInput alone must veto, even if WantCaptureKeyboard were
        // ever reported false alongside it — the guards are independent.
        Assert.False(EndTurnKey.ShouldFire(
            spaceIsDown: true, spaceWasDown: false,
            imGuiWantsKeyboard: false, imGuiWantsTextInput: true));
        Assert.False(EndTurnKey.ShouldFire(
            spaceIsDown: true, spaceWasDown: false,
            imGuiWantsKeyboard: true, imGuiWantsTextInput: true));
    }

    [Fact]
    public void DoesNotFire_WithoutAPress()
    {
        Assert.False(EndTurnKey.ShouldFire(false, false, false, false)); // idle
        Assert.False(EndTurnKey.ShouldFire(false, true, false, false));  // release edge
    }

    [Fact]
    public void TruthTable_ExactlyOneOfSixteenStatesFires()
    {
        // The predicate is a 4-input boolean function; enumerate the whole
        // domain so any rewiring (a dropped guard, an inverted flag) fails
        // loudly. Exactly ONE state may end a turn: the pressed edge with
        // ImGui wanting nothing.
        int firing = 0;
        for (int bits = 0; bits < 16; bits++)
        {
            bool down = (bits & 1) != 0;
            bool was = (bits & 2) != 0;
            bool capture = (bits & 4) != 0;
            bool text = (bits & 8) != 0;
            if (EndTurnKey.ShouldFire(down, was, capture, text))
            {
                firing++;
                Assert.True(down && !was && !capture && !text,
                    $"fired on illegal state down={down} was={was} capture={capture} text={text}");
            }
        }
        Assert.Equal(1, firing);
    }
}
