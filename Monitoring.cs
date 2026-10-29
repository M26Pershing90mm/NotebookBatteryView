using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal static class PackMetrics
{
    public static bool CapacityValid(ulong? value) { return value.HasValue && value.Value < UInt32.MaxValue; }
    public static double? Wear(double? health)
    {
        return health.HasValue && health.Value <= 100 ? (double?)(100 - health.Value) : null;
    }
    public static double? Percent(BatteryRecord b)
    {
        if (b == null || !CapacityValid(b.Remaining) || !CapacityValid(b.Full) || b.Full == 0 || b.Remaining > b.Full) return null;
        return 100.0 * b.Remaining.Value / b.Full.Value;
    }
    public static double? SignedRate(BatteryRecord b)
    {
        if (b == null || !b.Charging.HasValue || !b.Discharging.HasValue || b.Charging == b.Discharging) return null;
        ulong? rate = b.Charging == true ? b.ChargeRate : b.DischargeRate;
        if (!rate.HasValue || rate.Value >= 0x80000000UL) return null;
        return b.Charging == true ? (double?)rate.Value : -(double)rate.Value;
    }
    public static double? Watts(BatteryRecord b)
    {
        double? rate = SignedRate(b);
        return b != null && b.AbsoluteUnits && rate.HasValue ? (double?)(rate.Value / 1000.0) : null;
    }
    public static double? RuntimeMinutes(BatteryRecord b)
    {
        double? rate = SignedRate(b);
        if (b == null || b.Discharging != true || !rate.HasValue || rate.Value >= 0 || !CapacityValid(b.Remaining)) return null;
        double minutes = 60.0 * b.Remaining.Value / -rate.Value;
        return minutes <= 10080 ? (double?)minutes : null;
    }
    public static string RuntimeText(BatteryRecord b)
    {
        double? minutes = RuntimeMinutes(b);
        if (!minutes.HasValue) return L.T("unknown");
        int whole = (int)Math.Floor(minutes.Value);
        return L.F("duration_value", whole / 60, whole % 60);
    }
    public static string RemainingText(BatteryRecord b)
    {
        if (!CapacityValid(b.Remaining)) return L.T("unknown");
        double? percent = Percent(b);
        return b.Remaining.Value.ToString("N0") + (b.AbsoluteUnits ? " mWh" : " (" + L.T("driver_units") + ")") +
            (percent.HasValue ? " · " + percent.Value.ToString("0.#") + "%" : "");
    }
    public static string State(BatteryRecord b)
    {
        if (!b.Charging.HasValue || !b.Discharging.HasValue || b.Charging == true && b.Discharging == true) return "unknown";
        return b.Charging == true ? "charging" : b.Discharging == true ? "discharging" : "idle";
    }
    public static string RateText(BatteryRecord b)
    {
        double? watts = Watts(b);
        if (!watts.HasValue) return L.T("unknown");
        return (Math.Abs(watts.Value)).ToString("0.00") + " W · " + L.T(State(b));
    }
}

internal sealed class BatterySample
{
    public long Series;
    public string Name;
    public DateTime Time;
    public DateTime DetailsTime;
    public double ElapsedSeconds;
    public string State;
    public double? Percent, Watts, Voltage, Health;
    public ulong? Remaining, Cycles, Design, Full;
    public bool Absolute;
}

