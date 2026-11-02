using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Mods;
using ElementKit.Hosting;
using ElementKit.Values;

namespace Element.Tests;

// Another mod's values from ElementKit, through Element's overlay: found in
// static fields, checked, shown, hidden while the mod is off and kept by texts.
internal static class ElementKitTests
{
  // What a mod keeps in static fields; attaching finds them.
  private static class ModFields
  {
    public static readonly ValueGroup Group = new("타법", "Tabub");
    public static readonly NumberValue LeftKeys = Group.Number("left-keys", "왼손 키", "Left keys", 3);
    public static readonly TimeValue Rest = Group.Time("rest", "남은 시간", "Rest", 87);
    public static readonly TextValue Pattern = Group.Text("pattern", "지금 타법", "Pattern", "2 + 2 + 1");
    public static readonly NumberValue Spaced = Group.Number("Left Keys", "왼손", "Left", 1);
    public static readonly NumberValue Twice = Group.Number("left-keys", "왼손 2", "Left 2", 1);
    public static readonly TextValue NoSample = Group.Text("empty", "빈 값", "Empty", "");
  }

  // A class whose values fail to be made; the rest of the mod still attaches.
  private static class Broken
  {
    public static readonly ValueGroup Group = Fail();

    private static ValueGroup Fail() => throw new InvalidOperationException("no values today");
  }

  private static bool _active = true;

  public static void Run()
  {
    Reset();
    try
    {
      List<string> reports = Attach();
      ProblemsAreReportedAndSkipped(reports);
      OverlayValues values = new();
      ModValues feed = new(_ => { });
      feed.Pump(values);
      AttachedValuesJoinTheDock();
      SetValuesReachTheOverlay(feed, values);
      UnchangedValuesCostNothing(feed, values);
      TurnedOffModsStepAside(feed, values);
      ReloadsAndDetachesKeepTexts(feed, values);
      ANewCanvasGetsEveryValue(feed);
      AnInvalidModIdAddsNothing();
    }
    finally
    {
      Reset();
    }
  }

  private static List<string> Attach()
  {
    List<string> reports = new();
    KitRegistry.Attach("Tabub", typeof(ElementKitTests).Assembly, () => _active, reports.Add);
    return reports;
  }

  private static void ProblemsAreReportedAndSkipped(List<string> reports)
  {
    Check(reports.Any(r => r.Contains("\"Left Keys\"") && r.Contains("left-keys")), "a key texts can't save is explained");
    Check(reports.Any(r => r.Contains("Two values use the key \"left-keys\"")), "a repeated key is explained");
    Check(reports.Any(r => r.Contains("\"empty\"") && r.Contains("example")), "a missing example is explained");
    Check(
      reports.Any(r => r.Contains("Broken") && r.Contains("no values today")),
      "a class that fails is named; reported: " + string.Join(" | ", reports)
    );
    Check(ModFields.LeftKeys.Accepted && !ModFields.Spaced.Accepted && !ModFields.Twice.Accepted, "only the faulty values are left out");
    Check(ModFields.LeftKeys.FullKey == "Tabub/left-keys", "values learn their mod's id");
  }

  private static void AttachedValuesJoinTheDock()
  {
    OverlayValueSection section = OverlayValueCatalog.Sections().Single(s => s.Group != null && s.Group.Source == "Tabub");
    Check(section.Group.KoreanName == "타법" && section.Group.EnglishName == "Tabub", "the group names its heading");
    Check(
      section.Values.Select(value => value.Key).SequenceEqual(new[] { "Tabub/left-keys", "Tabub/rest", "Tabub/pattern" }),
      "accepted values are offered in the order they were made"
    );
    Check(Definition("Tabub/rest").Sample == "1:27" && Definition("Tabub/rest").Kind == OverlayValueKind.Time, "a time shows m:ss");
    Check(Definition("Tabub/pattern").Sample == "2 + 2 + 1" && !Definition("Tabub/pattern").IsNumeric, "words stay words");
  }

