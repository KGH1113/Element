using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.App;

// ElementNote has no Dock icon, so only an activation brings it forward; its
// own request would be refused while the game is in front (macOS 14 and later).
// Through the Objective-C runtime, so no native library is needed.
internal sealed class MacDmNoteAppFocus : IDmNoteAppFocus
{
  private const string ObjC = "/usr/lib/libobjc.A.dylib";
  private const string BundleIdentifier = "io.github.kgh1113.elementnote";

  // NSApplicationActivateIgnoringOtherApps: needed before macOS 14, ignored since.
  private const ulong IgnoringOtherApps = 1 << 1;

  [DllImport(ObjC)]
  private static extern IntPtr objc_getClass(string name);

  [DllImport(ObjC)]
  private static extern IntPtr sel_registerName(string name);

  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  private static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  private static extern IntPtr SendIndex(IntPtr receiver, IntPtr selector, UIntPtr index);

  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  private static extern long SendLong(IntPtr receiver, IntPtr selector);

  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  [return: MarshalAs(UnmanagedType.I1)]
  private static extern bool SendBool(IntPtr receiver, IntPtr selector);

  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  [return: MarshalAs(UnmanagedType.I1)]
  private static extern bool SendOptions(IntPtr receiver, IntPtr selector, UIntPtr options);

  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  private static extern IntPtr SendUtf8(
    IntPtr receiver,
    IntPtr selector,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string value
  );

  public void PassFocus()
  {
    IntPtr pool = Send(Send(objc_getClass("NSAutoreleasePool"), Selector("alloc")), Selector("init"));
    try
    {
      IntPtr identifier = SendUtf8(objc_getClass("NSString"), Selector("stringWithUTF8String:"), BundleIdentifier);
      IntPtr apps = Send(
        objc_getClass("NSRunningApplication"),
        Selector("runningApplicationsWithBundleIdentifier:"),
        identifier
      );
      long count = SendLong(apps, Selector("count"));
      for (long i = 0; i < count; i++)
      {
        IntPtr app = SendIndex(apps, Selector("objectAtIndex:"), (UIntPtr)(ulong)i);
        if (SendBool(app, Selector("isTerminated")))
          continue;
        SendOptions(app, Selector("activateWithOptions:"), (UIntPtr)IgnoringOtherApps);
        return;
      }
    }
    finally
    {
      Send(pool, Selector("drain"));
    }
  }

  private static IntPtr Selector(string name) => sel_registerName(name);
}
