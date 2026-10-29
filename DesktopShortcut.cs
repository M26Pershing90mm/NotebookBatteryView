using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class DesktopShortcut
{
    public const string Copyright = "Copyright © 2026 Sakai. All rights reserved.";
    public static string Create()
    {
        object shell = null;
        object link = null;
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (String.IsNullOrEmpty(desktop) || !Directory.Exists(desktop)) return "shortcut_error";
            string path = Path.Combine(desktop, "BatteryView.lnk");
            string exe = Path.GetFullPath(Application.ExecutablePath);
            Type type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return "shortcut_error";
            shell = Activator.CreateInstance(type);
            link = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            Type linkType = link.GetType();
            if (File.Exists(path))
            {
                string target = Convert.ToString(linkType.InvokeMember("TargetPath", BindingFlags.GetProperty, null, link, null));
                string arguments = Convert.ToString(linkType.InvokeMember("Arguments", BindingFlags.GetProperty, null, link, null));
                if (!CanReplace(target, arguments, exe)) return "shortcut_collision";
            }
            Set(link, "TargetPath", exe);
            Set(link, "Arguments", "");
            Set(link, "WorkingDirectory", Environment.GetFolderPath(Environment.SpecialFolder.System));
            Set(link, "IconLocation", exe + ",0");
            Set(link, "Description", "BatteryView " + L.Version + " · " + L.T("author"));
            Set(link, "WindowStyle", 1);
            linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            return File.Exists(path) ? "shortcut_done" : "shortcut_error";
        }
        catch { return "shortcut_error"; }
        finally
        {
            Release(link);
            Release(shell);
        }
    }
    internal static bool CanReplace(string target, string arguments, string exe)
    {
        return !String.IsNullOrEmpty(exe) && String.Equals(target, exe, StringComparison.OrdinalIgnoreCase) && String.IsNullOrEmpty(arguments);
    }
    private static void Set(object item, string property, object value)
    {
        item.GetType().InvokeMember(property, BindingFlags.SetProperty, null, item, new object[] { value });
    }
    private static void Release(object value)
    {
        if (value != null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }
}
