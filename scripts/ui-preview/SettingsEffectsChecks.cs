using System.Linq;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Presentation.Settings.Pages;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Input;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;

namespace Element.Editor
{
  /// <summary>Effects and the preset editor on ElementUI: preset cards, applying, editing a preset, its zoom and its shortcut.</summary>
  internal static class SettingsEffectsChecks
  {
    [PageCheck("_effects")]
    static int CheckEffects(SettingsCheck check)
    {
      using (PageUi.Errors(check))
        return CheckEffectsPage(check) + 1;
    }

    private static int CheckEffectsPage(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      EffectsModel effects = check.Model.Effects;
      PresetEditorModel editor = check.Model.PresetEditor;
      ElementSettings settings = check.Settings;
      GameObject root = check.Root;
      PageUi.Settle(check);
      int original = settings.Effects.Presets.Count;
      Assert(effects.Cards.Length == original && settings.Effects.Presets.All(p => PageUi.CardOf(root, p.Name) != null),
        "Effects shows a card per preset: Remove All, No Filters, Only Tiles");
      Assert(PageUi.Shows(root, UiText("끄면 맵의 원래 이펙트가 표시됩니다.", "Off · original map effects are shown.")),
        "with removal off the switch says the map's own effects show");

      // A new preset opens a draft with nothing to delete.
      PageUi.Press(PageUi.ButtonWith(root, UiText("프리셋 추가", "New preset")));
      PageUi.Settle(check);
      Assert(check.Model.Current == editor && editor.IsNew && !editor.CanSave.Value && !PageUi.IconButtons(root, UiIconKind.Delete).Any()
        && editor.Title == UiText("새 프리셋", "New preset"), "new preset opens a dedicated draft with no destructive action");
      TMP_InputField name = NameField(root);
      name.text = " " + settings.Effects.Presets[0].Name.ToLowerInvariant() + " ";
      PageUi.Settle(check);
      Assert(!editor.CanSave.Value && editor.NameProblem.Value.Length > 0 && PageUi.Shows(root, editor.NameProblem.Value)
        && settings.Effects.Presets.Count == original, "preset names are unique after whitespace and case normalization");
      name.text = "Fixture calm";
      PageUi.Settle(check);
      Assert(PageUi.Shows(root, UiText("이 프리셋에서 제거할 이펙트를 선택하세요.", "Choose the effects this preset removes.")),
        "a usable name shows the intro again");
      PageUi.Press(PageUi.SwitchBeside(root, editor.Camera.Title));
      Assert(editor.CanSave.Value && editor.DraftEffects != RemovedEffects.None, "draft effect choices are editable before a preset exists");
      check.Model.Back.TryExecute();
      PageUi.Settle(check);
      Assert(check.Model.Current == effects && settings.Effects.Presets.Count == original,
        "leaving an unsaved preset draft does not mutate the gallery");

      PageUi.Press(PageUi.ButtonWith(root, UiText("프리셋 추가", "New preset")));
      PageUi.Settle(check);
      Assert(editor.Name == "" && !editor.Camera.Removed && editor.Tab == (int)EffectGroup.Screen, "a new draft starts empty on Screen");
      name = NameField(root);
      name.text = "Fixture calm";
      PageUi.Press(PageUi.SwitchBeside(root, editor.Camera.Title));
      PageUi.Settle(check);
      RemovedEffects expected = editor.DraftEffects;
      TMP_InputField zoom = root.GetComponentsInChildren<TMP_InputField>(false).Single(field => field != name);
      Assert((expected & RemovedEffects.CameraMotion) != 0 && zoom.interactable && zoom.text == "250",
        "removing camera movement offers its zoom, starting at 250%");
      Rect camera = PageUi.World(PageUi.Label(root, editor.Camera.Title));
      Rect zoomRect = PageUi.World(zoom);
      Rect zoomLabel = PageUi.World(PageUi.Label(root, UiText("카메라 확대", "Camera zoom")));
      Assert(zoomLabel.yMax <= camera.yMin && zoomRect.yMax <= zoomLabel.yMin, "the zoom sits right under camera movement");
      foreach (EffectChoiceModel choice in editor.Groups[(int)EffectGroup.Screen])
        Assert(PageUi.World(PageUi.Label(root, choice.Title)).yMax <= zoomRect.yMin,
          "the other Screen effects start below the zoom instead of covering it: " + choice.Title);
      zoom.onEndEdit.Invoke("1200");
      PageUi.Settle(check);
      Assert(Mathf.Approximately(editor.Zoom, 1000) && zoom.text == "1000", "a zoom above the range becomes the maximum");
      zoom.onEndEdit.Invoke("50");
      PageUi.Settle(check);
      Assert(Mathf.Approximately(editor.Zoom, 100) && zoom.text == "100", "a zoom below the range becomes the minimum");
      zoom.text = "300";
      zoom.onEndEdit.Invoke("300");
      PageUi.Settle(check);
      Assert(Mathf.Approximately(editor.Zoom, 300), "a typed zoom applies");
      PageUi.Press(PageUi.ButtonWith(root, UiText("저장", "Save preset")));
      PageUi.Settle(check);
      EffectPreset created = settings.Effects.Presets.Single(preset => preset.Name == "Fixture calm");
      Assert(created.CameraZoom == 300, "saving keeps the preset's zoom");
      Assert(settings.Effects.Presets.Count == original + 1 && created.Effects == expected && check.Model.Current == effects
        && PageUi.CardOf(root, "Fixture calm") != null, "saving creates the named preset and returns to its gallery");

      PageUi.Press(PageUi.CardOf(root, "Fixture calm"));
      PageUi.Settle(check);
      Assert(settings.Effects.Enabled && settings.Effects.ActivePresetId == created.Id && PageUi.Shows(root, UiText("적용 중", "Active"))
        && PageUi.Shows(root, UiText("적용 프리셋: ", "Active preset: ") + "Fixture calm"), "selecting a preset applies it and enables removal");
      // Turning removal off and on keeps every card where it was.
      Rect[] cards = settings.Effects.Presets.Select(p => PageUi.World(PageUi.CardOf(root, p.Name))).ToArray();
      Pressable removal = PageUi.SwitchBeside(root, UiText("이펙트 제거", "Effect removal"));
      PageUi.Press(removal);
      PageUi.Settle(check);
      Assert(!settings.Effects.Enabled && settings.Effects.ActivePresetId == created.Id && PageUi.Shows(root, UiText("선택됨 · 꺼짐", "Selected · Off")),
        "turning removal off preserves the selected preset");
      CheckCardRects(check, cards, "off", ref count);
      PageUi.Press(removal);
      PageUi.Settle(check);
      Assert(settings.Effects.Enabled && settings.Effects.ActivePresetId == created.Id, "turning removal back on preserves the active preset");
      CheckCardRects(check, cards, "on", ref count);
      PageUi.Press(removal);
      PageUi.Settle(check);
      CheckCardRects(check, cards, "off again", ref count);

      // A preset applied by its shortcut, outside the window, shows when the window probes.
      EffectPreset other = settings.Effects.Presets[2];
      settings.Effects.Toggle(other.Id);
      check.Panel.RefreshPresets();
      check.Settle();
      Assert(effects.Cards[2].Selected.Value && effects.Cards[2].State.Value == UiText("적용 중", "Active")
        && PageUi.Shows(root, UiText("적용 프리셋: ", "Active preset: ") + other.Name), "a preset applied from outside the window shows at once");
      settings.Effects.Toggle(other.Id);
      settings.Effects.Select(created.Id);
      check.Panel.RefreshPresets();
      check.Settle();

      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, "Fixture calm"), UiIconKind.Edit));
      PageUi.Settle(check);
      Assert(check.Model.Current == editor && !editor.IsNew && editor.Title == UiText("프리셋 편집", "Edit preset")
        && PageUi.IconButtons(root, UiIconKind.Delete).Count() == 1, "the pencil edits the preset, with Delete");
      name = NameField(root);
      Assert(name.text == "Fixture calm", "the draft holds the preset's name");
      name.text = settings.Effects.Presets[0].Name;
      PageUi.Settle(check);
      Assert(!editor.CanSave.Value && created.Name == "Fixture calm", "duplicate rename cannot mutate the existing preset");
      name.text = "Fixture renamed";
      PageUi.Settle(check);
      PageUi.Press(PageUi.ButtonWith(root, UiText("저장", "Save preset")));
      PageUi.Settle(check);
      Assert(created.Name == "Fixture renamed" && settings.Effects.ActivePresetId == created.Id && PageUi.CardOf(root, "Fixture renamed") != null,
        "renaming keeps stable identity and selected state");
      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, "Fixture renamed"), UiIconKind.Edit));
      PageUi.Settle(check);
      PageUi.Press(PageUi.IconButtons(root, UiIconKind.Delete).Single());
      PageUi.Settle(check);
      Assert(settings.Effects.Presets.Count == original && settings.Effects.ActivePreset != null && !settings.Effects.Enabled
        && check.Model.Current == effects && effects.Cards.Length == original,
        "deleting the selected preset keeps a valid fallback without enabling removal");

      // Three columns on desktop, fewer on narrow windows, never overlapping.
      Rect[] placed = settings.Effects.Presets.Select(p => PageUi.World(PageUi.CardOf(root, p.Name))).ToArray();
      for (int i = 0; i < placed.Length; i++)
      {
        Assert(PageUi.Inside(check, placed[i]), "preset card fits the page at " + check.Width);
        for (int j = i + 1; j < placed.Length; j++)
          Assert(!placed[i].Overlaps(placed[j]), "preset cards do not overlap at " + check.Width);
      }
      // Three columns from 600 wide, as the old page: three of the gallery's narrowest (192) beside each other.
      Rect window = PageUi.World((RectTransform)root.transform.Find("Settings window"));
      if (window.width - 64 >= 3 * 192 + 2 * 12)
        Assert(Mathf.Abs(placed[0].yMin - placed[2].yMin) < .5f, "the preset gallery uses three columns on desktop");

      count += CheckPresetShortcut(check);
      PageUi.Settle(check);
      count += PageUi.CheckFits(check, "Effects");
      return count;
    }

    private static void CheckCardRects(SettingsCheck check, Rect[] expected, string state, ref int count)
    {
      Rect[] now = check.Settings.Effects.Presets.Select(p => PageUi.World(PageUi.CardOf(check.Root, p.Name))).ToArray();
      check.Assert(now.Length == expected.Length && now.SequenceEqual(expected), "effect toggle keeps every preset card in place: " + state);
      check.Assert(now.Distinct().Count() == now.Length, "effect toggle cannot pile preset cards onto one origin: " + state);
      count += 2;
    }

    // A preset's shortcut: recorded in the draft, refused when taken, saved with the preset, shown on its card.
    private static int CheckPresetShortcut(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      PresetEditorModel editor = check.Model.PresetEditor;
      ElementSettings settings = check.Settings;
      GameObject root = check.Root;
      EffectPreset other = settings.Effects.Presets[0], target = settings.Effects.Presets[1];
      other.ShortcutKey = KeyCode.Alpha1;
      other.ShortcutModifier = KeyCode.LeftAlt;
      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, target.Name), UiIconKind.Edit));
      PageUi.Settle(check);
      Assert(editor.TabNames.Length == 6 && editor.TabNames.All(tab => TabButton(root, tab) != null),
        "the preset editor adds a Shortcut tab after the five effect groups");
      PageUi.Press(TabButton(root, editor.TabNames[PresetEditorModel.ShortcutTab]));
      PageUi.Settle(check);
      Assert(PageUi.Switches(root).Length == 0
        && PageUi.Shows(root, UiText(
          "누르면 이 프리셋을 적용하고, 한 번 더 누르면 이펙트 제거를 끕니다. 플레이 중에는 동작하지 않습니다.",
          "Press it to apply this preset, and again to turn effect removal off. It does nothing during play.")),
        "the Shortcut tab swaps the effect switches for the shortcut card");
      ShortcutFieldModel field = editor.Shortcut;
      Pressable remove = PageUi.IconButtons(root, UiIconKind.Close).Single();
      Assert(!field.ShowsKeys.Value && PageUi.Shows(root, UiText("눌러서 단축키 지정", "Click to set a shortcut")) && !remove.IsInteractable(),
        "a preset without a shortcut invites setting one and offers nothing to remove");
      PageUi.Press(PageUi.ButtonWith(root, UiText("눌러서 단축키 지정", "Click to set a shortcut")));
      Assert(field.Recording && editor.TickCapture(), "clicking the field records");
      Assert(!field.TryAssign(KeyCode.Alpha1, KeyCode.LeftAlt) && field.Recording && field.Hint.Contains(other.Name),
        "a shortcut another preset uses names that preset and keeps recording");
      Assert(!field.TryAssign(settings.SettingsShortcutKey, settings.SettingsShortcutModifier) && field.Recording,
        "the settings shortcut cannot apply a preset");
      Assert(field.TryAssign(KeyCode.Alpha2, KeyCode.LeftAlt) && !field.Recording, "a free shortcut ends recording");
      PageUi.Settle(check);
      Assert(field.ShowsKeys.Value && PageUi.Shows(root, "2") && remove.IsInteractable(), "a free shortcut shows as keycaps with remove available");
      Assert(!target.HasShortcut, "the shortcut waits in the draft until the preset is saved");
      Rect capture = PageUi.World(PageUi.CaptureButton(root, PageUi.Label(root, "2")));
      Rect description = PageUi.World(PageUi.Label(root, UiText(
        "누르면 이 프리셋을 적용하고, 한 번 더 누르면 이펙트 제거를 끕니다. 플레이 중에는 동작하지 않습니다.",
        "Press it to apply this preset, and again to turn effect removal off. It does nothing during play.")));
      Assert(PageUi.Inside(check, capture) && PageUi.Inside(check, PageUi.World(remove))
        && capture.xMin >= description.xMin - .5f && PageUi.World(remove).xMax <= description.xMin + description.width + 16.5f,
        "the shortcut field and its button fit the card at " + check.Width);
      PageUi.Press(PageUi.ButtonWith(root, UiText("저장", "Save preset")));
      PageUi.Settle(check);
      Assert(target.UsesShortcut(KeyCode.Alpha2, KeyCode.LeftAlt) && check.Model.Current == check.Model.Effects,
        "saving keeps the shortcut on the preset");
      TMP_Text label = PageUi.Label(root, ShortcutKeys.TextLabel(KeyCode.Alpha2, KeyCode.LeftAlt));
      Assert(label != null && !label.isTextTruncated && PageUi.World(PageUi.CardOf(root, target.Name)).Contains(PageUi.World(label).center),
        "the preset card shows its shortcut in full");
      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, target.Name), UiIconKind.Edit));
      PageUi.Settle(check);
      Assert(editor.Tab == (int)EffectGroup.Screen, "editing a preset again starts on its effects");
      PageUi.Press(TabButton(root, editor.TabNames[PresetEditorModel.ShortcutTab]));
      PageUi.Settle(check);
      PageUi.Press(PageUi.IconButtons(root, UiIconKind.Close).Single());
      PageUi.Settle(check);
      Assert(!field.HasShortcut.Value && !PageUi.IconButtons(root, UiIconKind.Close).Single().IsInteractable() && target.HasShortcut,
        "removing clears the draft's shortcut only");
      check.Model.Back.TryExecute();
      PageUi.Settle(check);
      Assert(target.HasShortcut, "leaving without saving keeps the saved shortcut");
      target.ClearShortcut();
      check.Model.Effects.Refresh();
      return count;
    }

    [PageCheck("_presetEditor")]
    static int CheckPresetEditor(SettingsCheck check)
    {
      using (PageUi.Errors(check))
        return CheckPresetEditorPage(check) + 1;
    }

    private static int CheckPresetEditorPage(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      PresetEditorModel editor = check.Model.PresetEditor;
      ElementSettings settings = check.Settings;
      GameObject root = check.Root;
      editor.Edit(settings.Effects.Presets[0]);
      PageUi.Settle(check);
      Assert(check.Model.Current == editor && editor.Title == UiText("프리셋 편집", "Edit preset") && PageUi.Shows(root, editor.Title),
        "the editor is titled Edit preset");
      Assert(NameField(root).text == settings.Effects.Presets[0].Name && editor.CanSave.Value,
        "the editor opens a draft of the preset, ready to save");
      Assert(PageUi.IconButtons(root, UiIconKind.Delete).Count() == 1, "a saved preset can be deleted: " + PageUi.IconButtons(root, UiIconKind.Delete).Count());
      Assert(PageUi.Switches(root).Length == EffectCatalog.Entries.Count(entry => entry.Group == EffectGroup.Screen),
        "the Screen tab shows its effects");
      Rect[] tabs = editor.TabNames.Select(tab => PageUi.World(TabButton(root, tab))).ToArray();
      bool wide = PageUi.World((RectTransform)root.transform.Find("Settings window")).width - 64 >= 600;
      Assert(wide ? tabs.All(tab => Mathf.Abs(tab.yMin - tabs[0].yMin) < .5f)
          : Mathf.Abs(tabs[2].yMin - tabs[0].yMin) < .5f && tabs[3].yMax <= tabs[0].yMin + .5f,
        "the tabs sit in one row, or in two rows of three on narrow windows");
      Rect first = PageUi.World(PageUi.Label(root, editor.Groups[(int)EffectGroup.Screen][0].Title));
      Rect second = PageUi.World(PageUi.Label(root, editor.Groups[(int)EffectGroup.Screen][1].Title));
      Assert(wide ? Mathf.Abs(first.yMax - second.yMax) < .5f && second.xMin > first.xMax : second.yMax < first.yMin,
        "effects sit in two columns, or one on narrow windows");
      PageUi.Press(TabButton(root, editor.TabNames[(int)EffectGroup.Track]));
      PageUi.Settle(check);
      Assert(editor.Tab == (int)EffectGroup.Track && PageUi.Switches(root).Length == EffectCatalog.Entries.Count(entry => entry.Group == EffectGroup.Track),
        "a tab shows its group's effects");
      Assert(editor.Choices.Where(choice => choice.Definition.Group == EffectGroup.Track)
          .All(choice => choice.Removed == ((settings.Effects.Presets[0].Effects & choice.Definition.Flag) != 0)),
        "the switches show what the preset removes");
      PageUi.Press(TabButton(root, editor.TabNames[(int)EffectGroup.Screen]));
      if (check.Name.StartsWith("preset-shortcut"))
        count += ShowPresetShortcut(check, check.Name.EndsWith("narrow"));
      PageUi.Settle(check);
      count += PageUi.CheckFits(check, "Preset editor");
      return count;
    }

    // Leaves the editor on its Shortcut tab for the frame: a set shortcut, or
    // on narrow windows a recording that hit another preset's.
    private static int ShowPresetShortcut(SettingsCheck check, bool conflict)
    {
      PresetEditorModel editor = check.Model.PresetEditor;
      editor.ShortcutKey = KeyCode.Alpha1;
      editor.ShortcutModifier = KeyCode.LeftAlt;
      PageUi.Press(TabButton(check.Root, editor.TabNames[PresetEditorModel.ShortcutTab]));
      PageUi.Settle(check);
      check.Assert(editor.Shortcut.ShowsKeys.Value && PageUi.Shows(check.Root, "1"), "the frame shows the draft's shortcut as keycaps");
      if (!conflict)
        return 1;
      check.Settings.Effects.Presets[1].ShortcutKey = KeyCode.Alpha2;
      check.Settings.Effects.Presets[1].ShortcutModifier = KeyCode.LeftAlt;
      PageUi.Press(PageUi.CaptureButton(check.Root, PageUi.Label(check.Root, "1")));
      check.Assert(!editor.Shortcut.TryAssign(KeyCode.Alpha2, KeyCode.LeftAlt) && editor.Shortcut.Recording,
        "the frame shows a conflicting shortcut");
      PageUi.Settle(check);
      check.Assert(PageUi.Shows(check.Root, editor.Shortcut.Hint), "the conflict is said under the field");
      return 3;
    }

    private static TMP_InputField NameField(GameObject root) => PageUi.FieldWithPlaceholder(root, UiText("프리셋 이름", "Preset name"));

    private static Pressable TabButton(GameObject root, string title) =>
      PageUi.Texts(root).Where(text => text.text == title).Select(text => text.GetComponentInParent<Pressable>()).FirstOrDefault(button => button != null);

    private static string UiText(string ko, string en) => Element.Presentation.Ui.Theme.UiLocale.L(ko, en);
  }
}
