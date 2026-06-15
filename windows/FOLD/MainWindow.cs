using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FOLD.VirtualDisplay;

namespace FOLD;

public sealed class MainWindow : Form
{
    private readonly TrayApp _app;
    public TrayApp App => _app;

    private Panel  _sidebar = null!;
    private Panel  _host    = null!;
    private Button _navSet  = null!;
    private Button _navAdv  = null!;
    private Button _navAbt  = null!;

    private Panel  _pgSettings = null!;
    private Panel  _pgAdvanced = null!;
    private Panel  _pgAbout    = null!;

    private Label         _lblIp  = null!;
    private FlatComboBox  _cmbRes = null!;
    private Button        _btnSS  = null!;
    private Button        _btnUsb = null!;

    // Palette
    static readonly Color CB      = Color.FromArgb(11, 22, 41);
    static readonly Color CS      = Color.FromArgb(27, 46, 80);
    static readonly Color CMenu   = Color.FromArgb(217, 217, 217);
    static readonly Color CActive = Color.FromArgb(51, 51, 51);
    static readonly Color CBdr    = Color.FromArgb(217, 217, 217);
    static readonly Color CC      = Color.FromArgb(17, 27, 54);
    static readonly Color CGreen  = Color.FromArgb(0, 206, 128);
    static readonly Color CPurp   = Color.FromArgb(128, 102, 230);

    const int SW = 155; // sidebar width

    public MainWindow(TrayApp app)
    {
        _app = app;
        SuspendLayout();
        SetupForm();
        BuildLayout();
        BuildSidebar();
        BuildPages();
        ResumeLayout(true);
        SelectNav(_navSet, _pgSettings);
    }

