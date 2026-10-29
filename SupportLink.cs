using System;
using System.Diagnostics;

internal static class SupportLink
{
    public const string Copyright = "Copyright © 2026 Sakai. All rights reserved.";
    public static string Url { get { return L.Language == 0 ? "https://toon.at/donate/637833976686140024" : "https://ko-fi.com/sakai38666"; } }
    public static bool Open()
    {
        try
        {
            using (Process process = Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true })) { }
            return true;
        }
        catch { return false; }
    }
}
