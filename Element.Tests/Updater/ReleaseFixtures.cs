extern alias launcher;
extern alias updater;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Element.Updates;
using Newtonsoft.Json.Linq;
using LauncherEntry = launcher::Element.Launcher.Entry;
using UpdateManager = updater::Element.Updater.UpdateManager;
using UpdateSource = updater::Element.Updater.UpdateSource;

namespace Element.Tests.Updater;

/// <summary>
/// Releases for the update tests: runtime files, packages, manifests and a local
/// server that hands them out.
/// </summary>
internal static class ReleaseFixtures
{
  internal const string Platform = "macos";

  internal static IEnumerable<(string File, byte[] Content)> RuntimeFiles(string version)
  {
    yield return (
      ModLayout.InfoFile,
      Encoding.UTF8.GetBytes(
        new JObject
        {
          ["Id"] = "Element",
          ["DisplayName"] = "Element",
          ["Version"] = version,
          ["AssemblyName"] = ModLayout.PayloadAssembly,
          ["EntryMethod"] = ModLayout.PayloadEntry,
        }.ToString()
      )
    );
    yield return (ModLayout.PayloadAssembly, File.ReadAllBytes(typeof(Main).Assembly.Location));
    yield return (ModLayout.LauncherAssembly, File.ReadAllBytes(typeof(LauncherEntry).Assembly.Location));
    yield return (ModLayout.UpdaterAssembly, File.ReadAllBytes(typeof(UpdateManager).Assembly.Location));
    string bundle =
      Environment.GetEnvironmentVariable("ADOFAI_IPC_BUNDLE")
      ?? throw new InvalidOperationException("Run the tests through ./scripts/run.sh so ADOFAI_IPC_BUNDLE is set.");
    foreach (string file in ModLayout.IpcBundle)
      yield return (file, File.ReadAllBytes(Path.Combine(bundle, file)));
    yield return ("ElementNote/macos/ElementNote.app.zip", Encoding.UTF8.GetBytes("app"));
  }

  internal static byte[] Package(string version, bool unsafeEntry = false)
  {
    using MemoryStream buffer = new();
    using (ZipArchive archive = new(buffer, ZipArchiveMode.Create, true))
    {
      string root = "Element/Runtime/versions/" + version + "/";
      Add(archive, "Element/Info.json", "{}");
      foreach ((string file, byte[] content) in RuntimeFiles(version))
        Add(archive, root + file, content);
      if (unsafeEntry)
        Add(archive, "../escape.txt", Encoding.UTF8.GetBytes("escaped"));
    }
    return buffer.ToArray();
  }

  private static void Add(ZipArchive archive, string name, string content) =>
    Add(archive, name, Encoding.UTF8.GetBytes(content));

  private static void Add(ZipArchive archive, string name, byte[] content)
  {
    using Stream entry = archive.CreateEntry(name).Open();
    entry.Write(content, 0, content.Length);
  }

  internal static string Manifest(
    string version,
    byte[] package,
    string sha256 = null,
    long? bytes = null,
    string asset = null,
    string runtimePath = null
  ) =>
    new JObject
    {
      ["schemaVersion"] = 1,
      ["version"] = version,
      ["packages"] = new JObject
      {
        [Platform] = new JObject
        {
          ["asset"] = asset ?? "Element-" + Platform + ".zip",
          ["bytes"] = bytes ?? package.Length,
          ["sha256"] = sha256 ?? Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(),
          ["runtimePath"] = runtimePath ?? "Element/Runtime/versions/" + version,
        },
      },
    }.ToString();

  /// <summary>Serves releases/latest/download/&lt;manifest&gt; and releases/download/v&lt;v&gt;/&lt;zip&gt;.</summary>
  internal sealed class ReleaseServer : IDisposable
  {
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly byte[] _manifest;
    private readonly byte[] _package;
    private readonly Task _worker;
    private int _packageRequests;

    public ReleaseServer(string manifest, byte[] package)
    {
      int port = FreePort();
      Releases = new Uri($"http://127.0.0.1:{port}/releases/");
      _manifest = manifest == null ? null : Encoding.UTF8.GetBytes(manifest);
      _package = package;
      _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
      _listener.Start();
      _worker = Task.Run(Serve);
    }

    public Uri Releases { get; }
    public TimeSpan PackageDelay { get; set; }
    public int PackageRequests => Volatile.Read(ref _packageRequests);

    private async Task Serve()
    {
      while (!_stop.IsCancellationRequested)
      {
        HttpListenerContext context;
        try
        {
          context = await _listener.GetContextAsync();
        }
        catch when (_stop.IsCancellationRequested)
        {
          return;
        }
        _ = Task.Run(() => Respond(context));
      }
    }

    private async Task Respond(HttpListenerContext context)
    {
      try
      {
        string path = context.Request.Url?.AbsolutePath ?? "";
        byte[] body = null;
        if (path == "/releases/latest/download/" + UpdateSource.ManifestAsset)
          body = _manifest;
        else if (
          path.StartsWith("/releases/download/v", StringComparison.Ordinal)
          && path.EndsWith(".zip", StringComparison.Ordinal)
        )
        {
          Interlocked.Increment(ref _packageRequests);
          await Task.Delay(PackageDelay);
          body = _package;
        }
        if (body == null)
        {
          context.Response.StatusCode = 404;
          context.Response.Close();
          return;
        }
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
      }
      catch (HttpListenerException) { }
      catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
      _stop.Cancel();
      _listener.Stop();
      try
      {
        _worker.Wait(TimeSpan.FromSeconds(1));
      }
      catch (AggregateException) { }
      _listener.Close();
      _stop.Dispose();
    }

    private static int FreePort()
    {
      var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      int port = ((IPEndPoint)listener.LocalEndpoint).Port;
      listener.Stop();
      return port;
    }
  }
}
