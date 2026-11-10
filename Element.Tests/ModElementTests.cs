using System;
using System.Collections.Generic;
using System.Linq;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Mods;
using ElementKit.Elements;
using ElementKit.Hosting;
using UnityEngine;

namespace Element.Tests;

// Overlay elements other mods add through ElementKit: found and checked on
// attach, offered while their mod is on, their properties saved, worked out
// and kept through undo, profiles and a missing mod.
internal static class ModElementTests
{
  private const string ModId = "Bars";

  // A well-made type: the instance it keeps is the one Element uses.
  internal sealed class GoodBars : ElementType
  {
    public static readonly GoodBars Shared = new();
    public static readonly ColorProperty Fill = new("fill", "채우기", "Fill", ColorProperty.Highlight, .5f);
    public static readonly NumberProperty Count = new("count", "개수", "Count", 8, 2, 32, NumberUnit.None);
    public static readonly ChoiceProperty Grow = new(
      "grow",
      "방향",
      "Grow",
      new ChoiceOption("up", "위로", "Up"),
      new ChoiceOption("middle", "가운데", "Middle")
    );
    public static readonly ToggleProperty Labels = new("labels", "숫자", "Labels", true);
    public static readonly NumberProperty Width = new("width", "너비", "Width", 300, 40, 1920);
    public static readonly NumberProperty Height = new("height", "높이", "Height", 60, 10, 400);

    public int Draws;

    public GoodBars()
      : base("good-bars", "막대", "Bars") { }

    protected override void Edit(EditorLayout editor)
    {
      editor.Bar(Fill, Count, Grow, Labels);
      editor.Below(Width);
      editor.Beside(Height);
    }

    protected override Vector2 Measure(Props props) => new((float)props[Width], (float)props[Height]);

    protected override void Draw(Surface surface, Props props)
    {
      Draws++;
      int count = (int)props[Count];
      for (int i = 0; i < count; i++)
        surface.Box(new Rect(i * 10, 0, 8, surface.Size.y), props[Fill]);
    }
  }

  // Lays its properties out by default: every one in the bar.
  internal sealed class Plain : ElementType
  {
    public static readonly ToggleProperty On = new("on", "켜기", "On", false);

    public Plain()
      : base("plain", "기본", "Plain") { }

    protected override Vector2 Measure(Props props) => new(10, 10);

    protected override void Draw(Surface surface, Props props) { }
  }

  internal sealed class BadKey : ElementType
  {
    public BadKey()
      : base("Bad Key", "나쁨", "Bad") { }

    protected override Vector2 Measure(Props props) => Vector2.one;

    protected override void Draw(Surface surface, Props props) { }
  }

  internal sealed class NeedsArgument : ElementType
  {
    public NeedsArgument(int size)
      : base("needs-argument", "인자", "Argument") { }

    protected override Vector2 Measure(Props props) => Vector2.one;

    protected override void Draw(Surface surface, Props props) { }
  }

  internal sealed class BrokenScreen : ElementType
  {
    public BrokenScreen()
      : base("broken-screen", "고장", "Broken") { }

    protected override void Edit(EditorLayout editor) => throw new InvalidOperationException("no screen today");

    protected override Vector2 Measure(Props props) => Vector2.one;

    protected override void Draw(Surface surface, Props props) { }
  }

  // Properties that break the rules are left out; the type still attaches.
  internal sealed class Faulty : ElementType
  {
    public static readonly NumberProperty Upside = new("upside", "거꾸로", "Upside", 5, 10, 1);
    public static readonly ColorProperty Named = new("named", "이름 색", "Named", "red");
    public static readonly ChoiceProperty Single = new(
      "single",
      "하나",
      "Single",
      new ChoiceOption("only", "하나", "Only")
    );
    public static readonly ToggleProperty Twin = new("twin", "쌍", "Twin", true);
    public static readonly ToggleProperty TwinAgain = new("twin", "쌍 2", "Twin 2", true);
    public static readonly ToggleProperty Hidden = new("hidden", "숨김", "Hidden", true);

    public Faulty()
      : base("faulty", "고장 난 속성", "Faulty") { }

    protected override void Edit(EditorLayout editor) => editor.Bar(Upside, Named, Single, Twin, TwinAgain);

