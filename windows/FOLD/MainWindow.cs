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
    private FlatComboBox  _cmbMon = null!;
    private RefreshButton _btnRefresh = null!;
    private FlatComboBox  _cmbRes = null!;
    private Button        _btnSS  = null!;
    private Button        _btnApply = null!;
    private Button        _btnUsb = null!;

    // Scaling Factor
    private float _scale = 1.0f;
    private int Scale(int val) => (int)(val * _scale);
    private float Scale(float val) => val * _scale;

    // Pending settings for Apply
    private int _pendingResIndex;
    private IntPtr _pendingMonitorHandle;
    private string _pendingMonitorLabel = "";

    // Palette (Premium Navy & Teal Theme matching idhamdev-pallete)
    static readonly Color CB      = Color.FromArgb(15, 23, 42);   // #0F172A - Deep Navy 1 background
    static readonly Color CS      = Color.FromArgb(16, 34, 77);   // #10224D - Deep Navy 2 sidebar
    static readonly Color CMenu   = Color.Transparent;            // Inactive navigation buttons
    static readonly Color CActive = Color.FromArgb(56, 56, 56);   // #383838 - Active nav background (Tertiary UI)
    static readonly Color CBdr    = Color.FromArgb(217, 217, 217); // #D9D9D9 - Divider/Border color
    static readonly Color CC      = Color.FromArgb(22, 32, 56);   // #162038 - Deep Navy 3 card container
    static readonly Color CGreen  = Color.FromArgb(30, 204, 145); // Teal / Green Accent
    static readonly Color CPurp   = Color.FromArgb(128, 102, 230); // Purple Accent


    private int SW => Scale(170); // Sidebar width (scaled)

    public MainWindow(TrayApp app)
    {
        _app = app;
        using (var g = CreateGraphics())
        {
            _scale = g.DpiX / 96f;
        }
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
        ClientSize      = new Size(Scale(800), Scale(480));
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
            if (e.CloseReason == CloseReason.UserClosing)
            {
                _app.Shutdown();
            }
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
        _sidebar.Controls.Add(new Label { Text = "F O L D",       Font = FontLoader.CreateFont(Scale(24f), FontStyle.Bold), ForeColor = Color.White, TextAlign = ContentAlignment.BottomCenter, Location = new Point(0, Scale(8)),  Size = new Size(SW, Scale(44)) });
        _sidebar.Controls.Add(new Label { Text = "by @idham.dev", Font = FontLoader.CreateFont(Scale(9.5f)), ForeColor = Color.White, TextAlign = ContentAlignment.TopCenter,    Location = new Point(0, Scale(52)), Size = new Size(SW, Scale(24)) });
        _sidebar.Controls.Add(new Panel { Location = new Point(0, Scale(82)),  Size = new Size(SW, Scale(1)), BackColor = CBdr });

        _navSet = NavBtn("Settings");  _navSet.Location = new Point(0, Scale(83));
        _navAdv = NavBtn("Advanced");  _navAdv.Location = new Point(0, Scale(140));
        _navAbt = NavBtn("About");     _navAbt.Location = new Point(0, Scale(197));

        _sidebar.Controls.Add(_navSet);
        _sidebar.Controls.Add(_navAdv);
        _sidebar.Controls.Add(_navAbt);
        _sidebar.Controls.Add(new Panel { Location = new Point(0, Scale(254)), Size = new Size(SW, Scale(1)), BackColor = CBdr });

        _navSet.Click += (_, _) => SelectNav(_navSet, _pgSettings);
        _navAdv.Click += (_, _) => SelectNav(_navAdv, _pgAdvanced);
        _navAbt.Click += (_, _) => SelectNav(_navAbt, _pgAbout);
    }

    Button NavBtn(string text)
    {
        var b = new Button
        {
            Text = text, FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(217, 217, 217), ForeColor = Color.White,
            Font = FontLoader.CreateFont(Scale(15f), FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand, Size = new Size(SW, Scale(57)),
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 200, 200);
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 180, 180);
        return b;
    }

    void SelectNav(Button active, Panel page)
    {
        foreach (var b in new[] { _navSet, _navAdv, _navAbt })
        {
            bool on = b == active;
            b.BackColor = on ? CActive : Color.FromArgb(217, 217, 217);
            b.ForeColor = Color.White;
            b.FlatAppearance.MouseOverBackColor = on ? CActive : Color.FromArgb(200, 200, 200);
            b.FlatAppearance.MouseDownBackColor = on ? CActive : Color.FromArgb(180, 180, 180);
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

    private struct MonitorComboItem
    {
        public IntPtr Handle { get; set; }
        public string Label { get; set; }
        public override string ToString() => Label;
    }

    private void PopulateMonitors()
    {
        _cmbMon.Items.Clear();
        var monitors = VirtualDisplay.VirtualDisplayManager.GetAllMonitors();
        
        int selectedIndex = 0;
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            _cmbMon.Items.Add(new MonitorComboItem { Handle = m.Handle, Label = m.Label });
            if (m.Handle == _app.SelectedMonitor)
            {
                selectedIndex = i;
            }
            else if (_app.SelectedMonitor == IntPtr.Zero && m.IsPrimary)
            {
                selectedIndex = i;
            }
        }

        if (_cmbMon.Items.Count > 0)
        {
            _cmbMon.SelectedIndex = selectedIndex;
            var item = (MonitorComboItem)_cmbMon.Items[selectedIndex];
            _pendingMonitorHandle = item.Handle;
            _pendingMonitorLabel = item.Label;
        }
    }

    public void UpdateResolutionDropdown()
    {
        if (_cmbRes == null) return;

        _cmbRes.Items.Clear();
        lock (_app.ResolutionOptions)
        {
            foreach (var opt in _app.ResolutionOptions)
            {
                _cmbRes.Items.Add(opt);
            }
        }

        int selectedIndex = 0;
        for (int i = 0; i < _app.ResolutionOptions.Count; i++)
        {
            var opt = _app.ResolutionOptions[i];
            if (opt.Width == _app.ForceWidth && opt.Height == _app.ForceHeight)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < _cmbRes.Items.Count)
        {
            _cmbRes.SelectedIndex = selectedIndex;
            _pendingResIndex = selectedIndex;
            _cmbRes.Invalidate();
        }
    }

    void BuildSettings()
    {
        var p  = _pgSettings;
        int lp = Scale(28);                         // left padding
        int cw = _host.Width - lp - Scale(28);     // content width

        // IP address
        _lblIp = new Label { Text = "IP address :", Font = FontLoader.CreateFont(Scale(12f), FontStyle.Bold), ForeColor = Color.White, Location = new Point(lp, Scale(20)), AutoSize = true };
        p.Controls.Add(_lblIp);

        // Monitor Header
        p.Controls.Add(new CircleDotHeader { Text = "Monitor", Font = FontLoader.CreateFont(Scale(16f), FontStyle.Bold), ForeColor = Color.White, Location = new Point(lp - Scale(2), Scale(65)), Size = new Size(cw, Scale(36)) });

        // Monitor Combobox
        _cmbMon = new FlatComboBox(CB, CBdr) { Location = new Point(lp, Scale(110)), Size = new Size(cw - Scale(60), Scale(44)), Font = FontLoader.CreateFont(Scale(11f)) };
        _cmbMon.SelectedIndexChanged += (_, _) =>
        {
            if (_cmbMon.SelectedIndex >= 0)
            {
                var item = (MonitorComboItem)_cmbMon.Items[_cmbMon.SelectedIndex];
                _pendingMonitorHandle = item.Handle;
                _pendingMonitorLabel = item.Label;
            }
        };
        p.Controls.Add(_cmbMon);

        // Refresh Button
        _btnRefresh = new RefreshButton(CBdr) { Location = new Point(lp + cw - Scale(48), Scale(110)), Size = new Size(Scale(44), Scale(44)) };
        _btnRefresh.Click += (_, _) => PopulateMonitors();
        p.Controls.Add(_btnRefresh);

        // Stream Quality Header
        p.Controls.Add(new CircleDotHeader { Text = "Stream Quality", Font = FontLoader.CreateFont(Scale(16f), FontStyle.Bold), ForeColor = Color.White, Location = new Point(lp - Scale(2), Scale(175)), Size = new Size(cw, Scale(36)) });

        // Stream Quality Dropdown
        _cmbRes = new FlatComboBox(CB, CBdr) { Location = new Point(lp, Scale(220)), Size = new Size(cw, Scale(44)), Font = FontLoader.CreateFont(Scale(11f)) };
        _cmbRes.SelectedIndexChanged += (_, _) =>
        {
            _pendingResIndex = _cmbRes.SelectedIndex;
        };
        p.Controls.Add(_cmbRes);
        UpdateResolutionDropdown();

        // Populate monitors initial list
        PopulateMonitors();

        // START & APPLY Buttons side by side
        int btnW = (cw - Scale(16)) / 2;
        _btnSS = BigBtn("START", CGreen, lp, Scale(295), btnW);
        _btnSS.Click += (_, _) => _app.ToggleStreaming(this);
        p.Controls.Add(_btnSS);

        _btnApply = BigBtn("APPLY", CPurp, lp + btnW + Scale(16), Scale(295), btnW);
        _btnApply.Click += (_, _) =>
        {
            bool wasRunning = _app.IsRunning;
            bool wasUsb = _app.IsUsbMode;
            if (wasRunning) _app.StopStreaming();

            // Set settings
            _app.SetMonitor(_pendingMonitorHandle, _pendingMonitorLabel);
            _app.SetResolutionIndex(_pendingResIndex);

            if (wasRunning) _app.StartStreaming();
            if (wasUsb) _app.EnableUsbMode();

            _app.ShowBalloon($"Settings applied. Monitor: {_pendingMonitorLabel}");
            RefreshStatus();
        };
        p.Controls.Add(_btnApply);
    }

    Button BigBtn(string text, Color bg, int x, int y, int w)
    {
        var b = new Button
        {
            Text = text, Location = new Point(x, y), Size = new Size(w, Scale(62)),
            FlatStyle = FlatStyle.Flat, BackColor = bg, ForeColor = Color.White,
            Font = FontLoader.CreateFont(Scale(20f), FontStyle.Bold), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 3;
        b.FlatAppearance.BorderColor = CBdr;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Min(bg.R + 20, 255), Math.Min(bg.G + 20, 255), Math.Min(bg.B + 20, 255));
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(Math.Max(bg.R - 20, 0), Math.Max(bg.G - 20, 0), Math.Max(bg.B - 20, 0));
        return b;
    }

    // ── Advanced ──────────────────────────────────────────────────────────

    void BuildAdvanced()
    {
        var p  = _pgAdvanced;
        int lp = Scale(22);
        int cw = _host.Width - lp * 2;

        // Card 1 — USB Mode (No card border matching Group 10 mockup)
        var c1 = new Panel { Location = new Point(lp, Scale(20)), Size = new Size(cw, Scale(150)), BackColor = CC };
        c1.Controls.Add(new Label { Text = "USB Mode (ADB)",   Font = FontLoader.CreateFont(Scale(18f), FontStyle.Bold), ForeColor = Color.White, Location = new Point(Scale(20), Scale(16)), AutoSize = true });
        c1.Controls.Add(new Label { Text = "Zero - latency via USB Cable. Requires USB Debugging on the device.", Font = FontLoader.CreateFont(Scale(10f)), ForeColor = Color.FromArgb(217, 217, 217), Location = new Point(Scale(20), Scale(60)), Size = new Size(cw - Scale(180), Scale(75)) });
        _btnUsb = AdvBtn("ENABLE", CGreen, cw - Scale(145), Scale(45), Scale(125), Scale(45));
        _btnUsb.Click += (_, _) => _app.ToggleUsbMode(this);
        c1.Controls.Add(_btnUsb);
        p.Controls.Add(c1);

        // Card 2 — Virtual Display Driver (No card border matching Group 10 mockup)
        bool driverInstalled = VirtualDisplay.VirtualDisplayManager.IsVirtualDriverInstalled();
        var c2 = new Panel { Location = new Point(lp, Scale(190)), Size = new Size(cw, Scale(150)), BackColor = CC };
        c2.Controls.Add(new Label { Text = "Virtual Display Driver", Font = FontLoader.CreateFont(Scale(18f), FontStyle.Bold), ForeColor = Color.White, Location = new Point(Scale(20), Scale(16)), AutoSize = true });

        var statusLabel = new Label
        {
            Text = driverInstalled
                ? "✓  Driver installed. Extends your desktop with a virtual second screen."
                : "Adds a Virtual Monitor so you can use extended display mode.\nWithout this driver you can only mirror the Primary Display.",
            Font = FontLoader.CreateFont(Scale(10f)),
            ForeColor = driverInstalled ? Color.FromArgb(30, 204, 145) : Color.FromArgb(217, 217, 217),
            Location = new Point(Scale(20), Scale(60)),
            Size = new Size(cw - Scale(180), Scale(75))
        };
        c2.Controls.Add(statusLabel);

        var vBtn = AdvBtn(
            driverInstalled ? "REINSTALL" : "INSTALL",
            driverInstalled ? CGreen : CPurp,
            cw - Scale(145), Scale(45), Scale(125), Scale(45));
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
            Font = FontLoader.CreateFont(Scale(11f), FontStyle.Bold), Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 3;
        b.FlatAppearance.BorderColor = CBdr;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Min(bg.R + 20, 255), Math.Min(bg.G + 20, 255), Math.Min(bg.B + 20, 255));
        b.FlatAppearance.MouseDownBackColor = Color.FromArgb(Math.Max(bg.R - 20, 0), Math.Max(bg.G - 20, 0), Math.Max(bg.B - 20, 0));
        return b;
    }

    // ── About ─────────────────────────────────────────────────────────────

    void BuildAbout()
    {
        var p  = _pgAbout;
        int lp = Scale(22);
        int cw = _host.Width - lp * 2;

        var card = new AboutCard(CBdr) { Location = new Point(lp, Scale(20)), Size = new Size(cw, Scale(320)), BackColor = CC };
        card.Controls.Add(new Label
        {
            Text =
                "FOLD is a lightweight, high-performance utility designed to seamlessly expand your digital workspace by transforming any standard Android tablet into a fully functional second display.\n\n" +
                "Engineered for efficiency, FOLD utilizes H.264 hardware-accelerated streaming to deliver a crisp, lag-free visual experience. It offers flexible connectivity, allowing users to choose between the wireless convenience of a Wi-Fi connection or the rock-solid, zero-latency stability of a direct USB link.\n\n" +
                "Built on a robust technical foundation that includes Sdcb.FFmpeg, Android MediaCodec, and ADB, the application provides a smooth and reliable screen extension. Whether you need extra real estate for monitoring live deployments, managing content strategies, or simply keeping your workspace organized, FOLD bridges the gap between mobile hardware and desktop productivity.",
            Font = FontLoader.CreateFont(Scale(8.0f)), ForeColor = Color.FromArgb(217, 217, 217),
            Location = new Point(Scale(26), Scale(102)), Size = new Size(cw - Scale(52), Scale(206))
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

        // Border (3px matching Group 9)
        using var bp = new Pen(_border, 3f);
        g.DrawRectangle(bp, 1.5f, 1.5f, Width - 3f, Height - 3f);

        // Text
        string txt = SelectedText;
        using var tf = new SolidBrush(Color.White);
        var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
        g.DrawString(txt, Font, tf, new RectangleF(12, 0, Width - 50, Height), fmt);

        // Custom chevron arrow ▼
        int ax = Width - 30;
        int ay = Height / 2 - 4;
        using var ap = new Pen(Color.White, 2f);
        g.DrawLine(ap, ax, ay,     ax + 6, ay + 6);
        g.DrawLine(ap, ax + 6, ay + 6, ax + 12, ay);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_dropped) CloseDropdown(); else OpenDropdown();
    }

    void OpenDropdown()
    {
        _dropped = true;
        int itemH = Height - 8;

        _dropPanel = new Panel
        {
            BackColor = Color.FromArgb(22, 32, 56), // Deep Navy 3
            Size      = new Size(Width, Items.Count * itemH),
            BorderStyle = BorderStyle.None
        };
        _dropPanel.Paint += (s, pe) => { using var pen = new Pen(_border, 3f); pe.Graphics.DrawRectangle(pen, 1.5f, 1.5f, _dropPanel.Width - 3f, _dropPanel.Height - 3f); };

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
                BackColor = idx == SelectedIndex ? Color.FromArgb(56, 56, 56) : Color.FromArgb(22, 32, 56),
                ForeColor = Color.White,
                Font      = Font,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(12, 0, 0, 0),
                Size      = new Size(Width, itemH),
                Location  = new Point(0, idx * itemH),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, 56, 56);
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
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true;  Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color bg = _hover ? Color.FromArgb(56, 56, 56) : Color.FromArgb(22, 32, 56);
        using var bgBr = new SolidBrush(bg);
        g.FillRectangle(bgBr, 0, 0, Width, Height);

        using var bp = new Pen(_border, 3f);
        g.DrawRectangle(bp, 1.5f, 1.5f, Width - 3f, Height - 3f);

        // Draw circular arrow using arc + arrowhead
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int m = 12;
        var rect = new Rectangle(m, m, Width - m * 2, Height - m * 2);

        using var ap = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.ArrowAnchor };
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

        // Draw white circle outline (ring, 3px thick matching Group 9)
        using var pen = new Pen(Color.White, 3f);
        g.DrawEllipse(pen, 4, Height / 2 - 8, 16, 16);

        using var br = new SolidBrush(ForeColor);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using var fontLalezar = FontLoader.CreateFont(Font.Size, FontStyle.Bold);
        g.DrawString(Text, fontLalezar, br, 28, Height / 2 - fontLalezar.Height / 2 - 1);
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

        // Draw 12px thick light gray border matching Group 11 mockup
        using var pen = new Pen(Color.FromArgb(217, 217, 217), 12f);
        g.DrawRectangle(pen, 6f, 6f, Width - 12f, Height - 12f);

        using var fBig   = FontLoader.CreateFont(26f, FontStyle.Bold);
        using var brush  = new SolidBrush(Color.White);

        string p1 = "F  O  L  D";
        string p2 = "  by @idham.dev";

        using var fSmall = FontLoader.CreateFont(10f);

        var sf = StringFormat.GenericTypographic;
        var s1 = g.MeasureString(p1, fBig, PointF.Empty, sf);
        var s2 = g.MeasureString(p2, fSmall, PointF.Empty, sf);

        float sx = (Width - s1.Width - s2.Width) / 2f;
        float y1 = 32f;

        // Mathematically align the baselines using font metrics
        float em1 = fBig.FontFamily.GetEmHeight(fBig.Style);
        float asc1 = fBig.FontFamily.GetCellAscent(fBig.Style);
        float ascent1_px = (fBig.Size * asc1 / em1) * (g.DpiY / 72f);

        float em2 = fSmall.FontFamily.GetEmHeight(fSmall.Style);
        float asc2 = fSmall.FontFamily.GetCellAscent(fSmall.Style);
        float ascent2_px = (fSmall.Size * asc2 / em2) * (g.DpiY / 72f);

        float y2 = y1 + (ascent1_px - ascent2_px);

        g.DrawString(p1, fBig,   brush, sx, y1, sf);
        g.DrawString(p2, fSmall, brush, sx + s1.Width, y2, sf);

        using var lp = new Pen(Color.FromArgb(217, 217, 217), 1f);
        g.DrawLine(lp, 26, 88, Width - 26, 88);
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

    public static Font CreateFont(float size, FontStyle style = FontStyle.Regular)
    {
        try
        {
            var fam = Lalezar;
            if (fam != null && fam.IsStyleAvailable(style))
                return new Font(fam, size, style);
            if (fam != null && fam.IsStyleAvailable(FontStyle.Regular))
                return new Font(fam, size, FontStyle.Regular);
        }
        catch { }
        return new Font("Segoe UI", size, style);
    }

    static void Load()
    {
        try
        {
            _pfc = new PrivateFontCollection();
            var asm = Assembly.GetExecutingAssembly();
            
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FOLDHost");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(tempDir, "fonts"));
            string tempFontPath = System.IO.Path.Combine(tempDir, "fonts", "Lalezar-Regular.ttf");

            if (!System.IO.File.Exists(tempFontPath))
            {
                using var s = asm.GetManifestResourceStream("FOLD.Resources.Lalezar-Regular.ttf");
                if (s != null)
                {
                    using var fs = new System.IO.FileStream(tempFontPath, System.IO.FileMode.Create, System.IO.FileAccess.Write);
                    s.CopyTo(fs);
                }
            }

            if (System.IO.File.Exists(tempFontPath))
            {
                _pfc.AddFontFile(tempFontPath);
                if (_pfc.Families.Length > 0)
                {
                    _fam = _pfc.Families[0];
                }
            }
        }
        catch { }
    }
}
