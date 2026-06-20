using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WhereWindsMeetMidiPlayer.Controls;

/// <summary>Card/panel with a chrome-opacity surface for see-through overlay on the game.</summary>
public class ChromeFadeCard : ContentControl
{
    public static readonly DependencyProperty SurfaceBackgroundProperty =
        DependencyProperty.Register(nameof(SurfaceBackground), typeof(Brush), typeof(ChromeFadeCard));

    public static readonly DependencyProperty CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner(typeof(ChromeFadeCard));

    public static readonly DependencyProperty CardEffectProperty =
        DependencyProperty.Register(nameof(CardEffect), typeof(Effect), typeof(ChromeFadeCard));

    public Brush? SurfaceBackground
    {
        get => (Brush?)GetValue(SurfaceBackgroundProperty);
        set => SetValue(SurfaceBackgroundProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Effect? CardEffect
    {
        get => (Effect?)GetValue(CardEffectProperty);
        set => SetValue(CardEffectProperty, value);
    }
}