    void SetupForm()
    {
        Text            = "FOLD by @idham.dev";
        ClientSize      = new Size(800, 480);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = CB;
        ForeColor       = Color.White;
        DoubleBuffered  = true;
        Icon            = TrayApp.LoadIcon();

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized) { Hide(); _app.ShowBalloon("FOLD is in the tray."); }
        };
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); _app.ShowBalloon("FOLD is in the tray."); }
        };
    }

    void BuildLayout()
    {
        _sidebar = new Panel { Location = new Point(0, 0), Size = new Size(SW, ClientSize.Height), BackColor = CS };
        _host    = new Panel { Location = new Point(SW, 0), Size = new Size(ClientSize.Width - SW, ClientSize.Height), BackColor = CB };
        Controls.Add(_host);
        Controls.Add(_sidebar);
    }

    // ── Sidebar ─────────────────────────────────────────────────────────

    void BuildSidebar()
    {
        _sidebar.Controls.Add(new Label { Text = "F O L D",       Font = new Font(FontLoader.Lalezar, 22f), ForeColor = Color.White, TextAlign = ContentAlignment.BottomCenter, Location = new Point(0, 8),  Size = new Size(SW, 44) });
        _sidebar.Controls.Add(new Label { Text = "by @idham.dev", Font = new Font(FontLoader.Lalezar, 9f),  ForeColor = Color.White, TextAlign = ContentAlignment.TopCenter,    Location = new Point(0, 52), Size = new Size(SW, 24) });
        _sidebar.Controls.Add(new Panel { Location = new Point(0, 82),  Size = new Size(SW, 2), BackColor = CBdr });

        _navSet = NavBtn("Settings");  _navSet.Location = new Point(0, 84);
        _navAdv = NavBtn("Advanced");  _navAdv.Location = new Point(0, 131);
        _navAbt = NavBtn("About");     _navAbt.Location = new Point(0, 178);

        _sidebar.Controls.Add(_navSet);
        _sidebar.Controls.Add(_navAdv);
        _sidebar.Controls.Add(_navAbt);
        _sidebar.Controls.Add(new Panel { Location = new Point(0, 225), Size = new Size(SW, 2), BackColor = CBdr });

        _navSet.Click += (_, _) => SelectNav(_navSet, _pgSettings);
        _navAdv.Click += (_, _) => SelectNav(_navAdv, _pgAdvanced);
        _navAbt.Click += (_, _) => SelectNav(_navAbt, _pgAbout);
    }

    Button NavBtn(string text)
    {
        var b = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat,
            BackColor = CMenu, ForeColor = Color.White,
            Font = new Font(FontLoader.Lalezar, 16f),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand, Size = new Size(SW, 47),
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = CMenu;
        b.FlatAppearance.MouseDownBackColor = CMenu;
        return b;
    }

    void SelectNav(Button active, Panel page)
    {
        foreach (var b in new[] { _navSet, _navAdv, _navAbt })
        {
            bool on = b == active;
            b.BackColor = on ? CActive : CMenu;
            b.FlatAppearance.MouseOverBackColor = on ? CActive : CMenu;
            b.FlatAppearance.MouseDownBackColor = on ? CActive : CMenu;
        }
        foreach (Control c in _host.Controls) c.Visible = c == page;
        RefreshStatus();
    }

    // ── Pages ────────────────────────────────────────────────────────────

    void BuildPages()
    {
        _pgSettings = MkPage(); BuildSettings();
        _pgAdvanced = MkPage(); BuildAdvanced();
        _pgAbout    = MkPage(); BuildAbout();
    }

    Panel MkPage()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = CB, Visible = false };
        _host.Controls.Add(p);
        return p;
    }

    // ── Settings ─────────────────────────────────────────────────────────

    void BuildSettings()
    {
        var p  = _pgSettings;
        int lp = 28;                         // left padding
        int cw = _host.Width - lp - 28;     // content width

        // IP address
        _lblIp = new Label { Text = "IP address :", Font = new Font(FontLoader.Lalezar, 14f), ForeColor = Color.White, Location = new Point(lp, 16), AutoSize = true };
        p.Controls.Add(_lblIp);

        // ● Stream Quality
        p.Controls.Add(new CircleDotHeader { Text = "Stream Quality", Font = new Font(FontLoader.Lalezar, 26f), ForeColor = Color.White, Location = new Point(lp - 2, 60), Size = new Size(cw, 44) });

        _cmbRes = new FlatComboBox(CB, CBdr) { Location = new Point(lp, 114), Size = new Size(cw, 44), Font = new Font(FontLoader.Lalezar, 14f) };
        _cmbRes.Items.AddRange(new object[]
        {
            "Auto ( Match Device Display)",
            "1080p Full HD (1920x1080 @ 60 FPS)",
            "2.5K Quad HD (2560x1600 @ 60 FPS)",
            "4K Ultra HD (3840x2160 @ 60 FPS)"
        });
        _cmbRes.SelectedIndex = _app.SelectedResIndex;
        _cmbRes.SelectedIndexChanged += (_, _) => _app.UpdateResolutionSelection(_cmbRes.SelectedIndex);
        p.Controls.Add(_cmbRes);

        // START
        _btnSS = BigBtn("START", CGreen, lp, 220, cw);
        _btnSS.Click += (_, _) => _app.ToggleStreaming(this);
        p.Controls.Add(_btnSS);
    }

    Button BigBtn(string text, Color bg, int x, int y, int w)
    {
        var b = new Button
        {
            Text = text, Location = new Point(x, y), Size = new Size(w, 66),
            FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White,
            Font = new Font(FontLoader.Lalezar, 26f), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 4;
        b.FlatAppearance.BorderColor = CBdr;
        b.FlatAppearance.MouseOverBackColor = bg;
        b.FlatAppearance.MouseDownBackColor = bg;
        return b;
    }

    // ── Advanced ──────────────────────────────────────────────────────────

    void BuildAdvanced()
    {
        var p  = _pgAdvanced;
        int lp = 22;
        int cw = _host.Width - lp * 2;

        // Card 1 — USB Mode
        var c1 = new Panel { Location = new Point(lp, 28), Size = new Size(cw, 130), BackColor = CC };
        c1.Controls.Add(new Label { Text = "USB Mode (ADB)",   Font = new Font(FontLoader.Lalezar, 21f), ForeColor = Color.White, Location = new Point(20, 16), AutoSize = true });
        c1.Controls.Add(new Label { Text = "Zero - latency via USB Cable. Requires USB Debugging  on the device.", Font = new Font(FontLoader.Lalezar, 11f), ForeColor = Color.White, Location = new Point(20, 60), Size = new Size(cw - 175, 55) });
        _btnUsb = AdvBtn("ENABLE", CGreen, cw - 152, 38, 130, 50);
        _btnUsb.Click += (_, _) => _app.ToggleUsbMode(this);
        c1.Controls.Add(_btnUsb);
        p.Controls.Add(c1);

        // Card 2 — Virtual Display Driver
        bool driverInstalled = VirtualDisplay.VirtualDisplayManager.IsVirtualDriverInstalled();
        var c2 = new Panel { Location = new Point(lp, 186), Size = new Size(cw, 130), BackColor = CC };
        c2.Controls.Add(new Label { Text = "Virtual Display Driver", Font = new Font(FontLoader.Lalezar, 21f), ForeColor = Color.White, Location = new Point(20, 16), AutoSize = true });

        var statusLabel = new Label
        {
            Text = driverInstalled
                ? "✓  Driver installed. Extends your desktop with a virtual second screen."
                : "Adds a Virtual Monitor so you can use extended display mode.\nWithout this driver you can only mirror the Primary Display.",
            Font = new Font(FontLoader.Lalezar, 11f),
            ForeColor = driverInstalled ? Color.FromArgb(0, 206, 128) : Color.White,
            Location = new Point(20, 60),
            Size = new Size(cw - 175, 55)
        };
        c2.Controls.Add(statusLabel);

        var vBtn = AdvBtn(
            driverInstalled ? "REINSTALL" : "INSTALL",
            driverInstalled ? CGreen : CPurp,
            cw - 152, 38, 130, 50);
        vBtn.Click += (_, _) => _app.InstallVirtualDisplay();
        c2.Controls.Add(vBtn);
        p.Controls.Add(c2);
    }

    Button AdvBtn(string text, Color bg, int x, int y, int w, int h)
    {
        var b = new Button
        {
            Text = text, Location = new Point(x, y), Size = new Size(w, h),
            FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White,
            Font = new Font(FontLoader.Lalezar, 14f), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 3;
        b.FlatAppearance.BorderColor = CBdr;
        b.FlatAppearance.MouseOverBackColor = bg;
        b.FlatAppearance.MouseDownBackColor = bg;
        return b;
    }

    // ── About ─────────────────────────────────────────────────────────────

    void BuildAbout()
    {
        var p  = _pgAbout;
        int lp = 22;
        int cw = _host.Width - lp * 2;

        var card = new AboutCard(CBdr) { Location = new Point(lp, 22), Size = new Size(cw, 430), BackColor = CC };
        card.Controls.Add(new Label
        {
            Text =
                "FOLD is a lightweight, high-performance utility designed to seamlessly\n" +
                "expand your digital workspace by transforming any standard Android\n" +
                "tablet into a fully functional second display.\n\n" +
                "Engineered for efficiency, FOLD utilizes H.264 hardware-accelerated\n" +
                "streaming to deliver a crisp, lag-free visual experience.\n\n" +
                "Built on Sdcb.FFmpeg, Android MediaCodec, and ADB, FOLD bridges\n" +
                "the gap between mobile hardware and desktop productivity.",
            Font = new Font(FontLoader.Lalezar, 12f), ForeColor = Color.White,
            Location = new Point(26, 108), Size = new Size(cw - 52, 300)
        });
        p.Controls.Add(card);
    }

    // ── Public refresh ────────────────────────────────────────────────────

    public void RefreshStatus()
    {
        if (!IsHandleCreated) return;
        Invoke(() =>
        {
            bool on  = _app.IsRunning;
            bool usb = _app.IsUsbMode;
            _lblIp.Text       = "IP address : " + TrayApp.GetLocalIp();
            _btnSS.Text       = on  ? "STOP"    : "START";
            _btnSS.BackColor  = on  ? Color.FromArgb(239, 68, 68) : CGreen;
            if (_btnUsb != null) { _btnUsb.Text = usb ? "DISABLE" : "ENABLE"; _btnUsb.BackColor = usb ? Color.FromArgb(239, 68, 68) : CGreen; }
        });
    }


}

// ═══════════════════════════════════════════════════════════════════════
// FlatComboBox — fully custom-drawn combo, no Windows arrow
// ═══════════════════════════════════════════════════════════════════════

public sealed class FlatComboBox : Control
{
    private readonly Color _bg;
    private readonly Color _border;
    private bool _dropped;

    private Panel? _dropPanel;

    public System.Collections.Generic.List<object> Items { get; } = new();
    public int    SelectedIndex { get; set; } = -1;
    public string SelectedText  => SelectedIndex >= 0 ? Items[SelectedIndex].ToString()! : "";

    public event EventHandler? SelectedIndexChanged;

    public FlatComboBox(Color bg, Color border) { _bg = bg; _border = border; DoubleBuffered = true; Cursor = Cursors.Hand; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Fill
        using var bgBr = new SolidBrush(_bg);
        g.FillRectangle(bgBr, 0, 0, Width, Height);

        // Border
        using var bp = new Pen(_border, 3f);
        g.DrawRectangle(bp, 1, 1, Width - 3, Height - 3);

        // Text
        string txt = SelectedText;
        using var tf = new SolidBrush(Color.White);
        var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(txt, Font, tf, new RectangleF(10, 0, Width - 50, Height), fmt);

        // Custom chevron arrow ▼
        int ax = Width - 34;
        int ay = Height / 2 - 5;
        using var ap = new Pen(Color.White, 2.5f);
        g.DrawLine(ap, ax, ay,     ax + 10, ay + 10);
        g.DrawLine(ap, ax + 10, ay + 10, ax + 20, ay);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_dropped) CloseDropdown(); else OpenDropdown();
    }

    void OpenDropdown()
    {
        _dropped = true;
        int itemH = 34;

        _dropPanel = new Panel
        {
            BackColor = Color.FromArgb(17, 27, 54),
            Size      = new Size(Width, Items.Count * itemH),
            BorderStyle = BorderStyle.None
        };

        // Position in parent coords
        var loc = Parent!.PointToClient(PointToScreen(new Point(0, Height)));
        _dropPanel.Location = loc;

        for (int i = 0; i < Items.Count; i++)
        {
            int idx   = i;
            string lbl = Items[i].ToString()!;
            var btn = new Button
            {
                Text      = lbl,
                FlatStyle = FlatStyle.Flat,
                BackColor = idx == SelectedIndex ? Color.FromArgb(51, 51, 51) : Color.FromArgb(17, 27, 54),
                ForeColor = Color.White,
                Font      = Font,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0),
                Size      = new Size(Width, itemH),
                Location  = new Point(0, idx * itemH),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 80);
            btn.Click += (_, _) =>
            {
                SelectedIndex = idx;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                Tag = null;
                CloseDropdown();
                Invalidate();
            };
            _dropPanel.Controls.Add(btn);
        }

        Parent.Controls.Add(_dropPanel);
        _dropPanel.BringToFront();

        // Click away to close
        Parent.MouseClick += CloseOnClickAway;
        FindForm()!.Deactivate += (_, _) => CloseDropdown();
    }

    void CloseOnClickAway(object? sender, MouseEventArgs e)
    {
        if (_dropPanel != null && !_dropPanel.Bounds.Contains(e.Location))
            CloseDropdown();
    }

    void CloseDropdown()
    {
        _dropped = false;
        if (_dropPanel != null)
        {
            Parent?.Controls.Remove(_dropPanel);
            _dropPanel.Dispose();
            _dropPanel = null;
        }
        if (Parent != null) Parent.MouseClick -= CloseOnClickAway;
        Invalidate();
    }

    protected override void Dispose(bool disposing) { if (disposing) CloseDropdown(); base.Dispose(disposing); }
}

