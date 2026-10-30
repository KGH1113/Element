using System;
using Newtonsoft.Json;

namespace Element.Updates;

/// <summary>What happened to updates this launch. The launcher and updater
/// publish it; the mod shows it in Settings → General.</summary>
internal sealed class UpdateReport
{
  private const string Key = "Element.UpdateReport";
  private static readonly object Sync = new();

  public string Outcome { get; set; }

  /// <summary>The version running now.</summary>
  public string Version { get; set; }

  /// <summary>The newer version this outcome is about, when there is one.</summary>
  public string Target { get; set; }

  /// <summary>One of <see cref="UpdateErrors"/> when <see cref="Outcome"/> is an error.</summary>
  public string Error { get; set; }

  public static UpdateReport Of(string outcome, string version, string target = null, string error = null) =>
    new()
    {
      Outcome = outcome,
      Version = version,
      Target = target,
      Error = error,
    };

  public void Publish()
  {
    lock (Sync)
      AppDomain.CurrentDomain.SetData(Key, JsonConvert.SerializeObject(this));
  }

  /// <summary>Null before the launcher has decided, or when the mod runs without it.</summary>
  public static UpdateReport Current() => Parse(CurrentJson());

  /// <summary>The published report as JSON; unchanged reports are the same string instance.</summary>
  public static string CurrentJson()
  {
    lock (Sync)
      return AppDomain.CurrentDomain.GetData(Key) as string;
  }

  public static UpdateReport Parse(string json)
  {
    if (json == null)
      return null;
    try
    {
      return JsonConvert.DeserializeObject<UpdateReport>(json);
    }
    catch (JsonException)
    {
      return null;
    }
  }
}

internal static class UpdateOutcomes
{
  public const string UpToDate = "up-to-date";
  /// <summary>This launch switched to <see cref="UpdateReport.Version"/>.</summary>
  public const string Updated = "updated";
  /// <summary>The download outlasted startup; it continues and applies next launch.</summary>
  public const string Downloading = "downloading";
  /// <summary>A background download finished; it applies next launch.</summary>
  public const string Ready = "ready";
  /// <summary>The target failed to start once and is not offered again.</summary>
  public const string Skipped = "skipped";
  public const string Error = "error";
  public const string Off = "off";
  public const string Development = "development";
}

internal static class UpdateErrors
{
  public const string Network = "network";
  public const string Damaged = "damaged";
  public const string Disk = "disk";
  public const string Unknown = "unknown";
}
