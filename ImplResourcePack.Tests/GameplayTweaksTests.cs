using System;
using System.IO;
using System.Xml.Serialization;
using ImplResourcePack.Configuration;
using ImplResourcePack.Domain.Editor;
using ImplResourcePack.Domain.Judgement;

namespace ImplResourcePack.Tests;

internal static class GameplayTweaksTests
{
  public static void Run()
  {
    Judgements();
    JudgementMigration();
    Rotations();
    Persistence();
  }

  private static void Judgements()
  {
    JudgementVisualOptions options = new();
    foreach (HitMargin margin in Enum.GetValues(typeof(HitMargin)))
    {
      bool perfect = margin is HitMargin.PerfectMinus or HitMargin.XPerfect or HitMargin.PerfectPlus;
      Check(
        JudgementVisualPolicy.ShouldShow(options, false, (int)margin) == !perfect,
        "migrated default hides only the three Perfect margins"
      );
      Check(
        !JudgementVisualPolicy.ShouldShow(options, true, (int)margin),
        "record mode suppresses all judgement popups regardless of individual visibility"
      );
    }
    options.HideEarlyPerfect = true;
    Check(
      !JudgementVisualPolicy.ShouldShow(options, false, (int)HitMargin.EarlyPerfect),
      "early Perfect can be hidden separately"
    );
    Check(
      JudgementVisualPolicy.ShouldShow(options, false, (int)HitMargin.LatePerfect),
      "neighboring judgement remains unchanged"
    );
    options.HidePerfect = false;
    Check(
      JudgementVisualPolicy.ShouldShow(options, false, (int)HitMargin.PerfectMinus)
        && JudgementVisualPolicy.ShouldShow(options, false, (int)HitMargin.PerfectPlus)
        && !JudgementVisualPolicy.ShouldShow(options, false, (int)HitMargin.XPerfect),
      "the Perfect minus/plus pair can be shown without changing XPerfect"
    );
    options.HideXPerfect = false;
    Check(
      JudgementVisualPolicy.ShouldShow(options, false, (int)HitMargin.XPerfect),
      "XPerfect visibility can be restored separately"
    );
    options.HidePerfect = true;
    Check(
      !JudgementVisualPolicy.ShouldShow(options, false, (int)HitMargin.PerfectMinus)
        && !JudgementVisualPolicy.ShouldShow(options, false, (int)HitMargin.PerfectPlus)
        && JudgementVisualPolicy.ShouldShow(options, false, (int)HitMargin.XPerfect),
      "hiding the Perfect minus/plus pair leaves XPerfect visible"
    );
    options.HideOverload = true;
    foreach (HitMargin margin in new[] { HitMargin.Multipress, HitMargin.FailOverload, HitMargin.OverPress })
      Check(
        !JudgementVisualPolicy.ShouldShow(options, false, (int)margin),
        "all overload variants share their visibility choice"
      );
    Check(JudgementVisualPolicy.ShouldShow(options, false, 100), "future unknown judgement types fail open");
  }

  private static void JudgementMigration()
  {
    XmlSerializer serializer = new(typeof(JudgementVisualOptions));
    foreach (bool hide in new[] { false, true })
    {
      using StringReader old = new(
        "<JudgementVisualOptions><HidePerfect>" + (hide ? "true" : "false") + "</HidePerfect></JudgementVisualOptions>"
      );
      JudgementVisualOptions migrated = (JudgementVisualOptions)serializer.Deserialize(old);
      migrated.Normalize();
      Check(
        migrated.Version == 1 && migrated.HidePerfect == hide && migrated.HideXPerfect == hide,
        "old combined Perfect visibility migrates to both the signed pair and XPerfect"
      );
      migrated.HideXPerfect = !hide;
      migrated.Normalize();
      Check(migrated.HideXPerfect == !hide, "repeated migration preserves an independent XPerfect choice");

      using StringWriter writer = new();
      serializer.Serialize(writer, migrated);
      using StringReader current = new(writer.ToString());
      JudgementVisualOptions restored = (JudgementVisualOptions)serializer.Deserialize(current);
      restored.Normalize();
      Check(
        restored.HidePerfect == hide && restored.HideXPerfect == !hide,
        "different Perfect and XPerfect visibility survives XML and normalization"
      );
    }
    using StringReader oldGameplay = new(
      "<GameplayTweaksSettings><Judgement><HidePerfect>false</HidePerfect>" + "</Judgement></GameplayTweaksSettings>"
    );
    GameplayTweaksSettings oldSettings = (GameplayTweaksSettings)
      new XmlSerializer(typeof(GameplayTweaksSettings)).Deserialize(oldGameplay);
    oldSettings.Normalize();
    Check(
      !oldSettings.Judgement.HidePerfect && !oldSettings.Judgement.HideXPerfect,
      "normalizing old gameplay settings performs the nested judgement migration"
    );
  }

