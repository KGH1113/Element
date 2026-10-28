using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ImplResourcePack.Infrastructure.DmNoteApp;

/// <summary>Runs a short system tool; callers stay off the game thread.</summary>
internal static class SystemCommand
{
  public static int Run(string file, string arguments, TimeSpan timeout) => Run(file, arguments, timeout, out _);

  public static void RunOrThrow(string file, string arguments, TimeSpan timeout)
  {
    int code = Run(file, arguments, timeout, out string error);
    if (code != 0)
      throw new IOException(Path.GetFileName(file) + " exited with " + code + (error.Length > 0 ? ": " + error : "."));
  }

  public static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

  private static int Run(string file, string arguments, TimeSpan timeout, out string error)
  {
    var stderr = new StringBuilder();
    using Process process = new()
    {
      StartInfo = new ProcessStartInfo(file, arguments)
      {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
      },
    };
    // Drain both pipes so a chatty tool cannot block on a full buffer.
    process.OutputDataReceived += (_, _) => { };
    process.ErrorDataReceived += (_, line) =>
    {
      if (line.Data == null)
        return;
      lock (stderr)
        stderr.AppendLine(line.Data);
    };
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    if (!process.WaitForExit((int)timeout.TotalMilliseconds))
    {
      try
      {
        process.Kill();
      }
      catch (InvalidOperationException) { }
      throw new TimeoutException(Path.GetFileName(file) + " did not finish in time.");
    }
    // The timed overload can return before the output callbacks finish.
    process.WaitForExit();
    lock (stderr)
      error = stderr.ToString().Trim();
    return process.ExitCode;
  }
}
