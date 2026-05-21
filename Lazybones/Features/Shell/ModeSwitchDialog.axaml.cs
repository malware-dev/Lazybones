using Avalonia.Controls;

namespace Lazybones.Features.Shell;

public partial class ModeSwitchDialog : Window
{
    public enum Choice
    {
        Closed,
        StartNow,
        Dismiss
    }

    public Choice UserChoice { get; private set; } = Choice.Closed;

    public ModeSwitchDialog()
    {
        InitializeComponent();
    }

    public void SetMessage(string message)
    {
        var viewModel = new ModeSwitchDialogViewModel(message);
        viewModel.CloseRequested += (_, _) =>
        {
            UserChoice = viewModel.StartNow ? Choice.StartNow : Choice.Dismiss;
            Close();
        };
        DataContext = viewModel;
    }
}
