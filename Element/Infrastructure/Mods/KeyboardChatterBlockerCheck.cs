using System;
using System.Reflection;

namespace Element.Infrastructure.Mods;

/// <summary>
/// KeyboardChatterBlocker filters the same keys Element does: ADOFAI's key limiter
/// now carries ElementNote's keys, and Tweaks has a chatter blocker. When that mod
/// is loaded, the log says which of its options to turn off.
/// </summary>
internal static class KeyboardChatterBlockerCheck
{
  private const string AssemblyName = "KeyboardChatterBlocker";

  public static void WarnIfLoaded(Action<string> logWarning)
  {
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      if (!string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
        continue;

      logWarning(
        "[Input] KeyboardChatterBlocker is loaded. ADOFAI's key limiter now carries ElementNote's keys, and Tweaks > Chatter blocker replaces its chatter filter; turn its limiter and chatter filter off to avoid filtering input twice."
      );
      return;
    }
  }
}
