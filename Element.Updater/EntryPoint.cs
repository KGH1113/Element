using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Element.Updates;
using Newtonsoft.Json;
using UnityModManagerNet;

namespace Element.Updater;

/// <summary>
/// The launcher calls <see cref="Resolve"/> by reflection from the running
/// version's folder, passing JSON both ways, so a launcher and updater from
/// different versions keep understanding each other.
/// </summary>
public static class EntryPoint
{
  public static string Resolve(UnityModManager.ModEntry modEntry, string requestJson)
  {
    UpdateManager manager = null;
    UpdateRequest request = null;
    try
    {
      request =
        JsonConvert.DeserializeObject<UpdateRequest>(requestJson)
        ?? throw new InvalidDataException("The update request is empty.");
      string platform = CurrentPlatform();
      if (platform == null)
        return Serialize(UpdateResult.None());
      UpdateSource source = request.Source != null ? UpdateSource.Local(new Uri(request.Source)) : UpdateSource.GitHub;
      manager = new UpdateManager(request.InstallPath, platform, source);
      UpdateResult result = manager.Resolve(request.CurrentVersion, request.Skipped);
      if (result.Completion == null)
      {
        manager.Dispose();
        return Serialize(result);
      }
      FinishInBackground(modEntry, manager, request.CurrentVersion, result);
      return Serialize(result);
    }
    catch (Exception exception)
    {
      manager?.Dispose();
      string error = Classify(exception);
      modEntry?.Logger.Warning("[Update] Check failed (" + error + "): " + exception);
      return Serialize(UpdateResult.Failed(error));
    }
  }

  /// <summary>Settings shows the download until it lands; the next launch applies it.</summary>
  private static void FinishInBackground(
    UnityModManager.ModEntry modEntry,
    UpdateManager manager,
    string running,
    UpdateResult result
  )
  {
    UpdateReport.Of(UpdateOutcomes.Downloading, running, result.Version).Publish();
    modEntry?.Logger.Log("[Update] " + result.Version + " is still downloading; it applies next launch.");
    result.Completion.ContinueWith(
      completed =>
      {
        try
        {
          if (completed.Status == TaskStatus.RanToCompletion)
          {
            UpdateReport.Of(UpdateOutcomes.Ready, running, result.Version).Publish();
            modEntry?.Logger.Log("[Update] " + result.Version + " downloaded; it applies next launch.");
          }
          else
          {
            Exception exception = completed.Exception?.GetBaseException() ?? new OperationCanceledException();
            string error = Classify(exception);
            UpdateReport.Of(UpdateOutcomes.Error, running, result.Version, error).Publish();
            modEntry?.Logger.Warning("[Update] Download of " + result.Version + " failed (" + error + "): " + exception);
          }
        }
        finally
        {
          manager.Dispose();
        }
      },
      TaskScheduler.Default
    );
  }

  internal static string CurrentPlatform()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return "macos";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "windows";
    return null;
  }

  internal static string Classify(Exception exception)
  {
    for (Exception current = exception; current != null; current = current.InnerException)
    {
      switch (current)
      {
        case HttpRequestException or WebException or SocketException or TimeoutException:
        case OperationCanceledException:
          return UpdateErrors.Network;
        case InvalidDataException:
          return UpdateErrors.Damaged;
        case IOException or UnauthorizedAccessException:
          return UpdateErrors.Disk;
      }
    }
    return UpdateErrors.Unknown;
  }

  private static string Serialize(UpdateResult result) => JsonConvert.SerializeObject(result);
}

internal sealed class UpdateRequest
{
  public string InstallPath { get; set; }
  public string CurrentVersion { get; set; }
  public string[] Skipped { get; set; }

  /// <summary>A loopback test server already validated by the launcher, or null for GitHub.</summary>
  public string Source { get; set; }
}
