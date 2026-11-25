using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Element.Application.Settings;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Conditions;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Tests.Support;
using static Element.Tests.Domain.Overlay.OverlayFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

// An If in a text: how a condition compares, which case's runs show, how the
// editing document keeps an If's stretch through edits, saving, and the move
// from colors that followed a value.
internal static class OverlayIfTests
{
  private static readonly OverlayColorSettings White = OverlayColorSettings.Custom("#FFFFFF");
  private static readonly OverlayColorSettings Gold = OverlayColorSettings.Custom("#FFD84D");

  [Suite("an If shows the look of its first case that holds")]
  public static void Run()
  {
    ConditionsCompareThreeWays();
    TheFirstCaseThatHoldsShows();
    TheDocumentReadsAndWritesIfs();
    LooksAreEditedOneAtATime();
    TypingStaysInsideAnIf();
    DeletingAroundAnIfTakesIt();
    CasesComeAndGo();
    IfsSurviveSaving();
    FontsInsideCasesCount();
    ValueColorsBecomeIfs();
  }

  private static OverlayCondition When(string value, string than, bool smaller, bool same, bool bigger) =>
    new()
    {
      Value = value,
      Than = than,
      Smaller = smaller,
      Same = same,
      Bigger = bigger,
    };

  private static void ConditionsCompareThreeWays()
  {
    Numbers numbers = new() { [OverlayValueId.Accuracy] = 99.81, [OverlayValueId.Miss] = 0 };
    OverlayConditionCheck atLeast = new(When("accuracy", "100", false, true, true));
    Check(
      atLeast.Compare(numbers) == OverlayComparison.Smaller && !atLeast.Holds(numbers),
      "99.81 is smaller than 100"
    );
    numbers[OverlayValueId.Accuracy] = 100;
    Check(atLeast.Compare(numbers) == OverlayComparison.Same && atLeast.Holds(numbers), "100 is the same as 100");
    Check(new OverlayConditionCheck(When("miss", "0", false, true, false)).Holds(numbers), "no miss is the same as 0");
    Check(!new OverlayConditionCheck(When("miss", "0", true, false, true)).Holds(numbers), "0 is not different from 0");
    Check(
      new OverlayConditionCheck(When("accuracy", "{miss}+99.5", false, false, true)).Holds(numbers),
      "the other side can be a calculation"
    );
    Check(
      OverlayConditionCheck.Compare(0.1 + 0.2, 0.3) == OverlayComparison.Same
        && OverlayConditionCheck.Compare(99.99, 100) == OverlayComparison.Smaller,
      "numbers the same to six decimals are the same, and no further apart"
    );
    OverlayConditionCheck noData = new(When("combo", "10", true, true, true));
    Check(
      noData.Compare(numbers) == OverlayComparison.Unknown && !noData.Holds(numbers),
      "without data a condition is off, whatever it turns on for"
    );
    Check(!new OverlayConditionCheck(When("", "", true, true, true)).Holds(numbers), "an empty condition is off");
    Check(
      atLeast.Mask.Contains(OverlayValueId.Accuracy) && !atLeast.Mask.Contains(OverlayValueId.Miss),
      "it knows the values it reads"
    );
  }

  // "Acc " then an If on the number: gold at 100, sky from 98, white otherwise.
  private static List<OverlayTextRun> Tiers() =>
    new()
    {
      OverlayTextRun.Literal("Acc ", White.Clone()),
      new OverlayTextRun
      {
        If = new OverlayTextIf
        {
          Cases =
          {
            new OverlayTextCase
            {
              When = When("accuracy", "100", false, true, true),
              Runs = { OverlayTextRun.Literal("PERFECT", Gold.Clone()) },
            },
            new OverlayTextCase
            {
              When = When("accuracy", "98", false, true, true),
              Runs = { OverlayTextRun.ValueRun("accuracy", OverlayColorSettings.Custom("#5AC8FA")) },
            },
          },
          Otherwise = { OverlayTextRun.ValueRun("accuracy", White.Clone()) },
        },
      },
      OverlayTextRun.Literal("%", White.Clone()),
    };

