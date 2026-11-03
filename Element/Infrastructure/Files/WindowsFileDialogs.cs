using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Element.Application.Files;

namespace Element.Infrastructure.Files;

/// <summary>
/// The Explorer-style open and save windows from comdlg32, owned by the game
/// window. They run on the game's main thread and pump its messages meanwhile.
/// </summary>
internal sealed class WindowsFileDialogs : IFileDialogs
{
  private const int PathCapacity = 32768;
  private const int OverwritePrompt = 0x2;
  private const int HideReadOnly = 0x4;

  // Keeps the game's working directory where it was.
  private const int NoChangeDirectory = 0x8;
  private const int PathMustExist = 0x800;
  private const int FileMustExist = 0x1000;
  private const int Explorer = 0x80000;
  private const int EnableSizing = 0x800000;

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  private struct OpenFileName
  {
    public int StructSize;
    public IntPtr Owner;
    public IntPtr Instance;
    public string Filter;
    public IntPtr CustomFilter;
    public int MaxCustomFilter;
    public int FilterIndex;
    public IntPtr File;
    public int MaxFile;
    public IntPtr FileTitle;
    public int MaxFileTitle;
    public string InitialDirectory;
    public string Title;
    public int Flags;
    public short FileOffset;
    public short FileExtension;
    public string DefaultExtension;
    public IntPtr CustomData;
    public IntPtr Hook;
    public string TemplateName;
    public IntPtr Reserved;
    public int ReservedFlags;
    public int FlagsEx;
  }

  [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
  private static extern bool GetOpenFileName(ref OpenFileName dialog);

  [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetSaveFileNameW")]
  private static extern bool GetSaveFileName(ref OpenFileName dialog);

  [DllImport("comdlg32.dll")]
  private static extern int CommDlgExtendedError();

  [DllImport("user32.dll")]
  private static extern IntPtr GetActiveWindow();

  public FileDialogResult Open(FileDialogRequest request) =>
    Show(request, "", PathMustExist | FileMustExist, save: false);

  public FileDialogResult Save(FileDialogRequest request) =>
    Show(request, request.FileName, PathMustExist | OverwritePrompt, save: true);

  private static FileDialogResult Show(FileDialogRequest request, string fileName, int flags, bool save)
  {
    IntPtr buffer = Marshal.AllocHGlobal(PathCapacity * sizeof(char));
    try
    {
      char[] initial = new char[PathCapacity];
      string name = fileName ?? "";
      name.CopyTo(0, initial, 0, Math.Min(name.Length, PathCapacity - 1));
      Marshal.Copy(initial, 0, buffer, PathCapacity);
      string extension = request.Extension ?? "";
      OpenFileName dialog = new()
      {
        StructSize = Marshal.SizeOf<OpenFileName>(),
        Owner = GetActiveWindow(),
        // Pairs of display name and pattern; the marshaled string ends with the
        // second null the list needs.
        Filter = request.TypeName + " (*." + extension + ")\0*." + extension + "\0",
        FilterIndex = 1,
        File = buffer,
        MaxFile = PathCapacity,
        Title = request.Title,
        DefaultExtension = extension,
        Flags = flags | Explorer | NoChangeDirectory | HideReadOnly | EnableSizing,
      };
      bool chosen = save ? GetSaveFileName(ref dialog) : GetOpenFileName(ref dialog);
      if (chosen)
        return FileDialogResult.Chosen(Marshal.PtrToStringUni(buffer));
      int error = CommDlgExtendedError();
      return error == 0
        ? FileDialogResult.Cancelled
        : FileDialogResult.Unavailable(
          new Win32Exception(error, "The file window failed (CommDlgExtendedError 0x" + error.ToString("X") + ").")
        );
    }
    finally
    {
      Marshal.FreeHGlobal(buffer);
    }
  }
}
