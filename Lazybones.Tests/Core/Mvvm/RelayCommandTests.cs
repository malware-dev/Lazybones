using System.Windows.Input;
using Lazybones.Core.Mvvm;
using Xunit;

namespace Lazybones.Tests.Core.Mvvm;

public class RelayCommandTests
{
    [Fact]
    public void Execute_invokes_the_supplied_action()
    {
        var fired = 0;
        ICommand cmd = new RelayCommand(() => fired++);

        cmd.Execute(parameter: null);
        cmd.Execute(parameter: null);

        Assert.Equal(2, fired);
    }

    [Fact]
    public void CanExecute_always_returns_true()
    {
        ICommand cmd = new RelayCommand(() => { });
        Assert.True(cmd.CanExecute(parameter: null));
        Assert.True(cmd.CanExecute(parameter: "anything"));
    }

    [Fact]
    public void CanExecuteChanged_subscribe_and_unsubscribe_are_noops()
    {
        // RelayCommand has no real CanExecute toggling — the event accessors
        // are no-ops. This pins the contract so callers know not to expect
        // dynamic enable/disable from this implementation.
        ICommand cmd = new RelayCommand(() => { });
        void Handler(object? s, System.EventArgs e) { }

        cmd.CanExecuteChanged += Handler;
        cmd.CanExecuteChanged -= Handler;
        // No throw, no exception.
    }
}
