extern alias launcher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Element.Tests.Support;
using Element.Updates;
using Newtonsoft.Json.Linq;
using static Element.Tests.Support.Assert;
using static Element.Tests.Updater.ReleaseFixtures;
using LauncherInfo = launcher::Element.Launcher.LauncherInfo;
using RuntimeState = launcher::Element.Launcher.RuntimeState;
using RuntimeStore = launcher::Element.Launcher.RuntimeStore;

namespace Element.Tests.Launcher;

/// <summary>
/// The launcher's version bookkeeping: an interrupted trial, promotion, a newer
/// download kept for the next launch, rollback, the old payload, and Info.json.
/// </summary>
internal static class RuntimeStoreTests
{
  internal static void Run()
  {
    RecordsAnInterruptedTrialAsFailed();
    PromotesAndKeepsOnePreviousVersion();
    KeepsANewerDownloadForTheNextLaunch();
    RollsBackADamagedCurrentVersion();
    RemovesTheLegacyRootPayloadButNotSettings();
    PointsInfoJsonAtTheNewLauncher();
  }

  private static void RecordsAnInterruptedTrialAsFailed()
  {
    using TempFolder install = new("element-update-test-", "Element");
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
    using TempFolder install = new("element-update-test-", "Element");
    foreach (string version in new[] { "0.1.0", "0.2.0", "0.3.0" })
      CreateRuntime(install.Path, version);
    RuntimeStore store = new(install.Path);
    RuntimeState state = new()
    {
      Current = "0.2.0",
      Previous = "0.1.0",
      Failed = new List<string> { "0.3.0" },
    };
    store.Save(state);
    store.Promote(state, "0.3.0");
    Check(state.Current == "0.3.0" && state.Previous == "0.2.0", "Promotion keeps the last version for rollback.");
    Check(state.Failed.Count == 0, "A version that starts is no longer failed.");
    Check(!Directory.Exists(ModLayout.VersionPath(install.Path, "0.1.0")), "Older versions are removed.");
  }

  private static void KeepsANewerDownloadForTheNextLaunch()
  {
    using TempFolder install = new("element-update-test-", "Element");
    foreach (string version in new[] { "0.2.0", "0.3.0", "0.4.0" })
      CreateRuntime(install.Path, version);
    RuntimeStore store = new(install.Path);
    store.Save(
      new RuntimeState
      {
        Current = "0.2.0",
        Failed = new List<string> { "0.4.0" },
      }
    );
    store.LoadAndRepair();
    Check(Directory.Exists(ModLayout.VersionPath(install.Path, "0.3.0")), "A finished background download waits.");
    Check(!Directory.Exists(ModLayout.VersionPath(install.Path, "0.4.0")), "A failed version's folder goes.");
  }

  private static void RollsBackADamagedCurrentVersion()
  {
    using TempFolder install = new("element-update-test-", "Element");
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
    using TempFolder install = new("element-update-test-", "Element");
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
    using TempFolder install = new("element-update-test-", "Element");
    CreateRuntime(install.Path, "0.2.0");
    CreateRuntime(install.Path, "0.3.0");
    string started = Path.Combine(install.Path, ModLayout.LauncherAssemblyName("0.2.0"));
    Directory.CreateDirectory(Path.GetDirectoryName(started));
    File.WriteAllText(started, "running launcher");
    File.WriteAllText(
      Path.Combine(install.Path, ModLayout.InfoFile),
      new JObject { ["Id"] = "Element", ["AssemblyName"] = ModLayout.LauncherAssemblyName("0.2.0") }.ToString()
    );
    RuntimeStore store = new(install.Path);
    RuntimeState state = new() { Current = "0.2.0" };
    store.Save(state);
    store.Promote(state, "0.3.0");
    LauncherInfo.Commit(install.Path, store.Get("0.3.0"));
    JObject info = JObject.Parse(File.ReadAllText(Path.Combine(install.Path, ModLayout.InfoFile)));
    Check(
      info.Value<string>("AssemblyName") == "Runtime/launchers/0.3.0/Element.Launcher.dll",
      "UMM starts the new launcher next."
    );
    Check(
      info.Value<string>("EntryMethod") == ModLayout.LauncherEntry && info.Value<string>("Version") == "0.3.0",
      "with its version."
    );
    Check(info.Value<string>("DisplayName") == "Element", "The mod identity comes from the new runtime.");
    Check(
      File.Exists(Path.Combine(install.Path, ModLayout.LauncherAssemblyName("0.3.0"))),
      "The new launcher is in place."
    );
    Check(File.ReadAllText(started) == "running launcher", "The running launcher's file is untouched.");
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
}
