using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImplResourcePack.Updates;
using Newtonsoft.Json.Linq;

namespace ImplResourcePack.Updater;

/// <summary>How long startup waits. The game is frozen while the launcher waits.</summary>
internal sealed class UpdateTimings
{
  public static readonly UpdateTimings Default = new();

  /// <summary>Offline players wait at most this long for the manifest.</summary>
  public TimeSpan Manifest { get; set; } = TimeSpan.FromSeconds(5);

  /// <summary>An update that installs within this window starts this launch.</summary>
  public TimeSpan Startup { get; set; } = TimeSpan.FromSeconds(20);

  /// <summary>A slower download keeps going in the background for up to this long.</summary>
  public TimeSpan Background { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Finds the latest release, downloads and verifies this OS's package, and
/// installs its runtime as a new version folder. It never touches the running
/// version, the root Info.json or user settings: the launcher decides what to load.
/// </summary>
internal sealed class UpdateManager : IDisposable
{
  internal const long MaximumPackageBytes = 256L * 1024 * 1024;
  private const long MaximumExtractedBytes = 512L * 1024 * 1024;
  private const int MaximumManifestBytes = 64 * 1024;

  private readonly string _install;
  private readonly string _runtimeRoot;
  private readonly string _platform;
  private readonly UpdateSource _source;
  private readonly UpdateTimings _timings;
  private readonly HttpClient _client;

  public UpdateManager(string install, string platform, UpdateSource source, UpdateTimings timings = null)
  {
    _install = Path.GetFullPath(install ?? throw new ArgumentNullException(nameof(install)));
    _runtimeRoot = ModLayout.RuntimeRoot(_install);
    _platform = platform ?? throw new ArgumentNullException(nameof(platform));
    _source = source ?? throw new ArgumentNullException(nameof(source));
    _timings = timings ?? UpdateTimings.Default;
    _client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    _client.DefaultRequestHeaders.UserAgent.ParseAdd("ImplResourcePack-Updater/1");
  }

  /// <summary>
  /// Blocks for at most <see cref="UpdateTimings.Startup"/>. A download that
  /// takes longer returns <see cref="UpdateResult.Deferred"/> and finishes in
  /// <see cref="UpdateResult.Completion"/>.
  /// </summary>
  public UpdateResult Resolve(string currentVersion, IEnumerable<string> skipped)
  {
    Stopwatch clock = Stopwatch.StartNew();
    CleanupTemporaryFiles();
    SemanticVersion current = SemanticVersion.Parse(currentVersion);
    ReleaseManifest manifest = FetchManifest();
    if (manifest == null)
      return UpdateResult.None();
    ReleasePackage package = manifest.PackageFor(_platform);
    SemanticVersion available = SemanticVersion.Parse(manifest.Version);
    string version = available.ToString();
    PruneStaged(current, version);
    if (available.CompareTo(current) <= 0)
      return UpdateResult.None();
    if ((skipped ?? Enumerable.Empty<string>()).Any(item => SemanticVersion.Same(item, version)))
      return UpdateResult.Skipped(version);

    string target = ModLayout.VersionPath(_install, version);
    if (IsInstalled(target, version, package))
      return UpdateResult.Candidate(version, target);

    CancellationTokenSource background = new(_timings.Background);
    Task<string> install = Task.Run(() => DownloadAndInstallAsync(package, version, background.Token));
    _ = install.ContinueWith(_ => background.Dispose(), TaskScheduler.Default);
    TimeSpan remaining = _timings.Startup - clock.Elapsed;
    if (remaining > TimeSpan.Zero && Task.WhenAny(install, Task.Delay(remaining)).GetAwaiter().GetResult() == install)
      return UpdateResult.Candidate(version, install.GetAwaiter().GetResult());
    return UpdateResult.Deferred(version, install);
  }

  /// <summary>Null when the latest release has no manifest (for example, none is published yet).</summary>
  private ReleaseManifest FetchManifest()
  {
    using CancellationTokenSource timeout = new(_timings.Manifest);
    Task<string> request = DownloadTextAsync(_source.Manifest, timeout.Token);
    if (Task.WhenAny(request, Task.Delay(_timings.Manifest)).GetAwaiter().GetResult() != request)
    {
      timeout.Cancel();
      _ = request.ContinueWith(failed => _ = failed.Exception, TaskScheduler.Default);
      throw new TimeoutException("The update manifest did not arrive within " + _timings.Manifest.TotalSeconds + " s.");
    }
    string json = request.GetAwaiter().GetResult();
    return json == null ? null : ReleaseManifest.Parse(json);
  }

  private async Task<string> DownloadTextAsync(Uri url, CancellationToken cancellation)
  {
    using HttpResponseMessage response = await _client
      .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation)
      .ConfigureAwait(false);
    RequireTrusted(response);
    if (response.StatusCode == HttpStatusCode.NotFound)
      return null;
    response.EnsureSuccessStatusCode();
    using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    using MemoryStream buffer = new();
    await CopyAsync(stream, buffer, MaximumManifestBytes, cancellation).ConfigureAwait(false);
    return Encoding.UTF8.GetString(buffer.ToArray());
  }

  private async Task<string> DownloadAndInstallAsync(ReleasePackage package, string version, CancellationToken cancellation)
  {
    Directory.CreateDirectory(_runtimeRoot);
    string archive = Path.Combine(_runtimeRoot, "download-" + Guid.NewGuid().ToString("N") + ".zip");
    try
    {
      await DownloadFileAsync(_source.Package(version, package.Asset), archive, package.Bytes, cancellation)
        .ConfigureAwait(false);
      VerifyChecksum(archive, package.Sha256);
      cancellation.ThrowIfCancellationRequested();
      return Install(archive, version, package);
    }
    finally
    {
      TryDeleteFile(archive);
    }
  }

  private async Task DownloadFileAsync(Uri url, string path, long bytes, CancellationToken cancellation)
  {
    using HttpResponseMessage response = await _client
      .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation)
      .ConfigureAwait(false);
    RequireTrusted(response);
    response.EnsureSuccessStatusCode();
    long? declared = response.Content.Headers.ContentLength;
    if (declared.HasValue && declared.Value != bytes)
      throw new InvalidDataException("The update package size does not match its manifest.");
    using Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    using FileStream destination = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
    if (await CopyAsync(source, destination, bytes, cancellation).ConfigureAwait(false) != bytes)
      throw new InvalidDataException("The update package download is incomplete.");
  }

