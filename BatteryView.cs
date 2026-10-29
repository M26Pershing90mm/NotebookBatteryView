using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Management;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("BatteryView")]
[assembly: AssemblyDescription("Windows battery status viewer")]
[assembly: AssemblyCompany("Sakai")]
[assembly: AssemblyCopyright("Copyright © 2026 Sakai. All rights reserved.")]
[assembly: AssemblyVersion("1.2.2.0")]
[assembly: AssemblyFileVersion("1.2.2.0")]

internal static class BatteryMath
{
    public static double? Health(ulong? design, ulong? full)
    {
        if (!design.HasValue || !full.HasValue || design.Value == 0 || full.Value == 0 ||
            design.Value >= UInt32.MaxValue || full.Value >= UInt32.MaxValue) return null;
        double value = 100.0 * full.Value / design.Value;
        return value <= 200.0 ? (double?)value : null;
    }

    public static int? Charge(float value)
    {
        if (Single.IsNaN(value) || Single.IsInfinity(value) || value < 0 || value > 1) return null;
        return (int)Math.Round(value * 100, MidpointRounding.AwayFromZero);
    }

    public static double? Voltage(ulong? millivolts)
    {
        if (!millivolts.HasValue || millivolts.Value == 0 || millivolts.Value > 1000000) return null;
        return millivolts.Value / 1000.0;
    }

    public static double? StateVoltage(ulong? millivolts, bool? charging, bool? discharging, bool forCharging)
    {
        if (!charging.HasValue || !discharging.HasValue || charging.Value == discharging.Value) return null;
        if (forCharging ? charging.Value : discharging.Value) return Voltage(millivolts);
        return null;
    }

    public static string Advice(double? health)
    {
        if (!health.HasValue) return L.T("health_unknown");
        if (health.Value > 100) return L.T("health_high");
        if (health.Value >= 80) return L.T("health_good");
        if (health.Value >= 60) return L.T("health_medium");
        return L.T("health_low");
    }
}

internal sealed class BatteryRecord
{
    public string Name;
    public string Instance;
    public ulong? Tag;
    public ulong? Design;
    public ulong? Full;
    public ulong? Cycles;
    public bool AbsoluteUnits;
    public ulong? Remaining;
    public ulong? ChargeRate;
    public ulong? DischargeRate;
    public DateTime StatusTime;
    public DateTime DetailsTime;
    public ulong? Millivolts;
    public bool? Charging;
    public bool? Discharging;
    public override string ToString() { return Name; }
}

internal sealed class ReadResult
{
    public List<BatteryRecord> Batteries = new List<BatteryRecord>();
    public string Message;
}

internal static class BatteryReader
{
    private static object Property(ManagementBaseObject item, string key)
    {
        try { return item[key]; } catch (ManagementException) { return null; }
    }

    private static ulong? Number(ManagementBaseObject item, string key)
    {
        object value = Property(item, key);
        if (value == null) return null;
        try { return Convert.ToUInt64(value, CultureInfo.InvariantCulture); }
        catch (FormatException) { return null; }
        catch (OverflowException) { return null; }
        catch (InvalidCastException) { return null; }
    }

    private static string Text(ManagementBaseObject item, string key)
    {
        return DeviceText.Clean(Convert.ToString(Property(item, key), CultureInfo.InvariantCulture), 160);
    }

