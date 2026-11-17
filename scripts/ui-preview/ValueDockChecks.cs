using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.TextEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Mods;
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

  // The input dock's values: the list of places (categories, then one row
  // per mod), the open place's chips with what they read now, and the search.
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

      // The list: each category, a small heading, then one row per mod however many groups it made.
      Settle();
      string[] rows = dock.Root.GetComponentsInChildren<Pressable>(true).Where(row => row.name.StartsWith("Place ")).Select(row => row.name).ToArray();
      Assert(rows.SequenceEqual(folders.Select(folder => "Place " + folder.Key)),
        "the list has each category, then each mod once: " + string.Join(", ", rows));
      Assert(folders.Count(folder => folder.IsMod) == 2 && rows.Count(row => row.StartsWith("Place mod:")) == 2,
        "two mods are two rows, not one per group");
      Pressable tuf = DockPlace(dock, "mod:" + PreviewMod), sample = DockPlace(dock, "mod:Sample"), system = DockPlace(dock, "System");
      Assert(tuf.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == "TUFOverlay")
        && tuf.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == "11"),
        "a mod's row names the mod and counts every value it adds");
      Assert(sample.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == "Sample"),
        "a mod with no name of its own goes by its id");
      TextMeshProUGUI modsHeading = ByName<TextMeshProUGUI>(dock.Root, "Mods heading");
      Assert(modsHeading != null && modsHeading.text == L("모드", "Mods")
        && ScreenRectOf(modsHeading.rectTransform).yMax <= ScreenRectOf((RectTransform)system.transform).yMin + .5f
        && ScreenRectOf((RectTransform)tuf.transform).yMax <= ScreenRectOf(modsHeading.rectTransform).yMin + .5f,
        "a small heading sets the mods apart, under the categories");

      // A place opens its values, lit while open; the next dock opens there again.
      ValueFolderViewModel judgement = values.Categories.Single(folder => folder.Key == "Judgement"),
        tufFolder = values.Mods.Single(folder => folder.Key == "mod:" + PreviewMod);
      Press(DockPlace(dock, "Judgement"));
      Pressable[] chips = DockChips(dock);
      Assert(values.OpenFolder == judgement && chips.Select(chip => chip.name).SequenceEqual(judgement.Chips.Select(chip => chip.Name)),
        "a place shows its values, and only them");
      Assert(chips.All(chip => chip.GetComponent<ValueChipDragInput>() != null), "each chip can be carried out");
      Assert(ByName<TextMeshProUGUI>(dock.Root, "Values title").text == judgement.Name, "the page is named after its place");
      Assert(!judgement.Fill.Value.Equals(tufFolder.Fill.Value) && judgement.Fill.Value.Literal.a == 1
        && judgement.Ink.Value.Equals((ElementUI.UiColor)UiPalette.Foreground),
        "the open place is lit, opaque");
      ValueBrowserViewModel next = new ValueBrowserViewModel(host.Values, NoDockActions.Instance);
      Assert(next.OpenFolder.Key == "Judgement", "the next editor's dock opens on the place opened last");
      next.Dispose();

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
      float start = Time.unscaledTime + 10;
      values.Follow(start);
      long before = GC.GetAllocatedBytesForCurrentThread();
      for (int i = 1; i <= 120; i++)
        values.Follow(start + i);
      for (int i = 0; i < 120; i++)
        dock.Tick();
      long made = GC.GetAllocatedBytesForCurrentThread() - before;
      Assert(made == 0, "following values that didn't change makes nothing: " + made + " bytes");
      SetField(values, "_nextFollow", 0f);
      host.Values.Set(OverlayValueId.SongTitle, "<color=#FF4400>Hello</color> <noparse><</noparse>3\nagain");
      DockChip(dock, "Value song-title");
      FollowNow(values);
      Assert(Reading(dock, "Value song-title") == "Hello <3 again", "a value's markup is left out of its chip: " + Reading(dock, "Value song-title"));
      host.Values.Clear(OverlayValueId.SongTitle);
      FollowNow(values);
      Assert(Reading(dock, "Value song-title") == null, "a value that goes away leaves its name alone");
      Press(DockPlace(dock, "Judgement"));
      CheckValueLabels(dock, "judgment page");
      SaveFrame(camera, texture, canvases, name + "-dock-values");

      // A mod's page: a row per group beside the group's heading.
      Press(tuf);
      chips = DockChips(dock);
      Assert(values.OpenFolder == tufFolder && chips.Length == 11 && chips.All(chip => chip.name.StartsWith("Value " + PreviewMod + "/")),
        "a mod's page holds all it adds");
      string[] headings = korean ? new[] { "레벨", "실시간", "순위" } : new[] { "Level", "Live", "Rank" };
      Rect[] headingRects = headings.Select(heading =>
        ScreenRectOf(dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Single(label => label.text == heading).rectTransform)).ToArray();
      Assert(headingRects[0].yMin > headingRects[1].yMax && headingRects[1].yMin > headingRects[2].yMax,
        "the groups stand one under another, in the order the mod made them");
      float column = headingRects.Max(rect => rect.xMax);
      Assert(chips.All(chip => ScreenRectOf((RectTransform)chip.transform).xMin >= column - .5f), "each group's chips wrap beside its heading");
      Assert(Reading(dock, "Value " + PreviewMod + "/level-id") == "14898" && Reading(dock, "Value " + PreviewMod + "/song") == "Hello (BPM) 2024"
        && Reading(dock, "Value " + PreviewMod + "/difficulty") == null,
        "a mod's chips read what the mod set");
      CheckValueLabels(dock, "mod page");
      SaveFrame(camera, texture, canvases, name + "-dock-mod");

      // The search: every place at once, under the places' names; the open place dims.
      TMP_InputField search = DockSearch(dock);
      Assert(search != null && search.placeholder is TMP_Text hint && hint.text == L("값 찾기", "Find a value"), "the search says what it finds");
      Rect searchRect = ScreenRectOf((RectTransform)search.transform);
      ElementUI.Ugui.RoundedRect searchSurface = dock.Root.GetComponentsInChildren<ElementUI.Ugui.RoundedRect>(false)
        .FirstOrDefault(surface => surface.color.a == 1
          && ScreenRectOf(surface.rectTransform).Contains(searchRect.center)
          && Mathf.Approximately(surface.Radius, surface.rectTransform.rect.height / 2));
      Assert(search.targetGraphic.color.a == 0 && searchSurface != null,
        "the composed search capsule is opaque while its plain input leaves the surface to its container");
      CheckSearchFits(search, name);
      search.text = korean ? "정확" : "acc";
      chips = DockChips(dock);
      Assert(values.Searching.Value && chips.Select(chip => chip.name).SequenceEqual(new[] { "Value accuracy", "Value " + PreviewMod + "/accuracy" }),
        "the search finds values in every place, in the list's order: " + string.Join(", ", chips.Select(chip => chip.name)));
      Assert(tufFolder.Fill.Value.Equals((ElementUI.UiColor)UiPalette.EditorControl) && !tufFolder.Open.Value,
        "the open place dims while the search shows");
      Assert(ByName<TextMeshProUGUI>(dock.Root, "Values title").text == L("검색 결과", "Results")
        && dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Count(label => label.text == judgement.Name) == 2
        && dock.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Count(label => label.text == "TUFOverlay") == 2,
        "what it finds sits under its place's name");
      Assert(ReadingOf(chips[0]) == "99.81", "found chips read their values too");
      CheckValueLabels(dock, "search");
      SaveFrame(camera, texture, canvases, name + "-dock-search");
      search.text = korean ? "실시간" : "live";
      chips = DockChips(dock);
      Assert(chips.Length == 4 && chips.All(chip => chip.name.StartsWith("Value " + PreviewMod + "/")), "a mod's group heading finds its values");
      search.text = "zzz";
      chips = DockChips(dock);
      TextMeshProUGUI nothing = ByName<TextMeshProUGUI>(dock.Root, "Nothing found");
      Assert(chips.Length == 0 && Shown(nothing) && nothing.text.Contains("zzz"), "a search that finds nothing says so: " + (nothing == null ? "" : nothing.text));
      SaveFrame(camera, texture, canvases, name + "-dock-nothing");
      Press(ByName<Pressable>(dock.Root, "Clear value search"));
      chips = DockChips(dock);
      Assert(!values.Searching.Value && search.text == "" && values.OpenFolder == tufFolder && chips.Length == 11 && tufFolder.Open.Value,
        "× clears the search and the open place comes back: searching " + values.Searching.Value + ", text '" + search.text + "', open "
          + values.OpenFolder.Key + ", chips " + chips.Length + ", shown " + tufFolder.Open.Value);
      // Typed over frames, as a player types: the field and the dock agree before Esc.
      search.text = "combo";
      Settle();
      bool searchingBefore = values.Searching.Value;
      SetField(search, "m_WasCanceled", true);
      bool canceled = search.wasCanceled;
      search.onEndEdit.Invoke(search.text);
      SetField(search, "m_WasCanceled", false);
      Settle();
      Assert(searchingBefore && !values.Searching.Value && search.text == "", "Esc clears the search too: before " + searchingBefore
        + ", canceled " + canceled + ", searching " + values.Searching.Value + ", text '" + search.text + "', query '" + values.Query.Value + "'");
      search.text = "x";
      Settle();
      Press(DockPlace(dock, "Time"));
      Settle();
      Assert(!values.Searching.Value && search.text == "" && values.OpenFolder.Key == "Time", "picking a place leaves the search");

      // Enter adds the first value found, at the caret, as a click would.
      OverlayTextDocument document = TextDocumentOf(text).Document;
      int count = document.Count, caret = Field<int>(TextDocumentOf(text), "_caretIndex");
      search.text = korean ? "콤보" : "combo";
      search.onSubmit.Invoke(search.text);
      document = TextDocumentOf(text).Document;
      Assert(document.Count == count + 1 && document[caret].Value == "combo", "Enter in the search adds the first value it found");
      TextDocumentOf(text).Undo();
      Assert(TextDocumentOf(text).Document.Count == count && Field<int>(TextDocumentOf(text), "_caretIndex") == caret, "and undoes as one step");
      Press(ByName<Pressable>(dock.Root, "Clear value search"));

      // While the search has the keyboard the editor leaves keys to it.
      EventSystem.current.SetSelectedGameObject(null);
      EventSystem.current.SetSelectedGameObject(search.gameObject);
      typeof(TMP_InputField).GetMethod("LateUpdate", Private).Invoke(search, null);
      Assert(search.isFocused && values.SearchFocused.Value && dock.IsTyping, "the search has the keys while it is focused");
      EventSystem.current.SetSelectedGameObject(null);
      Assert(!search.isFocused && !values.SearchFocused.Value && dock.IsTyping,
        "ending search focus keeps the terminating key from reaching the editor in the same frame");
      Assert(values.Query.Value == "" && search.text == "" && !values.Searching.Value,
        "clearing the search then focusing and blurring before a render cannot restore its old query");
      EventSystem.current.SetSelectedGameObject(search.gameObject);
      typeof(TMP_InputField).GetMethod("LateUpdate", Private).Invoke(search, null);
      search.text = korean ? "콤보" : "combo";
      int beforeClearedSubmit = TextDocumentOf(text).Document.Count;
      values.ClearSearch.TryExecute();
      search.onSubmit.Invoke(korean ? "콤보" : "combo");
      search.onEndEdit.Invoke(korean ? "콤보" : "combo");
      EventSystem.current.SetSelectedGameObject(null);
      Assert(values.Query.Value == "" && search.text == "" && !values.Searching.Value
        && TextDocumentOf(text).Document.Count == beforeClearedSubmit,
        "clearing a focused search wins through submit followed by end even with stale native event arguments");
      SetField(dock.Panel, "_typedFrame", -1);
      Assert(!dock.IsTyping, "and gives them back once it lets go");
      host.Values.Clear(OverlayValueId.Accuracy);
      host.Values.Clear(OverlayValueId.Combo);
      FollowNow(values);
    }

    // The search's hint and what is typed show whole between the glass and ×: the field clips, it doesn't cut short.
    private static void CheckSearchFits(TMP_InputField search, string where)
    {
      TMP_Text hint = (TMP_Text)search.placeholder;
      float room = search.textViewport.rect.width;
      Assert(hint.GetPreferredValues(hint.text).x <= room + .5f && search.textComponent.GetPreferredValues(search.text).x <= room + .5f,
        where + ": the search's words fit its field: " + hint.text + " / " + search.text + " in " + room);
    }

    // Lets the dock take what its values read now, without waiting for the next look.
    private static void FollowNow(ValueBrowserViewModel values)
    {
      SetField(values, "_nextFollow", 0f);
      values.Follow(Time.unscaledTime);
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
        RenderValueDock(font, live, width, 720, language, name, "Judgement", null);
        if (language == SystemLanguage.Korean ? width == 1280 : width == 540)
        {
          RenderValueDock(font, live, width, 720, language, name + "-mod", "mod:" + PreviewMod, null);
          RenderValueDock(font, live, width, 720, language, name + "-search", "Judgement", language == SystemLanguage.Korean ? "정확" : "perfect");
        }
      }
    }

    private static void RenderValueDock(TMP_FontAsset font, OverlayValues live, int width, int height, SystemLanguage language,
      string name, string place, string query)
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
      ScreenPanel panel = null;
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
        float top = frameHeight - 16 - (FormulaLineView.Height + InputDockView.LineGap + KeypadView.Height);
        TextMeshProUGUI measure = ui.Text("", controls, 0, 0, 10, 10, 13);
        measure.textWrappingMode = TextWrappingModes.NoWrap;
        measure.color = Color.clear;
        Func<string, float> measurer = words => Mathf.Ceil(measure.GetPreferredValues(words).x);
        EditorFrameViewModel notices = new EditorFrameViewModel("Value dock", "", () => { }, () => { }, true);
        InputDockViewModel model = new InputDockViewModel(live, notices, NoDockActions.Instance);
        InputDockView dockView = new InputDockView(model, contentWidth, measurer, true);
        panel = new ScreenPanel("Input dock", controls, Color.clear, false);
        panel.Mount(new ElementBackend(new ElementTheme(UiFonts.Choose(font), true) { Compact = true }), dockView, model, contentWidth);
        panel.Root.anchoredPosition = new Vector2(left, -(top - InputDockView.NoticeHeight));
        model.Show(null, new FormulaEditing(), "", null,
          L("값을 누르면 커서 자리에 넣어요 · 글 속 값이나 크기를 누르면 여기서 계산해요",
            "Click a value to add it at the caret · Click a value in the text or a number to calculate here"));
        Settle();
        Press(ByName<Pressable>(panel.Root, "Place " + place));
        if (query != null)
          ByName<TMP_InputField>(panel.Root, "Value search").text = query;
        Settle();
        panel.Sync();
        Settle();

        // Everything fits: the dock in the window, the values clear of the keypad, no label cut short.
        Rect window = ScreenRectOf(view), dock = ScreenRectOf(panel.Root);
        Pressable[] keys = panel.Root.GetComponentsInChildren<Pressable>(false).Where(key => key.name.StartsWith("Key ")).ToArray();
        float keypad = keys.Min(key => ScreenRectOf((RectTransform)key.transform).xMin);
        Assert(dock.xMin >= window.xMin - .5f && dock.xMax <= window.xMax + .5f && dock.yMin >= window.yMin - .5f,
          name + ": the dock fits the window: " + dock + " / " + window);
        Pressable[] chips = panel.Root.GetComponentsInChildren<Pressable>(false).Where(chip => chip.name.StartsWith("Value ")).ToArray();
        Pressable[] places = panel.Root.GetComponentsInChildren<Pressable>(false).Where(row => row.name.StartsWith("Place ")).ToArray();
        Assert(chips.Length > 0 && places.Length == OverlayValueCatalog.Folders().Count, name + ": the list and the page are drawn");
        float listEdge = places.Max(row => ScreenRectOf((RectTransform)row.transform).xMax);
        foreach (Pressable chip in chips)
        {
          Rect rect = ScreenRectOf((RectTransform)chip.transform);
          Assert(rect.xMin > listEdge && rect.xMax <= keypad - InputDockView.KeypadGap * scale + .5f,
            name + ": a chip stays between the list and the keypad: " + chip.name + " " + rect);
        }
        foreach (TextMeshProUGUI label in panel.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
        {
          if (label.name == "Value now" || label.name == "Formula" || label.richText)
            continue;
          label.ForceMeshUpdate();
          Assert(!label.isTextTruncated, name + ": dock label fits: " + label.text);
        }
        CheckSearchFits(ByName<TMP_InputField>(panel.Root, "Value search"), name);
        SaveFrame(camera, target, new[] { canvas }, name);
      }
      finally
      {
        _refresh = refresh;
        panel?.Dispose();
        ui.Dispose();
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(cameraRoot);
        Object.DestroyImmediate(target);
      }
    }
  }
}
