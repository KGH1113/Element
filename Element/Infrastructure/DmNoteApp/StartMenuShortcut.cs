using System;
using System.IO;
using System.Text;

namespace Element.Infrastructure.DmNoteApp;

/// <summary>
/// A shortcut in the current user's Start Menu. The shell writes it through
/// IShellLinkW, which keeps non-ASCII user folders intact where WScript.Shell
/// turns them into question marks. That runs in a short PowerShell process, so
/// a failure stays out of the game.
/// </summary>
internal static class StartMenuShortcut
{
  // Add-Type compiles the shell interface first; a slow PC with antivirus
  // scanning may need a while, and this runs off the game thread.
  private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

  public static string PathFor(string name)
  {
    string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
    if (string.IsNullOrEmpty(programs))
      programs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft",
        "Windows",
        "Start Menu",
        "Programs"
      );
    return Path.Combine(programs, name + ".lnk");
  }

  public static void Create(string shortcut, string target, string workingDirectory, string description)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(shortcut));
    SystemCommand.RunOrThrow(
      Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
      "-NoProfile -NonInteractive -EncodedCommand " + Encode(Script(shortcut, target, workingDirectory, description)),
      Timeout
    );
  }

  /// <summary>PowerShell's -EncodedCommand form: Base64 of UTF-16LE, so no quoting reaches the command line.</summary>
  internal static string Encode(string script) => Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

  // Kept to C# 5: Windows PowerShell 5.1 compiles Add-Type with the .NET Framework compiler.
  internal static string Script(string shortcut, string target, string workingDirectory, string description) =>
    "$ErrorActionPreference = 'Stop'\n"
    + "$ProgressPreference = 'SilentlyContinue'\n"
    + "try {\n"
    + "Add-Type -TypeDefinition @'\n"
    + "using System;\n"
    + "using System.Runtime.InteropServices;\n"
    + "using System.Runtime.InteropServices.ComTypes;\n"
    + "using System.Text;\n"
    + "[ComImport, Guid(\"00021401-0000-0000-C000-000000000046\")]\n"
    + "public class ElementNoteShellLink { }\n"
    + "[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid(\"000214F9-0000-0000-C000-000000000046\")]\n"
    + "public interface IElementNoteShellLink {\n"
    + "  void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int length, IntPtr data, int flags);\n"
    + "  void GetIDList(out IntPtr list);\n"
    + "  void SetIDList(IntPtr list);\n"
    + "  void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int length);\n"
    + "  void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);\n"
    + "  void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int length);\n"
    + "  void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);\n"
    + "  void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int length);\n"
    + "  void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);\n"
    + "  void GetHotkey(out short hotkey);\n"
    + "  void SetHotkey(short hotkey);\n"
    + "  void GetShowCmd(out int command);\n"
    + "  void SetShowCmd(int command);\n"
    + "  void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, out int index);\n"
    + "  void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);\n"
    + "  void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, int reserved);\n"
    + "  void Resolve(IntPtr window, int flags);\n"
    + "  void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);\n"
    + "}\n"
    + "public static class ElementNoteShortcut {\n"
    + "  public static void Save(string shortcut, string target, string directory, string description) {\n"
    + "    IElementNoteShellLink link = (IElementNoteShellLink)new ElementNoteShellLink();\n"
    + "    link.SetPath(target);\n"
    + "    link.SetWorkingDirectory(directory);\n"
    + "    link.SetIconLocation(target, 0);\n"
    + "    link.SetDescription(description);\n"
    + "    ((IPersistFile)link).Save(shortcut, true);\n"
    + "  }\n"
    + "}\n"
    + "'@\n"
    + "[ElementNoteShortcut]::Save("
    + Literal(shortcut)
    + ", "
    + Literal(target)
    + ", "
    + Literal(workingDirectory)
    + ", "
    + Literal(description)
    + ")\n"
    + "} catch {\n"
    + "[Console]::Error.WriteLine($_.Exception.Message)\n"
    + "exit 1\n"
    + "}\n";

  // PowerShell also ends single-quoted strings at the curly quotes ‘ ’ ‚ ‛.
  internal static string Literal(string value)
  {
    var text = new StringBuilder("'");
    foreach (char c in value)
    {
      if (c is '\'' or '‘' or '’' or '‚' or '‛')
        text.Append(c);
      text.Append(c);
    }
    return text.Append('\'').ToString();
  }
}
