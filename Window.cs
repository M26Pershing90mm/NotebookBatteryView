using System;
using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class ViewStyle
{
    public const string Copyright = "Copyright © 2026 Sakai. All rights reserved.";
    public static string FontName { get { return L.Language == 2 ? "Meiryo" : L.Language == 0 ? "Malgun Gothic" : "Segoe UI"; } }
    public static int LineHeight(Font font)
    {
        return Math.Max(font.Height, TextRenderer.MeasureText("Ag100% " + L.T("unknown"), font,
            new Size(10000, 10000), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height);
    }
}

internal sealed class BatteryGauge : Control
{
    private double? value;
    public double? Value { get { return value; } set { this.value = value; Invalidate(); } }
    public Color Accent = Color.FromArgb(40, 144, 117);
    public static int PreferredHeight
    {
        get { using (Font font = new Font(ViewStyle.FontName, 22, FontStyle.Bold)) return Math.Max(70, ViewStyle.LineHeight(font) + 40); }
    }
    public BatteryGauge()
    {
        DoubleBuffered = true;
        Height = 70;
        BackColor = Color.White;
        AccessibleRole = AccessibleRole.Indicator;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (Font font = new Font(ViewStyle.FontName, 22, FontStyle.Bold))
        {
        int line = ViewStyle.LineHeight(font);
        int top = line + 10;
        Rectangle bar = new Rectangle(5, top, Math.Max(1, Width - 10), Math.Max(1, Height - top - 6));
        using (Brush track = new SolidBrush(Color.FromArgb(230, 236, 241))) e.Graphics.FillRectangle(track, bar);
        if (value.HasValue)
        {
            Rectangle fill = bar;
            fill.Width = (int)(bar.Width * Math.Max(0, Math.Min(100, value.Value)) / 100);
            using (Brush brush = new SolidBrush(Accent)) e.Graphics.FillRectangle(brush, fill);
        }
        string text = value.HasValue ? value.Value.ToString("0.#", CultureInfo.CurrentCulture) + "%" : L.T("unknown");
            TextRenderer.DrawText(e.Graphics, text, font, new Rectangle(5, 0, Width - 10, line + 4),
                Color.FromArgb(25, 46, 65), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        AccessibleName = text;
        }
    }
}

internal sealed class BatteryWindow : Form
{
    private readonly BatteryGauge charge = new BatteryGauge();
    private readonly BatteryGauge health = new BatteryGauge();
    private readonly Label power = new Label();
    private readonly Label duration = new Label();
    private readonly Label details = new Label();
    private readonly SessionHistory history = new SessionHistory();
    private HistoryWindow historyWindow;
    private readonly Label advice = new Label();
    private readonly Label message = new Label();
    private readonly Label updated = new Label();
    private readonly ComboBox batteries = new ComboBox();
    private readonly Button refresh = new Button();
    private readonly Timer timer = new Timer();
    private readonly ComboBox intervals = new ComboBox();
    private readonly int[] intervalSeconds = { 0, 30, 60, 90, 120, 300 };
    private readonly Stopwatch liveClock = Stopwatch.StartNew();
    private readonly Stopwatch voltageClock = Stopwatch.StartNew();
    private bool statusOnly;
    private DateTime lastVoltage = DateTime.MinValue;
    private Task<ReadResult> pending;
    private DateTime queryStarted;
    private DateTime lastRead = DateTime.MinValue;
    private bool delayed;

    public BatteryWindow()
    {
        Text = "BatteryView " + L.Version + " · " + L.T("title");
        Font = new Font(ViewStyle.FontName, 10);
        BackColor = Color.FromArgb(242, 246, 249);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(790, 820);
        MinimumSize = new Size(600, 420);
        StartPosition = FormStartPosition.CenterScreen;
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        Controls.Add(root);
        Panel viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        TableLayoutPanel layout = new TableLayoutPanel {
            Dock = DockStyle.Top, Padding = new Padding(16, 8, 16, 8), ColumnCount = 1, RowCount = 0,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Font = Font
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.SizeChanged += delegate { FitLabels(layout); };
        viewport.Controls.Add(layout);
        root.Controls.Add(viewport, 0, 1);
        Label footer = MakeLabel(L.T("author") + " · " + L.T("copyright"), 9, FontStyle.Regular);
        footer.Dock = DockStyle.Fill;
        footer.Padding = new Padding(20, 3, 20, 3);
        footer.BackColor = Color.FromArgb(226, 234, 241);
        footer.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(footer, 0, 4);
        Add(layout, MakeLabel(L.T("scope"), 9, FontStyle.Regular), 26);
        Add(layout, MakeLabel(L.T("charge"), 12, FontStyle.Bold), 26);
        Add(layout, charge, BatteryGauge.PreferredHeight);
        Add(layout, power, 24);
        Add(layout, duration, 24);
        batteries.DropDownStyle = ComboBoxStyle.DropDownList;
        batteries.AccessibleName = L.T("select");
        batteries.SelectedIndexChanged += delegate { ShowBattery(); };
        Add(layout, batteries, 34);
        Add(layout, MakeLabel(L.T("health_compact"), 11, FontStyle.Bold), 26);
        Add(layout, health, BatteryGauge.PreferredHeight);
        Add(layout, advice, 36);
        Add(layout, details, 0);
        TableLayoutPanel intervalBar = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            Padding = new Padding(20, 8, 20, 8)
        };
        intervalBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        intervalBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Label intervalLabel = MakeLabel(L.T("interval_label"), 10, FontStyle.Regular);
        intervalLabel.AutoSize = true;
        intervalLabel.Anchor = AnchorStyles.Left;
        intervalLabel.Margin = new Padding(0, 0, 12, 0);
        intervalBar.Controls.Add(intervalLabel, 0, 0);
        root.Controls.Add(intervalBar, 0, 0);
        intervals.DropDownStyle = ComboBoxStyle.DropDownList;
        intervals.Dock = DockStyle.Fill;
        intervals.IntegralHeight = true;
        intervals.MaxDropDownItems = intervalSeconds.Length;
        intervals.DropDownHeight = Math.Max(160, intervals.ItemHeight * intervalSeconds.Length + 8);
        intervals.TabIndex = 0;
        intervals.AccessibleName = L.T("interval_label");
        foreach (string key in new string[] { "interval_manual", "interval_30", "interval_60", "interval_90", "interval_120", "interval_300" })
            intervals.Items.Add(L.T(key));
        intervals.SelectedIndex = 1;
        intervals.SelectedIndexChanged += delegate { voltageClock.Restart(); };
        intervalBar.Controls.Add(intervals, 1, 0);
        refresh.Text = L.T("refresh");
        refresh.Click += delegate { BeginRead(); };
        Button shortcut = new Button { Text = L.T("shortcut") };
        shortcut.Click += delegate
        {
            string result = DesktopShortcut.Create();
            if (result != "shortcut_done") ErrorLog.Write(result);
            MessageBox.Show(L.T(result), "BatteryView", MessageBoxButtons.OK,
                result == "shortcut_done" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        };
        TableLayoutPanel actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(20, 2, 20, 2) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        Button support = new Button { Text = L.T("support"), Dock = DockStyle.Fill };
        support.Click += delegate
        {
            if (!SupportLink.Open())
            {
                ErrorLog.Write("support_error");
                MessageBox.Show(L.T("support_error"), "BatteryView", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        refresh.Dock = DockStyle.Fill;
        shortcut.Dock = DockStyle.Fill;
        actions.Controls.Add(refresh, 0, 0);
        actions.Controls.Add(shortcut, 1, 0);
        actions.Controls.Add(support, 2, 0);
        root.Controls.Add(actions, 0, 2);
        TableLayoutPanel tools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(20, 2, 20, 2) };
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Button historyButton = new Button { Text = L.T("history"), Dock = DockStyle.Fill };
        historyButton.Click += delegate {
            if (historyWindow == null || historyWindow.IsDisposed) historyWindow = new HistoryWindow(history);
            if (!historyWindow.Visible) historyWindow.Show(this);
            historyWindow.BringToFront();
        };
        CheckBox pin = new CheckBox { Text = L.T("pin"), Dock = DockStyle.Fill };
        pin.CheckedChanged += delegate { TopMost = pin.Checked; };
        tools.Controls.Add(historyButton, 0, 0); tools.Controls.Add(pin, 1, 0);
        root.Controls.Add(tools, 0, 3);
        Add(layout, message, 42);
        Add(layout, updated, 62);
        timer.Interval = 250;
        timer.Tick += delegate { Tick(); };
        Shown += delegate
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
            ShowBattery(); power.Text = L.T("power_unknown"); duration.Text = L.T("remaining_unknown"); UpdateStamp(); BeginRead(); timer.Start();
        };
        FormClosed += delegate { timer.Stop(); timer.Dispose(); };
    }

    private static Label MakeLabel(string text, float size, FontStyle style)
    {
        return new Label { Text = text, Font = new Font(ViewStyle.FontName, size, style) };
    }

    private static void FitLabels(TableLayoutPanel layout)
    {
        int width = Math.Max(160, layout.ClientSize.Width - layout.Padding.Horizontal);
        foreach (Control control in layout.Controls)
        {
            Label label = control as Label;
            if (label != null && label.MaximumSize.Width != width) label.MaximumSize = new Size(width, 0);
        }
    }

    private static void Add(TableLayoutPanel layout, Control control, int height)
    {
        if (control.Font.Equals(Control.DefaultFont)) control.Font = layout.Font;
        int row = layout.RowCount++;
        Label label = control as Label;
        if (label != null)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.TopLeft;
            label.AutoEllipsis = false;
            label.UseCompatibleTextRendering = false;
            label.MaximumSize = new Size(Math.Max(160, layout.ClientSize.Width - layout.Padding.Horizontal), 0);
            label.Dock = DockStyle.Top;
        }
        else
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            control.Dock = DockStyle.Fill;
        }
        control.Margin = new Padding(0, 3, 0, 3);
        layout.Controls.Add(control, 0, row);
    }

    private void UpdateLive()
    {
        try
        {
            PowerStatus status = SystemInformation.PowerStatus;
            BatteryChargeStatus flags = status.BatteryChargeStatus;
            bool unknown = flags == BatteryChargeStatus.Unknown;
            bool absent = !unknown && (flags & BatteryChargeStatus.NoSystemBattery) != 0;
            charge.Value = absent ? null : BatteryMath.Charge(status.BatteryLifePercent);
            charge.Accent = charge.Value.HasValue && charge.Value < 20 ? Color.FromArgb(200, 82, 64) : Color.FromArgb(40, 144, 117);
            charge.Invalidate();
            if (status.PowerLineStatus == PowerLineStatus.Online) power.Text = L.T("plugged");
            else if (absent) power.Text = L.T("no_battery");
            else if (status.PowerLineStatus == PowerLineStatus.Offline) power.Text = L.T("on_battery");
            else power.Text = L.T("power_unknown");
            int seconds = status.BatteryLifeRemaining;
            duration.Text = !absent && status.PowerLineStatus == PowerLineStatus.Offline && seconds >= 0 ?
                L.F("remaining", seconds / 3600, (seconds % 3600) / 60) : L.T("remaining_unknown");
            UpdateStamp();
        }
        catch
        {
            charge.Value = null;
            power.Text = L.T("live_error");
            duration.Text = L.T("remaining_unknown");
            updated.Text = L.T("live_failed");
            ErrorLog.Write("live_error");
        }
    }

    private void UpdateStamp()
    {
        updated.Text = L.F("updated_details", lastRead == DateTime.MinValue ? L.T("unknown") : lastRead.ToString("HH:mm:ss")) +
            "\n" + L.F("updated_voltage", lastVoltage == DateTime.MinValue ? L.T("unknown") : lastVoltage.ToString("HH:mm:ss"));
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0x0218 && m.WParam.ToInt64() == 0x000A && !IsDisposed)
        {
            UpdateLive();
            liveClock.Restart();
        }
    }

    private void BeginRead() { BeginRead(false); }

    private void BeginRead(bool voltageOnly)
    {
        if (pending != null) return;
        UpdateLive();
        refresh.Enabled = false;
        message.Text = L.T("reading");
        message.Visible = !voltageOnly;
        queryStarted = DateTime.Now;
        delayed = false;
        statusOnly = voltageOnly;
        pending = Task.Factory.StartNew<ReadResult>(voltageOnly ?
            (Func<ReadResult>)BatteryReader.ReadStatus : BatteryReader.Read);
        timer.Start();
    }

    private void Tick()
    {
        if (liveClock.ElapsedMilliseconds >= 1000)
        {
            UpdateLive();
            liveClock.Restart();
        }
        if (pending != null && pending.IsCompleted)
        {
            BatteryRecord selected = batteries.SelectedItem as BatteryRecord;
            string selectedInstance = selected == null ? null : selected.Instance;
            ReadResult result;
            try { result = pending.Result; }
            catch { result = new ReadResult { Message = L.T("detail_error") }; ErrorLog.Write("detail_error"); }
            pending = null;
            if (statusOnly)
            {
                foreach (BatteryRecord battery in batteries.Items)
                {
                    BatteryRecord match = null;
                    int matches = 0;
                    foreach (BatteryRecord current in result.Batteries)
                        if (!String.IsNullOrEmpty(battery.Instance) && current.Instance == battery.Instance)
                        { match = current; matches++; }
                    bool valid = matches == 1 && (!battery.Tag.HasValue || !match.Tag.HasValue || battery.Tag == match.Tag);
                    battery.Millivolts = valid ? match.Millivolts : null;
                    battery.Charging = valid ? match.Charging : null;
                    battery.Discharging = valid ? match.Discharging : null;
                    battery.Remaining = valid ? match.Remaining : null;
                    battery.ChargeRate = valid ? match.ChargeRate : null;
                    battery.DischargeRate = valid ? match.DischargeRate : null;
                    battery.StatusTime = valid ? match.StatusTime : DateTime.MinValue;
                }
            }
            else
            {
                lastRead = DateTime.Now;
                delayed = false;
                batteries.Items.Clear();
                foreach (BatteryRecord battery in result.Batteries) batteries.Items.Add(battery);
                if (batteries.Items.Count > 0)
                {
                    batteries.SelectedIndex = 0;
                    for (int i = 0; i < batteries.Items.Count; i++)
                        if (((BatteryRecord)batteries.Items[i]).Instance == selectedInstance) batteries.SelectedIndex = i;
                }
            }
            foreach (BatteryRecord battery in batteries.Items)
                if (battery.StatusTime != DateTime.MinValue) history.Add(battery);
            if (historyWindow != null && !historyWindow.IsDisposed) historyWindow.Reload();
            foreach (BatteryRecord b in batteries.Items)
                if (b.StatusTime > lastVoltage) lastVoltage = b.StatusTime;
            voltageClock.Restart();
            delayed = false;
            ShowBattery();
            message.Text = result.Message;
            message.Visible = result.Message != L.T("read_ok") && result.Message != L.T("voltage_ok");
            refresh.Enabled = true;
            UpdateStamp();

        }
        if (pending != null && !delayed && DateTime.Now - queryStarted > TimeSpan.FromSeconds(15))
        {
            delayed = true;
            ShowBattery();
            advice.Text = L.T("slow");
            message.Text = L.T("waiting");
            message.Visible = true;
            ErrorLog.Write("slow");
        }
        int seconds = intervalSeconds[Math.Max(0, intervals.SelectedIndex)];
        if (pending == null && seconds > 0 && voltageClock.Elapsed >= TimeSpan.FromSeconds(seconds))
            BeginRead(batteries.Items.Count > 0);
    }

    private static string Capacity(ulong? value, bool absolute)
    {
        if (!value.HasValue || value.Value == 0 || value.Value >= UInt32.MaxValue) return L.T("unknown");
        return value.Value.ToString("N0") + (absolute ? " mWh" : " (" + L.T("driver_units") + ")");
    }

    private static string Volts(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.000") + " V" : L.T("unknown");
    }

    private void ShowBattery()
    {
        BatteryRecord battery = delayed && !statusOnly ? null : batteries.SelectedItem as BatteryRecord;
        batteries.Enabled = batteries.Items.Count > 0;
        double? value = battery == null ? null : BatteryMath.Health(battery.Design, battery.Full);
        health.Value = value;
        health.Accent = value.HasValue && value.Value < 80 ? Color.FromArgb(196, 128, 30) : Color.FromArgb(58, 112, 186);
        health.Invalidate();
        advice.Text = BatteryMath.Advice(value);
        double? wear = PackMetrics.Wear(value);
        details.Text = L.F("capacity_design", battery == null ? L.T("unknown") : Capacity(battery.Design, battery.AbsoluteUnits)) +
            "\n" + L.F("capacity_full", battery == null ? L.T("unknown") : Capacity(battery.Full, battery.AbsoluteUnits)) +
            "\n" + L.F("cycles", battery != null && battery.Cycles.HasValue ? L.F("cycle_count", battery.Cycles.Value.ToString("N0")) : L.T("unknown")) +
            "\n" + L.F("voltage_current", Volts(battery == null ? null : BatteryMath.Voltage(battery.Millivolts))) +
            "\n" + L.F("wear", wear.HasValue ? wear.Value.ToString("0.#") + "%" : L.T("unknown")) +
            "\n" + L.F("pack_remaining", battery == null ? L.T("unknown") : PackMetrics.RemainingText(battery)) +
            "\n" + L.F("pack_rate", battery == null ? L.T("unknown") : PackMetrics.RateText(battery)) +
            "\n" + L.F("pack_runtime", PackMetrics.RuntimeText(battery));
    }
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
        {
            StartupFailure.Report(args.ExceptionObject as Exception ?? new Exception("Unhandled runtime error"));
        };
        try { RunUI(); }
        catch (Exception error) { StartupFailure.Report(error); }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void RunUI()
    {
        L.SetCulture();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs args)
        {
            ErrorLog.Write("ui_error");
            StartupFailure.Report(args.Exception);
        };
        Application.Run(new BatteryWindow());
    }
}