// ═══════════════════════════════════════════════════════════════════════
// RefreshButton — custom drawn circular arrow
// ═══════════════════════════════════════════════════════════════════════

public sealed class RefreshButton : Control
{
    private readonly Color _border;
    private bool _hover;

    public RefreshButton(Color border)
    {
        _border = border;
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable, false);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color bg = _hover ? Color.FromArgb(200, 200, 200) : Color.FromArgb(217, 217, 217);
        using var bgBr = new SolidBrush(bg);
        g.FillRectangle(bgBr, 0, 0, Width, Height);

        using var bp = new Pen(_border, 3f);
        g.DrawRectangle(bp, 1, 1, Width - 3, Height - 3);

        // Draw circular arrow using arc + arrowhead
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int m = 8;
        var rect = new Rectangle(m, m, Width - m * 2, Height - m * 2);

        using var ap = new Pen(Color.Black, 2.8f) { StartCap = LineCap.Round, EndCap = LineCap.ArrowAnchor };
        g.DrawArc(ap, rect, -220f, 300f);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// CircleDotHeader
// ═══════════════════════════════════════════════════════════════════════

public sealed class CircleDotHeader : Control
{
    public CircleDotHeader() { DoubleBuffered = true; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Outer ring
        using var pen = new Pen(Color.White, 3.5f);
        g.DrawEllipse(pen, 3, 12, 20, 20);
        // Inner dot
        g.FillEllipse(Brushes.White, 8, 17, 10, 10);

        using var br = new SolidBrush(ForeColor);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.DrawString(Text, Font, br, 32, 0);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// AboutCard
// ═══════════════════════════════════════════════════════════════════════

public sealed class AboutCard : Panel
{
    private readonly Color _bc;
    public AboutCard(Color bc) { _bc = bc; DoubleBuffered = true; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        using var pen = new Pen(_bc, 4f);
        g.DrawRectangle(pen, 2, 2, Width - 5, Height - 5);

        using var fBig   = new Font(FontLoader.Lalezar, 30f);
        using var fSmall = new Font(FontLoader.Lalezar, 14f);
        using var brush  = new SolidBrush(Color.White);

        string p1 = "F  O  L  D";
        string p2 = "  by @idham.dev";
        var s1 = g.MeasureString(p1, fBig);
        var s2 = g.MeasureString(p2, fSmall);
        float sx = (Width - s1.Width - s2.Width) / 2f;

        g.DrawString(p1, fBig,   brush, sx, 18f);
        g.DrawString(p2, fSmall, brush, sx + s1.Width, 34f);

        using var lp = new Pen(Color.White, 2.5f);
        g.DrawLine(lp, 26, 82, Width - 26, 82);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// FontLoader
// ═══════════════════════════════════════════════════════════════════════

public static class FontLoader
{
    private static PrivateFontCollection? _pfc;
    private static FontFamily? _fam;

    public static FontFamily Lalezar
    {
        get { if (_fam == null) Load(); return _fam ?? FontFamily.GenericSansSerif; }
    }

    static void Load()
    {
        try
        {
            _pfc = new PrivateFontCollection();
            var asm = Assembly.GetExecutingAssembly();
            using var s = asm.GetManifestResourceStream("FOLD.Resources.Lalezar-Regular.ttf");
            if (s == null) return;
            int len = (int)s.Length;
            byte[] buf = new byte[len];
            int pos = 0;
            while (pos < len) { int r = s.Read(buf, pos, len - pos); if (r <= 0) break; pos += r; }
            IntPtr ptr = Marshal.AllocCoTaskMem(len);
            Marshal.Copy(buf, 0, ptr, len);
            _pfc.AddMemoryFont(ptr, len);
            Marshal.FreeCoTaskMem(ptr);
            if (_pfc.Families.Length > 0) _fam = _pfc.Families[0];
        }
        catch { }
    }
}
