using System.Windows.Markup;
using System.Windows.Media;

namespace WhereWindsMeetMidiPlayer.Helpers;

[MarkupExtensionReturnType(typeof(ImageSource))]
public sealed class LocalAssetExtension : MarkupExtension
{
    [ConstructorArgument("fileName")]
    public string FileName { get; set; } = "";

    public LocalAssetExtension()
    {
    }

    public LocalAssetExtension(string fileName) => FileName = fileName;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(FileName))
            return AssetImage.LoadOrPlaceholder("debra-bg-landscape.png");

        return AssetImage.LoadOrPlaceholder(FileName.Trim());
    }
}
