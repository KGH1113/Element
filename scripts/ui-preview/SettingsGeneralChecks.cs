using System;
using System.Collections.Generic;
using System.Linq;
using Element.Application.Fonts;
using Element.Application.Profiles;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Configuration.Overlay;
using Element.Configuration.Profiles;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Settings.Pages;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Popovers;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Element.Editor
{
  /// <summary>General and Profiles on ElementUI: the settings shortcut, the switches, the profile cards, a new profile and an import's missing fonts.</summary>
  internal static class SettingsGeneralChecks
  {
    [PageCheck("_general")]
    static int CheckGeneral(SettingsCheck check)
    {
      using (PageUi.Errors(check))
        return CheckGeneralPage(check) + 1;
    }

    private static int CheckGeneralPage(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      GeneralModel general = check.Model.General;
      ElementSettings settings = check.Settings;
      GameObject root = check.Root;
      PageUi.Settle(check);

      // Profiles first, then the shortcut, then motion, ElementNote and updates.
      TMP_Text link = PageUi.Label(root, UiText("프로필", "Profiles"));
      TMP_Text shortcutTitle = PageUi.Label(root, UiText("설정 단축키", "Settings shortcut"));
      Assert(link != null && shortcutTitle != null && PageUi.World(link).yMin > PageUi.World(shortcutTitle).yMax,
        "General starts with the profile link, above the shortcut card");
      Assert(PageUi.Shows(root, UiText("사용 중: ", "In use: ") + check.Features.Profiles.Active.Name),
        "the profile link names the profile in use");
      Assert(PageUi.Switches(root).Length == 3
        && PageUi.Shows(root, UiText("움직임 줄이기", "Reduce motion"))
        && PageUi.Shows(root, UiText("ElementNote 자동 열기/닫기", "Auto open/close ElementNote"))
        && PageUi.Shows(root, UiText("자동 업데이트", "Automatic updates")),
        "General has the shortcut, motion, ElementNote auto open and automatic update switches");
      Assert(PageUi.Shows(root, Element.Presentation.Notices.UpdateStatusText.For(check.Features.Updates)),
        "the update switch says what happened to updates");

      // The switches write the settings they show.
      bool motion = settings.ReduceUiMotion;
      PageUi.Press(PageUi.SwitchBeside(root, UiText("움직임 줄이기", "Reduce motion")));
      PageUi.Settle(check);
      Assert(settings.ReduceUiMotion == !motion, "the motion switch writes Reduce motion");
      PageUi.Press(PageUi.SwitchBeside(root, UiText("움직임 줄이기", "Reduce motion")));
      PageUi.Settle(check);
      Assert(settings.ReduceUiMotion == motion, "the motion switch turns it back");
      bool note = settings.AutoOpenCloseDmNote;
      PageUi.Press(PageUi.SwitchBeside(root, UiText("ElementNote 자동 열기/닫기", "Auto open/close ElementNote")));
      PageUi.Settle(check);
      Assert(settings.AutoOpenCloseDmNote == !note, "the ElementNote switch writes its setting");
      PageUi.Press(PageUi.SwitchBeside(root, UiText("ElementNote 자동 열기/닫기", "Auto open/close ElementNote")));
      PageUi.Settle(check);
      Assert(settings.AutoOpenCloseDmNote == note, "the ElementNote switch turns it back");
      PageUi.Press(PageUi.SwitchBeside(root, UiText("자동 업데이트", "Automatic updates")));
      PageUi.Settle(check);
      Assert(!check.Features.Updates.AutomaticUpdates
        && PageUi.Shows(root, Element.Presentation.Notices.UpdateStatusText.For(check.Features.Updates)),
        "turning updates off says what to do instead");
      PageUi.Press(PageUi.SwitchBeside(root, UiText("자동 업데이트", "Automatic updates")));
      PageUi.Settle(check);
      Assert(check.Features.Updates.AutomaticUpdates, "the update switch turns updates back on");

      // The shortcut as keycaps; every key reads on one line inside its cap.
      ShortcutFieldModel field = general.Shortcut;
      foreach (KeyCode key in new[] { KeyCode.BackQuote, KeyCode.Slash, KeyCode.LeftBracket, KeyCode.Backslash, KeyCode.Backspace, KeyCode.PageDown })
      {
        settings.SettingsShortcutKey = key;
        PageUi.Settle(check);
        TMP_Text cap = PageUi.Label(root, ShortcutKeys.KeyLabel(key));
        Assert(cap != null && cap.textInfo.lineCount == 1 && !cap.isTextTruncated, "the shortcut key reads on one line: " + key);
        Assert(PageUi.World(PageUi.CaptureButton(root, cap)).Contains(PageUi.World(cap).center),
          "the keycap sits inside the shortcut field: " + key);
      }
      Assert(ShortcutKeys.KeyLabel(KeyCode.BackQuote) == "`" && ShortcutKeys.KeyLabel(KeyCode.Backslash) == "\\"
        && ShortcutKeys.KeyLabel(KeyCode.Slash) == "/" && ShortcutKeys.KeyLabel(KeyCode.Alpha1) == "1",
        "printable keys use their actual characters instead of enum names");
      foreach (KeyCode modifier in new[] { KeyCode.LeftCommand, KeyCode.RightAlt, KeyCode.LeftControl, KeyCode.RightShift })
      {
        settings.SettingsShortcutModifier = modifier;
        PageUi.Settle(check);
        string side = modifier == KeyCode.RightAlt || modifier == KeyCode.RightShift ? "R" : "L";
        if (ShortcutKeys.IsMac)
        {
          UiKeySymbol symbol = root.GetComponentsInChildren<UiKeySymbol>(false).SingleOrDefault();
          Assert(symbol != null && !symbol.raycastTarget && PageUi.Shows(root, side), "the modifier draws as its symbol with its side: " + modifier);
          using (var mesh = new UnityEngine.UI.VertexHelper())
          {
            symbol.GetType().GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
              .Invoke(symbol, new object[] { mesh });
            Assert(mesh.currentVertCount > 0, "the modifier symbol renders without a font glyph: " + modifier);
          }
        }
        else
          Assert(PageUi.Shows(root, ShortcutKeys.ModifierLabel(modifier)), "the modifier is spelled out: " + modifier);
      }
      settings.SettingsShortcutKey = ElementSettings.DefaultSettingsShortcutKey;
      settings.SettingsShortcutModifier = ElementSettings.DefaultSettingsShortcutModifier;
      PageUi.Settle(check);

      // The reset button shares the field's line, or goes under it when both don't fit.
      Pressable capture = PageUi.CaptureButton(root, PageUi.Label(root, ShortcutKeys.KeyLabel(ElementSettings.DefaultSettingsShortcutKey)));
      Pressable reset = PageUi.IconButtons(root, UiIconKind.Reset).Single();
      Rect captureRect = PageUi.World(capture), resetRect = PageUi.World(reset);
      Assert(Mathf.Abs(PageUi.World((RectTransform)capture.transform).height - PageUi.World((RectTransform)reset.transform).height) < .5f,
        "the field and its button share one height");
      Assert(Mathf.Abs(resetRect.yMax - captureRect.yMax) < .5f || resetRect.yMax <= captureRect.yMin + .5f,
        "Reset shares the field's line or sits under it on narrow windows");
      Assert(PageUi.Inside(check, resetRect) && PageUi.Inside(check, captureRect), "the shortcut field and Reset stay inside the window");

      // Recording: a preset's shortcut is refused and named; a free one saves.
      EffectPreset owner = settings.Effects.Presets[0];
      owner.ShortcutKey = KeyCode.F6;
      PageUi.Press(capture);
      PageUi.Settle(check);
      Assert(field.Recording && general.TickCapture() && PageUi.Shows(root, UiText("새 단축키를 입력하세요…", "Press a shortcut…")),
        "clicking the field records and takes the keyboard");
      Assert(!field.TryAssign(KeyCode.F6, KeyCode.None) && field.Recording && field.Hint.Contains(owner.Name)
        && settings.SettingsShortcutKey == ElementSettings.DefaultSettingsShortcutKey,
        "the settings shortcut cannot take a preset's shortcut and names that preset");
      PageUi.Settle(check);
      Assert(PageUi.Shows(root, field.Hint), "the field says why under it");
      Assert(field.TryAssign(KeyCode.F7, KeyCode.LeftControl) && !field.Recording && field.Hint.Length == 0
        && settings.SettingsShortcutKey == KeyCode.F7 && settings.SettingsShortcutModifier == KeyCode.LeftControl && !general.TickCapture(),
        "a free settings shortcut saves and ends recording");
      owner.ClearShortcut();
      PageUi.Press(capture);
      general.CancelCapture();
      Assert(!field.Recording && settings.SettingsShortcutKey == KeyCode.F7, "cancelling keeps the shortcut");
      PageUi.Press(reset);
      PageUi.Settle(check);
      Assert(settings.SettingsShortcutKey == ElementSettings.DefaultSettingsShortcutKey
        && settings.SettingsShortcutModifier == ElementSettings.DefaultSettingsShortcutModifier
        && PageUi.Shows(root, ShortcutKeys.KeyLabel(ElementSettings.DefaultSettingsShortcutKey)),
        "Reset restores the default and shows it");

      // The link opens Profiles; Back returns.
      PageUi.Press(link.GetComponentInParent<Pressable>());
      PageUi.Settle(check);
      Assert(check.Model.Current == check.Model.Profiles && PageUi.Shows(root, check.Model.Profiles.Count), "the profile link opens Profiles");
      check.Model.Back.TryExecute();
      PageUi.Settle(check);
      Assert(check.Model.Current == general, "Back returns to General");
      PageUi.Settle(check);
      count += PageUi.CheckFits(check, "General");
      return count;
    }

    [PageCheck("_profiles")]
    static int CheckProfiles(SettingsCheck check)
    {
      using (PageUi.Errors(check))
        return CheckProfilesPage(check) + 1;
    }

    private static int CheckProfilesPage(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      ProfilesModel page = check.Model.Profiles;
      IProfiles profiles = check.Features.Profiles;
      ElementSettings settings = check.Settings;
      GameObject root = check.Root;
      PageUi.Settle(check);

      Assert(page.Cards.Length == profiles.Profiles.Count && profiles.Profiles.All(p => PageUi.CardOf(root, p.Name) != null),
        "one card per profile");
      Rect[] rects = profiles.Profiles.Select(p => PageUi.World(PageUi.CardOf(root, p.Name))).ToArray();
      for (int i = 0; i < rects.Length; i++)
      {
        Assert(PageUi.Inside(check, rects[i]), "profile card fits the page: " + check.Width);
        for (int j = i + 1; j < rects.Length; j++)
          Assert(!rects[i].Overlaps(rects[j]), "profile cards do not overlap: " + check.Width);
      }
      Rect import = PageUi.World(PageUi.ButtonWith(root, UiText("가져오기", "Import")));
      Rect create = PageUi.World(PageUi.ButtonWith(root, UiText("새 프로필", "New profile")));
      Assert(import.xMax <= create.xMin + .5f && Mathf.Abs(import.yMin - create.yMin) < .5f, "Import sits before New profile");
      Assert(PageUi.Shows(root, page.Count) && !PageUi.Label(root, page.Count).isTextTruncated, "the heading counts the profiles");
      Assert(!PageUi.Texts(root).Any(t => t.text.Contains("Each profile keeps") || t.text.Contains("프로필마다")),
        "the page doesn't explain profiles");

      // Import whose window was closed: nothing happens and nothing is said.
      page.Import.TryExecute();
      Assert(page.Message == "", "a closed file window leaves no message");

      // Switching from a card: the card shows it; nothing above the cards moves.
      ElementProfile first = profiles.Profiles[0];
      var planets = settings.Gameplay.Planets;
      Rect before = PageUi.World(PageUi.CardOf(root, first.Name));
      PageUi.Press(PageUi.CardOf(root, first.Name));
      PageUi.Settle(check);
      Assert(profiles.Active == first && settings.Gameplay.Planets == planets, "a card switches profiles in place");
      Assert(page.Message == "" && !page.HasMessage.Value, "a switch shows on the card, not in a message");
      Assert(PageUi.World(PageUi.CardOf(root, first.Name)) == before, "switching doesn't move the cards");
      Assert(PageUi.Shows(root, UiText("사용 중", "In use")) && page.Cards[0].InUse, "the card says it is in use");
      Assert(page.InUse.Contains(first.Name), "General names the new profile in use");

      // Every card renames, duplicates, exports and deletes with one press.
      foreach (ElementProfile profile in profiles.Profiles)
      {
        Pressable card = PageUi.CardOf(root, profile.Name);
        foreach (UiIconKind icon in new[] { UiIconKind.Edit, UiIconKind.Duplicate, UiIconKind.Export, UiIconKind.Delete })
          Assert(PageUi.ActionOf(root, card, icon) != null, "every card has rename, duplicate, export and delete: " + icon);
        Rect title = PageUi.World(PageUi.Label(root, profile.Name));
        Rect rename = PageUi.World(PageUi.ActionOf(root, card, UiIconKind.Edit));
        Assert(title.xMax <= rename.xMin + .5f, "a card's name stops before its buttons: " + check.Width);
      }

      // Rename on the card: the card says why a taken name won't do, and the old name stays.
      ElementProfile second = profiles.Profiles[1];
      string original = second.Name;
      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, original), UiIconKind.Edit));
      PageUi.Settle(check);
      TMP_InputField field = root.GetComponentsInChildren<TMP_InputField>(false).SingleOrDefault();
      Assert(field != null && field.text == original && page.RenamingId == second.Id, "the pencil turns the card's name into a field holding it");
      Assert(!field.onFocusSelectAll, "the caret goes after the name instead of selecting it");
      field.text = profiles.Profiles[0].Name.ToUpperInvariant();
      PageUi.Settle(check);
      Assert(page.RenameProblem.Value.Length > 0 && PageUi.Shows(root, page.RenameProblem.Value) && page.Message == "",
        "a taken name is refused as it is typed, on the card");
      bool escaped = page.HandleEscape();
      PageUi.Settle(check);
      Assert(escaped && page.RenamingId == null && second.Name == original && PageUi.CardOf(root, original) != null,
        "Escape ends the rename first and keeps the name");
      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, original), UiIconKind.Edit));
      PageUi.Settle(check);
      field = root.GetComponentsInChildren<TMP_InputField>(false).Single();
      field.text = profiles.Profiles[0].Name.ToUpperInvariant();
      field.onEndEdit.Invoke(field.text);
      PageUi.Settle(check);
      Assert(second.Name == original && page.RenamingId == null && page.Message.Length > 0 && page.MessageIsProblem,
        "a taken name keeps the old name and says why");
      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, original), UiIconKind.Edit));
      PageUi.Settle(check);
      field = root.GetComponentsInChildren<TMP_InputField>(false).Single();
      field.text = original + " 2";
      field.onEndEdit.Invoke(field.text);
      PageUi.Settle(check);
      Assert(second.Name == original + " 2", "a new name saves when the field is left");
      Assert(PageUi.CardOf(root, second.Name) != null, "the card shows the new name");

      // Duplicate puts a copy next to the card and keeps the profile in use.
      ElementProfile active = profiles.Active;
      int total = profiles.Profiles.Count;
      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, second.Name), UiIconKind.Duplicate));
      PageUi.Settle(check);
      ElementProfile copy = profiles.Profiles[2];
      Assert(profiles.Profiles.Count == total + 1 && profiles.Active == active && copy.Name.StartsWith(second.Name)
        && page.Message.Contains(copy.Name), "duplicate adds a copy after the card");
      TMP_Text message = PageUi.Label(root, page.Message);
      Assert(message != null && PageUi.World(message).yMax <= PageUi.Cards(root, profiles).Min(card => card.yMin) + .5f,
        "messages sit below the cards");

      // Export opens the save window at once; a closed window says nothing new.
      string said = page.Message;
      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, second.Name), UiIconKind.Export));
      Assert(page.Message == said, "export opens the save window from the card; a closed one says nothing");

      // Delete asks on the card first, then removes the profile.
      total = profiles.Profiles.Count;
      Pressable trash = PageUi.ActionOf(root, PageUi.CardOf(root, copy.Name), UiIconKind.Delete);
      PageUi.Press(trash);
      PageUi.Settle(check);
      Assert(profiles.Profiles.Count == total && PageUi.Shows(root, UiText("한 번 더 누르면 삭제해요", "Press again to delete")),
        "the first delete press only asks, on the card");
      PageUi.Press(PageUi.ActionOf(root, PageUi.CardOf(root, copy.Name), UiIconKind.Delete));
      PageUi.Settle(check);
      Assert(profiles.Profiles.Count == total - 1 && !profiles.Profiles.Contains(copy) && page.Message.Contains(copy.Name),
        "the second press deletes it");
      message = PageUi.Label(root, page.Message);
      Assert(message != null && !message.isTextTruncated && PageUi.Inside(check, PageUi.World(message)), "the message fits at " + check.Width);

      // Each of these frames shows one thing left open.
      if (!check.Name.EndsWith("-fonts"))
        count += CheckNewProfile(check, check.Name.EndsWith("-new"));
      if (!check.Name.EndsWith("-new"))
        count += CheckFontReplacement(check, check.Name.EndsWith("-fonts"));
      // One frame shows a name being changed on its card.
      if (check.Name.EndsWith("-rename"))
      {
        page.Cards[0].Rename.TryExecute();
        PageUi.Settle(check);
        Assert(root.GetComponentsInChildren<TMP_InputField>(false).Single().text == profiles.Profiles[0].Name, "the rename frame shows the name field");
      }
      PageUi.Settle(check);
      count += PageUi.CheckFits(check, "Profiles");
      return count;
    }

    // New profile opens a small panel under its button; the profile starts from the initial settings.
    private static int CheckNewProfile(SettingsCheck check, bool keepOpen)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      ProfilesModel page = check.Model.Profiles;
      IProfiles profiles = check.Features.Profiles;
      GameObject root = check.Root;
      PageUi.Press(PageUi.ButtonWith(root, UiText("새 프로필", "New profile")));
      PageUi.Settle(check);
      TMP_Text hint = PageUi.Label(root, UiText("처음 설정으로 시작해요.", "Starts with the initial settings."));
      Assert(page.Creating.Open && hint != null && check.Model.Current == page, "New profile opens a panel on the same page");
      TMP_InputField name = root.GetComponentsInChildren<TMP_InputField>(false).Single();
      Pressable create = PageUi.ButtonWith(root, UiText("만들기", "Create"));
      Assert(name.text == "" && !create.IsInteractable(), "a new profile starts unnamed");
      Rect panel = PageUi.World(hint).Union(PageUi.World(create));
      Assert(PageUi.Inside(check, panel), "the panel stays inside the window at " + check.Width);
      name.text = profiles.Profiles[0].Name.ToUpperInvariant();
      PageUi.Settle(check);
      Assert(!create.IsInteractable() && page.Creating.Problem.Value.Length > 0 && PageUi.Shows(root, page.Creating.Problem.Value),
        "a taken name is refused as it is typed");
      foreach (TMP_Text label in new[] { hint, PageUi.Label(root, page.Creating.Problem.Value), PageUi.Label(root, UiText("만들기", "Create")) })
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated && PageUi.Inside(check, PageUi.World(label)), "the new profile panel fits at " + check.Width + ": " + label.text);
      }
      name.text = "Tournament";
      PageUi.Settle(check);
      Assert(create.IsInteractable() && page.Creating.Problem.Value == "", "a free name can be created");
      if (keepOpen)
        return count;
      check.Settings.HighlightColor = "#00FF00";
      int total = profiles.Profiles.Count;
      PageUi.Press(create);
      PageUi.Settle(check);
      Assert(profiles.Profiles.Count == total + 1 && profiles.Active.Name == "Tournament",
        "Create adds the profile and switches to it");
      Assert(!page.Creating.Open && PageUi.Label(root, UiText("처음 설정으로 시작해요.", "Starts with the initial settings.")) == null,
        "Create closes the panel");
      Assert(check.Settings.HighlightColor == ElementSettings.DefaultHighlightColor && check.Settings.OverlayLayouts.Count > 0,
        "the new profile has the initial settings, default overlay included");
      Assert(page.Message.Contains("Tournament") && !page.MessageIsProblem, "the page says the profile was made");
      // Enter with an empty name says why, as the button can't.
      page.NewProfile.TryExecute();
      page.Creating.Submit();
      Assert(page.Creating.Open && page.Creating.Problem.Value.Length > 0, "Enter on an empty name says to give one");
      Assert(check.Panel != null && PageUi.Escape(check) && !page.Creating.Open, "Escape closes the panel");
      return count;
    }

    // An imported profile names a font this PC lacks: the card offers a font to
    // use instead, and a pick lands where the missing font was.
    private static int CheckFontReplacement(SettingsCheck check, bool keepOpen)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      ProfilesModel page = check.Model.Profiles;
      ElementSettings settings = check.Settings;
      GameObject root = check.Root;
      OverlayFonts fonts = page.Fonts.Fonts;
      const string missing = "Element Missing Font|Bold";
      settings.OverlayFont = missing;
      OverlayTextRun run = OverlayTextRun.Literal("Progress", OverlayColorSettings.Custom("#FFFFFF"));
      run.Font = missing;
      settings.OverlayLayouts.Add(new OverlayLayoutSettings { Text = new OverlayTextSettings { Runs = new List<OverlayTextRun> { run } } });
      IOverlayFontLibrary library = fonts.Library;
      library.RequestInstalled();
      var waited = System.Diagnostics.Stopwatch.StartNew();
      while (library.Installed == null && waited.ElapsedMilliseconds < 30000)
        System.Threading.Thread.Sleep(50);
      page.Fonts.Offer();
      for (int i = 0; i < 20 && page.Fonts.Checking; i++)
        page.Tick();
      PageUi.Settle(check);
      string title = UiText("이 PC에 없는 폰트가 있어요", "Some fonts aren't on this PC");
      Assert(page.Fonts.Visible.Value && page.Fonts.Rows.Length == 1 && PageUi.Shows(root, title),
        "an imported profile's missing font is offered once: " + page.Fonts.Rows.Length);
      Rect card = PageUi.World(PageUi.Label(root, title));
      Assert(card.yMax <= PageUi.Cards(root, check.Features.Profiles).Min(c => c.yMin) + .5f, "the font card sits below the cards");
      foreach (string text in new[] { title, page.Fonts.Rows[0].Name, page.Fonts.Rows[0].State, UiText("폰트 고르기", "Choose font"), UiText("완료", "Done") })
      {
        TMP_Text label = PageUi.Label(root, text);
        Assert(label != null && !label.isTextTruncated && PageUi.Inside(check, PageUi.World(label)), "the missing font card fits at " + check.Width + ": " + text);
      }

      PageUi.Press(PageUi.ButtonWith(root, UiText("폰트 고르기", "Choose font")));
      PageUi.Settle(check);
      UiFontPopover list = FontListOf(root);
      Assert(page.Fonts.Rows[0].PickerOpen && list != null && list.Visible, "Choose font opens the font list");
      // Its frame work (fonts that finish listing, scrolling) runs from the page's Tick; the editor has no pointer to try it with.
      Assert(page.Fonts.PickerTick != null, "the open list runs every frame while Profiles shows");
      string georgia = library.Imported.First(face => face.Family == "Georgia").Key;
      page.Fonts.Rows[0].Pick(georgia);
      PageUi.Settle(check);
      Assert(settings.OverlayFont == georgia && run.Font == georgia && page.Fonts.Rows[0].Replaced
        && PageUi.Shows(root, page.Fonts.Rows[0].State), "a pick replaces the missing font where it was used");
      if (keepOpen)
        return count;
      Assert(PageUi.Escape(check) && !page.Fonts.Rows[0].PickerOpen && page.Fonts.PickerTick == null, "Escape closes the font list");
      PageUi.Press(PageUi.ButtonWith(root, UiText("완료", "Done")));
      PageUi.Settle(check);
      Assert(!page.Fonts.Visible.Value && !PageUi.Shows(root, title), "Done puts the card away");
      page.Fonts.Offer();
      for (int i = 0; i < 20 && page.Fonts.Checking; i++)
        page.Tick();
      Assert(!page.Fonts.Visible.Value, "nothing is offered once every font is on this PC");
      return count;
    }

    private static UiFontPopover FontListOf(GameObject root)
    {
      foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(false))
        if (behaviour is UiHost host)
        {
          // The font list is the only FontList view on screen; it holds its UiFontPopover.
          FontList view = FindView<FontList>(host.Root.RootView);
          if (view != null)
            return view.List;
        }
      return null;
    }

    private static T FindView<T>(ElementUI.View view) where T : ElementUI.View
    {
      if (view == null)
        return null;
      if (view is T found)
        return found;
      var children = (System.Collections.IEnumerable)typeof(ElementUI.View)
        .GetProperty("Children", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(view);
      foreach (ElementUI.View child in children)
      {
        T inside = FindView<T>(child);
        if (inside != null)
          return inside;
      }
      return null;
    }

    private static string UiText(string ko, string en) => Element.Presentation.Ui.Theme.UiLocale.L(ko, en);
  }

  /// <summary>Finding and pressing what an ElementUI settings page drew.</summary>
  internal static class PageUi
  {
    /// <summary>What a frame does: compare the view models, run effects, lay out.</summary>
    public static void Settle(SettingsCheck check)
    {
      FitRoot(check);
      ElementUI.Runtime.Probe();
      check.Settle();
      // A width read after layout can switch a branch, which lays out once more.
      for (int i = 0; i < 8 && ElementUI.Runtime.HasWork; i++)
        check.Settle();
      DropDisposedPeers(check);
      foreach (TMP_Text text in Texts(check.Root))
        text.ForceMeshUpdate();
    }

    /// <summary>
    /// While a page's checks run, an error ElementUI reports (from a view, an
    /// effect or a command) fails them; it would otherwise only reach stderr.
    /// </summary>
    public static IDisposable Errors(SettingsCheck check) => new ErrorWatch(check);

    private sealed class ErrorWatch : IDisposable
    {
      private readonly SettingsCheck _check;
      private readonly List<Exception> _errors = new List<Exception>();

      public ErrorWatch(SettingsCheck check)
      {
        _check = check;
        ElementUI.Runtime.ErrorReported += _errors.Add;
      }

      public void Dispose()
      {
        ElementUI.Runtime.ErrorReported -= _errors.Add;
        _check.Assert(_errors.Count == 0, "ElementUI reported no error at " + _check.Name + (_errors.Count > 0 ? ": " + _errors[0] : ""));
      }
    }

    /// <summary>
    /// The window's screen takes the window's size. In the game UiHost follows its
    /// rect by itself; outside play mode Unity doesn't tell it the rect changed,
    /// so a window resized for a narrow frame would keep laying out at its first size.
    /// </summary>
    public static void FitRoot(SettingsCheck check)
    {
      UiHost host = check.Root.GetComponentInChildren<UiHost>();
      Rect rect = ((RectTransform)host.transform).rect;
      if (host.Root.Size.Width != rect.width || host.Root.Size.Height != rect.height)
        host.Root.Resize(new ElementUI.Size(rect.width, rect.height));
    }

    /// <summary>The text field that shows <paramref name="placeholder"/> while empty.</summary>
    public static TMP_InputField FieldWithPlaceholder(GameObject root, string placeholder) =>
      root.GetComponentsInChildren<TMP_InputField>(false).FirstOrDefault(field => (field.placeholder as TMP_Text)?.text == placeholder);

    /// <summary>
    /// Removes what views that went away left behind. The pinned ElementUI
    /// destroys peers with Object.Destroy, which Unity refuses outside play mode
    /// (ElementUI fc436a1 destroys them at once there); in the game they go.
    /// Every child of a live container that no live view owns is such a leftover.
    /// </summary>
    public static void DropDisposedPeers(SettingsCheck check)
    {
      UiHost host = check.Root.GetComponentInChildren<UiHost>();
      var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
      var live = new HashSet<GameObject>();
      var containers = new List<GameObject>();
      void Add(ElementUI.IPeer peer)
      {
        if (!(peer?.GetNative(typeof(GameObject)) is GameObject gameObject) || gameObject == null)
          return;
        live.Add(gameObject);
        if (peer is ElementUI.IScrollPeer scroll && scroll.Content?.GetNative(typeof(GameObject)) is GameObject content)
        {
          live.Add(content);
          containers.Add(content);
        }
        else if (peer is ElementUI.IContainerPeer)
          containers.Add(gameObject);
      }
      void Walk(ElementUI.View view)
      {
        for (System.Type type = view.GetType(); type != null && type != typeof(object); type = type.BaseType)
          foreach (System.Reflection.FieldInfo field in type.GetFields(flags | System.Reflection.BindingFlags.DeclaredOnly))
            if (typeof(ElementUI.IPeer).IsAssignableFrom(field.FieldType))
              Add(field.GetValue(view) as ElementUI.IPeer);
        var children = (System.Collections.IEnumerable)typeof(ElementUI.View).GetProperty("Children", flags).GetValue(view);
        foreach (ElementUI.View child in children)
          Walk(child);
      }
      foreach (string name in new[] { "Container", "OverlayContainer" })
        Add(host.Root.GetType().GetField(name, flags)?.GetValue(host.Root) as ElementUI.IPeer
          ?? host.Root.GetType().GetProperty(name, flags)?.GetValue(host.Root) as ElementUI.IPeer);
      Walk(host.Root.RootView);
      foreach (GameObject container in containers)
        for (int i = container.transform.childCount - 1; i >= 0; i--)
        {
          GameObject child = container.transform.GetChild(i).gameObject;
          if (!live.Contains(child) && child.GetComponent<RectTransform>() != null && IsPeer(child))
            Object.DestroyImmediate(child);
        }
    }

    // A peer's own GameObject, as opposed to a part a peer made inside itself (a switch's thumb, a field's text area).
    private static bool IsPeer(GameObject child) =>
      child.GetComponent<TMP_Text>() != null
      || child.GetComponent<Pressable>() != null
      || child.GetComponent<UiIcon>() != null
      || child.GetComponent<TMP_InputField>() != null
      || child.name == "Box" || child.name == "Button" || child.name == "Switch" || child.name == "Scroll view"
      || child.name == "Popover content" || child.name == "Backdrop" || child.name == "Icon";

    public static TMP_Text[] Texts(GameObject root) => root.GetComponentsInChildren<TMP_Text>(false);

    public static TMP_Text Label(GameObject root, string text) => Texts(root).FirstOrDefault(label => label.text == text);

    public static bool Shows(GameObject root, string text) => Label(root, text) != null;

    public static Pressable ButtonWith(GameObject root, string text) => Label(root, text)?.GetComponentInParent<Pressable>();

    /// <summary>The card button that holds a name.</summary>
    public static Pressable CardOf(GameObject root, string name) => ButtonWith(root, name);

    /// <summary>Every card's rect, by profile.</summary>
    public static IEnumerable<Rect> Cards(GameObject root, IProfiles profiles) =>
      profiles.Profiles.Select(p => CardOf(root, p.Name)).Where(card => card != null).Select(card => World(card));

    public static IEnumerable<Pressable> IconButtons(GameObject root, UiIconKind kind) =>
      root.GetComponentsInChildren<UiIcon>(false).Where(icon => icon.Kind == kind).Select(icon => icon.GetComponentInParent<Pressable>()).Where(button => button != null);

    /// <summary>The icon button drawn over a card.</summary>
    public static Pressable ActionOf(GameObject root, Pressable card, UiIconKind kind)
    {
      Rect area = World(card);
      return IconButtons(root, kind).FirstOrDefault(button => area.Contains(World(button).center));
    }

    /// <summary>The shortcut field around a keycap or its words.</summary>
    public static Pressable CaptureButton(GameObject root, TMP_Text inside) => inside?.GetComponentInParent<Pressable>();

    public static Pressable[] Switches(GameObject root) =>
      root.GetComponentsInChildren<Pressable>(false).Where(pressable => pressable.name == "Switch").ToArray();

    /// <summary>The switch on the same card as a title: level with it, to its right.</summary>
    public static Pressable SwitchBeside(GameObject root, string title)
    {
      Rect label = World(Label(root, title));
      return Switches(root)
        .Where(toggle => World(toggle).xMin > label.xMax)
        .OrderBy(toggle => Mathf.Abs(World(toggle).center.y - label.center.y))
        .First();
    }

    public static void Press(Pressable button) => button.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });

    /// <summary>Escape as the window takes it: an open popover first, then the page.</summary>
    public static bool Escape(SettingsCheck check)
    {
      UiHost host = check.Root.GetComponentInChildren<UiHost>();
      bool handled = host.HandleEscape() || check.Model.Current.HandleEscape();
      Settle(check);
      return handled;
    }

    public static Rect World(Component component) => World((RectTransform)component.transform);

    public static Rect World(RectTransform rect)
    {
      var corners = new Vector3[4];
      rect.GetWorldCorners(corners);
      return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    public static Rect Union(this Rect a, Rect b) =>
      Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

    /// <summary>Inside the settings window across; below it is scrolled to.</summary>
    public static bool Inside(SettingsCheck check, Rect rect)
    {
      Rect window = World((RectTransform)check.Root.transform.Find("Settings window"));
      return rect.xMin >= window.xMin - .5f && rect.xMax <= window.xMax + .5f && rect.yMax <= window.yMax + .5f;
    }

    /// <summary>Every text shown fits the window across, and is whole unless it is meant to end in "…".</summary>
    public static int CheckFits(SettingsCheck check, string page)
    {
      int count = 0;
      foreach (TMP_Text text in Texts(check.Root))
      {
        if (string.IsNullOrEmpty(text.text))
          continue;
        text.ForceMeshUpdate();
        check.Assert(Inside(check, World(text)),
          page + " text stays inside the window at " + check.Width + ": " + text.text + " " + World(text) + " in " + World((RectTransform)check.Root.transform.Find("Settings window")));
        check.Assert(text.overflowMode == TextOverflowModes.Ellipsis || !text.isTextTruncated,
          page + " text is not cut at " + check.Width + ": " + text.text);
        count += 2;
      }
      return count;
    }
  }
}
