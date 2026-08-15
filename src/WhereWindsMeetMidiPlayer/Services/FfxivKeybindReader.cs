using System.Globalization;
using System.IO;
using System.Text;

namespace WhereWindsMeetMidiPlayer.Services;

/// <summary>
/// Reads the player's actual FFXIV Performance keybinds from the game's KEYBIND.DAT
/// (same binary format/algorithm as BardMusicPlayer/LightAmp's Seer DAT reader), so
/// playback adapts to whatever layout the user configured in game.
/// </summary>
public static class FfxivKeybindReader
{
    private const byte Xor = 0x73;

    // FFXIV internal key codes ≥ 130 → Windows virtual keys (BMP Seer KeyDictionary.MainKeyMap).
    // Codes < 130 already are Windows VK codes.
    private static readonly Dictionary<int, int> MainKeyMap = new()
    {
        { 130, 0xBB }, // =  VK_OEM_PLUS
        { 131, 0xBC }, // ,  VK_OEM_COMMA
        { 132, 0xBD }, // -  VK_OEM_MINUS
        { 133, 0xBE }, // .  VK_OEM_PERIOD
        { 134, 0xBF }, // /  VK_OEM_2
        { 135, 0xBA }, // ;  VK_OEM_1
        { 136, 0xC0 }, // '  VK_OEM_3
        { 137, 0xDB }, // [  VK_OEM_4
        { 138, 0xDC }, // \  VK_OEM_5
        { 139, 0xDD }, // ]  VK_OEM_6
        { 140, 0xDE }, // #  VK_OEM_7
        { 141, 0xDF }, // `  VK_OEM_8
        { 142, 0xE2 }  // <  VK_OEM_102
    };

    // DAT command names for the 37 note binds, index = MIDI note − 48 (PERFORMANCE_MODE_EX_C3 … C6).
    private static readonly string[] NoteCommandNames = BuildNoteCommandNames();

    private static string[] BuildNoteCommandNames()
    {
        string[] steps = ["C", "C_SHARP", "D", "D_SHARP", "E", "F", "F_SHARP", "G", "G_SHARP", "A", "A_SHARP", "B"];
        var names = new string[37];
        for (var i = 0; i < 36; i++)
        {
            var octave = 3 + i / 12;
            var step = steps[i % 12];
            names[i] = step.EndsWith("_SHARP", StringComparison.Ordinal)
                ? $"PERFORMANCE_MODE_EX_{step[..1]}{octave}_SHARP"
                : $"PERFORMANCE_MODE_EX_{step}{octave}";
        }

        names[36] = "PERFORMANCE_MODE_EX_C6";
        return names;
    }

    /// <summary>
    /// Attempts to read the Performance note keybinds of the most recently played character.
    /// Returns a keymap dictionary ("48".."84" → combo) ready to be written as a keymap file.
    /// </summary>
    public static bool TryReadPerformanceKeybinds(
        out Dictionary<string, string> midiToCombo,
        out string sourceDescription)
    {
        midiToCombo = [];
        sourceDescription = string.Empty;

        string? datPath;
        try
        {
            datPath = FindLatestKeybindDat();
        }
        catch
        {
            return false;
        }

        if (datPath is null)
            return false;

        Dictionary<string, (int Main1, int Mod1, int Main2, int Mod2)> binds;
        try
        {
            binds = ParseKeybindDat(File.ReadAllBytes(datPath));
        }
        catch
        {
            return false;
        }

        for (var i = 0; i < NoteCommandNames.Length; i++)
        {
            if (!binds.TryGetValue(NoteCommandNames[i], out var kb))
                continue;

            // Primary binding slot wins; secondary only if the primary is unbound (BMP KeyBind.GetKey).
            var combo = DecodeCombo(kb.Main1, kb.Mod1) ?? DecodeCombo(kb.Main2, kb.Mod2);
            if (combo is null)
                continue;

            midiToCombo[(48 + i).ToString()] = combo;
        }

        // Fewer than one bound octave means Performance keybinds aren't really set up in game.
        if (midiToCombo.Count < 12)
        {
            midiToCombo = [];
            return false;
        }

        var charFolder = Path.GetFileName(Path.GetDirectoryName(datPath)) ?? "?";
        sourceDescription = $"{charFolder} ({midiToCombo.Count}/37)";
        return true;
    }

