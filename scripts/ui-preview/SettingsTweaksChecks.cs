using Element.Presentation.Settings.Chatter;
using Element.Presentation.Settings.EditorTweaks;
using Element.Presentation.Settings.Judgment;
using Element.Presentation.Settings.Tweaks;
using Element.Presentation.Settings.Window;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Element.Configuration.Gameplay;
using Element.Domain.Tweaks;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Input;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  /// <summary>
  /// The Tweaks page and the pages it opens: Editor tweaks, Judgment visuals
  /// and Chatter blocker. Each check works the page through its view model and
  /// through the rendered controls, then leaves it as the screenshot shows it.
  /// </summary>
  internal static class SettingsTweaksChecks
  {
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [PageCheck("_tweaks")]
    private static int CheckTweaks(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      FitHost(check);
      SettingsWindowViewModel window = check.Model;
      TweaksViewModel tweaks = window.Tweaks;
      Assert(window.Current == tweaks && tweaks.Title == "Tweaks", "Tweaks is shown under its own title");

      // Each link opens its page inside the window; Back comes back to Tweaks.
      var links = new (string Title, string Detail, SettingsPageViewModel Page)[]
      {
        (L("에디터", "Editor tweaks"), L("선택 정보, 정밀 회전, 이벤트 좌표", "Selection info, fine rotation and event positions"), window.EditorTweaks),
        (L("판정 표시", "Judgment visuals"), L("어떤 판정 문구를 보여줄지 선택합니다.", "Choose which judgment popups appear."), window.Judgment),
        (L("행성", "Planet appearance"), L("공, 꼬리, 고리의 색상과 불투명도", "Body, trail and ring color & opacity"), window.Planets),
        (L("채터 블로커", "Chatter blocker"), L("스위치가 튀어 한 번 누른 키가 두 번 입력되는 것을 막습니다.", "Stops a bouncing switch from counting one press twice."), window.Chatter),
      };
      foreach (var link in links)
      {
        Pressable card = ButtonOf(check, link.Title);
        Assert(card != null && Label(check, link.Detail) != null, "Tweaks links to " + link.Title + " with what it holds");
        // The pointer is on the card as it is clicked, and the page leaves from under it.
        card.OnPointerEnter(null);
        Assert((card.State & ElementUI.Ugui.ButtonState.Hovered) != 0, link.Title + " lights under the pointer");
        Assert(Press(card), link.Title + " can be clicked");
        check.Settle();
        Assert(window.Current == link.Page && window.CanGoBack.Value && link.Page.Parent == tweaks, link.Title + " opens within settings, with Back");
        window.Back.TryExecute();
        check.Settle();
        Assert(window.Current == tweaks && Label(check, link.Title) != null, "Back from " + link.Title + " returns to Tweaks");
        Assert((ButtonOf(check, link.Title).State & ElementUI.Ugui.ButtonState.Hovered) == 0, link.Title + " isn't still lit after coming back");
      }
      Assert(Active<UiIcon>(check).Count(icon => icon.Kind == UiIconKind.PageForward) == links.Length, "every link shows a forward arrow");

      // Record mode everywhere; the trackpad only on macOS.
      bool mac = UnityEngine.Application.platform == RuntimePlatform.OSXEditor || UnityEngine.Application.platform == RuntimePlatform.OSXPlayer;
      Assert(Switches(check).Count == (mac ? 2 : 1) && (Label(check, L("macOS 트랙패드", "macOS Trackpad")) != null) == mac,
        "Tweaks contains the trackpad switch only on macOS");
      Pressable record = ButtonOf(check, L("녹화 모드", "Record mode"));
      Assert(!check.Settings.RecordMode && Press(record) && check.Settings.RecordMode, "a click on the record mode card turns it on");
      check.Settle();
      Assert(Press(SwitchIn(record)) && !check.Settings.RecordMode, "its switch turns it off again");
      check.Settle();
      if (mac)
      {
        Pressable trackpad = ButtonOf(check, L("macOS 트랙패드", "macOS Trackpad"));
        bool before = check.Settings.NativeEditorTrackpad;
        Assert(Press(SwitchIn(trackpad)) && check.Settings.NativeEditorTrackpad != before, "the trackpad switch flips the setting");
        check.Settle();
        Press(SwitchIn(trackpad));
        check.Settle();
        Rect left = WorldRect(record), right = WorldRect(trackpad);
        bool wide = WorldRect(Field<RectTransform>(check.Panel, "_window")).width >= 760 + 64;
        Assert(wide ? Mathf.Abs(left.yMax - right.yMax) < .5f && right.xMin > left.xMax : right.yMax < left.yMin,
          "the two switches share a row on a wide window and stack on a narrow one");
      }
      foreach (var link in links)
        Assert(!Truncated(Label(check, link.Detail)), "link description fits: " + link.Detail);

      CheckWindowControls(check, Assert);

      // A narrow window stacks the switches and wraps the descriptions.
      AtWidth(check, 360, () =>
      {
        Rect frame = WorldRect(Field<RectTransform>(check.Panel, "_window"));
        foreach (var link in links)
        {
          Rect card = WorldRect(ButtonOf(check, link.Title));
          Assert(card.xMax <= frame.xMax - 16 && !Truncated(Label(check, link.Detail)), "a link fits a narrow window: " + link.Title);
        }
        if (mac)
          Assert(WorldRect(ButtonOf(check, L("macOS 트랙패드", "macOS Trackpad"))).yMax < WorldRect(ButtonOf(check, L("녹화 모드", "Record mode"))).yMin,
            "the switches stack on a narrow window");
      });
      return count;
    }

    // Back and Close are borderless icons; the window settles in without scaling its text.
    private static void CheckWindowControls(SettingsCheck check, Action<bool, string> Assert)
    {
      UiIcon back = Active<UiIcon>(check).FirstOrDefault(icon => icon.Kind == UiIconKind.PageBack);
      UiIcon close = Active<UiIcon>(check).FirstOrDefault(icon => icon.Kind == UiIconKind.WindowClose);
      Pressable backButton = back?.GetComponentInParent<Pressable>(), closeButton = close?.GetComponentInParent<Pressable>();
      Assert(backButton != null && backButton.IsInteractable() && !back.raycastTarget, "a page under Home offers Back with the full button hit area");
      Assert(closeButton != null && !closeButton.GetComponentsInChildren<TextMeshProUGUI>(false).Any(), "Close is an icon without words");
      foreach (Pressable button in new[] { backButton, closeButton })
      {
        Graphic fill = button.targetGraphic;
        Assert(fill != null && fill.color.a == 0, "navigation icons have no button background at rest");
      }
      // Each one's hint shows at it: Close's at the window's right edge, not
      // its left. On a canvas the size of the rendered screen, as in the game,
      // not batch mode's small screen the window spills over.
      Canvas canvas = check.Root.GetComponent<Canvas>();
      Camera camera = new GameObject("Hint check camera", typeof(Camera)).GetComponent<Camera>();
      RenderTexture screen = new RenderTexture(check.Width, check.Height, 24);
      camera.orthographic = true;
      camera.targetTexture = screen;
      canvas.renderMode = RenderMode.ScreenSpaceCamera;
      canvas.worldCamera = camera;
      canvas.planeDistance = 1;
      Canvas.ForceUpdateCanvases();
      try
      {
        foreach (Pressable button in new[] { backButton, closeButton })
        {
          PointerArea hint = button.GetComponentInParent<PointerArea>();
          hint.OnPointerEnter(null);
          check.Settle();
          string misplaced = SettingsPreview.HintProblem(hint);
          Assert(misplaced == null, "a navigation icon's hint shows beside it: " + (button == backButton ? "Back " : "Close ") + misplaced);
          if (button == closeButton)
            check.Save(check.Name + "-close-hint");
          hint.OnPointerExit(null);
          check.Settle();
        }
      }
      finally
      {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
        UnityEngine.Object.DestroyImmediate(screen);
        Canvas.ForceUpdateCanvases();
      }

      var rect = Field<RectTransform>(check.Panel, "_window");
      Vector2 resting = rect.anchoredPosition;
      int page = Array.IndexOf(check.Model.Pages, check.Model.Current);
      CanvasGroup Open(bool reduceMotion)
      {
        check.Panel.HideForLayout();
        check.Settings.ReduceUiMotion = reduceMotion;
        check.Panel.Open();
        check.Panel.Resize(check.Width, check.Height);
        check.Model = Field<SettingsWindowViewModel>(check.Panel, "_model");
        check.Model.Show(check.Model.Pages[page]);
        ElementUI.Runtime.Flush();
        Canvas.ForceUpdateCanvases();
        return rect.GetComponentsInChildren<CanvasGroup>(true).First(group => group.name == "Presentation");
      }
      bool AtRest(CanvasGroup group) =>
        Mathf.Abs(((RectTransform)group.transform.parent).anchoredPosition.y + rect.rect.height / 2) < .01f;
      CanvasGroup group = Open(true);
      Assert(group.alpha == 1 && AtRest(group) && rect.anchoredPosition == resting,
        "reduced-motion window opens immediately at its resting position");
      group = Open(false);
      Assert(rect.localScale == Vector3.one && group.alpha == 0 && !AtRest(group) && rect.anchoredPosition == resting,
        "window entrance moves its presentation without scaling text or changing host geometry");
      ElementUI.Runtime.Flush(.3f);
      Assert(group.alpha == 1 && AtRest(group), "window entrance settles exactly through the shared animation scheduler");
      group = Open(false);
      check.Settings.ReduceUiMotion = true;
      check.Panel.GetType().GetMethod("Changed", Private).Invoke(check.Panel, null);
      check.Model = Field<SettingsWindowViewModel>(check.Panel, "_model");
      ElementUI.Runtime.Flush();
      group = rect.GetComponentsInChildren<CanvasGroup>(true).First(value => value.name == "Presentation");
      Assert(AtRest(group) && group.alpha == 1, "enabling reduced motion interrupts the entrance cleanly");
    }

    [PageCheck("_editorTweaks")]
    private static int CheckEditorTweaks(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      FitHost(check);
      EditorTweaksViewModel page = check.Model.EditorTweaks;
      EditorTweaksOptions editor = check.Settings.Gameplay.Editor;
      Assert(page.Title == L("에디터", "Editor tweaks") && page.Parent == check.Model.Tweaks, "Editor tweaks sits under Tweaks");

      // One section at a time: what the selection shows, or how editing behaves.
      string[] selection = { L("타일 각도", "Tile angle"), L("박자 수", "Beat count"), L("타일 수", "Tile count"), L("구간 시간", "Duration") };
      string exclude = L("마지막 간격 제외", "Exclude last interval"), fine = L("정밀 회전", "Fine rotation");
      Assert(selection.All(title => ButtonOf(check, title) != null) && Label(check, fine) == null,
        "editor suboptions use one focused section rather than one long list");
      Rect first = WorldRect(ButtonOf(check, selection[0])), second = WorldRect(ButtonOf(check, selection[1])), third = WorldRect(ButtonOf(check, selection[2]));
      Assert(Mathf.Abs(first.yMax - second.yMax) < .5f && second.xMin > first.xMax && third.yMax < first.yMin && Mathf.Abs(third.xMin - first.xMin) < .5f,
        "the selection switches form two columns on a wide window");
      Rect excludeRect = WorldRect(ButtonOf(check, exclude));
      Assert(excludeRect.yMax < third.yMin && excludeRect.width > first.width * 1.5f, "Exclude last interval spans the row under them");

      // Leaving out the last interval only means something for angle, beats and time.
      Func<bool> excludeOn = () => ButtonOf(check, exclude).IsInteractable() && SwitchIn(ButtonOf(check, exclude)).IsInteractable();
      Assert(!editor.HasSelectionInfo && !page.CanExcludeLastInterval.Value && !excludeOn(), "Exclude last interval waits until angle, beats or time is shown");
      Assert(Press(ButtonOf(check, selection[2])) && editor.ShowCount, "a click on Tile count shows it");
      check.Settle();
      Assert(!excludeOn(), "the tile count alone has no interval to leave out");
      Assert(Press(SwitchIn(ButtonOf(check, selection[0]))) && editor.ShowAngle, "the Tile angle switch shows it");
      check.Settle();
      Assert(excludeOn() && Press(SwitchIn(ButtonOf(check, exclude))) && editor.ExcludeLastInterval, "with the angle shown, the last interval can be left out");
      check.Settle();
      Press(SwitchIn(ButtonOf(check, exclude)));
      Press(ButtonOf(check, selection[0]));
      Press(ButtonOf(check, selection[2]));
      check.Settle();
      Assert(!editor.HasSelectionInfo && !editor.ExcludeLastInterval && !excludeOn(), "the selection options are back off");

      Assert(Press(Option(check, L("편집 동작", "Editing"))), "the Editing tab can be picked");
      check.Settle();
      Assert(Label(check, fine) != null && Label(check, selection[0]) == null, "editor section navigation swaps its suboptions");
      Assert(Label(check, page.FineRotationDetail) != null && page.FineRotationDetail.StartsWith(ShortcutKeys.IsMac ? "Cmd + Option" : "Ctrl + Alt"),
        "fine rotation names this platform's shortcut");

      // The step waits while fine rotation is off.
      TMP_InputField field = Active<TMP_InputField>(check).Single();
      Pressable[] presets = new[] { "22.5°", "15°", "7.5°", "1°" }.Select(label => ButtonOf(check, label)).ToArray();
      Assert(presets.All(preset => preset != null), "the common steps are offered as buttons");
      Assert(!editor.FineRotation && !field.interactable && presets.All(preset => !preset.IsInteractable()), "rotation step is disabled when fine rotation is off");
      Assert(Press(ButtonOf(check, fine)) && editor.FineRotation, "a click on the fine rotation card turns it on");
      check.Settle();
      Assert(field.interactable && presets.All(preset => preset.IsInteractable()), "enabling fine rotation activates its nested step control");
      // The old page also checked the caret (SettingsPreview.CheckInputCaret). ElementUI's
      // TextField centers its text on the midline and fails it; that is ElementUI's to fix.

      // A typed step is kept when typing ends; one out of range says why and keeps the old step.
      string error = L("0.1°부터 180° 사이의 각도를 입력하세요.", "Enter an angle between 0.1° and 180°.");
      float before = editor.RotationStep;
      Type(check, field, "not an angle");
      Assert(editor.RotationStep == before && page.RotationError.Value == error && Label(check, error) != null,
        "invalid custom rotation leaves the previous value intact and says what to enter");
      field.text = "7";
      check.Settle();
      Assert(page.RotationError.Value == "" && Label(check, error) == null, "typing again clears the message");
      Type(check, field, "7.5");
      Assert(Mathf.Abs(editor.RotationStep - 7.5f) < .001f && page.RotationError.Value == "", "valid custom rotation is applied");
      Type(check, field, "500");
      Assert(Mathf.Abs(editor.RotationStep - 7.5f) < .001f && Label(check, error) != null, "an angle above 180° is refused");
      Assert(Press(ButtonOf(check, "15°")), "a step button can be clicked");
      check.Settle();
      Assert(editor.RotationStep == 15 && field.text == "15" && Label(check, error) == null, "a step button sets the step, shows it and clears the message");
      Assert(!Truncated(Label(check, page.FineRotationDetail)), "the fine rotation description fits");

      Pressable sync = ButtonOf(check, L("이벤트 좌표 함께 변환", "Transform event positions"));
      Assert(Press(SwitchIn(sync)) && editor.SyncEventPositions, "event positions can follow flipped and rotated tiles");
      check.Settle();
      Press(sync);
      check.Settle();
      Assert(!editor.SyncEventPositions, "and stop following them");

      Press(ButtonOf(check, "22.5°"));
      check.Settle();
      // One frame shows each section; the editing one with fine rotation on.
      if (check.Name != "editor-editing-en-720")
      {
        Press(Option(check, L("선택 정보", "Selection")));
        check.Settle();
        Assert(Label(check, selection[0]) != null && Label(check, fine) == null, "the Selection tab comes back");
      }
      // A narrow window gives each switch its own row.
      AtWidth(check, 360, () =>
      {
        Rect frame = WorldRect(Field<RectTransform>(check.Panel, "_window"));
        if (Label(check, selection[0]) != null)
        {
          Rect angle = WorldRect(ButtonOf(check, selection[0])), beats = WorldRect(ButtonOf(check, selection[1]));
          Assert(beats.yMax < angle.yMin && angle.xMax <= frame.xMax - 16, "the selection switches stack on a narrow window");
        }
        else
          Assert(WorldRect(Active<TMP_InputField>(check).Single().transform).xMax <= frame.xMax - 16, "the step field wraps inside a narrow window");
      });
      return count;
    }

    [PageCheck("_judgment")]
    private static int CheckJudgment(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      FitHost(check);
      JudgmentViewModel page = check.Model.Judgment;
      JudgementVisualOptions judgment = check.Settings.Gameplay.Judgement;
      Func<bool>[] hidden =
      {
        () => judgment.HideEarlyPerfect, () => judgment.HidePerfect, () => judgment.HideXPerfect,
        () => judgment.HideLatePerfect, () => judgment.HideTooEarly, () => judgment.HideEarly,
        () => judgment.HideLate, () => judgment.HideTooLate, () => judgment.HideMiss, () => judgment.HideOverload
      };
      string[] names = { "Early Perfect", "Perfect - / +", "XPerfect", "Late Perfect", "Too Early", "Early", "Late", "Too Late", "Miss", "Overload" };
      Pressable[] tiles = names.Select(name => ButtonOf(check, name)).ToArray();
      Assert(page.Choices.Length == hidden.Length && tiles.All(tile => tile != null), "ten judgment controls expose XP separately from Perfect +/-");
      Assert(page.Title == L("판정 표시", "Judgment visuals"), "the page is Judgment visuals");

      Pressable showAll = ButtonOf(check, L("모두 표시", "Show all")), hideAll = ButtonOf(check, L("모두 숨김", "Hide all"));
      Assert(Press(showAll) && hidden.All(read => !read()), "Show all includes XP and every other judgment");
      check.Settle();
      Assert(Labels(check, L("표시", "Shown")).Count == hidden.Length, "every tile says Shown");
      for (int i = 0; i < tiles.Length; i++)
      {
        Press(tiles[i]);
        Assert(hidden.Select((read, index) => read() == (index == i)).All(value => value), "each judgment card changes only its own visibility: " + names[i]);
        check.Settle();
        Assert(Labels(check, L("숨김", "Hidden")).Count == 1, "the hidden tile says Hidden: " + names[i]);
        Press(tiles[i]);
        Assert(hidden.All(read => !read()), "each judgment can independently return to shown: " + names[i]);
        check.Settle();
      }
      Assert(Press(hideAll) && hidden.All(read => read()), "Hide all includes XP and every other judgment");
      check.Settle();
      Press(tiles[1]);
      Assert(!judgment.HidePerfect && judgment.HideXPerfect && judgment.HideMiss, "showing Perfect +/- leaves XP and failures hidden");
      Press(tiles[2]);
      Assert(!judgment.HidePerfect && !judgment.HideXPerfect && judgment.HideMiss, "showing XP leaves Perfect +/- and failures unchanged");
      check.Settle();
      Assert(Press(showAll) && hidden.All(read => !read()), "Show all restores all ten judgment types after independent editing");
      check.Settle();

      // The hint follows ADOFAI's Competitive Mode, read when the window opens.
      string apart = L("표시할 판정을 선택하세요. 경쟁 모드를 켜면 얼불춤 설정과 함께 바뀌어요.", "Choose the popups to show. Turn on Competitive Mode to sync with ADOFAI.");
      string linked = L("표시할 판정을 선택하세요. 얼불춤의 판정 텍스트 숨기기와 함께 바뀌어요.", "Choose the popups to show. Synced with ADOFAI's Hide Judgment Text.");
      Assert(Label(check, apart) != null, "without Competitive Mode the hint says how to sync");
      Func<bool> previous = check.Features.JudgementTextLinked;
      check.Features.JudgementTextLinked = () => true;
      page.Opened();
      ElementUI.Runtime.Probe();
      check.Settle();
      Assert(Label(check, linked) != null && Label(check, apart) == null, "with Competitive Mode the hint says the two are synced");
      check.Features.JudgementTextLinked = previous;
      page.Opened();
      ElementUI.Runtime.Probe();
      check.Settle();

      Rect a = WorldRect(tiles[0]), b = WorldRect(tiles[1]), c = WorldRect(tiles[2]), d = WorldRect(tiles[3]);
      Assert(Mathf.Abs(a.yMax - b.yMax) < .5f && Mathf.Abs(b.yMax - c.yMax) < .5f && d.yMax < a.yMin && Mathf.Abs(d.xMin - a.xMin) < .5f,
        "the tiles form three columns on a wide window");
      Rect hint = WorldRect(Label(check, apart).rectTransform), hide = WorldRect(hideAll);
      Assert(hide.yMax > a.yMax && hide.xMax <= c.xMax + .5f && hide.xMin > hint.xMax, "Show all and Hide all sit beside the hint, above the tiles");
      // Two columns, then one, as the window narrows; the buttons go under the hint.
      AtWidth(check, 540, () =>
      {
        Rect first = WorldRect(ButtonOf(check, names[0])), second = WorldRect(ButtonOf(check, names[1])), third = WorldRect(ButtonOf(check, names[2]));
        Assert(Mathf.Abs(first.yMax - second.yMax) < .5f && third.yMax < first.yMin, "two columns of tiles on a narrower window");
        Assert(WorldRect(ButtonOf(check, L("모두 숨김", "Hide all"))).yMax < WorldRect(Label(check, apart).rectTransform).yMin, "the buttons go under the hint");
      });
      AtWidth(check, 360, () =>
      {
        Rect frame = WorldRect(Field<RectTransform>(check.Panel, "_window"));
        Rect first = WorldRect(ButtonOf(check, names[0])), second = WorldRect(ButtonOf(check, names[1]));
        Assert(second.yMax < first.yMin && first.xMax <= frame.xMax - 16, "one column of tiles on a compact window");
        Assert(!Truncated(Label(check, apart)) && WorldRect(ButtonOf(check, L("모두 숨김", "Hide all"))).xMax <= frame.xMax - 16, "the hint wraps and the buttons fit");
      });
      return count;
    }

    [PageCheck("_chatter")]
    private static int CheckChatter(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }
      FitHost(check);
      ChatterViewModel page = check.Model.Chatter;
      ChatterBlockerOptions chatter = check.Settings.Gameplay.Chatter;
      Pressable toggle = ButtonOf(check, L("채터 블로커", "Chatter blocker"));
      Assert(toggle != null && Switches(check).Count == 1 && SwitchIn(toggle) != null, "the blocker is one switch card");
      TMP_InputField input = Active<TMP_InputField>(check).Single();
      SliderDrag slider = Active<SliderDrag>(check).Single();
      Action<float> slide = value => slider.Moved((value - ChatterFilter.MinimumWindowMs) / (ChatterFilter.MaximumWindowMs - ChatterFilter.MinimumWindowMs), true);

      Assert(chatter.Enabled && input.interactable, "the chatter blocker starts on with its window ready");
      Assert(chatter.WindowMs == ChatterFilter.DefaultWindowMs && page.Threshold == chatter.WindowMs, "building the page keeps the saved window");
      Assert(input.text == chatter.WindowMs.ToString(), "the typed window starts at the saved value");
      slide(ChatterFilter.MinimumWindowMs - 50);
      Assert(chatter.WindowMs == ChatterFilter.MinimumWindowMs, "the slider stops at 10 ms");
      slide(ChatterFilter.MaximumWindowMs + 50);
      Assert(chatter.WindowMs == ChatterFilter.MaximumWindowMs, "the slider stops at 500 ms");
      slide(33.3f);
      check.Settle();
      Assert(chatter.WindowMs == 33 && input.text == "33", "the slider saves whole milliseconds and the field shows them");

      Assert(Press(toggle) && !chatter.Enabled, "a click on the card turns blocking off");
      check.Settle();
      slide(200);
      Assert(chatter.WindowMs == 33 && !input.interactable, "the window waits while blocking is off");
      Assert(Press(SwitchIn(toggle)) && chatter.Enabled, "the switch turns blocking on");
      check.Settle();
      Assert(input.interactable, "turning blocking on enables the window");

      Type(check, input, "72");
      Assert(chatter.WindowMs == 72, "a typed window saves");
      Type(check, input, "4000");
      Assert(chatter.WindowMs == 500 && input.text == "500", "a window above the range becomes the maximum");
      Type(check, input, "2");
      Assert(chatter.WindowMs == 10 && input.text == "10", "a window below the range becomes the minimum");
      Type(check, input, "");
      Assert(chatter.WindowMs == 10 && input.text == "10", "clearing the field restores the saved window");
      page.Threshold = ChatterFilter.DefaultWindowMs;
      ElementUI.Runtime.Probe();
      check.Settle();
      Assert(chatter.Enabled && input.text == "100", "the page ends in the default state, blocking on");

      RectTransform window = Field<RectTransform>(check.Panel, "_window");
      Rect field = WorldRect((RectTransform)input.transform), track = WorldRect((RectTransform)slider.transform), frame = WorldRect(window);
      Assert(field.xMax <= frame.xMax - 16 && track.xMax <= frame.xMax - 16 && (track.xMax <= field.xMin || track.yMin >= field.yMax),
        "the slider ends before the typed window, or sits above it, inside the window: " + track + " " + field + " " + frame);
      Assert(track.width >= 160, "the slider keeps room to drag: " + track.width);

      // Keys never blocked: Change keys listens, each key goes on or comes off.
      string none = L("없음", "None"), change = L("키 바꾸기", "Change keys"), done = L("완료", "Done"), clear = L("비우기", "Clear");
      Assert(chatter.IgnoredKeys.Count == 0 && Label(check, none) != null && !ButtonOf(check, clear).IsInteractable(), "no key is ignored at first");
      Assert(Press(ButtonOf(check, change)) && page.Capturing, "Change keys starts listening");
      check.Settle();
      Assert(ButtonOf(check, done) != null && !ButtonOf(check, clear).IsInteractable(), "Change keys becomes Done while listening");
      chatter.ToggleIgnored(KeyCode.Space);
      chatter.ToggleIgnored(KeyCode.X);
      page.SettingsReplaced();
      ElementUI.Runtime.Probe();
      check.Settle();
      Assert(Label(check, "Space · X") != null, "the ignored keys are listed");
      Assert(Press(ButtonOf(check, done)) && !page.Capturing, "Done stops listening");
      check.Settle();
      Assert(ButtonOf(check, change) != null && ButtonOf(check, clear).IsInteractable(), "Clear is offered once keys are listed");
      page.Capturing = true;
      page.CancelCapture();
      Assert(!page.Capturing, "leaving the page stops listening");

      RectTransform hint = Label(check, L(
        "같은 키를 마지막으로 받은 입력에서 이 시간 안에 다시 누르면 무시해요. 이보다 빠르게 같은 키를 연타하면 입력이 빠지니, 그런 키는 아래에 넣어 주세요.",
        "A press of the same key within this time of its last accepted press is ignored. Faster repeats on one key are dropped too, so add such keys below.")).rectTransform;
      RectTransform title = Label(check, L("막지 않을 키", "Keys never blocked")).rectTransform;
      Assert(WorldRect(title).yMax <= WorldRect(hint).yMin, "the card sits below the hint");
      foreach (string text in new[] { L("막지 않을 키", "Keys never blocked"), L("여기 넣은 키는 아무리 빠르게 연타해도 막지 않아요.", "Keys here are never blocked, however fast you repeat them."), "Space · X", change, clear })
      {
        TextMeshProUGUI label = Label(check, text);
        Assert(label != null && !Truncated(label) && WorldRect(label.rectTransform).xMax <= frame.xMax - 16, "ignored keys text fits: " + text);
      }
      Assert(!Truncated(hint.GetComponent<TextMeshProUGUI>()) && WorldRect(hint).xMax <= frame.xMax - 16, "the hint wraps inside the window");

      // The count of blocked presses follows the blocker while the page shows.
      string blocked = L("이번 실행에서 막은 입력: ", "Blocked since ADOFAI started: ");
      Func<long> previous = check.Features.BlockedChatter;
      check.Features.BlockedChatter = () => 7;
      page.Tick();
      check.Settle();
      Assert(Label(check, blocked + "7") != null, "the blocked count shows the presses dropped");
      Assert(WorldRect(Label(check, blocked + "7").rectTransform).yMax <= WorldRect(ButtonOf(check, clear)).yMin, "the blocked count sits below the card");
      check.Features.BlockedChatter = previous;
      page.Tick();
      check.Settle();
      Assert(Label(check, blocked + "0") != null, "no blocker reads as 0");
      Assert(!page.OtherBlockerRunning && Label(check, L(
        "KeyboardChatterBlocker도 켜져 있어요. 입력이 두 번 걸러지지 않도록 한쪽만 사용하세요.",
        "KeyboardChatterBlocker is also running. Use only one of them so input is not filtered twice.")) == null,
        "the conflict warning shows only when KeyboardChatterBlocker is loaded");

      Assert(Press(ButtonOf(check, clear)) && chatter.IgnoredKeys.Count == 0, "Clear empties the list");
      check.Settle();
      Assert(Label(check, none) != null, "the list reads None again");
      // The frame shows one key kept.
      chatter.ToggleIgnored(KeyCode.Space);
      page.SettingsReplaced();
      ElementUI.Runtime.Probe();
      check.Settle();
      return count;
    }

    /// <summary>
    /// The preview sizes the window in the editor, where UiHost isn't told the
    /// new size (Unity sends it OnRectTransformDimensionsChange only in play
    /// mode); tell the screen its size so narrow frames are laid out narrow.
    /// </summary>
    /// <summary>
    /// Runs <paramref name="test"/> with the screen <paramref name="width"/> pixels wide,
    /// saves that frame as "&lt;name&gt;-at-&lt;width&gt;", and puts the window back.
    /// </summary>
    private static void AtWidth(SettingsCheck check, int width, Action test)
    {
      check.Panel.Resize(width, check.Height);
      FitHost(check);
      test();
      check.Save(check.Name + "-at-" + width);
      check.Panel.Resize(check.Width, check.Height);
      FitHost(check);
    }

    private static void FitHost(SettingsCheck check)
    {
      RectTransform window = Field<RectTransform>(check.Panel, "_window");
      window.GetComponent<UiHost>().Root.Resize(new ElementUI.Size(window.rect.width, window.rect.height));
      check.Settle();
    }

    // ---- Finding and using the rendered controls ----

    private static string L(string korean, string english) => Element.Presentation.Ui.Theme.UiLocale.L(korean, english);

    private static List<T> Active<T>(SettingsCheck check)
      where T : Component => check.Root.GetComponentsInChildren<T>(false).ToList();

    private static List<TextMeshProUGUI> Labels(SettingsCheck check, string text) =>
      Active<TextMeshProUGUI>(check).Where(label => label.text == text).ToList();

    /// <summary>The shown text that reads exactly <paramref name="text"/>, preferring one inside a button.</summary>
    private static TextMeshProUGUI Label(SettingsCheck check, string text)
    {
      List<TextMeshProUGUI> labels = Labels(check, text);
      return labels.FirstOrDefault(label => label.GetComponentInParent<Pressable>() != null) ?? labels.FirstOrDefault();
    }

    /// <summary>The button whose words include <paramref name="text"/>.</summary>
    private static Pressable ButtonOf(SettingsCheck check, string text) =>
      Labels(check, text).Select(label => label.GetComponentInParent<Pressable>()).FirstOrDefault(button => button != null);

    /// <summary>A tab of a segmented bar, by its title.</summary>
    private static Pressable Option(SettingsCheck check, string title) =>
      Active<Pressable>(check).FirstOrDefault(button => button.name == "Option " + title);

    private static List<Pressable> Switches(SettingsCheck check) => Active<Pressable>(check).Where(button => button.name == "Switch").ToList();

    private static Pressable SwitchIn(Pressable card) =>
      card.GetComponentsInChildren<Pressable>(false).FirstOrDefault(button => button != card && button.name == "Switch");

    /// <summary>A click, as the pointer would make it: nothing happens while the control is off.</summary>
    private static bool Press(Pressable button)
    {
      if (button == null || !button.IsInteractable())
        return false;
      button.Click?.Invoke();
      return true;
    }

    /// <summary>Types <paramref name="text"/> and ends typing, as Enter would.</summary>
    private static void Type(SettingsCheck check, TMP_InputField field, string text)
    {
      field.text = text;
      field.onEndEdit.Invoke(field.text);
      check.Settle();
    }

    private static bool Truncated(TextMeshProUGUI label)
    {
      if (label == null)
        return true;
      label.ForceMeshUpdate();
      return label.isTextTruncated;
    }

    private static Rect WorldRect(Component component)
    {
      var corners = new Vector3[4];
      ((RectTransform)component.transform).GetWorldCorners(corners);
      return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
  }
}
