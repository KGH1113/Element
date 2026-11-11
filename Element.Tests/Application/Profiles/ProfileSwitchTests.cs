using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Element.Application.Effects;
using Element.Application.Profiles;
using Element.Application.Settings;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Configuration.Gameplay;
using Element.Configuration.Overlay;
using Element.Configuration.Profiles;
using Element.Domain.Tweaks;
using static Element.Tests.Application.Profiles.ProfileFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Application.Profiles;

/// <summary>
/// Profiles switch in place, keep their own values through saving, and stay
/// named and in use.
/// </summary>
internal static class ProfileSwitchTests
{
  internal static void Run()
  {
    CopyKeepsHeldObjects();
    FirstProfileHoldsCurrentSettings();
    SwitchingKeepsEachProfilesValues();
    ProfilesSurviveTheSettingsFile();
    DeletingTheProfileInUseSwitchesFirst();
    NamesAreCheckedLikePresetNames();
    RepairKeepsOneProfileInUse();
  }

  // Settings windows and controllers hold nested objects; a copy must change
  // their values, not replace them, and carry every saved field.
  private static void CopyKeepsHeldObjects()
  {
    GameplayTweaksSettings live = new();
    GameplayTweaksSettings incoming = new();
    incoming.Editor.ShowAngle = true;
    incoming.Editor.RotationStep = 7.5f;
    incoming.Judgement.HideMiss = true;
    incoming.Planets.Enabled = true;
    incoming.Planets.Red.Body.ColorHex = "#010203";
    incoming.Planets.Blue.Ring.Opacity = 40;
    PlanetAppearanceProfile red = live.Planets.Red;
    EditorTweaksOptions editor = live.Editor;
    SettingsCopy.Into(live, incoming);
    Check(Xml(live) == Xml(incoming), "every gameplay value is copied");
    Check(live.Planets.Red == red && live.Editor == editor, "nested objects stay the ones already held");

    EffectSettings effects = new();
    EffectSettings other = new();
    Check(new EffectPresetLibrary(other).TryAdd("Mine", out _, out _), "a preset can be added");
    other.Enabled = true;
    List<EffectPreset> presets = effects.Presets;
    SettingsCopy.Into(effects, other);
    Check(Xml(effects) == Xml(other), "every effect value is copied");
    Check(
      effects.Presets == presets && effects.Presets.Count == other.Presets.Count,
      "the preset list stays the same list"
    );
  }

  private static void FirstProfileHoldsCurrentSettings()
  {
    ElementSettings settings = Fresh();
    string before = Values(settings);
    ProfileLibrary library = new(settings, "기본");
    Check(library.Profiles.Count == 1, "settings from before profiles become one profile");
    Check(
      library.Active?.Name == "기본" && library.Active.Content == null,
      "it is in use and named in the game's language"
    );
    Check(Values(settings) == before, "making the first profile changes nothing");
    ProfileSummary summary = library.Summarize(library.Active);
    Check(
      summary.Elements == settings.OverlayLayouts.Count && summary.Presets == settings.Effects.Presets.Count,
      "the profile in use is summarized from the live settings"
    );
  }

