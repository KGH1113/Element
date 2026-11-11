using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.Platform;

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
