using System;
using System.Collections.Generic;
using System.Linq;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Mods;
using ElementKit;
using ElementKit.Hosting;
using Element.Infrastructure.Mods;
using Element.Application.Mods;

namespace Element.Tests;

// Overlay elements other mods draw: found by Element in a mod's assembly with
// no call from the mod, checked, offered while their mod is on, and their
// settings kept through undo, saving and a missing mod.
internal static class ModElementTests
{
  private const string ModId = "Bars";

  [ElementKit.Element("bars", "막대", "Bars")]
  internal sealed class Bars : OverlayElement
  {
    public string Kept = "";

    protected override string SaveSettings() => Kept;

    protected override void LoadSettings(string data) => Kept = data;
  }

  [ElementKit.Element("bars", "또 막대", "Bars again")]
  internal sealed class Twin : OverlayElement { }

  [ElementKit.Element("Bad Key", "나쁨", "Bad")]
  internal sealed class BadKey : OverlayElement { }

  [ElementKit.Element("unnamed", "", "")]
  internal sealed class Unnamed : OverlayElement { }

  [ElementKit.Element("needs-argument", "인자", "Argument")]
  internal sealed class NeedsArgument : OverlayElement
  {
    public NeedsArgument(int size) => _ = size;
  }

  [ElementKit.Element("throws", "실패", "Throws")]
  internal sealed class Throws : OverlayElement
  {
    public Throws() => throw new InvalidOperationException("no element today");
  }

  // Not offered, and said so.
  internal sealed class Unmarked : OverlayElement { }

  // A base of the mod's own; left alone.
  internal abstract class Base : OverlayElement { }

  private static bool _active = true;

  public static void Run()
  {
    _active = true;
    ElementsAreFoundAndChecked();
    InstancesAreMadeAndTheirFailuresKept();
    TheCatalogFollowsTheMods();
    SamplesBarsAreFoundWithoutACall();
    SettingsAreKeptThroughUndoAndSaving();
  }

  private static void ElementsAreFoundAndChecked()
  {
    List<string> reports = new();
    List<ElementClass> found = ElementClasses.Find(ModId, typeof(ModElementTests).Assembly, reports.Add);
    string all = string.Join(" | ", reports);
    Check(
      found.Select(type => type.FullKey).SequenceEqual(new[] { "Bars/bars", "Bars/throws" }),
      "only well-made elements are offered: " + string.Join(",", found.Select(type => type.FullKey)) + " / " + all
    );
    Check(found[0].Type == typeof(Bars) && found[0].Info.KoreanName == "막대", "an element keeps its class and names");
    Check(
      reports.Any(r => r.Contains("\"Bad Key\"") && r.Contains("hand-bars")),
      "a key layouts can't keep is explained"
    );
    Check(reports.Any(r => r.Contains("Twin") && r.Contains("\"bars\"")), "a repeated key is explained");
    Check(reports.Any(r => r.Contains("Unnamed") && r.Contains("Korean and English")), "a missing name is explained");
    Check(
      reports.Any(r => r.Contains("NeedsArgument") && r.Contains("constructor that takes nothing")),
      "a class Element can't make is explained"
    );
    Check(
      reports.Any(r => r.Contains("Unmarked") && r.Contains("[Element(")),
      "an unmarked element says how to offer it"
    );
    Check(reports.All(r => !r.Contains("+Base")), "an abstract base of the mod's own is left alone");
    Check(
      ElementClasses.Find("bad id!", typeof(ModElementTests).Assembly, reports.Add).Count == 0,
      "a mod id layouts can't keep offers nothing"
    );
  }

  private static void InstancesAreMadeAndTheirFailuresKept()
  {
    List<ElementClass> found = ElementClasses.Find(ModId, typeof(ModElementTests).Assembly, _ => { });
    OverlayElement bars = found.Single(type => type.Info.Key == "bars").Create(out Exception none);
    Check(bars is Bars && none == null, "Element makes an instance per placed element");
    bars.RunLoad("{\"groups\":12}");
    Check(bars.RunSave() == "{\"groups\":12}", "an element's settings come back as it wrote them");
    bars.RunLoad(null);
    Check(bars.RunSave() == "", "a new element starts with empty settings");
    OverlayElement failed = found.Single(type => type.Info.Key == "throws").Create(out Exception error);
    Check(
      failed == null && error is InvalidOperationException && error.Message == "no element today",
      "a constructor's own failure is kept, unwrapped"
    );

    List<string> warned = new();
    ModElements types = new(new ListedModAssemblies(), warned.Add);
    ElementClass throws = found.Single(type => type.Info.Key == "throws");
    types.Failed(throws, "drawing", error);
    types.Failed(throws, "drawing", error);
    Check(
      warned.Count == 1
        && warned[0].Contains("[Bars]")
        && warned[0].Contains("Throws")
        && warned[0].Contains("no element today"),
      "a failure is logged once, naming the mod"
    );
  }

