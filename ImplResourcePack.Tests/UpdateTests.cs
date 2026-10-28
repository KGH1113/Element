extern alias launcher;
extern alias updater;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImplResourcePack.Updates;
using Newtonsoft.Json.Linq;
using LauncherEntry = launcher::ImplResourcePack.Launcher.Entry;
using LauncherInfo = launcher::ImplResourcePack.Launcher.LauncherInfo;
using ReleaseManifest = updater::ImplResourcePack.Updater.ReleaseManifest;
using RuntimeState = launcher::ImplResourcePack.Launcher.RuntimeState;
using RuntimeStore = launcher::ImplResourcePack.Launcher.RuntimeStore;
using UpdateManager = updater::ImplResourcePack.Updater.UpdateManager;
using UpdateResult = updater::ImplResourcePack.Updater.UpdateResult;
using UpdateSource = updater::ImplResourcePack.Updater.UpdateSource;
using UpdateTimings = updater::ImplResourcePack.Updater.UpdateTimings;

namespace ImplResourcePack.Tests;

/// <summary>The launcher's version bookkeeping and the updater against a local release server.</summary>
internal static class UpdateTests
{
  private const string Platform = "macos";

  public static void Run()
  {
    ComparesVersions();
    ValidatesManifests();
    InstallsANewerPackage();
    IgnoresTheSameOrAnOlderRelease();
    TreatsAMissingManifestAsNoUpdate();
    SkipsAVersionThatFailedToStart();
    RejectsAPackageWithTheWrongChecksum();
    RejectsAPackageWithTheWrongSize();
    RejectsAPackageThatEscapesItsFolder();
    ReusesADownloadedVersion();
    FinishesASlowDownloadInTheBackground();
    RecordsAnInterruptedTrialAsFailed();
    PromotesAndKeepsOnePreviousVersion();
    KeepsANewerDownloadForTheNextLaunch();
    RollsBackADamagedCurrentVersion();
    RemovesTheLegacyRootPayloadButNotSettings();
    PointsInfoJsonAtTheNewLauncher();
    ReadsAndWritesPreferences();
  }

  private static void ComparesVersions()
  {
    Check(SemanticVersion.Normalize("v0.3.0") == "0.3.0", "A leading v is dropped.");
    Check(Newer("0.3.0", "0.3.0-beta.2"), "A release is newer than its prerelease.");
    Check(Newer("0.10.0", "0.9.9"), "Versions compare numerically.");
    Check(Newer("1.0.0-beta.10", "1.0.0-beta.9"), "Prerelease numbers compare numerically.");
    Check(!SemanticVersion.TryParse("1.1", out _), "A version needs three parts, so one folder name exists per version.");
    Check(!SemanticVersion.TryParse("1.0.0/..", out _), "A version cannot name another folder.");
  }

  private static void ValidatesManifests()
  {
    byte[] package = Package("0.3.0");
    ReleaseManifest manifest = ReleaseManifest.Parse(Manifest("0.3.0", package));
    Check(manifest.PackageFor(Platform).Bytes == package.Length, "A valid manifest describes its package.");
    Throws<InvalidDataException>(() => manifest.PackageFor("windows"), "A missing OS package is reported.");
    Throws<InvalidDataException>(() => ReleaseManifest.Parse(Manifest("v0.3.0", package)), "Only canonical versions are accepted.");
    Throws<InvalidDataException>(
      () => ReleaseManifest.Parse(Manifest("0.3.0", package, sha256: "abc")).PackageFor(Platform),
      "A malformed checksum is refused."
    );
    Throws<InvalidDataException>(
      () => ReleaseManifest.Parse(Manifest("0.3.0", package, asset: "Other.zip")).PackageFor(Platform),
      "An unexpected asset name is refused."
    );
    Throws<InvalidDataException>(
      () => ReleaseManifest.Parse(Manifest("0.3.0", package, runtimePath: "ImplResourcePack/Runtime/versions/../x")).PackageFor(Platform),
      "A runtime path outside the version folder is refused."
    );
  }

