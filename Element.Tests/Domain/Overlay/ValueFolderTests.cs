using System.Collections.Generic;
using System.Linq;
using Element.Domain.Overlay.Values;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

// The dock's places and its search: a mod is one place however many groups
// it made, and a value is found by any name a player would call it.
internal static class ValueFolderTests
{
  [Suite("value dock: one section per category and per mod")]
  public static void Run()
  {
    OverlayValueCatalog.ForgetExtensions();
    try
    {
      BuiltInCategoriesComeFirst();
      AModsGroupsShareOnePlace();
      AWithdrawnModLeavesTheList();
    }
    finally
    {
      OverlayValueCatalog.ForgetExtensions();
    }
  }

  private static void BuiltInCategoriesComeFirst()
  {
    List<OverlayValueFolder> folders = OverlayValueCatalog.Folders();
    Check(
      folders.Select(folder => folder.Category).SequenceEqual(OverlayValueCatalog.Categories),
      "with no mods, the places are the built-in categories in order"
    );
    Check(folders.All(folder => !folder.IsMod && folder.Sections.Count == 1), "a category is one group");
    Check(
      folders.Sum(folder => folder.Count) == OverlayValueCatalog.Sections().Sum(section => section.Values.Count),
      "every value that can be added is in one place"
    );
    OverlayValueFolder judgement = folders.Single(folder => folder.Category == OverlayValueCategory.Judgement);
    Check(
      judgement.KoreanName == "판정" && judgement.EnglishName == "Judgment" && judgement.Key == "Judgement",
      "a category is named in both languages and kept by its kind"
    );
  }

  private static void AModsGroupsShareOnePlace()
  {
    OverlayValueGroup level = new("Tuf", "레벨", "Level", "TUF Overlay"),
      live = new("Tuf", "실시간", "Live", "TUF Overlay"),
      hands = new("Hands", "손", "Hands");
    Register("Tuf/level-id", level, "레벨 ID", "Level ID");
    Register("Hands/left", hands, "왼손", "Left");
    Register("Tuf/score", live, "실시간 점수", "Live score");
    Register("Tuf/speed", live, "배속", "Speed");
    List<OverlayValueFolder> mods = OverlayValueCatalog.Folders().Where(folder => folder.IsMod).ToList();
    Check(mods.Count == 2, "two mods are two places, not one per group: " + mods.Count);
    OverlayValueFolder tuf = mods[0];
    Check(
      tuf.Source == "Tuf" && tuf.KoreanName == "TUF Overlay" && tuf.EnglishName == "TUF Overlay",
      "a mod is named as the mod list names it"
    );
    Check(
      tuf.Sections.Count == 2 && tuf.Sections[0].Group == level && tuf.Sections[1].Group == live && tuf.Count == 3,
      "its groups stay in the order they arrived, under one place"
    );
    Check(mods[1].KoreanName == "Hands" && mods[1].Key == "mod:Hands", "a mod with no name of its own goes by its id");
  }

  private static void AWithdrawnModLeavesTheList()
  {
    OverlayValueCatalog.Withdraw("Hands/left");
    Check(
      OverlayValueCatalog.Folders().All(folder => folder.Source != "Hands"),
      "a mod whose values all went away leaves the list"
    );
  }

  private static void Register(string key, OverlayValueGroup group, string korean, string english) =>
    Check(
      OverlayValueCatalog.Register(key, group, OverlayValueKind.Number, korean, english, 1, null) != null,
      "registered " + key
    );
}
