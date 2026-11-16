using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.Platform;

// NSOpenPanel through the Objective-C runtime, so no native library is needed.
// runModal keeps the panel in front of the game, also in full screen.
internal sealed class MacFontFilePicker : IFontFilePicker
{
  private const string ObjC = "/usr/lib/libobjc.A.dylib";
  private const long ModalResponseOk = 1;

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
  private static extern void SendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool value);

  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  private static extern IntPtr SendUtf8(
    IntPtr receiver,
    IntPtr selector,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string value
  );

  [DllImport(ObjC, EntryPoint = "objc_msgSend")]
  private static extern long SendLong(IntPtr receiver, IntPtr selector);

  public IReadOnlyList<string> Pick(string title)
  {
    List<string> paths = new();
    IntPtr pool = Send(Send(objc_getClass("NSAutoreleasePool"), Selector("alloc")), Selector("init"));
    try
    {
      IntPtr panel = Send(objc_getClass("NSOpenPanel"), Selector("openPanel"));
      SendBool(panel, Selector("setCanChooseFiles:"), true);
      SendBool(panel, Selector("setCanChooseDirectories:"), false);
      SendBool(panel, Selector("setAllowsMultipleSelection:"), true);
      Send(panel, Selector("setMessage:"), Text(title));
      IntPtr types = Send(objc_getClass("NSMutableArray"), Selector("array"));
      foreach (string extension in FontFilePickers.Extensions)
        Send(types, Selector("addObject:"), Text(extension));
      // Deprecated since macOS 12 but still honored, and it needs no UTType lookup.
      Send(panel, Selector("setAllowedFileTypes:"), types);
      if (SendLong(panel, Selector("runModal")) != ModalResponseOk)
        return paths;
      IntPtr urls = Send(panel, Selector("URLs"));
      long count = SendLong(urls, Selector("count"));
      for (long i = 0; i < count; i++)
      {
        IntPtr url = SendIndex(urls, Selector("objectAtIndex:"), (UIntPtr)(ulong)i);
        IntPtr path = Send(url, Selector("path"));
        IntPtr utf8 = path == IntPtr.Zero ? IntPtr.Zero : Send(path, Selector("UTF8String"));
        string value = utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
        if (!string.IsNullOrEmpty(value))
          paths.Add(value);
      }
      return paths;
    }
    finally
    {
      Send(pool, Selector("drain"));
    }
  }

  private static IntPtr Selector(string name) => sel_registerName(name);

  private static IntPtr Text(string value) =>
    SendUtf8(objc_getClass("NSString"), Selector("stringWithUTF8String:"), value ?? string.Empty);
}