  private static void SwitchingKeepsEachProfilesValues()
  {
    ElementSettings settings = Fresh();
    settings.Gameplay.Chatter.WindowMs = ChatterFilter.MaximumWindowMs;
    settings.OverlayLayouts.Add(SpacedText("spaces"));
    ProfileLibrary library = new(settings, "Default");
    ElementProfile first = library.Active;
    string original = Values(settings);
    GameplayTweaksSettings gameplay = settings.Gameplay;
    PlanetAppearanceProfile red = settings.Gameplay.Planets.Red;
    EffectSettings effects = settings.Effects;
    List<EffectPreset> presets = settings.Effects.Presets;
    List<OverlayLayoutSettings> layouts = settings.OverlayLayouts;

    Check(library.TryCreate("Stream", out ElementProfile stream, out _), "a new profile is created");
    Check(library.Active == stream && first.Content != null, "the new profile is in use; the first keeps its values");
    Check(Values(settings) == Values(Fresh()), "a new profile starts with the initial settings, not a copy");
    Check(
      settings.Gameplay.Chatter.WindowMs == ChatterFilter.MaximumWindowMs
        && settings.Gameplay == gameplay
        && settings.OverlayLayouts == layouts,
      "a new profile keeps this PC's chatter filter and the objects the mod holds"
    );

    settings.HighlightColor = "#FF0000";
    settings.OverlayFont = "Pretendard|Bold";
    settings.Gameplay.Planets.Red.Body.ColorHex = "#00FF00";
    settings.Gameplay.Judgement.HideMiss = true;
    settings.Gameplay.Editor.FineRotation = true;
    settings.Effects.Enabled = true;
    Check(new EffectPresetLibrary(settings.Effects).TryAdd("Stream only", out _, out _), "a preset can be added");
    settings.OverlayLayouts.RemoveAt(0);
    settings.Gameplay.Chatter.WindowMs = ChatterFilter.MinimumWindowMs;
    string streamValues = Values(settings);

    Check(library.Switch(first.Id), "switching back works");
    Check(Values(settings) == original, "the first profile's values come back exactly");
    Check(
      settings.Gameplay.Chatter.WindowMs == ChatterFilter.MinimumWindowMs,
      "the chatter filter is shared, not switched"
    );
    Check(
      settings.Gameplay == gameplay
        && settings.Gameplay.Planets.Red == red
        && settings.Effects == effects
        && settings.Effects.Presets == presets
        && settings.OverlayLayouts == layouts,
      "switching changes values inside the objects the mod holds"
    );
    Check(
      settings.OverlayLayouts.Find(l => l.Id == "spaces")?.Text.Runs[1].Text == "  ",
      "a text of only spaces survives a switch"
    );
    Check(first.Content == null && stream.Content != null, "only the profile in use has no stored values");

    Check(library.Switch(stream.Id), "switching forward works");
    Check(Values(settings) == streamValues, "the second profile's values come back exactly");
    Check(!library.Switch(stream.Id), "switching to the profile in use does nothing");

    // Duplicating copies values next to the original and leaves the profile in use alone.
    ElementProfile streamCopy = library.Duplicate(stream.Id, "Stream copy");
    ElementProfile firstCopy = library.Duplicate(first.Id, "Default copy");
    ElementProfile again = library.Duplicate(stream.Id, "Stream copy");
    Check(
      library.Active == stream && Values(settings) == streamValues && again.Name == "Stream copy 2",
      "duplicating keeps the profile in use and finds a free name"
    );
    Check(
      library.Profiles.IndexOf(firstCopy) == library.Profiles.IndexOf(first) + 1
        && library.Profiles.IndexOf(again) == library.Profiles.IndexOf(stream) + 1,
      "a copy sits right after its original"
    );
    Check(
      library.Switch(streamCopy.Id) && Values(settings) == streamValues,
      "a copy of the profile in use has its values"
    );
    Check(library.Switch(firstCopy.Id) && Values(settings) == original, "a copy of a stored profile has its values");
    Check(library.Duplicate("missing", "x") == null, "a missing profile can't be duplicated");
  }

  private static void ProfilesSurviveTheSettingsFile()
  {
    ElementSettings settings = Fresh();
    settings.OverlayLayouts.Add(SpacedText("spaces"));
    ProfileLibrary library = new(settings, "Default");
    string first = Values(settings);
    library.TryCreate("Other", out _, out _);
    settings.OverlayLayouts.Clear();

    using MemoryStream file = new();
    new XmlSerializer(typeof(ElementSettings)).Serialize(file, settings);
    file.Position = 0;
    ElementSettings loaded = SettingsXml.Read<ElementSettings>(file);
    SettingsMigrator.Migrate(loaded);
    ProfileLibrary reloaded = new(loaded, "Default");
    Check(reloaded.Profiles.Count == 2 && reloaded.Active.Name == "Other", "profiles and the one in use load back");
    Check(reloaded.Switch(reloaded.Profiles[0].Id), "a loaded profile switches");
    Check(Values(loaded) == first, "a stored profile keeps its values, spaces included, through the settings file");
  }