  private static void TheFirstCaseThatHoldsShows()
  {
    List<OverlayTextRun> runs = Tiers();
    OverlayTextLooks looks = new();
    looks.Load(runs);
    Check(looks.HasIfs && looks.Mask.Contains(OverlayValueId.Accuracy), "the text reads the value its conditions use");
    Check(Describe(looks.Shown.ToList()) == "'Acc '[accuracy|custom]'%'", "before any data, otherwise shows");
    Numbers numbers = new() { [OverlayValueId.Accuracy] = 99 };
    Check(looks.Update(numbers) && looks.Shown[1].Color.Hex == "#5AC8FA", "99 is in the second case");
    numbers[OverlayValueId.Accuracy] = 99.5;
    Check(!looks.Update(numbers), "moving inside a case changes nothing shown");
    numbers[OverlayValueId.Accuracy] = 100;
    Check(
      looks.Update(numbers) && Describe(looks.Shown.ToList()) == "'Acc ''PERFECT''%'",
      "100 is in the first case, which is read first"
    );
    numbers[OverlayValueId.Accuracy] = 50;
    Check(looks.Update(numbers) && looks.Shown[1].Color.Hex == "#FFFFFF", "50 is in none, so otherwise shows");
    OverlayTextLooks plain = new();
    List<OverlayTextRun> noIf = new() { OverlayTextRun.Literal("hi", White.Clone()) };
    plain.Load(noIf);
    Check(
      !plain.HasIfs && ReferenceEquals(plain.Shown, noIf) && !plain.Update(numbers),
      "a text without Ifs shows its own runs"
    );
  }

  private static string Text(OverlayTextDocument document) =>
    document.PlainText(0, document.Count, key => "{" + key + "}");

  private static void TheDocumentReadsAndWritesIfs()
  {
    OverlayTextDocument document = OverlayTextDocument.FromRuns(Tiers());
    Check(
      Text(document) == "Acc {accuracy}%" && document.BlockCount == 1,
      "an If opens on otherwise: " + Text(document)
    );
    OverlayTextBlock block = document.Block(0);
    Check(
      block.Start == 4 && block.End == 5 && block.Cases == 2 && block.Shown == block.Otherwise,
      "the If is the value's stretch"
    );
    Check(
      block.When(0).Than == "100" && document.LookCount(0, 0) == 7 && document.LookItem(0, 0, 0).Text == "P",
      "its cases wait with their looks"
    );
    Check(OverlayTextSettings.SameRuns(document.ToRuns(), Tiers()), "it saves as it was read");
    Check(
      document.SameAs(OverlayTextDocument.FromRuns(Tiers())) && document.Clone().SameAs(document),
      "and is the same text"
    );
    Check(!document.IsBlank && new OverlayTextDocument().IsBlank, "a text with an If is not blank");
  }

  private static void LooksAreEditedOneAtATime()
  {
    OverlayTextDocument document = OverlayTextDocument.FromRuns(Tiers());
    OverlayTextDocument opened = document.Clone();
    document.Show(0, 0);
    Check(Text(document) == "Acc PERFECT%" && document.Block(0).End == 11, "showing a case puts its look in the text");
    Check(document.SameAs(opened), "which look is shown is no edit");
    document.SetColor(4, 11, OverlayColorSettings.Custom("#FF0000"));
    document.Show(0, 2);
    Check(
      Text(document) == "Acc {accuracy}%" && document[4].Color.Hex == "#FFFFFF",
      "the other looks keep their own color"
    );
    List<OverlayTextRun> saved = document.ToRuns();
    Check(
      saved[1].If.Cases[0].Runs[0].Color.Hex == "#FF0000" && saved[1].If.Cases[1].Runs[0].Color.Hex == "#5AC8FA",
      "the change is saved in the look it was made in"
    );
    Check(!document.SameAs(opened), "and it is an edit");
    OverlayTextDocument copy = document.Clone();
    copy.Show(0, 1);
    copy.Delete(4, 5);
    Check(document.LookCount(0, 1) == 1, "a copy for undo shares nothing with the text");
  }

  private static void TypingStaysInsideAnIf()
  {
    OverlayTextDocument document = OverlayTextDocument.FromRuns(Tiers());
    document.Show(0, 0);
    int caret = document.InsertText(11, 11, "!", Gold);
    Check(caret == 12 && document.Block(0).End == 12, "typing at an If's end goes on inside it");
    document.InsertText(4, 4, "x", White);
    Check(document.Block(0).Start == 5 && document.Block(0).End == 13, "typing before an If stays outside it");
    document.InsertText(7, 7, "_", Gold);
    Check(document.Block(0).End == 14, "typing inside grows it");
    document.Show(0, 2);
    Check(
      Text(document) == "Acc x{accuracy}%" && document.ToRuns()[1].If.Cases[0].Runs[0].Text == "PE_RFECT!",
      "only that look took the typing"
    );
    document.Delete(5, 6);
    Check(
      document.Block(0).Start == 5 && document.Block(0).End == 5 && document.BlockCount == 1,
      "a look emptied still has its place"
    );
    document.InsertText(5, 5, "?", White);
    Check(document.Block(0).End == 6 && Text(document) == "Acc x?%", "and typing there fills it again");
    OverlayTextDocument whole = new();
    whole.InsertText(0, 0, "ab", White);
    whole.AddCase(whole.AddBlock(0, 2), When("miss", "0", false, true, false));
    whole.InsertText(0, 0, "x", White);
    whole.InsertText(3, 3, "y", White);
    Check(whole.Block(0).Start == 0 && whole.Block(0).End == 4, "an If over the whole text takes typing at both ends");
  }

