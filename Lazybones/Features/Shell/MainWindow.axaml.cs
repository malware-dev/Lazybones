using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Lazybones.Features.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        PositionChanged += OnPositionChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is MainWindowViewModel viewModel)
        {
            Position = new PixelPoint((int)viewModel.WindowPosition.X, (int)viewModel.WindowPosition.Y);

            viewModel.Overlay.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(OverlayViewModel.IsVisible) &&
                    viewModel.Overlay.IsVisible && 
                    viewModel.Overlay.OverlayType == OverlayType.TimeAdjustment)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var textBox = this.FindControl<TextBox>("TimeInputBox");
                        textBox?.Focus();
                    }, DispatcherPriority.Input);
                }
            };
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as MainWindowViewModel)?.Dispose();
        base.OnClosed(e);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        // When minimized, Avalonia reports (-32000, -32000); ignore so we
        // don't persist a junk position over the user's real one.
        if (WindowState != WindowState.Normal) return;
        if (DataContext is MainWindowViewModel viewModel) viewModel.WindowPosition = new Point(e.Point.X, e.Point.Y);
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MinifyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void TimeInputBox_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // Delay SelectAll to avoid being overridden by mouse click caret positioning
            Dispatcher.UIThread.Post(() => textBox.SelectAll(), DispatcherPriority.Input);
        }
    }
}