internal sealed class SessionHistory
{
    public const int Limit = 2000;
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly List<BatterySample> samples = new List<BatterySample>();
    private readonly Dictionary<string, long> series = new Dictionary<string, long>(StringComparer.Ordinal);
    private long nextSeries;
    public IList<BatterySample> Samples { get { return samples.AsReadOnly(); } }
    public void Add(BatteryRecord b)
    {
        if (b == null || b.StatusTime == DateTime.MinValue || String.IsNullOrEmpty(b.Instance) || b.Instance.Length > 4096) return;
        string key = b.Instance.Length.ToString(CultureInfo.InvariantCulture) + ":" + b.Instance + ":" +
            (b.Tag.HasValue ? b.Tag.Value.ToString(CultureInfo.InvariantCulture) : "?");
        long id;
        if (!series.TryGetValue(key, out id)) { id = ++nextSeries; series.Add(key, id); }
        samples.Add(new BatterySample { Series = id, Name = DeviceText.Clean(b.Name, 160), Time = b.StatusTime, DetailsTime = b.DetailsTime,
            ElapsedSeconds = clock.Elapsed.TotalSeconds, Design = b.Design, Full = b.Full,
            State = PackMetrics.State(b), Percent = PackMetrics.Percent(b), Watts = PackMetrics.Watts(b),
            Voltage = BatteryMath.Voltage(b.Millivolts), Health = BatteryMath.Health(b.Design, b.Full),
            Remaining = PackMetrics.CapacityValid(b.Remaining) ? b.Remaining : null,
            Cycles = b.Cycles, Absolute = b.AbsoluteUnits });
        if (samples.Count > Limit)
        {
            long expired = samples[0].Series;
            samples.RemoveAt(0);
            bool retained = false;
            foreach (BatterySample sample in samples) if (sample.Series == expired) { retained = true; break; }
            if (!retained)
            {
                string expiredKey = null;
                foreach (KeyValuePair<string, long> pair in series) if (pair.Value == expired) { expiredKey = pair.Key; break; }
                if (expiredKey != null) series.Remove(expiredKey);
            }
        }
    }
    public void Clear() { samples.Clear(); series.Clear(); nextSeries = 0; }
    internal static string Cell(string value)
    {
        value = DeviceText.Clean(value, 512).Trim();
        string probe = value.Normalize(NormalizationForm.FormKC).TrimStart();
        if (probe.Length > 0 && "=+-@".IndexOf(probe[0]) >= 0) value = "text: " + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string Numeric(double? value) { return value.HasValue ? value.Value.ToString("0.######", CultureInfo.InvariantCulture) : ""; }
    public void WriteCsv(string path)
    {
        StringBuilder csv = new StringBuilder("sample_time,battery_series,battery_name,state,charge_percent,rate_w,voltage_v,remaining_capacity,capacity_units,design_capacity,full_charge_capacity,health_percent,cycles,details_time\r\n");
        foreach (BatterySample s in samples)
        {
            string[] row = { Cell(s.Time.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture)),
                s.Series.ToString(CultureInfo.InvariantCulture), Cell(s.Name), Cell(s.State), Numeric(s.Percent), Numeric(s.Watts), Numeric(s.Voltage),
                s.Remaining.HasValue ? s.Remaining.Value.ToString(CultureInfo.InvariantCulture) : "", Cell(s.Absolute ? "mWh" : "driver_units"),
                PackMetrics.CapacityValid(s.Design) && s.Design > 0 ? s.Design.Value.ToString(CultureInfo.InvariantCulture) : "",
                PackMetrics.CapacityValid(s.Full) && s.Full > 0 ? s.Full.Value.ToString(CultureInfo.InvariantCulture) : "",
                Numeric(s.Health), s.Cycles.HasValue ? s.Cycles.Value.ToString(CultureInfo.InvariantCulture) : "",
                Cell(s.DetailsTime == DateTime.MinValue ? "" : s.DetailsTime.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture)) };
            csv.Append(String.Join(",", row)).Append("\r\n");
        }
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        bool owned = false;
        try
        {
            SafeFiles.RejectLinkedTarget(path);
            using (FileStream stream = SafeFiles.CreateExclusive(temp))
            {
                owned = true;
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(true))) writer.Write(csv.ToString());
            }
            SafeFiles.RejectLinkedTarget(path);
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        finally { if (owned && File.Exists(temp)) File.Delete(temp); }
    }
}