    // BMP picks the character via game-memory reading; without that, the most recently
    // saved KEYBIND.DAT is the character the user played last.
    private static string? FindLatestKeybindDat()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(docs))
            return null;

        var root = Path.Combine(docs, "My Games", "FINAL FANTASY XIV - A Realm Reborn");
        if (!Directory.Exists(root))
            return null;

        return Directory.EnumerateDirectories(root, "FFXIV_CHR*")
            .SelectMany(dir => Directory.EnumerateFiles(dir, "KEYBIND*.DAT"))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static Dictionary<string, (int Main1, int Mod1, int Main2, int Mod2)> ParseKeybindDat(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes));
        reader.BaseStream.Seek(0x04, SeekOrigin.Begin);
        var fileSize = reader.ReadInt32();
        var dataEnd = reader.ReadInt32() + 16;

        if (bytes.Length - fileSize != 32)
            throw new InvalidDataException("Unexpected KEYBIND.DAT layout.");

        var result = new Dictionary<string, (int, int, int, int)>(StringComparer.Ordinal);
        reader.BaseStream.Seek(0x11, SeekOrigin.Begin);
        while (reader.BaseStream.Position < dataEnd)
        {
            var command = ReadSection(reader);
            var value = ReadSection(reader);

            var name = Encoding.UTF8.GetString(command).TrimEnd('\0');
            var parts = Encoding.UTF8.GetString(value).TrimEnd('\0').Split(',');
            if (parts.Length != 3)
                continue;

            var key1 = parts[0].Split('.');
            var key2 = parts[1].Split('.');
            if (key1.Length < 2 || key2.Length < 2)
                continue;

            result[name] = (
                int.Parse(key1[0], NumberStyles.HexNumber),
                int.Parse(key1[1], NumberStyles.HexNumber),
                int.Parse(key2[0], NumberStyles.HexNumber),
                int.Parse(key2[1], NumberStyles.HexNumber));
        }

        return result;
    }

    // Section = 3 header bytes then data; the meaningful header bytes and every data
    // byte are XOR-obfuscated with 0x73. Raw header order: [ignored, size, type].
    private static byte[] ReadSection(BinaryReader reader)
    {
        _ = reader.ReadByte();
        var size = reader.ReadByte() ^ Xor;
        _ = reader.ReadByte();

        var data = reader.ReadBytes(size);
        for (var i = 0; i < data.Length; i++)
            data[i] ^= Xor;
        return data;
    }

    private static string? DecodeCombo(int main, int mod)
    {
        if (main <= 0)
            return null;

        var vk = main < 130
            ? main
            : MainKeyMap.TryGetValue(main, out var mapped) ? mapped : 0;
        if (vk <= 0 || vk > 0xFE)
            return null;

        var token = VkToToken(vk);
        var sb = new StringBuilder();
        if ((mod & 2) != 0)
            sb.Append("Ctrl+");
        if ((mod & 4) != 0)
            sb.Append("Alt+");
        if ((mod & 1) != 0)
            sb.Append("Shift+");
        sb.Append(token);
        return sb.ToString();
    }

    // Tokens must round-trip through InputService.TryCharToVk. OEM keys use the exact
    // "VK<code>" form — layout-independent, unlike character lookups via VkKeyScan.
    private static string VkToToken(int vk) => vk switch
    {
        >= 0x41 and <= 0x5A => ((char)vk).ToString(), // A–Z
        >= 0x30 and <= 0x39 => ((char)vk).ToString(), // 0–9
        >= 0x60 and <= 0x69 => $"Numpad{vk - 0x60}",
        0x20 => "Space",
        0x09 => "Tab",
        _ => $"VK{vk}"
    };
}