  private static void InstallsANewerPackage()
  {
    using Folder install = new();
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    UpdateResult result = Resolve(install, server, "0.2.0");
    Check(result.Outcome == UpdateResult.CandidateOutcome, "A newer release is installed: " + result.Outcome);
    Check(result.RuntimePath == ModLayout.VersionPath(install.Path, "0.3.0"), "It lands in its own version folder.");
    Check(File.Exists(Path.Combine(result.RuntimePath, "ImplDmNote", "macos", "ImplDmNote.app.zip")), "The bundled app comes along.");
    Check(File.Exists(Path.Combine(result.RuntimePath, ModLayout.InstalledManifest)), "The package identity is recorded.");
    Check(!File.Exists(Path.Combine(install.Path, ModLayout.InfoFile)), "The root Info.json is the launcher's to change.");
    CheckNoTemporaryFiles(install);
  }

  private static void IgnoresTheSameOrAnOlderRelease()
  {
    using Folder install = new();
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    Check(Resolve(install, server, "0.3.0").Outcome == UpdateResult.NoneOutcome, "The running release is current.");
    Check(Resolve(install, server, "0.4.0").Outcome == UpdateResult.NoneOutcome, "An older release never downgrades.");
    Check(server.PackageRequests == 0, "Nothing is downloaded.");
  }

  private static void TreatsAMissingManifestAsNoUpdate()
  {
    using Folder install = new();
    using ReleaseServer server = new(null, Array.Empty<byte>());
    Check(Resolve(install, server, "0.2.0").Outcome == UpdateResult.NoneOutcome, "No published manifest means nothing to install.");
  }

  private static void SkipsAVersionThatFailedToStart()
  {
    using Folder install = new();
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    UpdateResult result = Resolve(install, server, "0.2.0", skipped: new[] { "0.3.0" });
    Check(result.Outcome == UpdateResult.SkippedOutcome && result.Version == "0.3.0", "A failed version is skipped.");
    Check(server.PackageRequests == 0, "A skipped version is not downloaded.");
  }

