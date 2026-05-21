using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Lazybones.Features.Dashboard;

public partial class DashboardWindow : Window
{
    public DashboardWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