  private static void TheCatalogFollowsTheMods()
  {
    ListedModAssemblies mods = new();
    ModElements types = new(mods, _ => { });
    types.Refresh();
    Check(
      types.Offered.Count == 0 && types.Find("Bars/bars") == null,
      "nothing is offered before a mod declares elements"
    );
    mods.Add(new ModAssembly(ModId, typeof(ModElementTests).Assembly, () => _active));
    types.Refresh();
    Check(types.Find("Bars/bars")?.Type == typeof(Bars) && types.Offered.Count == 2, "a mod that loads later is found");
    int version = types.Version;
    types.Refresh();
    Check(types.Version == version, "nothing new changes nothing");
    _active = false;
    types.Refresh();
    Check(
      types.Find("Bars/bars") == null && types.IsTurnedOff("Bars/bars") && types.Offered.Count == 0,
      "a turned-off mod's elements step aside"
    );
    _active = true;
    types.Refresh();
    Check(types.Find("Bars/bars") != null && types.Version > version, "turning it on brings them back");
    mods.Remove(ModId);
    types.Refresh();
    Check(
      types.Find("Bars/bars") == null && !types.IsTurnedOff("Bars/bars"),
      "an unloaded mod's elements are gone, not off"
    );
  }

  private static void SamplesBarsAreFoundWithoutACall()
  {
    System.Reflection.Assembly sample = typeof(Sample.SampleBars).Assembly;
    Check(
      ElementClasses.UsesKit(sample) && !ElementClasses.UsesKit(typeof(string).Assembly),
      "only mods built against ElementKit are read"
    );
    List<string> reports = new();
    List<ElementClass> found = ElementClasses.Find("Sample", sample, reports.Add);
    Check(
      found.Count == 1
        && found[0].FullKey == "Sample/bars"
        && found[0].Info.EnglishName == "Sample bars"
        && reports.Count == 0,
      "Sample's bars are found as they are: " + string.Join(" | ", reports)
    );
  }

  private static void SettingsAreKeptThroughUndoAndSaving()
  {
    OverlayLayoutSettings element = new()
    {
      Id = "mod:1",
      TemplateId = "Bars/bars",
      Mod = new OverlayModSettings
      {
        Data = "{\"groups\":16}",
        Width = 480,
        Height = 96,
      },
    };
    List<OverlayLayoutSettings> layouts = new() { element };
    LayoutHistory history = new();
    history.Reset(layouts);
    element.Mod.Data = "{\"groups\":32}";
    history.Commit(layouts);
    Check(history.CanUndo, "a settings change is its own undo step");
    history.Undo(layouts);
    Check(layouts[0].Mod.Data == "{\"groups\":16}", "undo brings the settings back");
    history.Redo(layouts);
    Check(layouts[0].Mod.Data == "{\"groups\":32}", "and redo takes them forward");

    OverlayLayoutSettings drawnAgain = layouts[0].Clone();
    drawnAgain.Mod.Width = 500;
    drawnAgain.Mod.KoreanName = "다른 이름";
    Check(drawnAgain.SameAs(layouts[0]), "the size and names it last drew with are not an edit");

    ElementSettings copy = SettingsXml.Clone(new ElementSettings { OverlayLayouts = layouts });
    OverlayLayoutSettings saved = copy.OverlayLayouts.Single();
    Check(
      saved.TemplateId == "Bars/bars" && saved.Mod.Data == "{\"groups\":32}" && saved.Mod.Width == 480,
      "settings survive saving, as profiles and Settings.xml do"
    );
    Check(
      OverlayElementKinds.IsMod(saved.TemplateId) && OverlayElementKinds.ModOf(saved.TemplateId) == ModId,
      "a saved element names the mod that draws it"
    );
    Check(
      !OverlayElementKinds.IsMod(OverlayElementKinds.Text) && !OverlayElementKinds.IsMod(""),
      "Element's own kinds are not mods'"
    );
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