  private static void DeletingAroundAnIfTakesIt()
  {
    OverlayTextDocument document = OverlayTextDocument.FromRuns(Tiers());
    document.Delete(3, 5);
    Check(document.BlockCount == 0 && Text(document) == "Acc%", "a selection holding an If and more takes the If");
    document = OverlayTextDocument.FromRuns(Tiers());
    document.InsertText(2, 6, "z", White);
    Check(document.BlockCount == 0 && Text(document) == "Acz", "typing over it too");
    document = OverlayTextDocument.FromRuns(Tiers());
    document.Delete(4, 5);
    document.Delete(3, 5);
    Check(document.BlockCount == 0, "an emptied look inside a deleted selection goes with it");
    document = OverlayTextDocument.FromRuns(Tiers());
    document.SetValue(4, "");
    Check(
      document.BlockCount == 1 && document.Block(0).Start == document.Block(0).End,
      "a value removed inside leaves the If"
    );
    Check(document.ToRuns()[1].If.Otherwise.Count == 0, "that then shows nothing otherwise");
  }

  private static void CasesComeAndGo()
  {
    OverlayTextDocument document = new();
    int caret = document.InsertText(0, 0, "Miss ", White);
    document.InsertValue(caret, caret, "miss", White);
    Check(document.BlockIn(0, document.Count) == -1, "a plain text has no If");
    int block = document.AddBlock(5, 6);
    int first = document.AddCase(block, When("miss", "0", false, true, false));
    Check(block == 0 && first == 0 && document.Block(0).Shown == 1, "a new case leaves otherwise showing");
    Check(document.LookCount(0, 0) == 1 && document.LookItem(0, 0, 0).Value == "miss", "and looks as the If did");
    Check(
      document.AddBlock(5, 6) == -1 && document.AddBlock(0, 6) == -1 && document.AddBlock(6, 6) == -1,
      "Ifs never overlap"
    );
    Check(document.AddBlock(0, 5) == 0 && document.Block(1).Start == 5, "but may sit side by side");
    document.RemoveCase(document.AddCase(0, null) >= 0 ? 0 : 0, 0);
    Check(document.BlockCount == 1, "an If without a case is plain text again");
    Check(
      document.BlockIn(5, 6) == 0 && document.BlockIn(6, 6) == 0 && document.BlockIn(0, 4) == -1,
      "a range or the caret finds the If it touches"
    );
    document.Show(0, 0);
    document.Delete(5, 6);
    document.InsertText(5, 5, "FC", Gold);
    int second = document.AddCase(0, When("miss", "3", false, false, true));
    Check(
      second == 1 && document.Block(0).Shown == 0 && document.LookItem(0, 1, 0).Text == "F",
      "a case added while one shows copies that one"
    );
    document.RemoveCase(0, 0);
    Check(
      Text(document) == "Miss {miss}" && document.Block(0).Cases == 1 && document.Block(0).Shown == 1,
      "removing the shown case shows otherwise"
    );
    document.RemoveCase(0, 0);
    Check(
      document.BlockCount == 0 && Text(document) == "Miss {miss}",
      "removing the last case leaves what otherwise showed"
    );
    OverlayTextDocument full = new();
    full.InsertText(0, 0, "a", White);
    full.AddBlock(0, 1);
    for (int i = 0; i < OverlayTextIf.MaximumCases; i++)
      full.AddCase(0, null);
    Check(full.AddCase(0, null) == -1, "an If holds up to eight cases");
  }