  private static void RejectsAPackageWithTheWrongChecksum()
  {
    using Folder install = new();
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package, sha256: new string('0', 64)), package);
    Throws<InvalidDataException>(() => Resolve(install, server, "0.2.0"), "A checksum mismatch is refused.");
    Check(!Directory.Exists(ModLayout.VersionPath(install.Path, "0.3.0")), "Nothing is installed.");
    CheckNoTemporaryFiles(install);
  }

  private static void RejectsAPackageWithTheWrongSize()
  {
    using Folder install = new();
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package, bytes: package.Length + 1), package);
    Throws<InvalidDataException>(() => Resolve(install, server, "0.2.0"), "A size mismatch is refused.");
    CheckNoTemporaryFiles(install);
  }

  private static void RejectsAPackageThatEscapesItsFolder()
  {
    using Folder install = new();
    byte[] package = Package("0.3.0", unsafeEntry: true);
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    Throws<InvalidDataException>(() => Resolve(install, server, "0.2.0"), "A zip-slip entry is refused.");
    Check(!File.Exists(Path.Combine(install.Path, "Runtime", "escape.txt")), "Nothing is written outside the package.");
  }

  private static void ReusesADownloadedVersion()
  {
    using Folder install = new();
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    Resolve(install, server, "0.2.0");
    UpdateResult again = Resolve(install, server, "0.2.0");
    Check(again.Outcome == UpdateResult.CandidateOutcome, "The downloaded version is offered again.");
    Check(server.PackageRequests == 1, "It is not downloaded twice.");
  }

  private static void FinishesASlowDownloadInTheBackground()
  {
    using Folder install = new();
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package) { PackageDelay = TimeSpan.FromMilliseconds(1500) };
    UpdateTimings quick = new() { Startup = TimeSpan.FromMilliseconds(300) };
    using UpdateManager manager = new(install.Path, Platform, UpdateSource.Local(server.Releases), quick);
    UpdateResult result = manager.Resolve("0.2.0", null);
    Check(result.Outcome == UpdateResult.DeferredOutcome, "A slow download does not hold up startup: " + result.Outcome);
    Check(result.Completion.Wait(TimeSpan.FromSeconds(10)), "The background download finishes.");
    Check(result.Completion.Result == ModLayout.VersionPath(install.Path, "0.3.0"), "It installs the version folder.");
    Check(Resolve(install, server, "0.2.0").Outcome == UpdateResult.CandidateOutcome, "The next launch applies it.");
    Check(server.PackageRequests == 1, "without downloading again.");
  }

  private static void RecordsAnInterruptedTrialAsFailed()
  {
    using Folder install = new();
    CreateRuntime(install.Path, "0.2.0");
    CreateRuntime(install.Path, "0.3.0");
    RuntimeStore store = new(install.Path);
    store.Save(new RuntimeState { Current = "0.2.0", Trial = "0.3.0" });
    RuntimeState state = store.LoadAndRepair();
    Check(state.Trial == null && state.Current == "0.2.0", "A crashed trial falls back to Current.");
    Check(state.Failed.SequenceEqual(new[] { "0.3.0" }), "and is remembered as failed.");
    Check(!Directory.Exists(ModLayout.VersionPath(install.Path, "0.3.0")), "Its folder is removed.");
  }

  private static void PromotesAndKeepsOnePreviousVersion()
  {
    using Folder install = new();
    foreach (string version in new[] { "0.1.0", "0.2.0", "0.3.0" })
      CreateRuntime(install.Path, version);
    RuntimeStore store = new(install.Path);
    RuntimeState state = new() { Current = "0.2.0", Previous = "0.1.0", Failed = new List<string> { "0.3.0" } };
    store.Save(state);
    store.Promote(state, "0.3.0");
    Check(state.Current == "0.3.0" && state.Previous == "0.2.0", "Promotion keeps the last version for rollback.");
    Check(state.Failed.Count == 0, "A version that starts is no longer failed.");
    Check(!Directory.Exists(ModLayout.VersionPath(install.Path, "0.1.0")), "Older versions are removed.");
  }

  private static void KeepsANewerDownloadForTheNextLaunch()
  {
    using Folder install = new();
    foreach (string version in new[] { "0.2.0", "0.3.0", "0.4.0" })
      CreateRuntime(install.Path, version);
    RuntimeStore store = new(install.Path);
    store.Save(new RuntimeState { Current = "0.2.0", Failed = new List<string> { "0.4.0" } });
    store.LoadAndRepair();
    Check(Directory.Exists(ModLayout.VersionPath(install.Path, "0.3.0")), "A finished background download waits.");
    Check(!Directory.Exists(ModLayout.VersionPath(install.Path, "0.4.0")), "A failed version's folder goes.");
  }

  private static void RollsBackADamagedCurrentVersion()
  {
    using Folder install = new();
    CreateRuntime(install.Path, "0.2.0");
    CreateRuntime(install.Path, "0.3.0");
    File.Delete(Path.Combine(ModLayout.VersionPath(install.Path, "0.3.0"), ModLayout.PayloadAssembly));
    RuntimeStore store = new(install.Path);
    store.Save(new RuntimeState { Current = "0.3.0", Previous = "0.2.0" });
    RuntimeState state = store.LoadAndRepair();
    Check(state.Current == "0.2.0" && state.Previous == null, "A damaged Current rolls back to Previous.");
  }

  private static void RemovesTheLegacyRootPayloadButNotSettings()
  {
    using Folder install = new();
    CreateRuntime(install.Path, "0.2.0");
    File.WriteAllText(Path.Combine(install.Path, ModLayout.PayloadAssembly), "legacy");
    Directory.CreateDirectory(Path.Combine(install.Path, "Assets", "Font"));
    File.WriteAllText(Path.Combine(install.Path, "Settings.xml"), "settings");
    File.WriteAllText(Path.Combine(install.Path, ModLayout.PreferencesFile), "{}");
    RuntimeStore store = new(install.Path);
    store.Save(new RuntimeState { Current = "0.2.0" });
    store.LoadAndRepair();
    Check(!File.Exists(Path.Combine(install.Path, ModLayout.PayloadAssembly)), "The old root DLL is removed.");
    Check(!Directory.Exists(Path.Combine(install.Path, "Assets")), "Old root fonts are removed.");
    Check(File.ReadAllText(Path.Combine(install.Path, "Settings.xml")) == "settings", "Settings stay.");
    Check(File.Exists(Path.Combine(install.Path, ModLayout.PreferencesFile)), "Update preferences stay.");
  }

  private static void PointsInfoJsonAtTheNewLauncher()
  {
    using Folder install = new();
    CreateRuntime(install.Path, "0.2.0");
    CreateRuntime(install.Path, "0.3.0");
    string started = Path.Combine(install.Path, ModLayout.LauncherAssemblyName("0.2.0"));
    Directory.CreateDirectory(Path.GetDirectoryName(started));
    File.WriteAllText(started, "running launcher");
    File.WriteAllText(
      Path.Combine(install.Path, ModLayout.InfoFile),
      new JObject { ["Id"] = "ImplResourcePack", ["AssemblyName"] = ModLayout.LauncherAssemblyName("0.2.0") }.ToString()
    );
    RuntimeStore store = new(install.Path);
    RuntimeState state = new() { Current = "0.2.0" };
    store.Save(state);
    store.Promote(state, "0.3.0");
    LauncherInfo.Commit(install.Path, store.Get("0.3.0"));
    JObject info = JObject.Parse(File.ReadAllText(Path.Combine(install.Path, ModLayout.InfoFile)));
    Check(info.Value<string>("AssemblyName") == "Runtime/launchers/0.3.0/ImplResourcePack.Launcher.dll", "UMM starts the new launcher next.");
    Check(info.Value<string>("EntryMethod") == ModLayout.LauncherEntry && info.Value<string>("Version") == "0.3.0", "with its version.");
    Check(info.Value<string>("DisplayName") == "Impl Resource Pack", "The mod identity comes from the new runtime.");
    Check(File.Exists(Path.Combine(install.Path, ModLayout.LauncherAssemblyName("0.3.0"))), "The new launcher is in place.");
    Check(File.ReadAllText(started) == "running launcher", "The running launcher's file is untouched.");
  }

  private static void ReadsAndWritesPreferences()
  {
    using Folder install = new();
    Check(UpdatePreferences.Load(install.Path).AutomaticUpdates, "Updates are on by default.");
    new UpdatePreferences { AutomaticUpdates = false }.Save(install.Path);
    Check(!UpdatePreferences.Load(install.Path).AutomaticUpdates, "Turning updates off persists.");
    File.WriteAllText(UpdatePreferences.PathFor(install.Path), "not json");
    Check(UpdatePreferences.Load(install.Path).AutomaticUpdates, "An unreadable file means the defaults.");
    Check(new UpdatePreferences { Source = "http://127.0.0.1:8123" }.LocalSource() != null, "A loopback test server is accepted.");
    Check(new UpdatePreferences { Source = "http://example.com/" }.LocalSource() == null, "A remote override is ignored.");
    Check(new UpdatePreferences { Source = "https://127.0.0.1/" }.LocalSource() == null, "Only plain loopback HTTP is a test server.");
  }

  private static bool Newer(string left, string right) =>
    SemanticVersion.Parse(left).CompareTo(SemanticVersion.Parse(right)) > 0;

  private static UpdateResult Resolve(Folder install, ReleaseServer server, string current, string[] skipped = null)
  {
    using UpdateManager manager = new(install.Path, Platform, UpdateSource.Local(server.Releases));
    return manager.Resolve(current, skipped);
  }

  private static void CheckNoTemporaryFiles(Folder install)
  {
    string runtime = ModLayout.RuntimeRoot(install.Path);
    if (!Directory.Exists(runtime))
      return;
    Check(
      Directory.GetFiles(runtime, "download-*").Length == 0 && Directory.GetDirectories(runtime, "extract-*").Length == 0,
      "Downloads and extraction folders are cleaned up."
    );
  }

  // A version folder with real assemblies, so identity checks run as in the game.
  private static void CreateRuntime(string install, string version)
  {
    string runtime = ModLayout.VersionPath(install, version);
    foreach ((string file, byte[] content) in RuntimeFiles(version))
    {
      string path = Path.Combine(runtime, file);
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      File.WriteAllBytes(path, content);
    }
  }

  private static IEnumerable<(string File, byte[] Content)> RuntimeFiles(string version)
  {
    yield return (ModLayout.InfoFile, Encoding.UTF8.GetBytes(new JObject
    {
      ["Id"] = "ImplResourcePack",
      ["DisplayName"] = "Impl Resource Pack",
      ["Version"] = version,
      ["AssemblyName"] = ModLayout.PayloadAssembly,
      ["EntryMethod"] = ModLayout.PayloadEntry,
    }.ToString()));
    yield return (ModLayout.PayloadAssembly, File.ReadAllBytes(typeof(Main).Assembly.Location));
    yield return (ModLayout.LauncherAssembly, File.ReadAllBytes(typeof(LauncherEntry).Assembly.Location));
    yield return (ModLayout.UpdaterAssembly, File.ReadAllBytes(typeof(UpdateManager).Assembly.Location));
    string bundle =
      Environment.GetEnvironmentVariable("ADOFAI_IPC_BUNDLE")
      ?? throw new InvalidOperationException("Run the tests through ./scripts/run.sh so ADOFAI_IPC_BUNDLE is set.");
    foreach (string file in ModLayout.IpcBundle)
      yield return (file, File.ReadAllBytes(Path.Combine(bundle, file)));
    yield return ("ImplDmNote/macos/ImplDmNote.app.zip", Encoding.UTF8.GetBytes("app"));
  }

  private static byte[] Package(string version, bool unsafeEntry = false)
  {
    using MemoryStream buffer = new();
    using (ZipArchive archive = new(buffer, ZipArchiveMode.Create, true))
    {
      string root = "ImplResourcePack/Runtime/versions/" + version + "/";
      Add(archive, "ImplResourcePack/Info.json", "{}");
      foreach ((string file, byte[] content) in RuntimeFiles(version))
        Add(archive, root + file, content);
      if (unsafeEntry)
        Add(archive, "../escape.txt", Encoding.UTF8.GetBytes("escaped"));
    }
    return buffer.ToArray();
  }

  private static void Add(ZipArchive archive, string name, string content) => Add(archive, name, Encoding.UTF8.GetBytes(content));

  private static void Add(ZipArchive archive, string name, byte[] content)
  {
    using Stream entry = archive.CreateEntry(name).Open();
    entry.Write(content, 0, content.Length);
  }

  private static string Manifest(
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
          ["asset"] = asset ?? "ImplResourcePack-" + Platform + ".zip",
          ["bytes"] = bytes ?? package.Length,
          ["sha256"] = sha256 ?? Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant(),
          ["runtimePath"] = runtimePath ?? "ImplResourcePack/Runtime/versions/" + version,
        },
      },
    }.ToString();

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }

  private static void Throws<T>(Action action, string message)
    where T : Exception
  {
    try
    {
      action();
    }
    catch (T)
    {
      return;
    }
    throw new InvalidOperationException(message + " (expected " + typeof(T).Name + ")");
  }

  private sealed class Folder : IDisposable
  {
    public string Path { get; } =
      System.IO.Path.Combine(System.IO.Path.GetTempPath(), "impl-update-test-" + Guid.NewGuid().ToString("N"), "ImplResourcePack");

    public Folder() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
      try
      {
        Directory.Delete(System.IO.Path.GetDirectoryName(Path), true);
      }
      catch (IOException) { }
    }
  }

  /// <summary>Serves releases/latest/download/&lt;manifest&gt; and releases/download/v&lt;v&gt;/&lt;zip&gt;.</summary>
  private sealed class ReleaseServer : IDisposable
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
        else if (path.StartsWith("/releases/download/v", StringComparison.Ordinal) && path.EndsWith(".zip", StringComparison.Ordinal))
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
