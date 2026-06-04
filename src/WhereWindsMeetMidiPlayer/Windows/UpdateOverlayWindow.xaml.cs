using System.Windows;
using WhereWindsMeetMidiPlayer.ViewModels;

namespace WhereWindsMeetMidiPlayer.Windows;

public partial class UpdateOverlayWindow : Window
{
    public UpdateOverlayWindow(UpdateOverlayViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
