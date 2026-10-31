using System;
using System.IO;
using Element.Updates;

namespace Element.Infrastructure.Ipc;

/// <summary>
/// ADOFAI-IPC finds runtimes at Mods/&lt;mod&gt;/ipc/manifest.json, so the bundle
/// this version ships is copied from its version folder to the mod root.
/// </summary>
internal static class BundledIpcFiles
{
  public static void PublishToModRoot(string runtimePath, string modRoot, Action<string> warn)
  {
    if (string.Equals(Path.GetFullPath(runtimePath), Path.GetFullPath(modRoot), StringComparison.OrdinalIgnoreCase))
      return;
    foreach (string file in ModLayout.IpcBundle)
    {
      string source = Path.Combine(runtimePath, file);
      if (!File.Exists(source))
        continue;
      try
      {
        AtomicFile.Copy(source, Path.Combine(modRoot, file));
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        // Another mod may already run the IPC runtime from this folder; it
        // stays loaded until the game restarts, and the copy is retried then.
        warn?.Invoke("The IPC bundle at the mod root is in use; it updates after a restart. " + exception.Message);
      }
    }
  }
}
