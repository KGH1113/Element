extern alias updater;
using System;
using System.IO;
using Element.Tests.Support;
using Element.Updates;
using static Element.Tests.Support.Assert;
using static Element.Tests.Updater.ReleaseFixtures;
using ReleaseManifest = updater::Element.Updater.ReleaseManifest;
using UpdateManager = updater::Element.Updater.UpdateManager;
using UpdateResult = updater::Element.Updater.UpdateResult;
using UpdateSource = updater::Element.Updater.UpdateSource;
using UpdateTimings = updater::Element.Updater.UpdateTimings;

namespace Element.Tests.Updater;

/// <summary>
/// The updater against a local release server: versions, manifests, verified
/// downloads, a download finished in the background, and the update preferences.
/// </summary>
internal static class UpdateManagerTests
{
  internal static void Run()
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
    ReadsAndWritesPreferences();
  }

  private static void ComparesVersions()
  {
    Check(SemanticVersion.Normalize("v0.3.0") == "0.3.0", "A leading v is dropped.");
    Check(Newer("0.3.0", "0.3.0-beta.2"), "A release is newer than its prerelease.");
    Check(Newer("0.10.0", "0.9.9"), "Versions compare numerically.");
    Check(Newer("1.0.0-beta.10", "1.0.0-beta.9"), "Prerelease numbers compare numerically.");
    Check(
      !SemanticVersion.TryParse("1.1", out _),
      "A version needs three parts, so one folder name exists per version."
    );
    Check(!SemanticVersion.TryParse("1.0.0/..", out _), "A version cannot name another folder.");
  }

  private static void ValidatesManifests()
  {
    byte[] package = Package("0.3.0");
    ReleaseManifest manifest = ReleaseManifest.Parse(Manifest("0.3.0", package));
    Check(
      manifest.PackageFor(ReleaseFixtures.Platform).Bytes == package.Length,
      "A valid manifest describes its package."
    );
    Throws<InvalidDataException>(() => manifest.PackageFor("windows"), "A missing OS package is reported.");
    Throws<InvalidDataException>(
      () => ReleaseManifest.Parse(Manifest("v0.3.0", package)),
      "Only canonical versions are accepted."
    );
    Throws<InvalidDataException>(
      () => ReleaseManifest.Parse(Manifest("0.3.0", package, sha256: "abc")).PackageFor(ReleaseFixtures.Platform),
      "A malformed checksum is refused."
    );
    Throws<InvalidDataException>(
      () => ReleaseManifest.Parse(Manifest("0.3.0", package, asset: "Other.zip")).PackageFor(ReleaseFixtures.Platform),
      "An unexpected asset name is refused."
    );
    Throws<InvalidDataException>(
      () =>
        ReleaseManifest
          .Parse(Manifest("0.3.0", package, runtimePath: "Element/Runtime/versions/../x"))
          .PackageFor(ReleaseFixtures.Platform),
      "A runtime path outside the version folder is refused."
    );
  }

  private static void InstallsANewerPackage()
  {
    using TempFolder install = new("element-update-test-", "Element");
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    UpdateResult result = Resolve(install, server, "0.2.0");
    Check(result.Outcome == UpdateResult.CandidateOutcome, "A newer release is installed: " + result.Outcome);
    Check(result.RuntimePath == ModLayout.VersionPath(install.Path, "0.3.0"), "It lands in its own version folder.");
    Check(
      File.Exists(Path.Combine(result.RuntimePath, "ElementNote", "macos", "ElementNote.app.zip")),
      "The bundled app comes along."
    );
    Check(
      File.Exists(Path.Combine(result.RuntimePath, ModLayout.InstalledManifest)),
      "The package identity is recorded."
    );
    Check(
      !File.Exists(Path.Combine(install.Path, ModLayout.InfoFile)),
      "The root Info.json is the launcher's to change."
    );
    CheckNoTemporaryFiles(install);
  }

  private static void IgnoresTheSameOrAnOlderRelease()
  {
    using TempFolder install = new("element-update-test-", "Element");
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    Check(Resolve(install, server, "0.3.0").Outcome == UpdateResult.NoneOutcome, "The running release is current.");
    Check(Resolve(install, server, "0.4.0").Outcome == UpdateResult.NoneOutcome, "An older release never downgrades.");
    Check(server.PackageRequests == 0, "Nothing is downloaded.");
  }

  private static void TreatsAMissingManifestAsNoUpdate()
  {
    using TempFolder install = new("element-update-test-", "Element");
    using ReleaseServer server = new(null, Array.Empty<byte>());
    Check(
      Resolve(install, server, "0.2.0").Outcome == UpdateResult.NoneOutcome,
      "No published manifest means nothing to install."
    );
  }

  private static void SkipsAVersionThatFailedToStart()
  {
    using TempFolder install = new("element-update-test-", "Element");
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    UpdateResult result = Resolve(install, server, "0.2.0", skipped: new[] { "0.3.0" });
    Check(result.Outcome == UpdateResult.SkippedOutcome && result.Version == "0.3.0", "A failed version is skipped.");
    Check(server.PackageRequests == 0, "A skipped version is not downloaded.");
  }

  private static void RejectsAPackageWithTheWrongChecksum()
  {
    using TempFolder install = new("element-update-test-", "Element");
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package, sha256: new string('0', 64)), package);
    Throws<InvalidDataException>(() => Resolve(install, server, "0.2.0"), "A checksum mismatch is refused.");
    Check(!Directory.Exists(ModLayout.VersionPath(install.Path, "0.3.0")), "Nothing is installed.");
    CheckNoTemporaryFiles(install);
  }

  private static void RejectsAPackageWithTheWrongSize()
  {
    using TempFolder install = new("element-update-test-", "Element");
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package, bytes: package.Length + 1), package);
    Throws<InvalidDataException>(() => Resolve(install, server, "0.2.0"), "A size mismatch is refused.");
    CheckNoTemporaryFiles(install);
  }

  private static void RejectsAPackageThatEscapesItsFolder()
  {
    using TempFolder install = new("element-update-test-", "Element");
    byte[] package = Package("0.3.0", unsafeEntry: true);
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    Throws<InvalidDataException>(() => Resolve(install, server, "0.2.0"), "A zip-slip entry is refused.");
    Check(!File.Exists(Path.Combine(install.Path, "Runtime", "escape.txt")), "Nothing is written outside the package.");
  }

  private static void ReusesADownloadedVersion()
  {
    using TempFolder install = new("element-update-test-", "Element");
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package);
    Resolve(install, server, "0.2.0");
    UpdateResult again = Resolve(install, server, "0.2.0");
    Check(again.Outcome == UpdateResult.CandidateOutcome, "The downloaded version is offered again.");
    Check(server.PackageRequests == 1, "It is not downloaded twice.");
  }

  private static void FinishesASlowDownloadInTheBackground()
  {
    using TempFolder install = new("element-update-test-", "Element");
    byte[] package = Package("0.3.0");
    using ReleaseServer server = new(Manifest("0.3.0", package), package)
    {
      PackageDelay = TimeSpan.FromMilliseconds(1500),
    };
    UpdateTimings quick = new() { Startup = TimeSpan.FromMilliseconds(300) };
    using UpdateManager manager = new(
      install.Path,
      ReleaseFixtures.Platform,
      UpdateSource.Local(server.Releases),
      quick
    );
    UpdateResult result = manager.Resolve("0.2.0", null);
    Check(
      result.Outcome == UpdateResult.DeferredOutcome,
      "A slow download does not hold up startup: " + result.Outcome
    );
    Check(result.Completion.Wait(TimeSpan.FromSeconds(10)), "The background download finishes.");
    Check(result.Completion.Result == ModLayout.VersionPath(install.Path, "0.3.0"), "It installs the version folder.");
    Check(Resolve(install, server, "0.2.0").Outcome == UpdateResult.CandidateOutcome, "The next launch applies it.");
    Check(server.PackageRequests == 1, "without downloading again.");
  }

  private static void ReadsAndWritesPreferences()
  {
    using TempFolder install = new("element-update-test-", "Element");
    Check(UpdatePreferences.Load(install.Path).AutomaticUpdates, "Updates are on by default.");
    new UpdatePreferences { AutomaticUpdates = false }.Save(install.Path);
    Check(!UpdatePreferences.Load(install.Path).AutomaticUpdates, "Turning updates off persists.");
    File.WriteAllText(UpdatePreferences.PathFor(install.Path), "not json");
    Check(UpdatePreferences.Load(install.Path).AutomaticUpdates, "An unreadable file means the defaults.");
    Check(
      new UpdatePreferences { Source = "http://127.0.0.1:8123" }.LocalSource() != null,
      "A loopback test server is accepted."
    );
    Check(
      new UpdatePreferences { Source = "http://example.com/" }.LocalSource() == null,
      "A remote override is ignored."
    );
    Check(
      new UpdatePreferences { Source = "https://127.0.0.1/" }.LocalSource() == null,
      "Only plain loopback HTTP is a test server."
    );
  }

  private static bool Newer(string left, string right) =>
    SemanticVersion.Parse(left).CompareTo(SemanticVersion.Parse(right)) > 0;

  private static UpdateResult Resolve(TempFolder install, ReleaseServer server, string current, string[] skipped = null)
  {
    using UpdateManager manager = new(install.Path, ReleaseFixtures.Platform, UpdateSource.Local(server.Releases));
    return manager.Resolve(current, skipped);
  }

  private static void CheckNoTemporaryFiles(TempFolder install)
  {
    string runtime = ModLayout.RuntimeRoot(install.Path);
    if (!Directory.Exists(runtime))
      return;
    Check(
      Directory.GetFiles(runtime, "download-*").Length == 0
        && Directory.GetDirectories(runtime, "extract-*").Length == 0,
      "Downloads and extraction folders are cleaned up."
    );
  }
}
