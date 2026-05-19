using System;
using System.Windows.Input;

namespace Lazybones.ViewModels;

public class ModeSwitchDialogViewModel : ViewModelBase
{
    private string _message;

    public ModeSwitchDialogViewModel(string message)
    {
        _message = message;
        StartNowCommand = new RelayCommand(() =>
        {
            StartNow = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        });
        DismissCommand = new RelayCommand(() =>
        {
            StartNow = false;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        });
    }

    public string Message
    {
        get => _message;
        set => SetField(ref _message, value);
    }

    public bool StartNow { get; private set; }
    public event EventHandler? CloseRequested;

    public ICommand StartNowCommand { get; }
    public ICommand DismissCommand { get; }
}
