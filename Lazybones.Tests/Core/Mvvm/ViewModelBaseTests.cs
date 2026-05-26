using System.ComponentModel;
using Lazybones.Core.Mvvm;
using Xunit;

namespace Lazybones.Tests.Core.Mvvm;

public class ViewModelBaseTests
{
    private sealed class Probe : ViewModelBase
    {
        private string _value = "";
        public string Value
        {
            get => _value;
            set => SetField(ref _value, value);
        }

        public void RaiseExternal(string name) => OnPropertyChanged(name);

        public int RaiseOverrideCount { get; private set; }
        public string? LastRaisedName { get; private set; }

        protected override void RaisePropertyChanged(string? propertyName)
        {
            RaiseOverrideCount++;
            LastRaisedName = propertyName;
            base.RaisePropertyChanged(propertyName);
        }
    }

    [Fact]
    public void SetField_raises_PropertyChanged_when_value_differs()
    {
        var probe = new Probe();
        string? received = null;
        probe.PropertyChanged += (_, e) => received = e.PropertyName;

        probe.Value = "hello";

        Assert.Equal(nameof(Probe.Value), received);
    }

    [Fact]
    public void SetField_skips_when_value_equal()
    {
        var probe = new Probe { Value = "x" };
        var raised = false;
        probe.PropertyChanged += (_, _) => raised = true;

        probe.Value = "x";

        Assert.False(raised);
    }

    [Fact]
    public void OnPropertyChanged_passes_name_to_handler()
    {
        var probe = new Probe();
        string? received = null;
        probe.PropertyChanged += (_, e) => received = e.PropertyName;

        probe.RaiseExternal("Explicit");

        Assert.Equal("Explicit", received);
    }

    [Fact]
    public void RaisePropertyChanged_is_overridable_subclass_sees_every_raise()
    {
        var probe = new Probe();
        probe.Value = "a";
        probe.Value = "b";
        probe.RaiseExternal("Manual");

        Assert.Equal(3, probe.RaiseOverrideCount);
        Assert.Equal("Manual", probe.LastRaisedName);
    }
}