    private static List<Dictionary<string, object>> Query(string className)
    {
        if (className != "BatteryStaticData" && className != "BatteryFullChargedCapacity" &&
            className != "BatteryCycleCount" && className != "BatteryStatus") throw new ArgumentException("Unsupported WMI class.");
        List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
        EnumerationOptions options = new EnumerationOptions();
        options.Timeout = TimeSpan.FromSeconds(4);
        options.ReturnImmediately = true;
        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
            new ManagementScope(@"\\.\root\wmi"), new ObjectQuery("SELECT * FROM " + className), options))
        using (ManagementObjectCollection collection = searcher.Get())
        {
            foreach (ManagementObject item in collection)
            {
                using (item)
                {
                    if (rows.Count >= 32) throw new InvalidOperationException();
                    object active = Property(item, "Active");
                    if (active is bool && !(bool)active) continue;
                    Dictionary<string, object> row = new Dictionary<string, object>(StringComparer.Ordinal);
                    string instance = Convert.ToString(Property(item, "InstanceName"), CultureInfo.InvariantCulture);
                    row["InstanceName"] = instance != null && instance.Length <= 4096 ? instance : "";
                    row["DeviceName"] = Text(item, "DeviceName");
                    foreach (string key in new string[] { "DesignedCapacity", "FullChargedCapacity", "CycleCount", "Capabilities", "Tag", "Voltage", "RemainingCapacity", "ChargeRate", "DischargeRate" })
                        row[key] = Number(item, key);
                    row["Charging"] = Property(item, "Charging") is bool ? (bool?)Property(item, "Charging") : null;
                    row["Discharging"] = Property(item, "Discharging") is bool ? (bool?)Property(item, "Discharging") : null;
                    rows.Add(row);
                }
            }
        }
        return rows;
    }

    internal static Dictionary<string, object> Match(List<Dictionary<string, object>> rows, Dictionary<string, object> source)
    {
        string instance = (string)source["InstanceName"];
        if (String.IsNullOrEmpty(instance)) return null;
        Dictionary<string, object> found = null;
        foreach (Dictionary<string, object> row in rows)
        {
            if (!String.Equals((string)row["InstanceName"], instance, StringComparison.Ordinal)) continue;
            if (found != null) return null;
            found = row;
        }
        if (found != null && source["Tag"] != null && found["Tag"] != null &&
            !source["Tag"].Equals(found["Tag"])) return null;
        return found;
    }

    internal static ulong? GetNumber(Dictionary<string, object> row, string key)
    {
        object value;
        return row.TryGetValue(key, out value) ? (ulong?)value : null;
    }

    public static ReadResult Read()
    {
        return Read(Query, ErrorLog.Write);
    }

    public static ReadResult ReadStatus()
    {
        return ReadStatus(Query, ErrorLog.Write);
    }

    internal static ReadResult ReadStatus(Func<string, List<Dictionary<string, object>>> query, Action<string> log)
    {
        ReadResult result = new ReadResult();
        try
        {
            foreach (Dictionary<string, object> row in query("BatteryStatus"))
                result.Batteries.Add(new BatteryRecord {
                    Instance = (string)row["InstanceName"],
                    Tag = (ulong?)row["Tag"],
                    Millivolts = (ulong?)row["Voltage"],
                    Charging = (bool?)row["Charging"],
                    Discharging = (bool?)row["Discharging"],
                    Remaining = GetNumber(row, "RemainingCapacity"),
                    ChargeRate = GetNumber(row, "ChargeRate"),
                    DischargeRate = GetNumber(row, "DischargeRate"),
                    StatusTime = DateTime.Now });
            result.Message = result.Batteries.Count == 0 ? L.T("read_none") : L.T("voltage_ok");
        }
        catch { result.Message = L.T("detail_error"); log("detail_error"); }
        return result;
    }

    internal static ReadResult Read(Func<string, List<Dictionary<string, object>>> query, Action<string> log)
    {
        ReadResult result = new ReadResult();
        try
        {
            bool incomplete = false;
            List<Dictionary<string, object>> statics;
            try { statics = query("BatteryStaticData"); }
            catch { statics = new List<Dictionary<string, object>>(); incomplete = true; log("read_partial"); }
            List<Dictionary<string, object>> full;
            List<Dictionary<string, object>> cycles;
            List<Dictionary<string, object>> status;
            try { full = query("BatteryFullChargedCapacity"); }
            catch { full = new List<Dictionary<string, object>>(); incomplete = true; log("read_partial"); }
            try { cycles = query("BatteryCycleCount"); }
            catch { cycles = new List<Dictionary<string, object>>(); }
            try { status = query("BatteryStatus"); }
            catch { status = new List<Dictionary<string, object>>(); incomplete = true; log("read_partial"); }
            List<Dictionary<string, object>> sources = statics.Count > 0 ? statics : status;
            foreach (Dictionary<string, object> row in sources)
            {
                BatteryRecord battery = new BatteryRecord();
                battery.DetailsTime = DateTime.Now;
                battery.Instance = (string)row["InstanceName"];
                battery.Tag = (ulong?)row["Tag"];
                battery.Name = (string)row["DeviceName"];
                battery.Name = DeviceText.Clean(battery.Name, 160);
                if (String.IsNullOrWhiteSpace(battery.Name)) battery.Name = L.T("name_unknown");
                battery.Name = L.F("battery_name", result.Batteries.Count + 1, battery.Name);
                battery.Design = (ulong?)row["DesignedCapacity"];
                ulong? caps = statics.Count > 0 ? (ulong?)row["Capabilities"] : null;
                battery.AbsoluteUnits = caps.HasValue && (caps.Value & 0x40000000UL) == 0;
                Dictionary<string, object> matched = Match(full, row);
                battery.Full = matched == null || Match(sources, row) == null ? null : (ulong?)matched["FullChargedCapacity"];
                matched = Match(cycles, row);
                battery.Cycles = matched == null || Match(sources, row) == null ? null : (ulong?)matched["CycleCount"];
                if (battery.Cycles == UInt32.MaxValue) battery.Cycles = null;
                matched = Match(sources, row) == null ? null : Match(status, row);
                battery.Millivolts = matched == null ? null : (ulong?)matched["Voltage"];
                battery.Charging = matched == null ? null : (bool?)matched["Charging"];
                battery.Discharging = matched == null ? null : (bool?)matched["Discharging"];
                battery.Remaining = matched == null ? null : GetNumber(matched, "RemainingCapacity");
                battery.ChargeRate = matched == null ? null : GetNumber(matched, "ChargeRate");
                battery.DischargeRate = matched == null ? null : GetNumber(matched, "DischargeRate");
                battery.StatusTime = matched == null ? DateTime.MinValue : DateTime.Now;
                result.Batteries.Add(battery);
            }
            result.Message = result.Batteries.Count == 0 ? L.T("read_none") :
                incomplete ? L.T("read_partial") : L.T("read_ok");
        }
        catch (UnauthorizedAccessException) { result.Message = L.T("detail_denied"); log("detail_denied"); }
        catch { result.Message = L.T("detail_error"); log("detail_error"); }
        return result;
    }
}

