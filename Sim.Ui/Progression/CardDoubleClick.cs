namespace Sim.Ui.Progression;

/// <summary>
/// DOUBLE-CLICK ON A RESEARCH CARD (Director playtest 2026-11: "hover / click to select / double-click to start
/// researching"). The host feeds every plain click on the research screen through this, on the HOST's frame clock (the
/// sum of the frame times it was updated with — never the wall clock): a click completes a double-click when the previous
/// click landed on a card, at most <see cref="WindowSeconds"/> earlier and at most <see cref="SlopPx"/> away on each axis.
/// The screen decides what a click landed on (<see cref="ProgressionScreen.LastCardClick"/>) and what a double-click
/// orders (<see cref="ProgressionScreen.DoubleClick"/>). UI state only; pure.
/// </summary>
public sealed class CardDoubleClick
{
    /// <summary>The longest gap between the two clicks of a double-click, in seconds of host frame time.</summary>
    public const double WindowSeconds = 0.4;

    /// <summary>The most the pointer may travel between the two clicks, in screen pixels on each axis.</summary>
    public const double SlopPx = 6;

    private double _time, _x, _y;
    private int _card = -1;

    /// <summary>The card (content index) the previous click pinned when a click at (<paramref name="x"/>,
    /// <paramref name="y"/>) at host time <paramref name="now"/> completes a double-click on it; otherwise −1.</summary>
    public int Pending(double now, double x, double y) =>
        _card >= 0 && now - _time <= WindowSeconds && now >= _time && Math.Abs(x - _x) <= SlopPx && Math.Abs(y - _y) <= SlopPx ? _card : -1;

    /// <summary>Records a click: <paramref name="card"/> is the card it landed on, or −1 (a completed double-click records
    /// −1, so a third click starts afresh).</summary>
    public void Record(double now, double x, double y, int card)
    {
        _time = now;
        _x = x;
        _y = y;
        _card = card;
    }

    /// <summary>Forgets the previous click (the screen opened or closed, or a click went elsewhere).</summary>
    public void Reset() => _card = -1;
}
