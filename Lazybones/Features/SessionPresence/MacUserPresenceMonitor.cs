using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Threading;

namespace Lazybones.Features.SessionPresence;

// Observes the macOS screen-lock / screen-unlock distributed notifications by
// bridging into the Objective-C runtime via raw libobjc P/Invoke.
//
// The shape mirrors what [NSDistributedNotificationCenter] addObserver:selector:
// name:object: would do from Objective-C:
//   1. Dynamically create a one-off subclass of NSObject with two callback
//      methods (handleLock:, handleUnlock:).
//   2. Allocate one instance of that subclass; keep it alive for the monitor's
//      lifetime.
//   3. Register that instance against the default distributed notification
//      center for the two notification names.
// The selector callbacks fire on the main thread; we still hop through
// Dispatcher.UIThread.Post to be explicit about thread affinity.
[SupportedOSPlatform("macos")]
public sealed partial class MacUserPresenceMonitor : IUserPresenceMonitor
{
    private const string ObjC = "/usr/lib/libobjc.dylib";

    public event EventHandler? Locked;
    public event EventHandler? Unlocked;

    // GC anchors: the function pointers we hand to ObjC must stay valid for
    // the monitor's lifetime, which means the underlying delegates must not
    // be collected.
    private readonly NotificationCallback _onLock;
    private readonly NotificationCallback _onUnlock;
    private IntPtr _observer;
    private bool _disposed;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NotificationCallback(IntPtr self, IntPtr cmd, IntPtr notification);

    public MacUserPresenceMonitor()
    {
        _onLock = OnLockNotification;
        _onUnlock = OnUnlockNotification;

        // Build a fresh subclass of NSObject. The Guid keeps the class name
        // unique per process so multiple instances (or reload-after-crash)
        // don't collide with the runtime's already-registered classes.
        var nsObject = objc_getClass("NSObject");
        var className = $"LazybonesLockObserver_{Guid.NewGuid():N}";
        var cls = objc_allocateClassPair(nsObject, className, IntPtr.Zero);

        var handleLockSel = sel_registerName("handleLock:");
        var handleUnlockSel = sel_registerName("handleUnlock:");

        // Method type encoding "v@:@" = void(id self, SEL _cmd, id notification)
        class_addMethod(cls, handleLockSel, Marshal.GetFunctionPointerForDelegate(_onLock), "v@:@");
        class_addMethod(cls, handleUnlockSel, Marshal.GetFunctionPointerForDelegate(_onUnlock), "v@:@");
        objc_registerClassPair(cls);

        // [[cls alloc] init] → owned reference, released in Dispose.
        var allocated = MsgSend(cls, sel_registerName("alloc"));
        _observer = MsgSend(allocated, sel_registerName("init"));

        // [[NSDistributedNotificationCenter defaultCenter]
        //   addObserver:_observer selector:handleLockSel
        //   name:@"com.apple.screenIsLocked" object:nil]
        var dncClass = objc_getClass("NSDistributedNotificationCenter");
        var center = MsgSend(dncClass, sel_registerName("defaultCenter"));
        var addObserverSel = sel_registerName("addObserver:selector:name:object:");
        var nsString = objc_getClass("NSString");
        var fromUtf8Sel = sel_registerName("stringWithUTF8String:");

        var lockName = MsgSendUtf8(nsString, fromUtf8Sel, "com.apple.screenIsLocked");
        var unlockName = MsgSendUtf8(nsString, fromUtf8Sel, "com.apple.screenIsUnlocked");

        MsgSend(center, addObserverSel, _observer, handleLockSel, lockName, IntPtr.Zero);
        MsgSend(center, addObserverSel, _observer, handleUnlockSel, unlockName, IntPtr.Zero);
    }

    private void OnLockNotification(IntPtr self, IntPtr cmd, IntPtr notification)
    {
        Dispatcher.UIThread.Post(() => Locked?.Invoke(this, EventArgs.Empty));
    }

    private void OnUnlockNotification(IntPtr self, IntPtr cmd, IntPtr notification)
    {
        Dispatcher.UIThread.Post(() => Unlocked?.Invoke(this, EventArgs.Empty));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_observer != IntPtr.Zero)
        {
            var dncClass = objc_getClass("NSDistributedNotificationCenter");
            var center = MsgSend(dncClass, sel_registerName("defaultCenter"));
            MsgSend(center, sel_registerName("removeObserver:"), _observer);
            MsgSend(_observer, sel_registerName("release"));
            _observer = IntPtr.Zero;
        }
        // The dynamically-allocated class can't be unregistered in the ObjC
        // runtime, but it costs negligible memory and lives until process exit.
    }

    // ---- libobjc surface -------------------------------------------------

    [LibraryImport(ObjC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_getClass(string name);

    [LibraryImport(ObjC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport(ObjC, EntryPoint = "objc_allocateClassPair", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_allocateClassPair(IntPtr superclass, string name, IntPtr extraBytes);

    [LibraryImport(ObjC, EntryPoint = "objc_registerClassPair")]
    private static partial void objc_registerClassPair(IntPtr cls);

    [LibraryImport(ObjC, EntryPoint = "class_addMethod", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool class_addMethod(IntPtr cls, IntPtr name, IntPtr imp, string types);

    // objc_msgSend is one symbol that is cast to the right signature at every
    // call site. .NET lets us declare per-signature overloads against the same
    // EntryPoint.

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr MsgSend(IntPtr receiver, IntPtr sel);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr MsgSend(IntPtr receiver, IntPtr sel, IntPtr a1);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr MsgSend(
        IntPtr receiver, IntPtr sel,
        IntPtr a1, IntPtr a2, IntPtr a3, IntPtr a4);

    // For [NSString stringWithUTF8String:cstr] — auto-marshals the .NET string
    // to a UTF-8 C string for the call.
    [LibraryImport(ObjC, EntryPoint = "objc_msgSend", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr MsgSendUtf8(IntPtr receiver, IntPtr sel, string a1);
}