    protected override Vector2 Measure(Props props) => Vector2.one;

    protected override void Draw(Surface surface, Props props) { }
  }

  // Counts what a type draws, without Unity.
  private sealed class CountingSurface : Surface
  {
    public int Boxes;
    public Color LastColor;

    public override Vector2 Size => new(300, 60);

    public override RectTransform Root => null;

    public override void Box(Rect rect, ElementColor fill, float rounding = 0)
    {
      Boxes++;
      LastColor = fill.Solid;
    }

    public override void Text(
      Rect rect,
      string text,
      float size,
      ElementColor color,
      TextAlign align = TextAlign.Center
    ) { }
  }

  private static bool _active = true;

  public static void Run()
  {
    KitRegistry.Clear();
    try
    {
      List<string> reports = new();
      KitRegistry.Attach(ModId, typeof(ModElementTests).Assembly, () => _active, reports.Add);
      TypesAreFoundAndChecked(reports);
      ModElements types = new(_ => { });
      TheCatalogOffersTypesWhileTheirModIsOn(types);
      PropertiesStartAtTheirDefaults();
      SavedPropertiesAreWorkedOut();
      EditsAreWrittenWithinLimits();
      ElementsKeepPropertiesThroughUndoAndProfiles();
      AMissingModLeavesTheElementAsItWas(types);
    }
    finally
    {
      KitRegistry.Clear();
      _active = true;
    }
  }

  private static void TypesAreFoundAndChecked(List<string> reports)
  {
    AttachedMod mod = KitRegistry.Snapshot.Single(m => m.Id == ModId);
    string all = string.Join(" | ", reports);
    Check(mod.Elements.Contains(GoodBars.Shared), "the instance a type keeps is the one attached; got " + all);
    Check(GoodBars.Shared.FullKey == "Bars/good-bars", "a type learns its mod's id");
    Check(
      GoodBars
        .Shared.Properties.Select(p => p.Key)
        .SequenceEqual(new[] { "fill", "count", "grow", "labels", "width", "height" }),
      "properties keep the order they are written"
    );
    Check(
      GoodBars.Shared.Layout.BarItems.Count == 4
        && GoodBars.Shared.Layout.BelowItem == GoodBars.Width
        && GoodBars.Shared.Layout.BesideItem == GoodBars.Height,
      "the editing screen holds what Edit placed"
    );
    ElementType plain = mod.Elements.Single(t => t.Key == "plain");
    Check(
      plain.Layout.BarItems.SequenceEqual(new Property[] { Plain.On }),
      "without Edit every property goes in the bar"
    );

    Check(
      mod.Elements.All(t => t.Key != "Bad Key")
        && reports.Any(r => r.Contains("\"Bad Key\"") && r.Contains("hand-bars")),
      "a key layouts can't keep is explained"
    );
    Check(
      reports.Any(r => r.Contains("NeedsArgument") && r.Contains("constructor that takes nothing")),
      "a type without a plain constructor is explained"
    );
    Check(
      reports.Any(r => r.Contains("BrokenScreen") && r.Contains("no screen today")),
      "a type whose screen fails is named"
    );
    Check(mod.Elements.All(t => t.Key != "broken-screen" && t.Key != "needs-argument"), "failing types are left out");

    ElementType faulty = mod.Elements.Single(t => t.Key == "faulty");
    Check(
      faulty.Properties.Select(p => p.Key).SequenceEqual(new[] { "twin", "hidden" }),
      "only the faulty properties are left out; kept " + string.Join(",", faulty.Properties.Select(p => p.Key))
    );
    Check(reports.Any(r => r.Contains("\"upside\"") && r.Contains("min")), "upside-down limits are explained");
    Check(
      reports.Any(r => r.Contains("\"named\"") && r.Contains("#FF9F43")),
      "a color name is explained with an example"
    );
    Check(reports.Any(r => r.Contains("\"single\"") && r.Contains("two or more")), "a choice of one is explained");
    Check(reports.Any(r => r.Contains("two properties with the key \"twin\"")), "a repeated property key is explained");
    Check(
      reports.Any(r => r.Contains("\"hidden\"") && r.Contains("isn't on its editing screen")),
      "a property players can't reach is pointed out"
    );
    Check(
      faulty.Layout.BarItems.SequenceEqual(new Property[] { Faulty.Twin }),
      "the screen drops properties that were left out"
    );
  }