internal sealed class HistoryGraph : Control
{
    public List<BatterySample> Samples = new List<BatterySample>();
    public bool Rate;
    public HistoryGraph() { DoubleBuffered = true; BackColor = Color.White; AccessibleRole = AccessibleRole.Chart; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 200 || Height < 100) return;
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle area = new Rectangle(62, 30, Width - 82, Height - 62);
        TextRenderer.DrawText(g, L.T(Rate ? "graph_rate" : "graph_charge"), Font, new Point(6, 5), ForeColor);
        double bound = 1;
        if (Rate) foreach (BatterySample s in Samples) if (s.Watts.HasValue) bound = Math.Max(bound, Math.Abs(s.Watts.Value));
        bound = Math.Ceiling(bound * 1.1);
        double min = Rate ? -bound : 0, max = Rate ? bound : 100;
        using (Pen grid = new Pen(Color.FromArgb(224, 231, 237)))
        for (int i = 0; i <= 4; i++)
        {
            int y = area.Top + area.Height * i / 4;
            g.DrawLine(grid, area.Left, y, area.Right, y);
            TextRenderer.DrawText(g, (max - (max - min) * i / 4).ToString("0.#"), Font,
                new Rectangle(0, y - Font.Height / 2, 58, Font.Height + 3), ForeColor, TextFormatFlags.Right);
        }
        if (Samples.Count == 0) return;
        DateTime start = Samples[0].Time, end = Samples[Samples.Count - 1].Time;
        double begin = Samples[0].ElapsedSeconds;
        double span = Math.Max(1, Samples[Samples.Count - 1].ElapsedSeconds - begin);
        PointF? previous = null;
        double previousTime = 0;
        using (Pen line = new Pen(Rate ? Color.FromArgb(60, 112, 186) : Color.FromArgb(40, 144, 117), 2))
        using (Brush dot = new SolidBrush(line.Color))
        foreach (BatterySample s in Samples)
        {
            double? value = Rate ? s.Watts : s.Percent;
            if (!value.HasValue) { previous = null; continue; }
            PointF p = new PointF(area.Left + (float)((s.ElapsedSeconds - begin) / span * area.Width),
                area.Bottom - (float)((value.Value - min) / (max - min) * area.Height));
            if (previous.HasValue && s.ElapsedSeconds >= previousTime && s.ElapsedSeconds - previousTime <= 660) g.DrawLine(line, previous.Value, p);
            g.FillEllipse(dot, p.X - 2, p.Y - 2, 4, 4);
            previous = p; previousTime = s.ElapsedSeconds;
        }
        TextRenderer.DrawText(g, start.ToString("HH:mm:ss"), Font, new Point(area.Left, area.Bottom + 6), ForeColor);
        TextRenderer.DrawText(g, end.ToString("HH:mm:ss"), Font, new Rectangle(area.Right - 100, area.Bottom + 6, 100, Font.Height + 3),
            ForeColor, TextFormatFlags.Right);
    }
}

