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

    private MainWindowViewModel? _subscribedVm;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedVm is not null)
        {
            _subscribedVm.StreakAdvanced -= OnStreakAdvanced;
            _subscribedVm = null;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            Position = new PixelPoint((int)viewModel.WindowPosition.X, (int)viewModel.WindowPosition.Y);

            viewModel.Overlay.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName != nameof(OverlayViewModel.IsVisible) ||
                    !viewModel.Overlay.IsVisible) return;

                if (viewModel.Overlay.OverlayType == OverlayType.TimeAdjustment)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        var textBox = this.FindControl<TextBox>("TimeInputBox");
                        textBox?.Focus();
                    }, DispatcherPriority.Input);
                }
                else if (viewModel.Overlay.OverlayType == OverlayType.AchievementToast)
                {
                    // Each achievement that becomes visible (one at a time
                    // off the queue) gets its own celebration. Confetti is
                    // rendered on top of the toast in the disk grid.
                    Dispatcher.UIThread.Post(() => this.FindControl<ConfettiBurst>("Confetti")?.Burst());
                }
            };

            viewModel.StreakAdvanced += OnStreakAdvanced;
            _subscribedVm = viewModel;
        }
    }

    private void OnStreakAdvanced()
    {
        Dispatcher.UIThread.Post(() => this.FindControl<ConfettiBurst>("Confetti")?.Burst());
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.StreakAdvanced -= OnStreakAdvanced;
            _subscribedVm = null;
        }
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