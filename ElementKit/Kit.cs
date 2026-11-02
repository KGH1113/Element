using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using ElementKit.Hosting;
using UnityModManagerNet;

namespace ElementKit;

/// <summary>
/// Connects your mod to Element. Write your mod as usual (read the game,
/// patch it with Harmony) and hand what you work out to Element as values;
/// players place and style them with Element's editor.
/// </summary>
/// <example>
/// <code>
/// public static bool Load(UnityModManager.ModEntry mod)
/// {
///     new Harmony(mod.Info.Id).PatchAll();
///     return Kit.Attach(mod);
/// }
/// </code>
/// </example>
public static class Kit
{
  /// <summary>What this ElementKit offers. It only grows; a mod can check it before using something newer.</summary>
  public const int ApiLevel = 1;

  /// <summary>
  /// Adds the values your mod keeps in static fields. Call it once from your
  /// mod's <c>Load</c>. A UMM reload attaches again and replaces what the
  /// earlier load added; texts that use your values keep them.
  /// </summary>
  /// <returns>True, so <c>return Kit.Attach(mod);</c> can end <c>Load</c>.</returns>
  /// <remarks>
  /// Problems, such as a key texts can't save, are written to your mod's
  /// log; only the value with the problem is left out.
  /// </remarks>
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static bool Attach(UnityModManager.ModEntry mod)
  {
    if (mod == null)
      throw new ArgumentNullException(nameof(mod));
    Assembly assembly = mod.Assembly ?? Assembly.GetCallingAssembly();
    UnityModManager.ModEntry entry = mod;
    KitRegistry.Attach(mod.Info.Id, assembly, () => entry.Active, message => entry.Logger.Error(message));
    // Detach when UMM unloads or reloads the mod, after its own handler.
    // Having an unload handler also turns on UMM's Reload button.
    Func<UnityModManager.ModEntry, bool> previous = mod.OnUnload;
    mod.OnUnload = unloading =>
    {
      bool unloaded = previous == null || previous(unloading);
      if (unloaded)
        Detach(unloading);
      return unloaded;
    };
    return true;
  }

  /// <summary>Takes your mod's values out of Element. Texts keep them and show nothing until you attach again.</summary>
  public static void Detach(UnityModManager.ModEntry mod)
  {
    if (mod != null)
      KitRegistry.Detach(mod.Info.Id);
  }
}
