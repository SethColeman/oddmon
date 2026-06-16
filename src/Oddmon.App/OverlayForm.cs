using System.Drawing;
using System.Windows.Forms;
using Oddmon.Core;

namespace Oddmon.App;

/// <summary>
/// Always-on-top desktop panel showing the HDD + Turbo LEDs with labels.
/// Drag anywhere to move. See scope §3.1.
/// </summary>
internal sealed class OverlayForm : Form
{
    private ActivityLevel _activity = ActivityLevel.Idle;
    private TurboState _turbo = TurboState.Off;
    private readonly Font _font = new("Consolas", 8f, FontStyle.Bold);

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        Size = new Size(132, 64);
        BackColor = Color.FromArgb(24, 24, 28);
        Opacity = 0.9;
    }

    public void SetActivity(ActivityLevel level) { _activity = level; Invalidate(); }
    public void SetTurbo(TurboState state) { _turbo = state; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        using var border = new Pen(Color.FromArgb(60, 60, 66));
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        using var label = new SolidBrush(Color.FromArgb(170, 170, 175));
        g.DrawString("HDD", _font, label, 12, 12);
        g.DrawString("TURBO", _font, label, 12, 38);

        DrawLed(g, new Rectangle(74, 11, 46, 14), LedColors.Activity(_activity));
        DrawLed(g, new Rectangle(74, 37, 46, 14), LedColors.Turbo(_turbo));
    }

    private static void DrawLed(Graphics g, Rectangle rect, Color color)
    {
        using var b = new SolidBrush(color);
        g.FillRectangle(b, rect);
    }

    // Drag the whole borderless window by treating the client area as the title bar.
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84, HTCLIENT = 1, HTCAPTION = 2;
        base.WndProc(ref m);
        if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
            m.Result = HTCAPTION;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _font.Dispose();
        base.Dispose(disposing);
    }
}