  private static void TheCatalogOffersTypesWhileTheirModIsOn(ModElements types)
  {
    types.Refresh();
    Check(types.Find("Bars/good-bars") == GoodBars.Shared, "a saved key finds its type");
    Check(
      types.Offered.Contains(GoodBars.Shared) && types.Offered.All(t => t.ModId == ModId),
      "the add menu offers attached types"
    );
    int version = types.Version;
    types.Refresh();
    Check(types.Version == version, "nothing new changes nothing");
    _active = false;
    types.Refresh();
    Check(
      types.Find("Bars/good-bars") == null && types.IsTurnedOff("Bars/good-bars") && types.Offered.Count == 0,
      "a turned-off mod's types step aside"
    );
    _active = true;
    types.Refresh();
    Check(types.Find("Bars/good-bars") == GoodBars.Shared && types.Version > version, "turning it on brings them back");
  }

  private static void PropertiesStartAtTheirDefaults()
  {
    ModProps props = new();
    props.Compile(GoodBars.Shared, new OverlayModSettings());
    props.Use(new OverlayValues(), false, Color.red);
    Check(props[GoodBars.Count] == 8 && props[GoodBars.Width] == 300, "numbers start at their defaults");
    Check(
      props[GoodBars.Grow] == 0 && props[GoodBars.Labels],
      "choices start at the first option, toggles as declared"
    );
    Color fill = props[GoodBars.Fill].Solid;
    Check(
      fill.r == 1 && fill.g == 0 && Mathf.Approximately(fill.a, .5f),
      "a highlight color follows the highlight with its own opacity"
    );
    CountingSurface surface = new();
    GoodBars.Shared.Draws = 0;
    GoodBars.Shared.RunDraw(surface, props);
    Check(surface.Boxes == 8 && GoodBars.Shared.Draws == 1, "the type draws from its properties");

    // Sample's bars, outside a level, draw their sample only in the editors.
    CountingSurface bars = new();
    ModProps sample = new();
    sample.Compile(Sample.SampleBars.Shared, new OverlayModSettings());
    sample.Use(new OverlayValues(), false, Color.white);
    Sample.SampleBars.Shared.RunDraw(bars, sample);
    Check(bars.Boxes == 0, "outside a level the game shows no bars");
    sample.Use(new OverlayValues().WithSamples, true, Color.white);
    Sample.SampleBars.Shared.RunDraw(bars, sample);
    Check(bars.Boxes == 16, "the editor shows a sample bar a group; drew " + bars.Boxes);
  }

  private static void SavedPropertiesAreWorkedOut()
  {
    OverlayValues values = new();
    OverlayModSettings saved = new();
    saved.Properties.Add(
      new OverlayPropertySetting
      {
        Key = "count",
        Value = "4",
        Formula = "{combo}*2",
      }
    );
    saved.Properties.Add(new OverlayPropertySetting { Key = "width", Value = "99999" });
    saved.Properties.Add(new OverlayPropertySetting { Key = "grow", Value = "middle" });
    saved.Properties.Add(new OverlayPropertySetting { Key = "labels", Value = "false" });
    saved.Properties.Add(new OverlayPropertySetting { Key = "from-a-newer-version", Value = "x" });
    ModProps props = new();
    props.Compile(GoodBars.Shared, saved);
    props.Use(values, false, Color.white);
    Check(props[GoodBars.Count] == 4, "a formula without data falls back to the saved number");
    Check(props.Mask.Contains(OverlayValueId.Combo), "the element knows the values its properties use");
    values.Set(OverlayValueId.Combo, new System.Text.StringBuilder("7"), 7);
    Check(props[GoodBars.Count] == 14, "a formula follows its value");
    values.Set(OverlayValueId.Combo, new System.Text.StringBuilder("100"), 100);
    Check(props[GoodBars.Count] == 32, "a worked-out number stays within its limits");
    Check(props[GoodBars.Width] == 1920, "a saved number past today's limit reads as the limit");
    Check(props[GoodBars.Grow] == 1 && !props[GoodBars.Labels], "choices and toggles read as saved");

    saved.Find("grow").Value = "gone";
    props.Compile(GoodBars.Shared, saved);
    Check(props[GoodBars.Grow] == 0, "an option the mod dropped reads as the first");
    props.Use(values.WithSamples, true, Color.white);
    Check(props.IsPreview, "the editors draw with samples");
  }

