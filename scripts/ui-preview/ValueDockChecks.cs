using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.TextEditor;
using ElementUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Overlay.Values;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementKit.Hosting;
using ElementKit.Values;
using ElementUI.Ugui;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Editor
{
  // A mod's values as ElementKit finds them in its static fields: three
  // groups, some set by the mod and some still waiting for a level.
  public static class PreviewTufValues
  {
    public static readonly ValueGroup Level = new ValueGroup("레벨", "Level");
    public static readonly NumberValue LevelId = Level.Number("level-id", "레벨 ID", "Level ID", 14898);
    public static readonly TextValue Song = Level.Text("song", "곡", "Song", "Hello (BPM) 2024");
    public static readonly TextValue Artist = Level.Text("artist", "아티스트", "Artist", "Camellia");
    public static readonly NumberValue Difficulty = Level.Number("difficulty", "난이도", "Difficulty", 21.5);
    public static readonly ValueGroup Live = new ValueGroup("실시간", "Live");
    public static readonly NumberValue Score = Live.Number("score", "실시간 점수", "Live score", 7041.28);
    public static readonly NumberValue Speed = Live.Number("speed", "배속", "Speed", 1);
    public static readonly NumberValue Accuracy = Live.Number("accuracy", "실시간 정확도", "Live accuracy", 99.22);
    public static readonly TimeValue Left = Live.Time("time-left", "남은 시간", "Time left", 63);
    public static readonly ValueGroup Rank = new ValueGroup("순위", "Rank");
    public static readonly NumberValue Expected = Rank.Number("expected-rank", "예상 순위", "Expected rank", 6);
    public static readonly NumberValue FirstScore = Rank.Number("first-score", "1위 점수", "First place score", 8914.18);
    public static readonly NumberValue NextGap = Rank.Number("next-gap", "다음 순위까지", "To the next rank", 31.07);
  }

  // The input dock's values: only those that fit where values go, under
  // words naming that place and a count of what was left out; the list of
  // places (categories, then one row per mod), the open place's chips with
  // what they read now, and the search.
  public static partial class SettingsPreview
  {
    private const string PreviewMod = "TUFOverlay";
    private static ModValues _previewMods;

    /// <summary>Attaches the preview's mod through ElementKit, as its Load would, and brings its values in.</summary>
    private static void AttachPreviewMods(OverlayValues values)
    {
      KitRegistry.Attach(PreviewMod, typeof(PreviewTufValues).Assembly, () => true, Debug.LogWarning, "TUFOverlay");
      PreviewTufValues.LevelId.Set(14898);
      PreviewTufValues.Song.Set("Hello (BPM) 2024");
      PreviewTufValues.Artist.Set("Camellia");
      PreviewTufValues.Score.Set(7041.28);
      PreviewTufValues.Speed.Set(1);
      PreviewTufValues.Expected.Set(6);
      _previewMods ??= new ModValues(Debug.LogWarning);
      _previewMods.Pump(values);
      Assert(OverlayValueCatalog.Folders().Count(folder => folder.Source == PreviewMod) == 1
        && OverlayValueCatalog.Sections().Count(section => section.Group != null && section.Group.Source == PreviewMod) == 3,
        "the preview's mod adds three groups through ElementKit");
    }

    private static void CheckValueDock(TextEditorScreen text, DockProbe dock, OverlayCanvasHost host, Camera camera,
      RenderTexture texture, Canvas[] canvases, string name)
    {
      bool korean = Korean(name);
      ValueBrowserViewModel values = dock.Values;
      List<OverlayValueFolder> folders = OverlayValueCatalog.Folders();

      // At the text's caret: what a text shows, a section under a section.
      // Colors and true or false don't show in a text and words are typed
      // there, so those are left out, and so is a section that holds nothing
      // else; after the values, how many that was.
      Settle();
      string[] sections = dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Where(label => label.name.StartsWith("Section "))
        .Select(label => label.name).ToArray();
      Assert(sections.SequenceEqual(folders.Where(folder => folder.Key != "Color").Select(folder => "Section " + folder.Key)),
        "at the caret every category with something a text shows has its section, in order, then each mod once: " + string.Join(", ", sections));
      Assert(!dock.Root.GetComponentsInChildren<TMP_InputField>(true).Any(field => field.name == "Value search")
        && !dock.Root.GetComponentsInChildren<Pressable>(true).Any(row => row.name.StartsWith("Place ")),
        "the values are one list of sections: no search field, no list of places beside it");
      Assert(values.Listing.Value && ByName<TextMeshProUGUI>(dock.Root, "Values title").text == L("넣을 수 있는 값", "Values that fit")
        && TypeMarkOf(dock) == "*",
        "a short title heads the values, after the mark of anything");
      Assert(!dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.name == "Values hidden" || label.name == "Values rest"),
        "no sentence counts what was left out or says what to do");
      ValueFolderViewModel make = values.Sections.Single(folder => folder.Key == "Make");
      string[] makeChips = DockChips(dock).Where(chip => make.Chips.Any(value => value.Name == chip.name)).Select(chip => chip.name).ToArray();
      Assert(makeChips.SequenceEqual(new[] { "Value make-number", "Value make-condition" }),
        "at the caret a number or a condition is made: no words, no true or false, no color, no group: " + string.Join(", ", makeChips));
      Assert(folders.Count(folder => folder.IsMod) == 2 && sections.Count(row => row.StartsWith("Section mod:")) == 2,
        "two mods are two sections, not one per group");
      Assert(DockSection(dock, "mod:" + PreviewMod).text == "TUFOverlay" && DockSection(dock, "mod:Sample").text == "Sample",
        "a mod's section is headed by the mod's name; one with no name of its own goes by its id");

      // A section's heading stands over its own values, and every value leads with its type's mark.
      ValueFolderViewModel judgement = values.Sections.Single(folder => folder.Key == "Judgement"),
        tufFolder = values.Sections.Single(folder => folder.Key == "mod:" + PreviewMod);
      Rect judgementHeading = ScreenRectOf(DockSection(dock, "Judgement").rectTransform),
        timeHeading = ScreenRectOf(DockSection(dock, "Time").rectTransform);
      Pressable[] chips = DockChips(dock);
      Pressable[] judgementChips = chips.Where(chip => judgement.Chips.Any(value => value.Name == chip.name)).ToArray();
      Assert(judgementChips.Length == judgement.Chips.Count
        && judgementChips.All(chip => ScreenRectOf((RectTransform)chip.transform).yMax <= judgementHeading.yMin + .5f
          && ScreenRectOf((RectTransform)chip.transform).yMin >= timeHeading.yMax - .5f),
        "a section's values stand under its heading and over the next section's");
      Assert(chips.All(chip => ExecuteEvents.GetEventHandler<IDragHandler>(chip.gameObject)?.GetComponent<DragSource>() == null),
        "no chip can be carried out: picking one is the only way in");
      Assert(chips.All(chip => ScreenRectOf(ByName<TextMeshProUGUI>(chip.transform, "Value name").rectTransform).xMin
          > ScreenRectOf((RectTransform)chip.transform).xMin + 16),
        "every value leads with its type's mark, its name after it");

      // More sections than fit: the list's bottom fades into its base, and its top once scrolled down.
      ScrollRect scroll = ByName<ScrollRect>(dock.Root, "Values");
      UiEdgeFadeGraphic topFade = ByName<UiEdgeFadeGraphic>(scroll.transform, "Top fade"),
        bottomFade = ByName<UiEdgeFadeGraphic>(scroll.transform, "Bottom fade");
      // What the scroll view reports each frame its content, frame or place changed.
      Action scrolled = () => scroll.onValueChanged.Invoke(scroll.normalizedPosition);
      scrolled();
      float overflow = scroll.content.rect.height - ((RectTransform)scroll.transform).rect.height;
      Assert(overflow > UiScrollCues.Reach, "the preview's values are more than fit: " + overflow);
      Assert(bottomFade.enabled && bottomFade.Strength == 1 && !topFade.enabled
        && bottomFade.transform.GetSiblingIndex() > scroll.content.GetSiblingIndex()
        && ScreenRectOf(bottomFade.rectTransform).yMin <= ScreenRectOf((RectTransform)scroll.transform).yMin + .5f,
        "at the top, the list's bottom edge fades over the values, its top doesn't");
      scroll.verticalNormalizedPosition = .5f;
      scrolled();
      Assert(topFade.enabled && bottomFade.enabled, "scrolled halfway, both edges fade");
      scroll.verticalNormalizedPosition = 0;
      scrolled();
      Assert(topFade.Strength == 1 && !bottomFade.enabled, "at the bottom, only the top fades");
      UiCapsuleGraphic bar = ByName<UiCapsuleGraphic>(scroll.transform, "Scroll bar");
      Rect listRect = ScreenRectOf((RectTransform)scroll.transform), barAtBottom = ScreenRectOf(bar.rectTransform);
      scroll.verticalNormalizedPosition = 1;
      scrolled();
      Rect barAtTop = ScreenRectOf(bar.rectTransform);
      Assert(bar.enabled && bar.transform.GetSiblingIndex() > bottomFade.transform.GetSiblingIndex()
        && barAtTop.yMax <= listRect.yMax && barAtTop.yMax > listRect.yMax - 8 && barAtBottom.yMin >= listRect.yMin
        && barAtBottom.yMin < listRect.yMin + 8 && barAtTop.height < listRect.height * .95f
        && barAtTop.xMax <= listRect.xMax && barAtTop.xMax > listRect.xMax - 8,
        "a slim bar along the list's right edge runs from top to bottom as it scrolls, as long as the share that shows: "
          + barAtTop + " / " + barAtBottom + " in " + listRect);

      // Chips show what their values read now, a few times a second; unchanged values make nothing.
      host.Values.Set(OverlayValueId.Accuracy, new StringBuilder("99.81"), 99.81);
      host.Values.Set(OverlayValueId.Combo, new StringBuilder("9"), 9);
      FollowNow(values);
      Assert(Reading(dock, "Value accuracy") == "99.81" && Reading(dock, "Value combo") == "9",
        "a chip shows its value as the overlay reads it: " + Reading(dock, "Value accuracy"));
      Assert(Reading(dock, "Value too-early") == null, "a value the game has no reading for shows its name alone");
      float narrow = ScreenRectOf((RectTransform)DockChip(dock, "Value combo").transform).width;
      host.Values.Set(OverlayValueId.Combo, new StringBuilder("412"), 412);
      FollowNow(values);
      float wide = ScreenRectOf((RectTransform)DockChip(dock, "Value combo").transform).width;
      host.Values.Set(OverlayValueId.Combo, new StringBuilder("88"), 88);
      FollowNow(values);
      float kept = ScreenRectOf((RectTransform)DockChip(dock, "Value combo").transform).width;
      Assert(Reading(dock, "Value combo") == "88" && wide > narrow && Mathf.Abs(kept - wide) < .5f,
        "a number keeps the room it grew to, so the chips after it don't shift: " + narrow + " / " + wide + " / " + kept);
      values.Follow();
      long before = GC.GetAllocatedBytesForCurrentThread();
      for (int i = 1; i <= 120; i++)
      {
        values.Follow();
        // The live timeline ticks: the chips look again and find nothing new.
        ElementUI.Runtime.Flush(.25f);
      }
      for (int i = 0; i < 120; i++)
        dock.Tick();
      long made = GC.GetAllocatedBytesForCurrentThread() - before;
      Assert(made == 0, "following values that didn't change makes nothing: " + made + " bytes");
      host.Values.Set(OverlayValueId.SongTitle, "<color=#FF4400>Hello</color> <noparse><</noparse>3\nagain");
      FollowNow(values);
      Assert(Reading(dock, "Value song-title") == "Hello <3 again", "a value's markup is left out of its chip: " + Reading(dock, "Value song-title"));
      host.Values.Clear(OverlayValueId.SongTitle);
      FollowNow(values);
      Assert(Reading(dock, "Value song-title") == null, "a value that goes away leaves its name alone");
      CheckValueLabels(dock, "values");
      SaveFrame(camera, texture, canvases, name + "-dock-values");

      // A mod's section: a row per group under the group's heading.
      Pressable[] tufChips = DockChips(dock).Where(chip => chip.name.StartsWith("Value " + PreviewMod + "/")).ToArray();
      Assert(tufChips.Length == 11 && tufFolder.Chips.Count == 11, "a mod's section holds all it adds");
      string[] headings = korean ? new[] { "레벨", "실시간", "순위" } : new[] { "Level", "Live", "Rank" };
      Rect[] headingRects = headings.Select(heading =>
        ScreenRectOf(dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Single(label => label.text == heading).rectTransform)).ToArray();
      Assert(headingRects[0].yMin > headingRects[1].yMax && headingRects[1].yMin > headingRects[2].yMax,
        "the groups stand one under another, in the order the mod made them");
      Assert(tufChips.All(chip => ScreenRectOf((RectTransform)chip.transform).yMax <= headingRects[0].yMin + .5f),
        "a mod's values stand under its groups' headings");
      Assert(Reading(dock, "Value " + PreviewMod + "/level-id") == "14898" && Reading(dock, "Value " + PreviewMod + "/song") == "Hello (BPM) 2024"
        && Reading(dock, "Value " + PreviewMod + "/difficulty") == null,
        "a mod's chips read what the mod set");
      scroll.verticalNormalizedPosition = 0;
      scrolled();
      SaveFrame(camera, texture, canvases, name + "-dock-mod");
      scroll.verticalNormalizedPosition = 1;
      scrolled();

      // A value pressed goes in at the caret, and undoes as one step.
      OverlayTextDocument document = TextDocumentOf(text).Document;
      int count = document.Count, caret = Field<int>(TextDocumentOf(text), "_caretIndex");
      Press(DockChip(dock, "Value combo"));
      document = TextDocumentOf(text).Document;
      Assert(document.Count == count + 1 && document[caret].Value == "combo", "a value pressed in the list goes in at the caret");
      TextDocumentOf(text).Undo();
      Assert(TextDocumentOf(text).Document.Count == count && Field<int>(TextDocumentOf(text), "_caretIndex") == caret, "and undoes as one step");
      host.Values.Clear(OverlayValueId.Accuracy);
      host.Values.Clear(OverlayValueId.Combo);
      FollowNow(values);
      CheckNumberValues(text, dock, camera, texture, canvases, name);
    }

    // A number field lists numbers only: a section with none, such as
    // colors, is left out, the song title isn't listed, and the words say so.
    private static void CheckNumberValues(TextEditorScreen text, DockProbe dock, Camera camera, RenderTexture texture,
      Canvas[] canvases, string name)
    {
      ValueBrowserViewModel values = dock.Values;
      PressBar(text, Field<TextSize>(text, "_size").Button);
      Settle();
      Assert(dock.Owner == "size" && values.Type.Value == OverlayValueType.Number && TypeMarkOf(dock) == "#",
        "opening the size, the mark over the values is a number's");
      Assert(DockSection(dock, "Color") == null && DockSection(dock, "Song") != null,
        "colors aren't listed for a number; the song's section is, for its numbers");
      Assert(DockChips(dock).All(chip => chip.name != "Value song-title") && DockChips(dock).Any(chip => chip.name == "Value attempt"),
        "the song title isn't listed for a number, the attempts are");
      CheckValueLabels(dock, "number field");
      SaveFrame(camera, texture, canvases, name + "-dock-number");
      Call(text, "CloseOpen");
      Call(text, "Render");
      Settle();
    }

    /// <summary>The sign in the mark over the values, as drawn now: "#", "*"; null while none shows.</summary>
    private static string TypeMarkOf(DockProbe dock) => TypeMarkIn(dock.Root);

    // "Any" has no glyph in the UI font: its mark is drawn, and reads here as "*".
    private static string TypeMarkIn(Transform root) =>
      root.GetComponentsInChildren<RectTransform>(false).Where(mark => mark.name == "Values type")
        .Select(mark => mark.GetComponentInChildren<UiIcon>() is { Kind: UiIconKind.AnyType } ? "*" : mark.GetComponentInChildren<TextMeshProUGUI>()?.text)
        .FirstOrDefault();

    // Lets the dock take what its values read now, without waiting for the next look.
    private static void FollowNow(ValueBrowserViewModel values)
    {
      values.Follow();
      Settle();
    }

    /// <summary>What a value's chip shows after its name; null while it shows its name alone.</summary>
    private static string Reading(DockProbe dock, string chip) => ReadingOf(DockChip(dock, chip));

    private static string ReadingOf(Pressable chip)
    {
      TextMeshProUGUI reading = ByName<TextMeshProUGUI>(chip.transform, "Value now");
      return Shown(reading) ? reading.text : null;
    }

    // The list's names and counts, the page's title, headings and chip names fit; a reading may end in an ellipsis.
    private static void CheckValueLabels(DockProbe dock, string where)
    {
      Settle();
      foreach (TextMeshProUGUI label in dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        if (label.name == "Value now" || label.name == "Formula" || label.richText)
          continue;
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "dock label fits (" + where + "): " + label.text);
      }
    }

    // The dock alone at the editor's sizes, as the editor places it: wide,
    // narrow and compact windows in both languages, a place, a mod and a search.
    private static void RenderValueDocks()
    {
      TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/MAPLESTORY_OTF_BOLD SDF.asset");
      OverlayValues live = new OverlayValues();
      AttachPreviewMods(live);
      void Number(OverlayValueId id, string shown, double number) => live.Set(id, new StringBuilder(shown), number);
      Number(OverlayValueId.ProgressStart, "0", 0);
      Number(OverlayValueId.ProgressCurrent, "48", 48);
      Number(OverlayValueId.Accuracy, "99.81", 99.81);
      Number(OverlayValueId.Overload, "0", 0);
      Number(OverlayValueId.TooEarly, "0", 0);
      Number(OverlayValueId.VeryEarly, "3", 3);
      Number(OverlayValueId.EarlyPerfect, "20", 20);
      Number(OverlayValueId.PerfectEarly, "41", 41);
      Number(OverlayValueId.PurePerfect, "1500", 1500);
      Number(OverlayValueId.PerfectLate, "38", 38);
      Number(OverlayValueId.LatePerfect, "16", 16);
      Number(OverlayValueId.VeryLate, "2", 2);
      Number(OverlayValueId.TooLate, "0", 0);
      Number(OverlayValueId.Miss, "0", 0);
      Number(OverlayValueId.Combo, "412", 412);
      Number(OverlayValueId.TimingScale, "100", 100);
      Number(OverlayValueId.CurrentTime, "1:27", 87);
      Number(OverlayValueId.TotalTime, "3:48", 228);
      Number(OverlayValueId.TileBpm, "180", 180);
      Number(OverlayValueId.CurrentBpm, "180", 180);
      Number(OverlayValueId.KeysPerSecond, "6.4", 6.4);
      live.Set(OverlayValueId.SongTitle, "<color=#FFAA00>Camellia</color> - Hello (BPM) 2024");
      Number(OverlayValueId.Attempt, "9", 9);
      Number(OverlayValueId.FullAttempt, "3799", 3799);
      Number(OverlayValueId.Fps, "240", 240);
      foreach (int width in new[] { 1280, 760, 540 })
      foreach (SystemLanguage language in new[] { SystemLanguage.Korean, SystemLanguage.English })
      {
        string name = "value-dock-" + (language == SystemLanguage.Korean ? "ko" : "en") + "-" + width;
        RenderValueDock(font, live, width, 720, language, name, true);
        if (language == SystemLanguage.Korean ? width == 1280 : width == 540)
          RenderValueDock(font, live, width, 720, language, name + "-rest", false);
      }
    }

    private static void RenderValueDock(TMP_FontAsset font, OverlayValues live, int width, int height, SystemLanguage language,
      string name, bool open)
    {
      RDString.language = language;
      GameObject root = new GameObject("Value dock fixture", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
      Canvas canvas = root.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      GameObject cameraRoot = new GameObject("Value dock camera", typeof(Camera));
      Camera camera = cameraRoot.GetComponent<Camera>();
      camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .13f, .2f);
      RenderTexture target = new RenderTexture(width, height, 24);
      camera.targetTexture = target;
      UiFactory ui = new UiFactory(font);
      ScreenPanel panel = null, method = null;
      Action refresh = _refresh;
      _refresh = Settle;
      try
      {
        // As the editor stands: the level faintly behind a deep dim, controls scaled to the window.
        RectTransform view = ui.Rect("View", root.transform, 0, 0, width, height);
        ui.Text("X-Accuracy: 100.0000%   Perfect   XPerfect: 1", view, 40, height - 300, width - 80, 200, 64, new Color(1, 1, 1, .55f));
        ui.Panel("Dim", view, 0, 0, width, height, new Color(0, 0, 0, .92f), false);
        float scale = Mathf.Clamp(Mathf.Min(width / 1120f, height / 760f), .55f, 2f);
        float frameWidth = width / scale, frameHeight = height / scale;
        RectTransform controls = ui.Rect("Controls", view, 0, 0, frameWidth, frameHeight);
        controls.localScale = Vector3.one * scale;
        float contentWidth = Mathf.Min(1600, frameWidth - EditorFramePanel.Margin * 2), left = (frameWidth - contentWidth) * .5f;
        // Two columns: the line and the values on the left, the input method on the right.
        float dockWidth = contentWidth - KeypadView.Width - InputDockView.ColumnGap;
        float top = frameHeight - 16 - InputDockView.Height;
        TextMeshProUGUI measure = ui.Text("", controls, 0, 0, 10, 10, 13);
        measure.textWrappingMode = TextWrappingModes.NoWrap;
        measure.color = Color.clear;
        Func<string, float> measurer = words => Mathf.Ceil(measure.GetPreferredValues(words).x);
        // A number field is open: the list holds what goes in a number.
        FormulaEditing editing = new FormulaEditing();
        OverlayVariable typed = new OverlayVariable(OverlayPiece.NumberPiece("160"));
        if (open)
          editing.Activate(new FormulaSlot("width", OverlayValueType.Number, () => typed, variable => typed = variable));
        InputDockViewModel model = new InputDockViewModel(live, editing, NoDockActions.Instance, true);
        InputDockView dockView = new InputDockView(model, dockWidth);
        panel = new ScreenPanel("Input dock", controls, Color.clear, false);
        panel.Mount(
          new ElementBackend(new ElementTheme(UiFonts.Choose(font), true) { Compact = true }),
          dockView.Environment(EditorEnvironment.Measure, measurer).Environment(EditorEnvironment.ReduceMotion, true),
          model,
          dockWidth
        );
        panel.Root.anchoredPosition = new Vector2(left, -top);
        method = new ScreenPanel("Input method", controls, Color.clear, false);
        method.Mount(
          new ElementBackend(new ElementTheme(UiFonts.Choose(font), true) { Compact = true }),
          new InputMethodView(model).Environment(EditorEnvironment.ReduceMotion, true),
          null,
          KeypadView.Width
        );
        method.Root.anchoredPosition = new Vector2(left + contentWidth - KeypadView.Width, -top);
        if (open)
          model.Show("width", L("너비", "Width"), "160 px");
        else
          model.Show(null, "", null);
        Settle();
        if (!open)
        {
          // Nothing open: no values and no words; the keys rest.
          Assert(!panel.Root.GetComponentsInChildren<Pressable>(false).Any(chip => chip.name.StartsWith("Value "))
            && !panel.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text.Length > 0)
            && method.Root.GetComponentsInChildren<Pressable>(false).All(key => !key.IsInteractable()),
            name + ": with nothing open the dock is clear, and Remove and Apply rest");
          SaveFrame(camera, target, new[] { canvas }, name);
          return;
        }
        Settle();
        panel.Sync();
        method.Sync();
        Settle();

        // Everything fits: the dock in the window, the values clear of the keypad, no label cut short.
        Rect window = ScreenRectOf(view), dock = ScreenRectOf(panel.Root), right = ScreenRectOf(method.Root);
        Pressable[] keys = method.Root.GetComponentsInChildren<Pressable>(false).Where(key => key.name.StartsWith("Key ")).ToArray();
        float keypad = keys.Min(key => ScreenRectOf((RectTransform)key.transform).xMin);
        Assert(dock.xMin >= window.xMin - .5f && right.xMax <= window.xMax + .5f && dock.yMin >= window.yMin - .5f && right.yMin >= window.yMin - .5f
          && dock.xMax < right.xMin && Mathf.Abs(dock.yMax - right.yMax) < 1 && Mathf.Abs(dock.yMin - right.yMin) < 1,
          name + ": the two columns fit the window side by side, as tall as each other: " + dock + " / " + right + " / " + window);
        Pressable[] chips = panel.Root.GetComponentsInChildren<Pressable>(false).Where(chip => chip.name.StartsWith("Value ")).ToArray();
        string[] sections = panel.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Where(label => label.name.StartsWith("Section "))
          .Select(label => label.name).ToArray();
        Assert(chips.Length > 0 && sections.SequenceEqual(model.Values.Sections.Where(section => section.Shown.Value).Select(section => "Section " + section.Key))
          && !sections.Contains("Section Color"),
          name + ": the list holds the sections with numbers: " + string.Join(", ", sections));
        Assert(model.Values.Type.Value == OverlayValueType.Number && ByName<TextMeshProUGUI>(panel.Root, "Values hidden") == null,
          name + ": the mark over the values is a number's, and nothing counts what was left out");
        foreach (Pressable chip in chips)
        {
          Rect rect = ScreenRectOf((RectTransform)chip.transform);
          Assert(rect.xMin >= dock.xMin - .5f && rect.xMax <= dock.xMax + .5f && rect.xMax < keypad,
            name + ": a chip stays in the left column: " + chip.name + " " + rect);
        }
        foreach (TextMeshProUGUI label in panel.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
        {
          if (label.name == "Value now" || label.name == "Formula" || label.richText)
            continue;
          label.ForceMeshUpdate();
          Assert(!label.isTextTruncated, name + ": dock label fits: " + label.text);
        }
        SaveFrame(camera, target, new[] { canvas }, name);
      }
      finally
      {
        _refresh = refresh;
        panel?.Dispose();
        method?.Dispose();
        ui.Dispose();
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraRoot);
        Object.DestroyImmediate(target);
      }
    }
  }
}