  private static void DeletingTheProfileInUseSwitchesFirst()
  {
    ElementSettings settings = Fresh();
    ProfileLibrary library = new(settings, "Default");
    ElementProfile first = library.Active;
    string firstValues = Values(settings);
    library.TryCreate("Second", out ElementProfile second, out _);
    settings.HighlightColor = "#00FF00";

    Check(library.Delete(second.Id, out bool switched) && switched, "deleting the profile in use switches away first");
    Check(library.Active == first && library.Profiles.Count == 1, "its neighbor is in use");
    Check(Values(settings) == firstValues, "the neighbor's values are live");
    Check(!library.CanDelete && !library.Delete(first.Id, out _), "the only profile is never deleted");
  }

  private static void NamesAreCheckedLikePresetNames()
  {
    ProfileLibrary library = new(Fresh(), "Default");
    Check(
      !library.ValidateName("   ", null, out _, out ProfileNameProblem empty) && empty == ProfileNameProblem.Empty,
      "an empty name is refused"
    );
    Check(
      !library.ValidateName(
        new string('a', ElementProfile.MaximumNameLength + 1),
        null,
        out _,
        out ProfileNameProblem tooLong
      )
        && tooLong == ProfileNameProblem.TooLong,
      "a long name is refused"
    );
    Check(
      !library.ValidateName(" default ", null, out _, out ProfileNameProblem taken)
        && taken == ProfileNameProblem.Taken,
      "names ignore case and spaces"
    );
    Check(
      library.ValidateName("default", library.Active.Id, out string same, out _) && same == "default",
      "a profile may keep its own name"
    );
    Check(
      library.TryRename(library.Active.Id, "  Practice ", out _) && library.Active.Name == "Practice",
      "renaming trims"
    );
  }

  private static void RepairKeepsOneProfileInUse()
  {
    ElementSettings settings = Fresh();
    ProfileContent stored = ProfileContent.Capture(settings);
    settings.Profiles.Add(
      new ElementProfile
      {
        Id = "a",
        Name = "Same",
        Content = stored,
      }
    );
    settings.Profiles.Add(
      new ElementProfile
      {
        Id = "a",
        Name = "Duplicate id",
        Content = stored,
      }
    );
    settings.Profiles.Add(
      new ElementProfile
      {
        Id = "b",
        Name = "same",
        Content = stored,
      }
    );
    settings.Profiles.Add(new ElementProfile { Id = "c", Name = "Lost values" });
    settings.Profiles.Add(null);
    settings.ActiveProfileId = "missing";
    ProfileLibrary library = new(settings, "Default");
    Check(library.Profiles.Count == 3, "broken entries are dropped: " + library.Profiles.Count);
    Check(
      library.Active != null && library.Active.Content == null && library.Active.Name == "Default",
      "the live settings get a profile"
    );
    Check(library.Profiles[1].Name == "Same" && library.Profiles[2].Name == "same 2", "names are made unique in order");

    ElementSettings inUse = Fresh();
    inUse.Profiles.Add(
      new ElementProfile
      {
        Id = "x",
        Name = "X",
        Content = ProfileContent.Capture(inUse),
      }
    );
    inUse.ActiveProfileId = "x";
    ProfileLibrary repaired = new(inUse, "Default");
    Check(
      repaired.Profiles.Count == 1 && repaired.Active.Content == null,
      "the profile in use stores nothing; the live settings win"
    );
  }
}
