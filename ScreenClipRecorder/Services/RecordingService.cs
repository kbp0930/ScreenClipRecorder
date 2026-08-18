using ScreenClipRecorder.Models;
using ScreenRecorderLib;
using System.IO;

namespace ScreenClipRecorder.Services;

public sealed class RecordingService : IDisposable
{
    private Recorder? _recorder;
    public bool IsRecording { get; private set; }
    public bool IsPaused { get; private set; }
    public event Action<string>? Completed;
    public event Action<string>? Failed;

    public void Start(string path, CaptureRegion region, RecordingPreset preset, bool includeCursor)
    {
        if (IsRecording) throw new InvalidOperationException("이미 녹화 중입니다.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenClipRecorder"));
        var (outputWidth, outputHeight) = FitEven(region.Width, region.Height, preset.MaxWidth, preset.MaxHeight);
        var display = new DisplayRecordingSource(region.DeviceName)
        {
            SourceRect = new ScreenRect(region.X, region.Y, region.Width, region.Height),
            OutputSize = new ScreenSize(outputWidth, outputHeight),
            Stretch = StretchMode.Uniform
        };
        var options = new RecorderOptions
        {
            SourceOptions = new SourceOptions { RecordingSources = [display] },
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
                OutputFrameSize = new ScreenSize(outputWidth, outputHeight),
                Stretch = StretchMode.Uniform
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = true, IsOutputDeviceEnabled = true, IsInputDeviceEnabled = false,
                Bitrate = ToAudioBitrate(preset.AudioBitrateKbps), Channels = AudioChannels.Stereo, OutputVolume = 1.0f
            },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Bitrate = preset.VideoBitrate, Framerate = preset.Fps, IsFixedFramerate = true,
                Encoder = new H264VideoEncoder { BitrateMode = H264BitrateControlMode.UnconstrainedVBR, EncoderProfile = H264Profile.Main },
                IsHardwareEncodingEnabled = true, IsLowLatencyEnabled = false, IsMp4FastStartEnabled = true,
                IsFragmentedMp4Enabled = false, IsThrottlingDisabled = false
            },
            MouseOptions = new MouseOptions { IsMousePointerEnabled = includeCursor, IsMouseClicksDetected = false },
            LogOptions = new LogOptions
            {
                IsLogEnabled = true,
                LogFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenClipRecorder", "recorder.log"),
                LogSeverityLevel = ScreenRecorderLib.LogLevel.Debug
            }
        };
        _recorder = Recorder.CreateRecorder(options);
        _recorder.OnRecordingComplete += OnComplete;
        _recorder.OnRecordingFailed += OnFailed;
        _recorder.Record(path);
        IsRecording = true;
        IsPaused = false;
    }

    public void Pause() { if (!IsRecording || IsPaused) return; _recorder?.Pause(); IsPaused = true; }
    public void Resume() { if (!IsRecording || !IsPaused) return; _recorder?.Resume(); IsPaused = false; }
    public void Stop() { if (IsRecording) _recorder?.Stop(); }

    private void OnComplete(object? sender, RecordingCompleteEventArgs e)
    { IsRecording = IsPaused = false; Completed?.Invoke(e.FilePath); ReleaseRecorder(); }

    private void OnFailed(object? sender, RecordingFailedEventArgs e)
    { IsRecording = IsPaused = false; Failed?.Invoke(e.Error); ReleaseRecorder(); }

    private void ReleaseRecorder()
    {
        if (_recorder is null) return;
        _recorder.OnRecordingComplete -= OnComplete; _recorder.OnRecordingFailed -= OnFailed;
        _recorder.Dispose(); _recorder = null;
    }

    private static AudioBitrate ToAudioBitrate(int kbps) => kbps switch
    { <= 96 => AudioBitrate.bitrate_96kbps, <= 128 => AudioBitrate.bitrate_128kbps, <= 160 => AudioBitrate.bitrate_160kbps, _ => AudioBitrate.bitrate_192kbps };

    private static (int Width, int Height) FitEven(int width, int height, int maxWidth, int maxHeight)
    {
        var scale = Math.Min(1d, Math.Min((double)maxWidth / width, (double)maxHeight / height));
        return (Math.Max(2, ((int)(width * scale)) & ~1), Math.Max(2, ((int)(height * scale)) & ~1));
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            _recorder?.Stop();
            return;
        }
        ReleaseRecorder();
    }
}
