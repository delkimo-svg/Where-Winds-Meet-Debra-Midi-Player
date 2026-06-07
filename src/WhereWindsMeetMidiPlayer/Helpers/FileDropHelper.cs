using System.Runtime.InteropServices;
using System.Windows;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class FileDropHelper
{
    private static readonly string[] ShellDragFormats =
    [
        DataFormats.FileDrop,
        "CF_HDROP",
        "FileGroupDescriptor",
        "FileGroupDescriptorW",
        "Shell IDList Array",
        "UsingDefaultDragImage",
        "DragImageBits",
        "DragContext",
        "DragSourceHelperFlags",
        "InShellDragLoop",
        "FileContents",
        "RenPrivateItem"
    ];

    public static bool IsInternalAppDrag(IDataObject data) =>
        data.GetDataPresent(DebraDialogs.SongDragFormat)
        || data.GetDataPresent(DebraDialogs.CatalogueTrackDragFormat);

    /// <summary>During DragOver, Explorer often exposes shell formats before FileDrop is readable.</summary>
    public static bool LooksLikeExternalFileDrag(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.FileDrop))
            return true;

        return HasExplorerShellFormats(data);
    }

    public static bool ShouldShowFileDropCursor(IDataObject data)
    {
        if (IsInternalAppDrag(data))
            return false;

        if (LooksLikeExternalFileDrag(data))
            return true;

        // Explorer may expose only a couple of shell formats during early DragOver.
        try
        {
            var formats = data.GetFormats(autoConvert: false);
            if (formats.Length > 0)
                return true;

            return data.GetFormats(autoConvert: true).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsExternalFileDrag(IDataObject data) =>
        LooksLikeExternalFileDrag(data);

    public static bool TryExtractPaths(IDataObject data, out string[] paths)
    {
        paths = [];

        if (TryReadFileDrop(data, autoConvert: false, out paths))
            return true;

        if (TryReadFileDrop(data, autoConvert: true, out paths))
            return true;

        foreach (var format in data.GetFormats())
        {
            if (!format.Contains("FileDrop", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                if (data.GetData(format) is string[] array && array.Length > 0)
                {
                    paths = NormalizePaths(array);
                    return paths.Length > 0;
                }
            }
            catch
            {
                // try next format
            }
        }

        return false;
    }

    private static bool HasExplorerShellFormats(IDataObject data)
    {
        try
        {
            var formats = data.GetFormats();
            if (formats.Length == 0)
                return false;

            foreach (var format in formats)
            {
                if (string.IsNullOrWhiteSpace(format))
                    continue;

                foreach (var shell in ShellDragFormats)
                {
                    if (format.Equals(shell, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                if (format.Contains("FileDrop", StringComparison.OrdinalIgnoreCase)
                    || format.Contains("HDROP", StringComparison.OrdinalIgnoreCase)
                    || format.Contains("FileGroupDescriptor", StringComparison.OrdinalIgnoreCase)
                    || format.Contains("Shell IDList", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Explorer file/folder drags typically register many private shell formats.
            return formats.Length >= 4;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadFileDrop(IDataObject data, bool autoConvert, out string[] paths)
    {
        paths = [];
        if (!data.GetDataPresent(DataFormats.FileDrop, autoConvert))
            return false;

        try
        {
            var raw = data.GetData(DataFormats.FileDrop, autoConvert);
            switch (raw)
            {
                case string[] array when array.Length > 0:
                    paths = NormalizePaths(array);
                    return paths.Length > 0;
                case string single when !string.IsNullOrWhiteSpace(single):
                    paths = NormalizePaths(single.Split(['\0', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries));
                    return paths.Length > 0;
                case MemoryStream stream:
                    return TryReadFileDropFromStream(stream, out paths);
            }
        }
        catch (COMException)
        {
            // Explorer sometimes throws until autoConvert is used — caller tries both.
        }
        catch
        {
            // ignored
        }

        return false;
    }

    private static bool TryReadFileDropFromStream(MemoryStream stream, out string[] paths)
    {
        paths = [];
        try
        {
            var bytes = stream.ToArray();
            if (bytes.Length < sizeof(int))
                return false;

            var count = BitConverter.ToInt32(bytes, 0);
            if (count <= 0)
                return false;

            var offset = sizeof(int);
            var list = new List<string>(count);
            for (var i = 0; i < count && offset < bytes.Length; i++)
            {
                var end = offset;
                while (end + 1 < bytes.Length && !(bytes[end] == 0 && bytes[end + 1] == 0))
                    end += 2;

                var len = end - offset;
                if (len > 0)
                {
                    var text = System.Text.Encoding.Unicode.GetString(bytes, offset, len);
                    if (!string.IsNullOrWhiteSpace(text))
                        list.Add(text);
                }

                offset = end + 2;
            }

            paths = NormalizePaths(list);
            return paths.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string[] NormalizePaths(IEnumerable<string> raw)
    {
        var list = new List<string>();
        foreach (var path in raw)
        {
            var trimmed = path.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            try
            {
                list.Add(Path.GetFullPath(trimmed));
            }
            catch
            {
                list.Add(trimmed);
            }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
