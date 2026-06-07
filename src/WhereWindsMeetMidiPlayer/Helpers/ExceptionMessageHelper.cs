namespace WhereWindsMeetMidiPlayer.Helpers;

public static class ExceptionMessageHelper
{
    public static string FormatUserMessage(Exception ex)
    {
        var root = ex.GetBaseException();
        if (ContainsSslHint(root.Message) || ContainsSslHint(ex.Message))
        {
            return
                "Secure connection to Discord failed while downloading the MIDI file. This is usually your network path — not the track itself.\n\n" +
                "Try:\n" +
                "• Catalogue → Refresh, then play again\n" +
                "• Disable VPN/proxy temporarily (some still break HTTPS)\n" +
                "• Pause antivirus HTTPS/SSL scanning for DebraMidiPlayer.exe\n" +
                "• Try another network (phone hotspot) to confirm\n\n" +
                $"Details: {root.Message}";
        }

        return string.IsNullOrWhiteSpace(ex.Message) ? root.Message : ex.Message;
    }

    private static bool ContainsSslHint(string message) =>
        message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
        || message.Contains("TLS", StringComparison.OrdinalIgnoreCase)
        || message.Contains("certificate", StringComparison.OrdinalIgnoreCase);
}