  private static void EditsAreWrittenWithinLimits()
  {
    OverlayModSettings settings = new();
    Check(
      ModPropertyValues.WriteNumber(settings, GoodBars.Count, OverlayFormula.FromNumber(50)),
      "a number past the limit says so"
    );
    Check(settings.Find("count").Value == "32" && settings.Find("count").Formula == "", "and is kept within it");
    ModPropertyValues.WriteNumber(settings, GoodBars.Count, OverlayFormula.Parse("{combo}+1"));
    Check(
      settings.Find("count").Formula == "{combo}+1" && settings.Find("count").Value == "32",
      "a formula keeps the number underneath"
    );
    Check(
      ModPropertyValues.NumberFormula(GoodBars.Count, settings.Find("count")).Serialize() == "{combo}+1",
      "the dock reads the formula back"
    );
    ModPropertyValues.WriteChoice(settings, GoodBars.Grow, 1);
    ModPropertyValues.WriteToggle(settings, GoodBars.Labels, false);
    ModPropertyValues.WriteColor(settings, GoodBars.Fill, OverlayColorSettings.Custom("#123456", .25f));
    Check(
      settings.Find("grow").Value == "middle" && settings.Find("labels").Value == "false",
      "choices save their key, toggles true or false"
    );
    Check(settings.Find("fill").Color.Hex == "#123456" && settings.Properties.Count == 4, "one entry a property");
  }

  private static void ElementsKeepPropertiesThroughUndoAndProfiles()
  {
    OverlayLayoutSettings element = new()
    {
      Id = "mod:1",
      TemplateId = "Bars/good-bars",
      Mod = new OverlayModSettings { Width = 300, Height = 60 },
    };
    List<OverlayLayoutSettings> layouts = new() { element };
    LayoutHistory history = new();
    history.Reset(layouts);
    element.Mod.Get("count").Value = "12";
    history.Commit(layouts);
    Check(history.CanUndo, "a property change is its own undo step");
    history.Undo(layouts);
    Check(layouts[0].Mod.Find("count") == null, "undo brings the properties back");
    history.Redo(layouts);
    Check(layouts[0].Mod.Find("count")?.Value == "12", "and redo takes them forward");

    OverlayLayoutSettings drawnAgain = layouts[0].Clone();
    drawnAgain.Mod.Width = 500;
    drawnAgain.Mod.KoreanName = "다른 이름";
    Check(drawnAgain.SameAs(layouts[0]), "the size and names it last drew with are not an edit");

    layouts[0].Mod.Get("fill").Color = OverlayColorSettings.HighlightColor(.3f);
    ElementSettings settings = new() { OverlayLayouts = layouts };
    ElementSettings copy = SettingsXml.Clone(settings);
    OverlayLayoutSettings saved = copy.OverlayLayouts.Single();
    Check(
      saved.TemplateId == "Bars/good-bars"
        && saved.Mod.Find("count").Value == "12"
        && saved.Mod.Find("fill").Color.Highlight
        && saved.Mod.Width == 300,
      "properties survive saving, as profiles and Settings.xml do"
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

  private static void AMissingModLeavesTheElementAsItWas(ModElements types)
  {
    KitRegistry.Detach(ModId);
    types.Refresh();
    Check(
      types.Find("Bars/good-bars") == null && !types.IsTurnedOff("Bars/good-bars"),
      "a detached mod's types are gone, not off"
    );
    OverlayModSettings saved = new();
    saved.Properties.Add(new OverlayPropertySetting { Key = "count", Value = "5" });
    ModProps props = new();
    props.Compile(null, saved);
    Check(
      props.Mask.IsEmpty && saved.Properties.Count == 1,
      "without its type nothing is worked out and nothing is dropped"
    );
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
