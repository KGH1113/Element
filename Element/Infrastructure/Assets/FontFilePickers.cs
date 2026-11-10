using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.Assets;

/// <summary>The system's open dialog for font files. Blocks until it closes; main thread only.</summary>
internal interface IFontFilePicker
{
  /// <summary>The chosen files, or none when the dialog was cancelled.</summary>
  IReadOnlyList<string> Pick(string title);
}

internal static class FontFilePickers
{
  public static readonly string[] Extensions = { "ttf", "otf", "ttc", "otc" };

  /// <summary>Null where the mod has no dialog (Linux).</summary>
  public static IFontFilePicker ForPlatform() =>
    UnityEngine.Application.platform switch
    {
      RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new MacFontFilePicker(),
      RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => new WindowsFontFilePicker(),
      _ => null,
    };
}

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

// The classic common dialog: owned by the game window so it stays in front,
// and its own message loop keeps the window responsive while it is open.
internal sealed class WindowsFontFilePicker : IFontFilePicker
{
  private const int BufferCharacters = 32768;
  private const int Flags =
    0x00080000 // OFN_EXPLORER
    | 0x00001000 // OFN_FILEMUSTEXIST
    | 0x00000800 // OFN_PATHMUSTEXIST
    | 0x00000200 // OFN_ALLOWMULTISELECT
    | 0x00000008; // OFN_NOCHANGEDIR

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  private struct OpenFileName
  {
    public int lStructSize;
    public IntPtr hwndOwner;
    public IntPtr hInstance;
    public string lpstrFilter;
    public IntPtr lpstrCustomFilter;
    public int nMaxCustFilter;
    public int nFilterIndex;
    public IntPtr lpstrFile;
    public int nMaxFile;
    public IntPtr lpstrFileTitle;
    public int nMaxFileTitle;
    public string lpstrInitialDir;
    public string lpstrTitle;
    public int Flags;
    public short nFileOffset;
    public short nFileExtension;
    public string lpstrDefExt;
    public IntPtr lCustData;
    public IntPtr lpfnHook;
    public string lpTemplateName;
    public IntPtr pvReserved;
    public int dwReserved;
    public int FlagsEx;
  }

  [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool GetOpenFileName(ref OpenFileName dialog);

  [DllImport("user32.dll")]
  private static extern IntPtr GetActiveWindow();

  public IReadOnlyList<string> Pick(string title)
  {
    List<string> paths = new();
    string patterns = "*." + string.Join(";*.", FontFilePickers.Extensions);
    IntPtr buffer = Marshal.AllocHGlobal(BufferCharacters * 2);
    try
    {
      Marshal.WriteInt16(buffer, 0);
      OpenFileName dialog = new()
      {
        lStructSize = Marshal.SizeOf<OpenFileName>(),
        hwndOwner = GetActiveWindow(),
        lpstrFilter = "Fonts (" + patterns + ")\0" + patterns + "\0\0",
        nFilterIndex = 1,
        lpstrFile = buffer,
        nMaxFile = BufferCharacters,
        lpstrTitle = title,
        Flags = Flags,
      };
      if (!GetOpenFileName(ref dialog))
        return paths;
      // One file is its full path; several are the folder, then each name, then an empty string.
      List<string> parts = new();
      int offset = 0;
      while (offset < BufferCharacters)
      {
        string part = Marshal.PtrToStringUni(IntPtr.Add(buffer, offset * 2));
        if (string.IsNullOrEmpty(part))
          break;
        parts.Add(part);
        offset += part.Length + 1;
      }
      if (parts.Count == 1)
        paths.Add(parts[0]);
      else
        for (int i = 1; i < parts.Count; i++)
          paths.Add(Path.Combine(parts[0], parts[i]));
      return paths;
    }
    finally
    {
      Marshal.FreeHGlobal(buffer);
    }
  }
}