  private static void Rotations()
  {
    Check(EditorTransform.RotateAngle(999, 22.5f) == 999, "fine rotation preserves midspin sentinels");
    Check(EditorTransform.RotateAngle(15, -22.5f) == -7.5f, "fine rotation preserves arbitrary signed angles");
    Check(EditorTransform.RotateLegacy('R', true) == 'A', "legacy clockwise rotation uses fifteen-degree steps");
    Check(
      EditorTransform.RotateLegacy('R', false) == 'p',
      "legacy counterclockwise rotation uses fifteen-degree steps"
    );
    foreach (char original in "RpJEToUqHQGWLxNZFVDYBCMA")
    {
      char rotated = original;
      for (int i = 0; i < 24; i++)
        rotated = EditorTransform.RotateLegacy(rotated, true);
      Check(rotated == original, "legacy rotations make a full cycle without losing direction codes");
      Check(
        EditorTransform.RotateLegacy(EditorTransform.RotateLegacy(original, true), false) == original,
        "legacy inverse rotation restores each direction"
      );
    }
    foreach (char special in "!thyj56789")
      Check(EditorTransform.RotateLegacy(special, true) == special, "relative and midspin path codes stay intact");
    var horizontal = EditorTransform.Position(2, 3, EditorTransformKind.FlipHorizontal, 0);
    Check(horizontal.X == -2 && horizontal.Y == 3, "horizontal flip changes only x");
    var vertical = EditorTransform.Position(2, 3, EditorTransformKind.FlipVertical, 0);
    Check(vertical.X == 2 && vertical.Y == -3, "vertical flip changes only y");
    var clockwise = EditorTransform.Position(2, 3, EditorTransformKind.Rotate, -90);
    Check(
      Near(clockwise.X, 3) && Near(clockwise.Y, -2),
      "event vectors rotate around the origin in the tile direction"
    );
    var back = EditorTransform.Position(clockwise.X, clockwise.Y, EditorTransformKind.Rotate, 90);
    Check(Near(back.X, 2) && Near(back.Y, 3), "opposite rotations restore event offsets");
    var partial = EditorTransform.Position(float.NaN, 2, EditorTransformKind.Rotate, 90);
    Check(Near(partial.X, -2) && Near(partial.Y, 0), "blank event axes resolve to origin during rotation");
    var flippedBlank = EditorTransform.Position(float.NaN, 2, EditorTransformKind.FlipVertical, 0);
    Check(float.IsNaN(flippedBlank.X) && flippedBlank.Y == -2, "flips preserve unrelated blank axes");
  }

  private static void Persistence()
  {
    GameplayTweaksSettings settings = new();
    Check(
      !settings.Planets.Enabled && !settings.Editor.HasSelectionInfo,
      "new opt-in gameplay tweaks leave gameplay unchanged"
    );
    Check(settings.Chatter.Enabled, "the chatter blocker starts on");
    settings.Editor.RotationStep = float.NaN;
    settings.Planets.Red.Body.Opacity = -10;
    settings.Planets.Blue.Tail.Opacity = 180;
    settings.Planets.Green.Ring.Opacity = float.PositiveInfinity;
    settings.Planets.Red.Body.ColorHex = "ba712b";
    settings.Planets.Blue.Tail.ColorHex = "invalid";
    settings.Normalize();
    Check(settings.Editor.RotationStep == 22.5f, "nonfinite rotation steps recover their default");
    Check(
      settings.Planets.Red.Body.Opacity == 0
        && settings.Planets.Blue.Tail.Opacity == 100
        && settings.Planets.Green.Ring.Opacity == 100,
      "opacity is finite and bounded before it reaches the renderer"
    );
    Check(
      settings.Planets.Red.Body.ColorHex == "#BA712B" && settings.Planets.Blue.Tail.ColorHex == "#3380FF",
      "hex colors normalize while invalid colors recover a planet-specific fallback"
    );
    settings.Planets.Enabled = true;
    settings.Planets.Green.Body.OverrideColor = false;
    settings.Planets.Green.Body.Opacity = 37;
    settings.Judgement.HideTooEarly = true;
    settings.Editor.SyncEventPositions = true;
    settings.Chatter.Enabled = false;
    XmlSerializer serializer = new(typeof(GameplayTweaksSettings));
    using StringWriter writer = new();
    serializer.Serialize(writer, settings);
    using StringReader reader = new(writer.ToString());
    GameplayTweaksSettings restored = (GameplayTweaksSettings)serializer.Deserialize(reader);
    restored.Normalize();
    Check(
      restored.Planets.Enabled
        && !restored.Planets.Green.Body.OverrideColor
        && restored.Planets.Green.Body.Opacity == 37
        && restored.Judgement.HideTooEarly
        && restored.Editor.SyncEventPositions
        && !restored.Chatter.Enabled,
      "nested tweak and RGBA choices survive XML roundtrips, including a chatter blocker turned off"
    );
    using StringReader missing = new("<GameplayTweaksSettings />");
    GameplayTweaksSettings defaults = (GameplayTweaksSettings)serializer.Deserialize(missing);
    defaults.Normalize();
    Check(
      defaults.Judgement.HidePerfect
        && defaults.Judgement.HideXPerfect
        && defaults.Planets.Red.Body.Opacity == 100
        && defaults.Chatter.Enabled,
      "older or empty settings preserve expected nested defaults"
    );
  }

  private static bool Near(float a, float b) => Math.Abs(a - b) < 0.0001f;

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