  private string Install(string archive, string version, ReleasePackage package)
  {
    string extraction = Path.Combine(_runtimeRoot, "extract-" + Guid.NewGuid().ToString("N"));
    try
    {
      PackageArchive.Extract(archive, extraction, MaximumExtractedBytes);
      string runtime = PackageArchive.Contained(extraction, package.RuntimePath);
      RuntimeFiles.Validate(runtime, version);
      WriteInstalledManifest(runtime, version, package);
      string target = ModLayout.VersionPath(_install, version);
      Directory.CreateDirectory(ModLayout.VersionsRoot(_install));
      if (Directory.Exists(target))
        Directory.Delete(target, true);
      Directory.Move(runtime, target);
      return target;
    }
    finally
    {
      TryDeleteDirectory(extraction);
    }
  }

  private bool IsInstalled(string directory, string version, ReleasePackage package)
  {
    if (!Directory.Exists(directory) || !RuntimeFiles.IsValid(directory, version))
      return false;
    try
    {
      string marker = Path.Combine(directory, ModLayout.InstalledManifest);
      if (!File.Exists(marker))
        return false;
      JObject installed = JObject.Parse(File.ReadAllText(marker));
      return SemanticVersion.Same(installed.Value<string>("version"), version)
        && package.SameAs(
          new ReleasePackage
          {
            Asset = installed.Value<string>("asset"),
            Bytes = installed.Value<long?>("bytes") ?? 0,
            Sha256 = installed.Value<string>("sha256"),
          }
        );
    }
    catch (Exception exception) when (exception is IOException or Newtonsoft.Json.JsonException)
    {
      return false;
    }
  }

