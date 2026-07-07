using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Merlin.BrowserHost;

internal sealed class NativeBrowserPointerOverlayWindow : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;
    private static readonly Color TransparentKeyColor = Color.FromArgb(255, 255, 0, 255);
    private BrowserPointerOverlayState _state = BrowserPointerOverlayState.Inactive;
    private bool _hasLoggedShown;

    public NativeBrowserPointerOverlayWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = TransparentKeyColor;
        TransparencyKey = TransparentKeyColor;
        TopMost = true;
        DoubleBuffered = true;
        Enabled = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public void ApplyState(Rectangle bounds, BrowserPointerOverlayState state, IWin32Window owner)
    {
        _state = state;
        if (!state.IsActive || bounds.Width <= 0 || bounds.Height <= 0)
        {
            Hide();
            return;
        }

        Bounds = bounds;
        if (!Visible)
        {
            Show(owner);
            if (!_hasLoggedShown)
            {
                _hasLoggedShown = true;
                Console.WriteLine("BrowserPointerNativeOverlayShown");
            }
        }

        Invalidate();
    }

    public void HideOverlay()
    {
        _state = BrowserPointerOverlayState.Inactive;
        Hide();
        Invalidate();
    }

    public bool TryGetCurrentScreenClickPoint(out Point point)
    {
        point = Point.Empty;
        if (!_state.IsActive
            || !_state.IsHandInFrame
            || !_state.IsTrackingReliable
            || Bounds.Width <= 0
            || Bounds.Height <= 0)
        {
            return false;
        }

        var x = (int)Math.Round(Bounds.X + Math.Clamp(_state.OverlayX, 0, ClientSize.Width));
        var y = (int)Math.Round(Bounds.Y + Math.Clamp(_state.OverlayY, 0, ClientSize.Height));
        point = new Point(x, y);
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = HTTRANSPARENT;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(TransparentKeyColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!_state.IsActive || !_state.IsHandInFrame)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var alpha = _state.IsTrackingReliable ? 210 : 86;
        alpha = (int)Math.Clamp(alpha * (0.45 + (_state.Confidence * 0.55)), 45, 230);
        var x = (float)Math.Clamp(_state.OverlayX, 0, ClientSize.Width);
        var y = (float)Math.Clamp(_state.OverlayY, 0, ClientSize.Height);
        var visual = (_state.ClickVisualState ?? "normal").Trim().ToLowerInvariant();
        var primaryColor = visual switch
        {
            "pinch_candidate" => Color.FromArgb(alpha, 120, 230, 255),
            "pinch_armed" => Color.FromArgb(alpha, 120, 255, 180),
            "click_sent" => Color.FromArgb(230, 255, 255, 255),
            "scroll_candidate" => Color.FromArgb(alpha, 150, 220, 255),
            "scrolling" => Color.FromArgb(alpha, 120, 255, 210),
            "cooldown" => Color.FromArgb(Math.Max(45, alpha / 2), 120, 180, 210),
            "low_confidence" => Color.FromArgb(Math.Max(45, alpha / 3), 70, 205, 255),
            _ => Color.FromArgb(alpha, 70, 205, 255)
        };
        var primaryRadius = visual switch
        {
            "pinch_candidate" => 15f,
            "pinch_armed" => 13f,
            "click_sent" => 18f,
            "scroll_candidate" => 17f,
            "scrolling" => 16f,
            "cooldown" => 20f,
            _ => 18f
        };

        if (visual is "scroll_candidate" or "scrolling")
        {
            using var pen = new Pen(Color.FromArgb(Math.Max(50, alpha / 2), primaryColor), visual == "scrolling" ? 2.2f : 1.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            e.Graphics.DrawLine(pen, x, y - 34f, x, y + 34f);
            DrawChevron(e.Graphics, x, y - 34f, -1, Color.FromArgb(Math.Max(45, alpha / 2), primaryColor));
            DrawChevron(e.Graphics, x, y + 34f, 1, Color.FromArgb(Math.Max(45, alpha / 2), primaryColor));
        }

        if (visual is "pinch_armed" or "click_sent" or "scrolling")
        {
            using var brush = new SolidBrush(Color.FromArgb(visual == "click_sent" ? 70 : 38, primaryColor));
            e.Graphics.FillEllipse(brush, x - primaryRadius, y - primaryRadius, primaryRadius * 2, primaryRadius * 2);
        }

        if (visual == "click_sent")
        {
            DrawRing(e.Graphics, x, y, 34f, Color.FromArgb(150, 255, 255, 255), 1.4f);
        }

        DrawRing(e.Graphics, x, y, primaryRadius, primaryColor, _state.IsTrackingReliable ? 2.2f : 1.6f);
        DrawRing(e.Graphics, x, y, 23f, Color.FromArgb(alpha / 4, 190, 242, 255), 1.1f);
    }

    private static void DrawChevron(Graphics graphics, float x, float y, int direction, Color color)
    {
        using var pen = new Pen(color, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        var tipY = y + (direction * 5f);
        var wingY = y - (direction * 3f);
        graphics.DrawLine(pen, x, tipY, x - 5f, wingY);
        graphics.DrawLine(pen, x, tipY, x + 5f, wingY);
    }

    private static void DrawRing(Graphics graphics, float x, float y, float radius, Color color, float width)
    {
        using var pen = new Pen(color, width);
        graphics.DrawEllipse(pen, x - radius, y - radius, radius * 2, radius * 2);
    }
}

internal sealed record BrowserPointerOverlayState(
    bool IsActive,
    bool IsTrackingReliable,
    bool IsHandInFrame,
    double OverlayX,
    double OverlayY,
    double Confidence,
    string? ClickVisualState = "normal")
{
    public static BrowserPointerOverlayState Inactive { get; } = new(false, false, false, 0, 0, 0, "normal");
}
