using System.Text;
using Melanchall.DryWetMidi.Core;

namespace WhereWindsMeetMidiPlayer.Helpers;

public static class MidiTextEncoding
{
    private static readonly Encoding[] FallbackEncodings = BuildFallbackEncodings();

    public static ReadingSettings ReadingSettings { get; } = new()
    {
        TextEncoding = Encoding.UTF8,
        DecodeTextCallback = (bytes, _) => Decode(bytes)
    };

    public static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        string? best = null;
        var bestScore = int.MinValue;

        foreach (var encoding in FallbackEncodings)
        {
            string text;
            try
            {
                text = encoding.GetString(bytes);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var score = ScoreDecodedText(text, encoding);
            if (score > bestScore)
            {
                bestScore = score;
                best = text;
            }
        }

        return best?.Trim() ?? Encoding.UTF8.GetString(bytes).Trim();
    }

    private static Encoding[] BuildFallbackEncodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var list = new List<Encoding> { Encoding.UTF8 };
        TryAddEncoding(list, "gb18030");
        TryAddEncoding(list, "shift_jis");
        TryAddEncoding(list, "euc-kr");
        list.Add(Encoding.Latin1);
        return list.ToArray();
    }

    private static void TryAddEncoding(List<Encoding> list, string name)
    {
        try
        {
            list.Add(Encoding.GetEncoding(name));
        }
        catch
        {
        }
    }

    private static int ScoreDecodedText(string text, Encoding encoding)
    {
        var score = 0;
        foreach (var ch in text)
        {
            if (char.IsControl(ch) && ch is not '\t' and not '\n' and not '\r')
            {
                score -= 8;
                continue;
            }

            if (ch == '\uFFFD')
            {
                score -= 20;
                continue;
            }

            if (ch >= 0x4E00 && ch <= 0x9FFF)
                score += 4;
            else if (ch >= 0x3040 && ch <= 0x30FF)
                score += 4;
            else if (ch >= 0xAC00 && ch <= 0xD7AF)
                score += 4;
            else if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
                score += 1;
            else if (ch > 127)
                score += 2;
            else
                score += 1;
        }

        if (encoding == Encoding.UTF8)
            score += 1;

        return score;
    }
}