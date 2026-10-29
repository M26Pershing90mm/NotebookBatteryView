using System;
using System.IO;
using System.Text;

internal static class SafeFiles
{
    internal static FileStream CreateExclusive(string path)
    {
        return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    }

    internal static string DiagnosticPath(string directory, string prefix, string extension)
    {
        return Path.Combine(directory, prefix + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" +
            Guid.NewGuid().ToString("N") + extension);
    }

    internal static string WriteDiagnostic(string directory, string prefix, string contents)
    {
        Directory.CreateDirectory(directory);
        string path = DiagnosticPath(directory, prefix, ".txt");
        using (FileStream stream = CreateExclusive(path))
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(true))) writer.Write(contents);
        return path;
    }

    internal static void RejectLinkedTarget(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked output files are not supported.");
        }
        catch (FileNotFoundException) { }
    }
}

internal sealed class DiagnosticLog : IDisposable
{
    private StreamWriter writer;
    public const long MaxBytes = 262144;
    public void Append(string directory, string language, string header, string line)
    {
        if (writer == null)
        {
            Directory.CreateDirectory(directory);
            string path = SafeFiles.DiagnosticPath(directory, "errors-" + language, ".log");
            writer = new StreamWriter(SafeFiles.CreateExclusive(path), new UTF8Encoding(true));
            writer.WriteLine(header);
            writer.Flush();
        }
        if (writer.BaseStream.Length + Encoding.UTF8.GetByteCount(line) + 2 > MaxBytes) return;
        writer.WriteLine(line);
        writer.Flush();
    }
    public void Dispose()
    {
        if (writer != null) { writer.Dispose(); writer = null; }
    }
}

internal static class DeviceText
{
    internal static string Clean(string value, int maximum)
    {
        if (String.IsNullOrEmpty(value)) return "";
        StringBuilder text = new StringBuilder(Math.Min(value.Length, maximum));
        for (int i = 0; i < value.Length && text.Length < maximum; i++)
        {
            char c = value[i];
            if (Char.IsHighSurrogate(c))
            {
                if (i + 1 < value.Length && Char.IsLowSurrogate(value[i + 1]))
                {
                    System.Globalization.UnicodeCategory pairCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(value, i);
                    if (pairCategory == System.Globalization.UnicodeCategory.Format)
                    { text.Append(' '); i++; continue; }
                    if (pairCategory == System.Globalization.UnicodeCategory.OtherNotAssigned)
                    { text.Append('\uFFFD'); i++; continue; }
                    if (text.Length + 2 > maximum) break;
                    text.Append(c).Append(value[++i]);
                }
                else text.Append('\uFFFD');
                continue;
            }
            if (Char.IsLowSurrogate(c)) { text.Append('\uFFFD'); continue; }
            System.Globalization.UnicodeCategory category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == System.Globalization.UnicodeCategory.OtherNotAssigned)
            { text.Append('\uFFFD'); continue; }
            text.Append(Char.IsControl(c) || category == System.Globalization.UnicodeCategory.Format ||
                category == System.Globalization.UnicodeCategory.LineSeparator || category == System.Globalization.UnicodeCategory.ParagraphSeparator ? ' ' : c);
        }
        return text.ToString();
    }
}
