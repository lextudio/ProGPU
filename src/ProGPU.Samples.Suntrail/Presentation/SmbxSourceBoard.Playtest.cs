using System.Numerics;
using Microsoft.UI.Xaml;
using ProGPU.Samples.Suntrail.Game.Import;
using ProGPU.Samples.Suntrail.Rendering;

namespace ProGPU.Samples.Suntrail.Presentation;

public sealed partial class SmbxSourceBoard
{
    private readonly ProceduralBatch _courierPreview = new();
    private uint _playtestRevision = uint.MaxValue;
    private Vector2 _playtestSize;
    public SmbxPlaytest? Playtest { get; private set; }
    public SmbxPlaytestInput PlaytestInput { get; set; }
    public event Action? PlaytestUpdated;

    public void SetPlaytest(SmbxPlaytest playtest, SmbxLevelPack? artwork)
    {
        ArgumentNullException.ThrowIfNull(playtest);
        SetEditor(playtest.Snapshot, artwork); Playtest = playtest;
        _playtestRevision = uint.MaxValue; _playtestSize = default;
        SetCustomAnimationActive(true); Invalidate();
    }
    public void ClearPlaytest()
    {
        Playtest?.ClearPendingInput(); Playtest = null; PlaytestInput = default;
        SetCustomAnimationActive(false);
    }
    protected override void OnUpdateAnimations(float elapsedSeconds)
    {
        base.OnUpdateAnimations(elapsedSeconds);
        if (Visibility == Visibility.Collapsed || Playtest is not { } playtest) return;
        playtest.Advance(elapsedSeconds, PlaytestInput);
        PlaytestInput = PlaytestInput with { JumpPressed = false, UsePressed = false };
        if (_playtestRevision != playtest.Revision || _playtestSize != Size)
        {
            _playtestRevision = playtest.Revision; _playtestSize = Size;
            var body = playtest.Character.Bounds; var section = playtest.Sections[playtest.SectionIndex].Bounds;
            Zoom = Math.Clamp(Size.Y / 700d, .35, 3);
            double halfWidth = Size.X / (2 * Zoom), halfHeight = Size.Y / (2 * Zoom);
            // Clamp the camera, not the source world. Small sections remain centered;
            // arbitrary negative/large source coordinates rebase before float output.
            double x = section.Width <= halfWidth * 2 ? section.X + section.Width / 2 :
                Math.Clamp(body.X + body.Width / 2 + playtest.Character.VelocityX * .12, section.X + halfWidth, section.Right - halfWidth);
            double y = section.Height <= halfHeight * 2 ? section.Y + section.Height / 2 :
                Math.Clamp(body.Y + body.Height / 2, section.Y + halfHeight, section.Bottom - halfHeight);
            SetCamera(x, y);
            _courierPreview.BuildCourierPreview(Size, ScreenPoint(body.X, body.Y), (float)Zoom,
                (float)(playtest.Character.Tick * SmbxPlaytest.StepSeconds), playtest.Character.Facing,
                playtest.Character.Grounded ? (float)Math.Clamp(Math.Abs(playtest.Character.VelocityX) / 180, 0, 1) : -1);
            Invalidate();
        }
        PlaytestUpdated?.Invoke();
    }
}
