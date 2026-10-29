using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class StartupFailure
{
    public const string Copyright = "Copyright © 2026 Sakai. All rights reserved.";
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int NativeMessage(IntPtr owner, string text, string title, uint flags);
    internal static string Details(Exception error)
    {
        StringBuilder text = new StringBuilder();
        for (int depth = 0; error != null && depth < 8; depth++, error = error.InnerException)
        {
            AppendBounded(text, error.GetType().FullName);
            AppendBounded(text, "HRESULT: 0x" + error.HResult.ToString("X8"));
            AppendBounded(text, error.Message);
            AppendBounded(text, error.StackTrace ?? "");
        }
        string result = text.ToString();
        return result.Length > 32768 ? result.Substring(0, 32768) : result;
    }
    private static void AppendBounded(StringBuilder text, string value)
    {
        int remaining = 32768 - text.Length;
        if (remaining <= 0) return;
        value = value ?? "";
        text.Append(value, 0, Math.Min(remaining, value.Length));
        if (text.Length + Environment.NewLine.Length <= 32768) text.Append(Environment.NewLine);
    }
    public static void Report(Exception error)
    {
        try
        {
            string code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            if (code != "ko" && code != "ja") code = "en";
            string intro = code == "ko" ? "프로그램 처리 중 오류가 발생했습니다. 아래 예외 정보를 확인해 주세요." : code == "ja" ? "プログラムの処理中にエラーが発生しました。以下の例外情報を確認してください。" : "The program encountered an error. Check the exception information below.";
            string details = Details(error);
            string path = "";
            try
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (String.IsNullOrEmpty(profile)) throw new IOException("User profile unavailable");
                string desktop = Path.Combine(profile, "Desktop");
                if (String.IsNullOrEmpty(desktop)) throw new IOException("Desktop unavailable");
                string folder = Path.Combine(desktop, "BatteryView-Errors");
                Directory.CreateDirectory(folder);
                path = SafeFiles.WriteDiagnostic(folder, "startup-exception-" + code, Copyright + "\r\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + intro + "\r\n\r\n" + details);
            }
            catch { path = ""; }
            string shown = details.Length > 3000 ? details.Substring(0, 3000) : details;
            try { NativeMessage(IntPtr.Zero, intro + "\r\n\r\n" + shown + (path.Length == 0 ? "" : "\r\n\r\n" + path), "BatteryView", 0x10); } catch { }
        }
        catch { }
    }
}
