using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Lazybones.Core.Behaviors;

public class DraggableWindowBehavior
{
    public static readonly AttachedProperty<bool> EnableDragProperty = AvaloniaProperty.RegisterAttached<DraggableWindowBehavior, Control, bool>("EnableDrag");

    public static bool GetEnableDrag(Control element) => element.GetValue(EnableDragProperty);

    public static void SetEnableDrag(Control element, bool value) => element.SetValue(EnableDragProperty, value);

    static DraggableWindowBehavior()
    {
        EnableDragProperty.Changed.AddClassHandler<Control>((control, args) =>
        {
            if (args.NewValue is true)
                control.PointerPressed += OnPointerPressed;
            else
                control.PointerPressed -= OnPointerPressed;
        });
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;

        // Allow attaching to either a Window directly or any descendant control
        // (e.g. a custom title strip) — walk up to find the hosting Window.
        var window = control as Window ?? TopLevel.GetTopLevel(control) as Window;
        window?.BeginMoveDrag(e);
    }
}
