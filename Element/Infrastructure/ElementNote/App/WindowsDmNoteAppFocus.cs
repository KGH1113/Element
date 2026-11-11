using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.App;

// Windows refuses SetForegroundWindow from an app in the background unless the
// app in front allows it. Allowed, ElementNote takes the front when it shows
// its window, without the simulated Alt press its window library falls back to.
internal sealed class WindowsDmNoteAppFocus : IDmNoteAppFocus
{
  [DllImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool AllowSetForegroundWindow(int processId);

  public void PassFocus()
  {
    Process[] running = Process.GetProcessesByName(WindowsDmNoteApp.ProcessName);
    try
    {
      foreach (Process process in running)
        AllowSetForegroundWindow(process.Id);
    }
    finally
    {
      foreach (Process process in running)
        process.Dispose();
    }
  }
}
