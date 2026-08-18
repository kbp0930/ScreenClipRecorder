namespace ScreenClipRecorder.Models;

public sealed record CaptureRegion(string DeviceName, int MonitorLeft, int MonitorTop, int X, int Y, int Width, int Height)
{
    public string Description => $"{Width} × {Height}  (화면 {X + MonitorLeft}, {Y + MonitorTop})";
}
