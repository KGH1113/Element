using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;

namespace Element.Tests;

// Values other mods add: texts keep them while the mod is missing, they come
// back when it registers, and they fit the same masks as the built-in values.
internal static class ValueExtensionTests
{
  public static void Run()
  {
    OverlayValueCatalog.ForgetExtensions();
    try
    {
      MissingModValuesSurviveSaving();
      RemovedBuiltInValuesAreStillDropped();
      CalculationsAreNotTakenForModKeys();
      RegisteringBringsBackAMissingValue();
      WithdrawnValuesLeaveTheDockButKeepTheirPlace();
      ValuesPastTheFirstWordStillRedraw();
      AFullCatalogRefusesMore();
    }
    finally
    {
      OverlayValueCatalog.ForgetExtensions();
    }
  }

  private static void MissingModValuesSurviveSaving()
  {
    Check(OverlayFormula.Parse("{Fingering/left-keys}+1").Serialize() == "{Fingering/left-keys}+1", "a formula keeps a mod's value");
    Check(OverlayFormula.Parse("Fingering/left-keys").Serialize() == "Fingering/left-keys", "a run keeps a mod's value");
    List<OverlayTextRun> runs = new()
    {
      OverlayTextRun.Literal("L ", OverlayColorSettings.Custom("#FFFFFF")),
      OverlayTextRun.ValueRun("Fingering/left-keys", OverlayColorSettings.HighlightColor()),
    };
    List<OverlayTextRun> saved = OverlayTextDocument.FromRuns(runs).ToRuns();
    Check(saved.Count == 2 && saved[1].Value == "Fingering/left-keys", "editing a text keeps a missing mod's value");
    Check(
      OverlayValueCatalog.TryGet("Fingering/left-keys", out OverlayValueDefinition missing) && !missing.Available,
      "a value nobody added yet is missing"
    );

    OverlayValues values = new();
    StringBuilder output = new();
    OverlayFormulaText.Append(output, OverlayFormula.Parse("Fingering/left-keys"), values, false);
    Check(output.Length == 0, "the overlay shows nothing for a missing value");
    OverlayFormulaText.Append(output, OverlayFormula.Parse("Fingering/left-keys"), values, true);
    Check(output.Length == 0, "the editor shows no example for a missing value");
  }

  private static void RemovedBuiltInValuesAreStillDropped()
  {
    Check(OverlayFormula.Parse("{progress}+1").Serialize() == "1", "a removed built-in value is dropped as before");
    Check(!OverlayValueCatalog.TryGet("progress", out _), "and gets no place");
  }

  private static void CalculationsAreNotTakenForModKeys()
  {
    Check(!OverlayValueCatalog.IsExtensionKey("10-4/2"), "a calculation isn't a mod's key");
    Check(!OverlayValueCatalog.IsExtensionKey("Mod/Left"), "keys are lowercase");
    Check(!OverlayValueCatalog.IsExtensionKey("Mod/a/b"), "one slash");
    Check(OverlayValueCatalog.IsExtensionKey("My.Mod_2/next-left-3"), "mod ids keep their own spelling");
    Check(
      OverlayFormula.Parse("10-4/2").TryEvaluate(null, out double result, out _) && Math.Abs(result - 8) < 1e-9,
      "the calculation still works"
    );
  }

  private static void RegisteringBringsBackAMissingValue()
  {
    OverlayValueCatalog.TryResolve("Fingering/right-keys", out OverlayValueDefinition missing);
    OverlayFormula formula = OverlayFormula.Parse("{Fingering/right-keys}*2");
    OverlayValueGroup group = new("Fingering", "타법", "Fingering");
    OverlayValueDefinition added = OverlayValueCatalog.Register(
      "Fingering/right-keys",
      group,
      OverlayValueKind.Number,
      "오른손 키",
      "Right keys",
      2,
      null
    );
    Check(added != null && added.Id == missing.Id && added.Available, "the mod takes the place its key already had");
    Check(added.Sample == "2" && added.Group == group && added.IsExtension, "it brings its names and example");
    Check(formula.Mask.Contains(added.Id), "formulas parsed before it arrived use it");

    OverlayValues values = new();
    values.Set(added.Id, new StringBuilder("3"), 3);
    Check(formula.TryEvaluate(values, out double doubled, out _) && doubled == 6, "and calculate with it");

    OverlayValueDefinition clock = OverlayValueCatalog.Register(
      "Fingering/rest",
      group,
      OverlayValueKind.Time,
      "남은 시간",
      "Rest",
      87,
      null
    );
    Check(clock.Sample == "1:27" && clock.SampleNumber == 87, "a time's example reads as m:ss");
    Check(
      OverlayValueCatalog.Register("left-keys", group, OverlayValueKind.Number, "a", "b", 1, null) == null,
      "a mod's value needs its mod's id in the key"
    );
  }

