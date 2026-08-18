namespace ScreenClipRecorder.Models;

public sealed record RecordingPreset(string Name, int Fps, int VideoBitrate, int AudioBitrateKbps, int MaxWidth, int MaxHeight)
{
    public static IReadOnlyList<RecordingPreset> All { get; } =
    [
        new("용량 절약 · 1080p / 30fps", 30, 3_000_000, 96, 1920, 1080),
        new("표준 · 1080p / 30fps", 30, 4_800_000, 128, 1920, 1080),
        new("고화질 · 1440p / 30fps", 30, 8_000_000, 160, 2560, 1440),
        new("동영상/게임 · 1440p / 60fps", 60, 14_000_000, 192, 2560, 1440)
    ];
    public override string ToString() => Name;
}
