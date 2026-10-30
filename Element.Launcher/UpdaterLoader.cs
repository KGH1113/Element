using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityModManagerNet;

namespace Element.Launcher;

/// <summary>Runs the updater that shipped with the running version.</summary>
internal static class UpdaterLoader
{
  private const string EntryType = "Element.Updater.EntryPoint";
  private const string EntryMethod = "Resolve";

  public static UpdaterResponse Resolve(
    UnityModManager.ModEntry modEntry,
    RuntimeCandidate current,
    IEnumerable<string> failed,
    Uri localSource
  )
  {
    if (!File.Exists(current.UpdaterPath))
      throw new FileNotFoundException("The running version has no updater.", current.UpdaterPath);
    Type type = Assembly.LoadFrom(current.UpdaterPath).GetType(EntryType, throwOnError: true);
    MethodInfo method =
      type.GetMethod(
        EntryMethod,
        BindingFlags.Public | BindingFlags.Static,
        null,
        new[] { typeof(UnityModManager.ModEntry), typeof(string) },
        null
      ) ?? throw new MissingMethodException(EntryType, EntryMethod);
    string request = JsonConvert.SerializeObject(
      new
      {
        InstallPath = modEntry.Path,
        CurrentVersion = current.Version,
        Skipped = failed?.ToArray() ?? Array.Empty<string>(),
        Source = localSource?.AbsoluteUri,
      }
    );
    object raw;
    try
    {
      raw = method.Invoke(null, new object[] { modEntry, request });
    }
    catch (TargetInvocationException exception) when (exception.InnerException != null)
    {
      throw exception.InnerException;
    }
    return UpdaterResponse.Parse(raw as string);
  }
}

internal sealed class UpdaterResponse
{
  public const string Candidate = "candidate";
  public const string Deferred = "deferred";
  public const string Skipped = "skipped";
  public const string Error = "error";

  public string Outcome { get; private set; }
  public string Version { get; private set; }
  public string RuntimePath { get; private set; }
  public string ErrorKind { get; private set; }

  public static UpdaterResponse Parse(string json)
  {
    JObject root = JObject.Parse(json ?? throw new InvalidDataException("The updater returned nothing."));
    return new UpdaterResponse
    {
      Outcome = root.Value<string>("Outcome"),
      Version = root.Value<string>("Version"),
      RuntimePath = root.Value<string>("RuntimePath"),
      ErrorKind = root.Value<string>("Error"),
    };
  }
}