  private static void WriteInstalledManifest(string directory, string version, ReleasePackage package)
  {
    JObject marker = new()
    {
      ["version"] = version,
      ["asset"] = package.Asset,
      ["bytes"] = package.Bytes,
      ["sha256"] = package.Sha256,
    };
    AtomicFile.WriteAllText(Path.Combine(directory, ModLayout.InstalledManifest), marker.ToString() + "\n");
  }

  /// <summary>A newer folder that is not the latest release is a withdrawn or superseded download.</summary>
  private void PruneStaged(SemanticVersion current, string latest)
  {
    string root = ModLayout.VersionsRoot(_install);
    if (!Directory.Exists(root))
      return;
    foreach (string directory in Directory.GetDirectories(root))
    {
      string name = Path.GetFileName(directory);
      if (
        SemanticVersion.TryParse(name, out SemanticVersion version)
        && version.CompareTo(current) > 0
        && !SemanticVersion.Same(name, latest)
      )
        TryDeleteDirectory(directory);
    }
  }

  private void CleanupTemporaryFiles()
  {
    if (!Directory.Exists(_runtimeRoot))
      return;
    foreach (string file in Directory.GetFiles(_runtimeRoot, "download-*.zip"))
      TryDeleteFile(file);
    foreach (string directory in Directory.GetDirectories(_runtimeRoot, "extract-*"))
      TryDeleteDirectory(directory);
  }

  private void RequireTrusted(HttpResponseMessage response)
  {
    Uri final = response.RequestMessage?.RequestUri;
    if (!_source.Trusts(final))
      throw new InvalidDataException("The update download was redirected to an untrusted address: " + final);
  }

  private static async Task<long> CopyAsync(Stream source, Stream destination, long limit, CancellationToken cancellation)
  {
    byte[] buffer = new byte[81920];
    long total = 0;
    int read;
    while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) > 0)
    {
      total += read;
      if (total > limit)
        throw new InvalidDataException("The update download is larger than its manifest allows.");
      await destination.WriteAsync(buffer, 0, read, cancellation).ConfigureAwait(false);
    }
    return total;
  }

  private static void VerifyChecksum(string path, string expected)
  {
    using SHA256 sha256 = SHA256.Create();
    using FileStream stream = File.OpenRead(path);
    string actual = string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2")));
    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException("The update package checksum does not match its manifest.");
  }

  private static void TryDeleteFile(string path)
  {
    try
    {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }

  private static void TryDeleteDirectory(string path)
  {
    try
    {
      if (Directory.Exists(path))
        Directory.Delete(path, true);
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }

  public void Dispose() => _client.Dispose();
}

internal sealed class UpdateResult
{
  public const string NoneOutcome = "none";
  public const string CandidateOutcome = "candidate";
  public const string DeferredOutcome = "deferred";
  public const string SkippedOutcome = "skipped";
  public const string ErrorOutcome = "error";

  public string Outcome { get; set; }
  public string Version { get; set; }
  public string RuntimePath { get; set; }
  public string Error { get; set; }

  /// <summary>The background install of a deferred update; not serialized.</summary>
  [Newtonsoft.Json.JsonIgnore]
  public Task<string> Completion { get; private set; }

  public static UpdateResult None() => new() { Outcome = NoneOutcome };

  public static UpdateResult Skipped(string version) => new() { Outcome = SkippedOutcome, Version = version };

  public static UpdateResult Candidate(string version, string runtimePath) =>
    new()
    {
      Outcome = CandidateOutcome,
      Version = version,
      RuntimePath = runtimePath,
    };

  public static UpdateResult Deferred(string version, Task<string> completion) =>
    new()
    {
      Outcome = DeferredOutcome,
      Version = version,
      Completion = completion,
    };

  public static UpdateResult Failed(string error) => new() { Outcome = ErrorOutcome, Error = error };
}