  private static void WithdrawnValuesLeaveTheDockButKeepTheirPlace()
  {
    OverlayValueGroup group = new("Hands", "손", "Hands");
    OverlayValueDefinition left = OverlayValueCatalog.Register(
      "Hands/left",
      group,
      OverlayValueKind.Number,
      "왼손",
      "Left",
      1,
      null
    );
    Check(Section(group).Contains(left), "an added value is offered under its group");
    int version = OverlayValueCatalog.Version;
    OverlayValueCatalog.Withdraw("Hands/left");
    Check(OverlayValueCatalog.Version != version, "withdrawing tells the dock to rebuild");
    OverlayValueDefinition withdrawn = OverlayValueCatalog.Get(left.Id);
    Check(!withdrawn.Available && withdrawn.KoreanName == "왼손", "a withdrawn value keeps its name for texts");
    Check(!Section(group).Any(), "and leaves the dock");
    Check(OverlayValueCatalog.Sections().Take(OverlayValueCatalog.Categories.Length).All(s => s.Group == null), "built-in headings come first");
    OverlayValueDefinition back = OverlayValueCatalog.Register(
      "Hands/left",
      group,
      OverlayValueKind.Number,
      "왼손",
      "Left",
      1,
      null
    );
    Check(back.Id == left.Id && back.Available, "turning the mod on again brings it back in place");
  }

  private static void ValuesPastTheFirstWordStillRedraw()
  {
    OverlayValueGroup group = new("Wide", "넓게", "Wide");
    OverlayValueDefinition last = null;
    for (int i = 0; OverlayValueCatalog.Count <= 70; i++)
      last = OverlayValueCatalog.Register("Wide/v" + i, group, OverlayValueKind.Number, "v", "v", 1, null);
    Check((int)last.Id >= 64, "the catalog reaches past 64 values");
    OverlayValues values = new();
    long seen = values.Stamp;
    values.Set(last.Id, new StringBuilder("1"), 1);
    OverlayValueMask mask = OverlayValueCatalog.Bit(last.Id);
    Check(!mask.IsEmpty && !mask.Overlaps(OverlayValueCatalog.Bit(OverlayValueId.Combo)), "its bit is its own");
    Check(values.ChangedSince(seen).Overlaps(mask), "an open editor sees it change");
    Check(values.TakeChanges().Overlaps(mask), "the canvas redraws texts that use it");
    Check(OverlayFormula.Parse("{Wide/v0}+{" + last.Key + "}").Mask.Contains(last.Id), "formulas include it");
  }

  private static void AFullCatalogRefusesMore()
  {
    OverlayValueGroup group = new("Full", "가득", "Full");
    while (OverlayValueCatalog.Count < OverlayValueCatalog.Capacity)
      OverlayValueCatalog.Register("Full/v" + OverlayValueCatalog.Count, group, OverlayValueKind.Number, "v", "v", 1, null);
    Check(
      OverlayValueCatalog.Register("Full/one-more", group, OverlayValueKind.Number, "v", "v", 1, null) == null,
      "a full catalog adds nothing"
    );
    Check(!OverlayValueCatalog.TryResolve("Other/missing", out _), "nor keeps a place for a missing value");
    Check(
      OverlayValueCatalog.Register("Hands/left", new OverlayValueGroup("Hands", "손", "Hands"), OverlayValueKind.Number, "왼손", "Left", 1, null) != null,
      "a value that already has a place can still come back"
    );
  }

  private static List<OverlayValueDefinition> Section(OverlayValueGroup group) =>
    OverlayValueCatalog.Sections().Where(section => section.Group == group).SelectMany(section => section.Values).ToList();

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
