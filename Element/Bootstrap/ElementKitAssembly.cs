using System;
using System.Reflection;

namespace Element.Bootstrap;

/// <summary>
/// Other mods bind to the ElementKit that Element ships. A request for
/// ElementKit, whatever version it names, gets that one. A second copy loaded
/// from another mod's folder is reported: that mod's values would go to the
/// copy and never reach Element.
/// </summary>
internal static class ElementKitAssembly
{
  private const string Name = "ElementKit";
  private static readonly Assembly Shipped = typeof(ElementKit.Kit).Assembly;
  private static Action<string> _warn;
  private static bool _installed;

  public static void Install(Action<string> warn)
  {
    _warn = warn;
    if (_installed)
      return;
    _installed = true;
    AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    AppDomain.CurrentDomain.AssemblyLoad += Loaded;
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
      Check(assembly);
  }

  public static void Uninstall()
  {
    if (!_installed)
      return;
    _installed = false;
    AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
    AppDomain.CurrentDomain.AssemblyLoad -= Loaded;
  }

  private static Assembly Resolve(object sender, ResolveEventArgs args) => IsElementKit(args.Name) ? Shipped : null;

  private static void Loaded(object sender, AssemblyLoadEventArgs args) => Check(args.LoadedAssembly);

  private static void Check(Assembly assembly)
  {
    if (assembly == null || assembly == Shipped || !IsElementKit(assembly.FullName))
      return;
    string location;
    try
    {
      location = string.IsNullOrEmpty(assembly.Location) ? "another mod" : assembly.Location;
    }
    catch (Exception)
    {
      location = "another mod";
    }
    _warn?.Invoke(
      "Another ElementKit.dll was loaded from "
        + location
        + ". The mod in that folder brought its own copy, so its values can't reach Element. Delete that ElementKit.dll; Element provides it."
    );
  }

  private static bool IsElementKit(string fullName)
  {
    if (string.IsNullOrEmpty(fullName))
      return false;
    try
    {
      return new AssemblyName(fullName).Name == Name;
    }
    catch (Exception)
    {
      return false;
    }
  }
}
