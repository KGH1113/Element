using System;
using System.IO;
using System.Reflection;
using UnityModManagerNet;

namespace ImplResourcePack.Launcher;

/// <summary>Loads a version's mod assembly and calls its UMM entry point.</summary>
internal static class PayloadLoader
{
  private static readonly object Sync = new();
  private static string _directory;
  private static bool _resolving;

  public static bool TryLoad(UnityModManager.ModEntry modEntry, RuntimeCandidate runtime, out Exception failure)
  {
    try
    {
      Load(modEntry, runtime);
      failure = null;
      return true;
    }
    catch (Exception exception)
    {
      failure = exception;
      return false;
    }
  }

  private static void Load(UnityModManager.ModEntry modEntry, RuntimeCandidate runtime)
  {
    int separator = runtime.EntryMethod.LastIndexOf('.');
    if (separator <= 0 || separator == runtime.EntryMethod.Length - 1)
      throw new InvalidDataException("EntryMethod must name a type and a method: " + runtime.EntryMethod);
    ResolveFrom(runtime.RuntimePath);
    Assembly assembly = Assembly.LoadFrom(runtime.AssemblyPath);
    string typeName = runtime.EntryMethod.Substring(0, separator);
    string methodName = runtime.EntryMethod.Substring(separator + 1);
    Type type = assembly.GetType(typeName, throwOnError: true);
    MethodInfo method =
      type.GetMethod(
        methodName,
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
        null,
        new[] { typeof(UnityModManager.ModEntry) },
        null
      ) ?? throw new MissingMethodException(typeName, methodName);
    object result;
    try
    {
      result = method.Invoke(null, new object[] { modEntry });
    }
    catch (TargetInvocationException exception) when (exception.InnerException != null)
    {
      throw exception.InnerException;
    }
    if (result is false)
      throw new InvalidOperationException(runtime.EntryMethod + " returned false. The UMM log has the reason.");
  }

  // The mod's own dependencies (AdofaiIpc) sit next to it in its version folder.
  private static void ResolveFrom(string directory)
  {
    lock (Sync)
    {
      _directory = directory;
      if (_resolving)
        return;
      AppDomain.CurrentDomain.AssemblyResolve += Resolve;
      _resolving = true;
    }
  }

  private static Assembly Resolve(object sender, ResolveEventArgs args)
  {
    string directory;
    lock (Sync)
      directory = _directory;
    if (string.IsNullOrEmpty(directory))
      return null;
    string path = Path.Combine(directory, new AssemblyName(args.Name).Name + ".dll");
    return File.Exists(path) ? Assembly.LoadFrom(path) : null;
  }
}
