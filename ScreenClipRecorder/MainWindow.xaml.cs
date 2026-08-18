using ScreenClipRecorder.Models;
using ScreenClipRecorder.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace ScreenClipRecorder;

public partial class MainWindow : Window
{
    private readonly RecordingService _recording = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Stopwatch _elapsed = new();
    private CaptureRegion? _region;
    private HotKeyService? _hotKeys;
    private TimeSpan? _limit;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent(); PresetBox.ItemsSource = RecordingPreset.All; PresetBox.SelectedIndex = 1; DelayBox.SelectedIndex = 1;
        SaveFolderText.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ScreenClip Recorder");
        _timer.Tick += Timer_Tick;
        _recording.Completed += path => Dispatcher.BeginInvoke(() => RecordingCompleted(path));
        _recording.Failed += error => Dispatcher.BeginInvoke(() => RecordingFailed(error));
        Loaded += (_, _) => InitializeHotKeys();
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(ShutdownWndProc);
        UpdateEstimate();
    }

    private IntPtr ShutdownWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WmClose = 0x0010;
        if (msg != WmClose) return IntPtr.Zero;

        handled = true;
        if (_isClosing) return IntPtr.Zero;
        _isClosing = true;

        if (!_recording.IsRecording)
        {
            Environment.Exit(0);
            return IntPtr.Zero;
        }

        Hide();
        _timer.Stop();
        _hotKeys?.Dispose();
        _hotKeys = null;

        _ = Task.Run(() =>
        {
            try { _recording.Stop(); }
            finally { Environment.Exit(0); }
        });
        return IntPtr.Zero;
    }

    private void InitializeHotKeys()
    {
        _hotKeys = new HotKeyService(this);
        _hotKeys.Pressed += id => Dispatcher.Invoke(async () =>
        { if (id == 1 && !_recording.IsRecording) await BeginRecordingAsync(); else if (id == 2) TogglePause(); else if (id == 3) StopRecording(); });
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        try
        {
            var screen = Forms.Screen.FromPoint(Forms.Cursor.Position); var selector = new RegionSelectorWindow(screen);
            if (selector.ShowDialog() == true && selector.SelectedRegion is not null)
            { _region = selector.SelectedRegion; RegionText.Text = _region.Description + $" · {screen.DeviceName}"; }
        }
        finally { Show(); Activate(); }
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await BeginRecordingAsync();
    private async Task BeginRecordingAsync()
    {
        if (_recording.IsRecording) return;
        if (_region is null) { MessageBox.Show("먼저 녹화할 영역을 선택하세요.", "영역 필요", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (PresetBox.SelectedItem is not RecordingPreset preset || !TryGetLimit(out _limit)) return;
        var folder = SaveFolderText.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder)) { MessageBox.Show("저장 위치를 입력하세요."); return; }
        try { Directory.CreateDirectory(folder); } catch (Exception ex) { MessageBox.Show($"저장 폴더를 만들 수 없습니다.\n{ex.Message}"); return; }
        var delay = int.Parse(((ComboBoxItem)DelayBox.SelectedItem).Tag!.ToString()!); StartButton.IsEnabled = false;
        for (var i = delay; i > 0; i--) { StatusText.Text = $"{i}초 후 녹화 시작"; await Task.Delay(1000); }
        var path = NextFilePath(folder);
        try
        {
            _recording.Start(path, _region, preset, CursorCheck.IsChecked == true);
            _elapsed.Restart(); _timer.Start(); PauseButton.IsEnabled = StopButton.IsEnabled = true; StatusText.Text = "● 녹화 중";
        }
        catch (Exception ex)
        {
            StartButton.IsEnabled = true; StatusText.Text = "시작 실패";
            var details = ex.ToString();
            WriteErrorLog(details);
            MessageBox.Show($"녹화를 시작하지 못했습니다.\n\n{details}\n\n오류 기록: {ErrorLogPath}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => TogglePause();
    private void Stop_Click(object sender, RoutedEventArgs e) => StopRecording();
    private void TogglePause()
    {
        if (!_recording.IsRecording) return;
        if (_recording.IsPaused) { _recording.Resume(); _elapsed.Start(); PauseButton.Content = "일시정지"; StatusText.Text = "● 녹화 중"; }
        else { _recording.Pause(); _elapsed.Stop(); PauseButton.Content = "계속"; StatusText.Text = "Ⅱ 일시정지"; }
    }
    private void StopRecording()
    { if (!_recording.IsRecording) return; StatusText.Text = "MP4 마무리 중…"; StopButton.IsEnabled = PauseButton.IsEnabled = false; _recording.Stop(); }
    private void Timer_Tick(object? sender, EventArgs e)
    { ElapsedText.Text = _elapsed.Elapsed.ToString(@"hh\:mm\:ss"); if (_limit is not null && _elapsed.Elapsed >= _limit) StopRecording(); }
    private void RecordingCompleted(string path) { ResetControls(); StatusText.Text = $"저장 완료 · {Path.GetFileName(path)}"; }
    private void RecordingFailed(string error)
    {
        ResetControls(); StatusText.Text = "녹화 실패";
        var details = string.IsNullOrWhiteSpace(error) ? "녹화 엔진이 상세 오류 없이 종료되었습니다." : error;
        WriteErrorLog(details);
        MessageBox.Show($"{details}\n\n오류 기록: {ErrorLogPath}", "녹화 오류", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    private void ResetControls()
    { _timer.Stop(); _elapsed.Reset(); StartButton.IsEnabled = true; StopButton.IsEnabled = PauseButton.IsEnabled = false; PauseButton.Content = "일시정지"; ElapsedText.Text = "00:00:00"; }

    private bool TryGetLimit(out TimeSpan? limit)
    {
        limit = null; if (AutoStopCheck.IsChecked != true) return true;
        if (!int.TryParse(HoursText.Text, out var h) || !int.TryParse(MinutesText.Text, out var m) || !int.TryParse(SecondsText.Text, out var s) || h < 0 || m is < 0 or > 59 || s is < 0 or > 59)
        { MessageBox.Show("자동 종료 시간을 올바르게 입력하세요."); return false; }
        limit = TimeSpan.FromHours(h) + TimeSpan.FromMinutes(m) + TimeSpan.FromSeconds(s);
        if (limit <= TimeSpan.Zero) { MessageBox.Show("자동 종료 시간은 1초 이상이어야 합니다."); return false; }
        return true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { Description = "녹화 파일 저장 폴더", SelectedPath = SaveFolderText.Text, UseDescriptionForTitle = true };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) SaveFolderText.Text = dialog.SelectedPath;
    }
    private void Settings_Changed(object sender, RoutedEventArgs e) => UpdateEstimate();
    private void UpdateEstimate()
    {
        if (EstimateText is null || PresetBox?.SelectedItem is not RecordingPreset preset) return;
        var duration = TimeSpan.FromMinutes(30);
        if (AutoStopCheck?.IsChecked == true && int.TryParse(HoursText?.Text, out var h) && int.TryParse(MinutesText?.Text, out var m) && int.TryParse(SecondsText?.Text, out var s))
            duration = TimeSpan.FromHours(Math.Max(0, h)) + TimeSpan.FromMinutes(Math.Max(0, m)) + TimeSpan.FromSeconds(Math.Max(0, s));
        var bytes = (preset.VideoBitrate + preset.AudioBitrateKbps * 1000d) * duration.TotalSeconds / 8d;
        EstimateText.Text = duration > TimeSpan.Zero ? $"약 {FormatBytes(bytes)} / {duration:hh\\:mm\\:ss}" : "시간을 입력하세요";
    }
    private static string FormatBytes(double bytes) => bytes >= 1_073_741_824 ? $"{bytes / 1_073_741_824:0.0} GB" : $"{bytes / 1_048_576:0} MB";
    private static string ErrorLogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenClipRecorder", "error.log");
    private static void WriteErrorLog(string details)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogPath)!);
            File.AppendAllText(ErrorLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{details}\n\n");
        }
        catch { }
    }
    private static string NextFilePath(string folder)
    { var stem = $"ScreenClip_{DateTime.Now:yyyyMMdd_HHmmss}"; var path = Path.Combine(folder, stem + ".mp4"); for (var i = 2; File.Exists(path); i++) path = Path.Combine(folder, $"{stem}_{i}.mp4"); return path; }
}