  private static void SetValuesReachTheOverlay(ModValues feed, OverlayValues values)
  {
    OverlayValueId left = Definition("Tabub/left-keys").Id;
    Check(!values.TryGet(left, out _), "nothing shows before the mod sets a value");
    ModFields.LeftKeys.Set(5);
    ModFields.Rest.Set(65.4);
    ModFields.Pattern.Set("<b>2</b> + 1");
    feed.Pump(values);
    Check(Text(values, "Tabub/left-keys") == "5" && values.TryGetNumber(left, out double number) && number == 5, "a number shows");
    Check(Text(values, "Tabub/rest") == "1:05", "a time shows as m:ss");
    Check(
      !Text(values, "Tabub/pattern").Contains("<b>") && Text(values, "Tabub/pattern").StartsWith("<noparse><</noparse>b>2"),
      "markup in words is never read"
    );
    Check(
      OverlayFormula.Parse("{Tabub/left-keys}*2").TryEvaluate(values, out double doubled, out _) && doubled == 10,
      "players calculate with it"
    );

    Task.Run(() => ModFields.LeftKeys.Set(6)).Wait();
    feed.Pump(values);
    Check(Text(values, "Tabub/left-keys") == "6", "a value set on another thread shows on the next frame");
    ModFields.LeftKeys.Set(double.NaN);
    feed.Pump(values);
    Check(!values.TryGet(left, out _), "NaN clears the value");
    ModFields.LeftKeys.Set(6);
    feed.Pump(values);
  }

  private static void UnchangedValuesCostNothing(ModValues feed, OverlayValues values)
  {
    values.TakeChanges();
    ModFields.LeftKeys.Set(6);
    ModFields.Pattern.Set("<b>2</b> + 1");
    feed.Pump(values);
    Check(values.TakeChanges().IsEmpty, "setting the same value again redraws nothing");

    for (int i = 0; i < 100; i++)
      feed.Pump(values);
    long before = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 10000; i++)
      feed.Pump(values);
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    Check(allocated == 0, "a frame with nothing new allocates nothing (" + allocated + " bytes)");
  }

  private static void TurnedOffModsStepAside(ModValues feed, OverlayValues values)
  {
    OverlayValueId left = Definition("Tabub/left-keys").Id;
    _active = false;
    feed.Pump(values);
    Check(!Definition("Tabub/left-keys").Available && !values.TryGet(left, out _), "a turned-off mod's values show nothing");
    Check(OverlayValueCatalog.Sections().All(s => s.Group == null || s.Group.Source != "Tabub"), "and leave the dock");
    _active = true;
    feed.Pump(values);
    Check(Definition("Tabub/left-keys").Available && Text(values, "Tabub/left-keys") == "6", "turning it on brings the latest value back");
  }

  private static void ReloadsAndDetachesKeepTexts(ModValues feed, OverlayValues values)
  {
    OverlayValueId left = Definition("Tabub/left-keys").Id;
    Attach();
    feed.Pump(values);
    Check(Definition("Tabub/left-keys").Id == left && Text(values, "Tabub/left-keys") == "6", "a reload keeps the value's place");

    KitRegistry.Detach("Tabub");
    feed.Pump(values);
    Check(!Definition("Tabub/left-keys").Available && !values.TryGet(left, out _), "a detached mod's values show nothing");
    Check(OverlayFormula.Parse("{Tabub/left-keys}+1").Serialize() == "{Tabub/left-keys}+1", "texts keep them");
    Attach();
    feed.Pump(values);
    Check(Definition("Tabub/left-keys").Available && Definition("Tabub/left-keys").Id == left, "attaching again brings them back in place");
  }

  private static void ANewCanvasGetsEveryValue(ModValues feed)
  {
    OverlayValues fresh = new();
    feed.Pump(fresh);
    Check(Text(fresh, "Tabub/left-keys") == "6" && Text(fresh, "Tabub/rest") == "1:05", "a new canvas shows the latest values");
  }

  private static void AnInvalidModIdAddsNothing()
  {
    List<string> reports = new();
    Check(KitRegistry.Attach("2fast", typeof(ElementKitTests).Assembly, () => true, reports.Add) == null, "a mod id texts can't save adds nothing");
    Check(reports.Count == 1 && reports[0].Contains("\"2fast\""), "and says why");
  }

  private static OverlayValueDefinition Definition(string key)
  {
    Check(OverlayValueCatalog.TryGet(key, out OverlayValueDefinition definition), key + " is in the catalog");
    return definition;
  }

  private static string Text(OverlayValues values, string key) =>
    values.TryGet(Definition(key).Id, out StringBuilder text) ? text.ToString() : null;

  private static void Reset()
  {
    KitRegistry.Clear();
    OverlayValueCatalog.ForgetExtensions();
    _active = true;
    ModFields.LeftKeys.Clear();
    ModFields.Rest.Clear();
    ModFields.Pattern.Clear();
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