internal sealed class HistoryWindow : Form
{
    private sealed class SeriesItem
    {
        public long Id; public string Name;
        public override string ToString() { return "#" + Id.ToString() + " · " + Name; }
    }
    private readonly SessionHistory history;
    private readonly ComboBox selector = new ComboBox();
    private readonly HistoryGraph charge = new HistoryGraph();
    private readonly HistoryGraph rate = new HistoryGraph { Rate = true };
    private readonly DataGridView grid = new DataGridView();
    private readonly Label count = new Label();
    private readonly Button export = new Button();
    public HistoryWindow(SessionHistory source)
    {
        history = source; Text = "BatteryView · " + L.T("history_title"); Font = new Font(ViewStyle.FontName, 10);
        AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(900, 740); MinimumSize = new Size(640, 640); StartPosition = FormStartPosition.CenterParent;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 30)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); Controls.Add(root);
        Label note = new Label { Text = L.T("history_note"), AutoSize = true, Dock = DockStyle.Top,
            Margin = new Padding(0, 3, 0, 8), MaximumSize = new Size(ClientSize.Width - 24, 0) };
        root.SizeChanged += delegate {
            int width = Math.Max(160, root.ClientSize.Width - root.Padding.Horizontal);
            if (note.MaximumSize.Width != width) note.MaximumSize = new Size(width, 0);
        };
        root.Controls.Add(note, 0, 0);
        selector.DropDownStyle = ComboBoxStyle.DropDownList; selector.Dock = DockStyle.Fill;
        selector.AccessibleName = L.T("select"); selector.SelectedIndexChanged += delegate { RenderSamples(); };
        root.Controls.Add(selector, 0, 1);
        charge.Dock = rate.Dock = DockStyle.Fill; root.Controls.Add(charge, 0, 2); root.Controls.Add(rate, 0, 3);
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.BackgroundColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(242, 246, 249);
        grid.Font = Font;
        grid.DefaultCellStyle.Font = Font;
        grid.ColumnHeadersDefaultCellStyle.Font = Font;
        grid.RowTemplate.Height = ViewStyle.LineHeight(Font) + 12;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.MultiSelect = false;
        string[] keys = { "time", "state", "col_charge", "col_rate", "col_voltage", "col_capacity", "col_units", "col_health", "col_cycles" };
        foreach (string key in keys) grid.Columns.Add(key, L.T(key));
        foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.Columns[0].MinimumWidth = TextRenderer.MeasureText("10-15 23:59:59", Font).Width + 20;
        int stateWidth = 80;
        foreach (string state in new[] { "charging", "discharging", "idle", "unknown" })
            stateWidth = Math.Max(stateWidth, TextRenderer.MeasureText(L.T(state), Font).Width + 20);
        grid.Columns[1].MinimumWidth = stateWidth;
        root.Controls.Add(grid, 0, 4); count.Dock = DockStyle.Fill; root.Controls.Add(count, 0, 5);
        FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        export.Text = L.T("export"); export.AutoSize = true; export.Click += delegate { Save(); };
        Button clear = new Button { Text = L.T("history_clear"), AutoSize = true };
        clear.Click += delegate { history.Clear(); Reload(); };
        actions.Controls.Add(export); actions.Controls.Add(clear); root.Controls.Add(actions, 0, 6);
        Shown += delegate {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
        };
        Reload();
    }
    public void Reload()
    {
        long selected = selector.SelectedItem is SeriesItem ? ((SeriesItem)selector.SelectedItem).Id : -1;
        selector.BeginUpdate(); selector.Items.Clear(); HashSet<long> ids = new HashSet<long>();
        foreach (BatterySample sample in history.Samples)
            if (ids.Add(sample.Series)) selector.Items.Add(new SeriesItem { Id = sample.Series, Name = sample.Name });
        selector.SelectedIndex = selector.Items.Count > 0 ? 0 : -1;
        for (int i = 0; i < selector.Items.Count; i++) if (((SeriesItem)selector.Items[i]).Id == selected) selector.SelectedIndex = i;
        selector.EndUpdate(); export.Enabled = history.Samples.Count > 0;
        count.Text = history.Samples.Count == 0 ? L.T("history_empty") : L.F("history_count", history.Samples.Count);
        RenderSamples();
    }
    private static string Number(double? value) { return value.HasValue ? value.Value.ToString("0.##") : ""; }
    private void RenderSamples()
    {
        SeriesItem selected = selector.SelectedItem as SeriesItem;
        List<BatterySample> values = new List<BatterySample>();
        foreach (BatterySample s in history.Samples) if (selected != null && s.Series == selected.Id) values.Add(s);
        charge.Samples = rate.Samples = values; charge.Invalidate(); rate.Invalidate();
        grid.Rows.Clear();
        for (int i = values.Count - 1; i >= 0; i--)
        {
            BatterySample s = values[i];
            grid.Rows.Add(s.Time.ToString("MM-dd HH:mm:ss"), L.T(s.State), Number(s.Percent), Number(s.Watts), Number(s.Voltage),
                s.Remaining.HasValue ? s.Remaining.Value.ToString("N0") : "", s.Absolute ? "mWh" : L.T("driver_units"), Number(s.Health),
                s.Cycles.HasValue ? s.Cycles.Value.ToString("N0") : "");
        }
        grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
    }
    private void Save()
    {
        using (SaveFileDialog dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", DefaultExt = "csv", AddExtension = true,
            FileName = "BatteryView-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv", RestoreDirectory = true })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try { history.WriteCsv(dialog.FileName); count.Text = L.T("export_done"); }
            catch { ErrorLog.Write("export_error"); MessageBox.Show(this, L.T("export_error"), "BatteryView", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
    }
}
