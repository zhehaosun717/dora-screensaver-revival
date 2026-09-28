using System.Drawing;

namespace DoraSaver.Core;

/// <summary>
/// Decides when mouse movement should end the screensaver.
/// During the grace period the baseline follows the pointer, so movement right after start
/// (e.g. letting go of the mouse after clicking "Preview") is forgotten rather than postponed.
/// </summary>
internal sealed class WakePolicy
{
    public const int DefaultMoveThresholdPixels = 16;
    public static readonly TimeSpan DefaultMoveGracePeriod = TimeSpan.FromMilliseconds(750);

    private readonly TimeSpan _gracePeriod;
    private readonly int _thresholdSquared;
    private Point? _baseline;

    public WakePolicy(Point? origin, int thresholdPixels, TimeSpan gracePeriod)
    {
        if (thresholdPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdPixels), thresholdPixels, "must be positive");
        }

        _baseline = origin;
        _gracePeriod = gracePeriod;
        _thresholdSquared = thresholdPixels * thresholdPixels;
    }

    public bool ShouldWakeOnMove(Point current, TimeSpan elapsed)
    {
        if (elapsed < _gracePeriod || _baseline is null)
        {
            _baseline = current;
            return false;
        }

        long dx = current.X - _baseline.Value.X;
        long dy = current.Y - _baseline.Value.Y;
        return dx * dx + dy * dy > _thresholdSquared;
    }
}
