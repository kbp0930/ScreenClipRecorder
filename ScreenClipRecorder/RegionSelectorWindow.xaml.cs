using ScreenClipRecorder.Models;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace ScreenClipRecorder;

public partial class RegionSelectorWindow : Window
{
    private readonly System.Windows.Forms.Screen _screen;
    private System.Windows.Point _start;
    private Rect _selection;
    private bool _dragging;
    public CaptureRegion? SelectedRegion { get; private set; }

    public RegionSelectorWindow(System.Windows.Forms.Screen screen)
    { InitializeComponent(); _screen = screen; SourceInitialized += (_, _) => PositionOnScreen(); }

    private void PositionOnScreen()
    {
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = transform.Transform(new System.Windows.Point(_screen.Bounds.Left, _screen.Bounds.Top));
        var bottomRight = transform.Transform(new System.Windows.Point(_screen.Bounds.Right, _screen.Bounds.Bottom));
        Left = topLeft.X; Top = topLeft.Y; Width = bottomRight.X - topLeft.X; Height = bottomRight.Y - topLeft.Y;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    { _start = e.GetPosition(Surface); _dragging = true; Surface.CaptureMouse(); Selection.Visibility = SizeBadge.Visibility = Visibility.Visible; UpdateSelection(_start); }
    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e) { if (_dragging) UpdateSelection(e.GetPosition(Surface)); }
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    { if (!_dragging) return; UpdateSelection(e.GetPosition(Surface)); _dragging = false; Surface.ReleaseMouseCapture(); }

    private void UpdateSelection(System.Windows.Point current)
    {
        var x = Math.Max(0, Math.Min(_start.X, current.X)); var y = Math.Max(0, Math.Min(_start.Y, current.Y));
        var right = Math.Min(ActualWidth, Math.Max(_start.X, current.X)); var bottom = Math.Min(ActualHeight, Math.Max(_start.Y, current.Y));
        _selection = new Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
        Canvas.SetLeft(Selection, x); Canvas.SetTop(Selection, y); Selection.Width = _selection.Width; Selection.Height = _selection.Height;
        var dpi = VisualTreeHelper.GetDpi(this);
        SizeText.Text = $"{(int)(_selection.Width * dpi.DpiScaleX)} × {(int)(_selection.Height * dpi.DpiScaleY)}";
        Canvas.SetLeft(SizeBadge, Math.Min(x, Math.Max(0, ActualWidth - 120))); Canvas.SetTop(SizeBadge, Math.Max(0, y - 34));
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { DialogResult = false; return; }
        if (e.Key != Key.Enter || _selection.Width < 16 || _selection.Height < 16) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var x = (int)Math.Round(_selection.X * dpi.DpiScaleX); var y = (int)Math.Round(_selection.Y * dpi.DpiScaleY);
        var width = ((int)Math.Round(_selection.Width * dpi.DpiScaleX)) & ~1; var height = ((int)Math.Round(_selection.Height * dpi.DpiScaleY)) & ~1;
        SelectedRegion = new CaptureRegion(_screen.DeviceName, _screen.Bounds.Left, _screen.Bounds.Top, x, y, width, height);
        DialogResult = true;
    }
}
