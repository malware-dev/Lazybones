using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Lazybones.Features.Dashboard;

public partial class DashboardWindow : Window
{
    public DashboardWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as DashboardViewModel)?.Dispose();
        base.OnClosed(e);
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