  private static void IfsSurviveSaving()
  {
    ElementSettings settings = new()
    {
      SettingsVersion = ElementSettings.CurrentSettingsVersion,
      OverlayLayouts = new()
      {
        new OverlayLayoutSettings
        {
          Id = "text:if",
          Text = new OverlayTextSettings { Runs = Tiers() },
        },
      },
    };
    XmlSerializer serializer = new(typeof(ElementSettings));
    using StringWriter writer = new();
    serializer.Serialize(writer, settings);
    using StringReader reader = new(writer.ToString());
    ElementSettings loaded = (ElementSettings)serializer.Deserialize(reader);
    SettingsMigrator.Migrate(loaded);
    Check(
      OverlayTextSettings.SameRuns(loaded.OverlayLayouts[0].Text.Runs, Tiers()),
      "an If reads back as it was saved"
    );
    Check(settings.OverlayLayouts[0].Text.Clone().SameAs(settings.OverlayLayouts[0].Text), "a copy is the same text");
    OverlayTextSettings changed = settings.OverlayLayouts[0].Text.Clone();
    changed.Runs[1].If.Cases[0].When.Bigger = false;
    Check(!changed.SameAs(settings.OverlayLayouts[0].Text), "a changed condition is a change");
  }

  private static void FontsInsideCasesCount()
  {
    List<OverlayTextRun> runs = Tiers();
    runs[1].If.Cases[0].Runs[0].Font = "Pretendard Bold";
    ElementSettings settings = new()
    {
      OverlayLayouts = new() { new OverlayLayoutSettings { Text = new OverlayTextSettings { Runs = runs } } },
    };
    List<OverlayFontUse> uses = OverlayFontUse.Find(settings);
    Check(uses.Count == 1 && uses[0].Runs.Count == 1, "a font used only inside a case is found");
  }

  private static void ValueColorsBecomeIfs()
  {
    OverlayValueColor tiers = new() { Value = "accuracy", Mode = OverlayValueColorMode.Step };
    tiers.Stops.Add(OverlayValueColorStop.Custom(95, "#FFFFFF"));
    tiers.Stops.Add(OverlayValueColorStop.HighlightColor(100));
    tiers.Stops.Add(OverlayValueColorStop.Custom(99.5f, "#5AC8FA", .5f));
    OverlayColorSettings Following()
    {
      OverlayColorSettings color = OverlayColorSettings.Custom("#00FF00");
      color.ValueColor = tiers.Clone();
      return color;
    }
    OverlayColorSettings noValue = OverlayColorSettings.Custom("#123456");
    noValue.ValueColor = new OverlayValueColor();
    ElementSettings settings = new()
    {
      SettingsVersion = 9,
      OverlayLayouts = new()
      {
        new OverlayLayoutSettings
        {
          Id = "text:acc",
          Text = new OverlayTextSettings
          {
            Runs =
            {
              OverlayTextRun.Literal("Acc ", White.Clone()),
              OverlayTextRun.ValueRun("accuracy", Following()),
              OverlayTextRun.Literal("%", Following()),
              OverlayTextRun.Literal("!", noValue),
            },
          },
        },
      },
    };
    SettingsMigrator.Migrate(settings);
    List<OverlayTextRun> runs = settings.OverlayLayouts[0].Text.Runs;
    Check(
      runs.Count == 3 && runs[1].If != null && runs[0].Text == "Acc ",
      "runs that followed a value the same way are one If"
    );
    OverlayTextIf converted = runs[1].If;
    Check(
      converted.Cases.Count == 2 && converted.Cases[0].When.Than == "100" && converted.Cases[1].When.Than == "99.5",
      "a case per stop, read from the highest"
    );
    Check(
      converted.Cases.All(item =>
        item.When.Value == "accuracy" && item.When.Same && item.When.Bigger && !item.When.Smaller
      ),
      "each holds from its stop's number up"
    );
    Check(
      converted.Cases[0].Runs.Count == 2
        && converted.Cases[0].Runs.All(run => run.Color.Highlight)
        && converted.Cases[1].Runs[0].Color.Hex == "#5AC8FA"
        && converted.Cases[1].Runs[0].Color.Alpha == .5f
        && converted.Otherwise[0].Color.Hex == "#FFFFFF"
        && converted.Otherwise[0].IsValue,
      "the looks are the runs in each stop's color"
    );
    Check(
      runs[2].Text == "!" && runs[2].Color.Hex == "#123456" && runs[2].Color.ValueColor == null,
      "a color that followed no value yet is its own again"
    );
    List<OverlayTextRun> all = new();
    OverlayTextSettings.Collect(runs, all);
    Check(all.All(run => run.Color.ValueColor == null), "nothing follows a value the old way any more");
    Check(settings.SettingsVersion == ElementSettings.CurrentSettingsVersion, "the settings are current");
    OverlayTextLooks looks = new();
    looks.Load(runs);
    looks.Update(new Numbers { [OverlayValueId.Accuracy] = 99.7 });
    Check(looks.Shown[1].Color.Hex == "#5AC8FA", "and it colors as the stops did: 99.7 is past 99.5");
  }
}
