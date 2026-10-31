using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

internal sealed partial class SettingsPanel
{
  private readonly List<Button> _tweakLinks = new();
  private readonly List<Button> _selectionOptions = new();
  private readonly List<Button> _editingOptions = new();
  private readonly List<Button> _judgementOptions = new();
  private RectTransform _editorTabs,
    _selectionPane,
    _editingPane,
    _rotationRow,
    _judgementHeader;
  private readonly List<Button> _editorTabButtons = new();
  private int _editorTab;
  private TMP_InputField _rotationInput;
  private TextMeshProUGUI _rotationError;
  private readonly List<Button> _rotationPresets = new();

  private void CreateTweakPages()
  {
    _tweakLinks.Add(
      DetailLink(
        _pages[1].transform,
        L("에디터", "Editor tweaks"),
        L("선택 정보, 정밀 회전, 이벤트 좌표", "Selection info, fine rotation and event positions"),
        () => NavigateTo(4)
      )
    );
    _tweakLinks.Add(
      DetailLink(
        _pages[1].transform,
        L("판정 표시", "Judgment visuals"),
        L("어떤 판정 문구를 보여줄지 선택합니다.", "Choose which judgment popups appear."),
        () => NavigateTo(5)
      )
    );
    _tweakLinks.Add(
      DetailLink(
        _pages[1].transform,
        L("행성", "Planet appearance"),
        L("공, 꼬리, 고리의 색상과 불투명도", "Body, trail and ring color & opacity"),
        () => NavigateTo(6)
      )
    );
    _tweakLinks.Add(
      DetailLink(
        _pages[1].transform,
        L("채터 블로커", "Chatter blocker"),
        L(
          "스위치가 튀어 한 번 누른 키가 두 번 입력되는 것을 막습니다.",
          "Stops a bouncing switch from counting one press twice."
        ),
        () => NavigateTo(ChatterPage)
      )
    );
    CreateEditorPage();
    CreateJudgementPage();
    CreatePlanetPage();
    CreateChatterPage();
  }

  private Button DetailLink(Transform parent, string title, string description, Action action)
  {
    Button link = _ui.Button(title, parent, 0, 0, 936, 80, action);
    link.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    var label = _ui.Text(title, link.transform, 20, 13, 820, 26, 18);
    SettingsUiFactory.StretchWidth(label.rectTransform, 20, 64);
    var detail = _ui.Text(description, link.transform, 20, 43, 820, 24, 14, SettingsUiFactory.Muted);
    SettingsUiFactory.StretchWidth(detail.rectTransform, 20, 64);
    var arrow = _ui.Rect("Open", link.transform, 892, 28, 20, 24).gameObject.AddComponent<SettingsNavigationIcon>();
    arrow.Kind = 3;
    arrow.raycastTarget = false;
    arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1, .5f);
    arrow.rectTransform.pivot = new Vector2(1, .5f);
    arrow.rectTransform.anchoredPosition = new Vector2(-20, 0);
    _ui.Bind(() => arrow.color = _ui.SoftAccent);
    return link;
  }

  private RectTransform Tabs(Transform parent, string[] names, List<Button> buttons, Func<int> read, Action<int> write)
  {
    RectTransform root = _ui.Panel("Sections", parent, 0, 0, 936, 44, new Color(.085f, .085f, .085f)).rectTransform;
    for (int i = 0; i < names.Length; i++)
    {
      int index = i;
      Button tab = _ui.Button(
        names[i],
        root,
        4,
        4,
        120,
        36,
        () =>
        {
          write(index);
          _ui.RefreshTheme();
        }
      );
      tab.GetComponentInChildren<TextMeshProUGUI>().fontSize = 14;
      buttons.Add(tab);
      _ui.Bind(() =>
      {
        bool selected = read() == index;
        ColorBlock colors = tab.colors;
        colors.normalColor = colors.selectedColor = selected ? _ui.AccentSurface : new Color(.085f, .085f, .085f);
        colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, .1f);
        ((SettingsButton)tab).SetPalette(colors);
        tab.GetComponentInChildren<TextMeshProUGUI>().color = selected ? _ui.SoftAccent : SettingsUiFactory.Muted;
      });
    }
    return root;
  }

  private static void LayoutTabs(RectTransform root, List<Button> buttons, float width, float y = 0)
  {
    SettingsUiFactory.Place(root, 0, y, width, 44);
    float w = (width - 8) / buttons.Count;
    for (int i = 0; i < buttons.Count; i++)
      SettingsUiFactory.Place((RectTransform)buttons[i].transform, 4 + i * w, 4, w, 36);
  }

  private void CreateEditorPage()
  {
    EditorTweaksOptions editor = _settings.Gameplay.Editor;
    Transform page = _pages[4].transform;
    _editorTabs = Tabs(
      page,
      new[] { L("선택 정보", "Selection"), L("편집 동작", "Editing") },
      _editorTabButtons,
      () => _editorTab,
      value =>
      {
        _editorTab = value;
        FitWindow();
      }
    );
    _selectionPane = _ui.Rect("Selection options", page, 0, 60, 936, 320);
    _editingPane = _ui.Rect("Editing options", page, 0, 60, 936, 320);
    AddSelection(
      L("타일 각도", "Tile angle"),
      L("선택한 타일의 각도 합계", "Total angle of selected tiles"),
      () => editor.ShowAngle,
      v => editor.ShowAngle = v
    );
    AddSelection(
      L("박자 수", "Beat count"),
      L("선택 구간의 박자 합계", "Beats in your selection"),
      () => editor.ShowBeats,
      v => editor.ShowBeats = v
    );
    AddSelection(
      L("타일 수", "Tile count"),
      L("선택한 타일의 개수", "Number of selected tiles"),
      () => editor.ShowCount,
      v => editor.ShowCount = v
    );
    AddSelection(
      L("구간 시간", "Duration"),
      L("선택 구간의 재생 시간", "Playback time of the selection"),
      () => editor.ShowDuration,
      v => editor.ShowDuration = v
    );
    AddSelection(
      L("마지막 간격 제외", "Exclude last interval"),
      L(
        "각도·박자·시간 계산에서 마지막 타일 다음 간격을 뺍니다.",
        "Count angle, beats and time up to the last selected tile."
      ),
      () => editor.ExcludeLastInterval,
      v => editor.ExcludeLastInterval = v
    );
    _ui.Bind(() => _selectionOptions[4].interactable = editor.ShowAngle || editor.ShowBeats || editor.ShowDuration);

    _editingOptions.Add(
      _ui.Switch(
        L("정밀 회전", "Fine rotation"),
        (IsMac ? "Cmd + Option" : "Ctrl + Alt")
          + L(" + 쉼표·마침표로 선택한 각도만큼 회전합니다.", " + comma or period rotates by your chosen step."),
        _editingPane,
        0,
        () => editor.FineRotation,
        v =>
        {
          editor.FineRotation = v;
          Changed();
        }
      )
    );
    _rotationRow = _ui.Panel("Rotation step", _editingPane, 0, 100, 936, 76, SettingsUiFactory.Surface).rectTransform;
    _ui.Text(L("회전 각도", "Step angle"), _rotationRow, 16, 8, 200, 24, 14, SettingsUiFactory.Muted);
    float[] steps = { 22.5f, 15, 7.5f, 1 };
    for (int i = 0; i < steps.Length; i++)
    {
      float step = steps[i];
      _rotationPresets.Add(
        _ui.Button(
          step.ToString(CultureInfo.InvariantCulture) + "°",
          _rotationRow,
          16 + i * 72,
          36,
          64,
          36,
          () =>
          {
            editor.RotationStep = step;
            _rotationInput.SetTextWithoutNotify(step.ToString(CultureInfo.InvariantCulture));
            _rotationError.text = "";
            Changed();
          }
        )
      );
    }
    _rotationInput = _ui.Input(
      "Custom angle",
      editor.RotationStep.ToString(CultureInfo.InvariantCulture),
      _rotationRow,
      320,
      34,
      92,
      _ => _rotationError.text = "",
      CommitRotationStep,
      6
    );
    _rotationError = _ui.Text("", _editingPane, 16, 190, 900, 24, 13, new Color(1, .66f, .53f));
    _editingOptions.Add(
      _ui.Switch(
        L("이벤트 좌표 함께 변환", "Transform event positions"),
        L(
          "타일을 뒤집거나 회전할 때 카메라·타일 좌표도 맞춥니다.",
          "Keep camera and track event positions aligned when flipping or rotating tiles."
        ),
        _editingPane,
        228,
        () => editor.SyncEventPositions,
        v =>
        {
          editor.SyncEventPositions = v;
          Changed();
        }
      )
    );
    _ui.Bind(() =>
    {
      foreach (Button button in _rotationPresets)
        button.interactable = editor.FineRotation;
      _rotationInput.interactable = editor.FineRotation;
    });
  }

  private void AddSelection(string title, string detail, Func<bool> read, Action<bool> write) =>
    _selectionOptions.Add(
      _ui.Switch(
        title,
        detail,
        _selectionPane,
        0,
        read,
        v =>
        {
          write(v);
          Changed();
        }
      )
    );

  private void CommitRotationStep(string text)
  {
    if (
      !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
      || float.IsNaN(value)
      || value < .1f
      || value > 180
    )
    {
      _rotationError.text = L("0.1°부터 180° 사이의 각도를 입력하세요.", "Enter an angle between 0.1° and 180°.");
      return;
    }
    _settings.Gameplay.Editor.RotationStep = value;
    _rotationError.text = "";
    Changed();
  }

  private void CreateJudgementPage()
  {
    var j = _settings.Gameplay.Judgement;
    j.Normalize();
    Transform page = _pages[5].transform;
    _judgementHeader = _ui.Rect("Judgment controls", page, 0, 0, 936, 52);
    var hint = _ui.Text(
      L("표시할 판정을 선택하세요.", "Choose the popups to show."),
      _judgementHeader,
      0,
      0,
      580,
      40,
      15,
      SettingsUiFactory.Muted
    );
    SettingsUiFactory.StretchWidth(hint.rectTransform, 0, 232);
    _ui.Button(L("모두 표시", "Show all"), _judgementHeader, 704, 0, 108, 40, () => SetAllJudgements(false));
    _ui.Button(L("모두 숨김", "Hide all"), _judgementHeader, 824, 0, 108, 40, () => SetAllJudgements(true));
    JudgmentOption("Early Perfect", new Color(.7f, .9f, .35f), () => j.HideEarlyPerfect, v => j.HideEarlyPerfect = v);
    JudgmentOption("Perfect - / +", new Color(.7f, .9f, .35f), () => j.HidePerfect, v => j.HidePerfect = v);
    JudgmentOption("XPerfect", new Color(.5f, .9f, .4f), () => j.HideXPerfect, v => j.HideXPerfect = v);
    JudgmentOption("Late Perfect", new Color(.7f, .9f, .35f), () => j.HideLatePerfect, v => j.HideLatePerfect = v);
    JudgmentOption("Too Early", new Color(1, .36f, .3f), () => j.HideTooEarly, v => j.HideTooEarly = v);
    JudgmentOption("Early", new Color(1, .6f, .4f), () => j.HideEarly, v => j.HideEarly = v);
    JudgmentOption("Late", new Color(1, .6f, .4f), () => j.HideLate, v => j.HideLate = v);
    JudgmentOption("Too Late", new Color(1, .36f, .3f), () => j.HideTooLate, v => j.HideTooLate = v);
    JudgmentOption("Miss", new Color(.85f, .5f, 1), () => j.HideMiss, v => j.HideMiss = v);
    JudgmentOption("Overload", new Color(.85f, .5f, 1), () => j.HideOverload, v => j.HideOverload = v);
  }

  private void JudgmentOption(string title, Color tint, Func<bool> hidden, Action<bool> write)
  {
    Button tile = _ui.Button(
      title,
      _pages[5].transform,
      0,
      0,
      300,
      86,
      () =>
      {
        write(!hidden());
        Changed();
      }
    );
    tile.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    _ui.Capsule("Judgment color", tile.transform, 16, 21, 8, 8, tint);
    var name = _ui.Text(title, tile.transform, 36, 12, 244, 28, 16);
    SettingsUiFactory.StretchWidth(name.rectTransform, 36, 16);
    var state = _ui.Text("", tile.transform, 36, 46, 244, 24, 13);
    SettingsUiFactory.StretchWidth(state.rectTransform, 36, 16);
    _ui.Bind(() =>
    {
      state.text = hidden() ? L("숨김", "Hidden") : L("표시", "Shown");
      state.color = hidden() ? SettingsUiFactory.Muted : _ui.SoftAccent;
      name.color = hidden() ? SettingsUiFactory.Muted : SettingsUiFactory.Foreground;
    });
    _judgementOptions.Add(tile);
  }

  private void SetAllJudgements(bool hidden)
  {
    var j = _settings.Gameplay.Judgement;
    j.HidePerfect =
      j.HideXPerfect =
      j.HideEarlyPerfect =
      j.HideLatePerfect =
      j.HideTooEarly =
      j.HideEarly =
      j.HideLate =
      j.HideTooLate =
      j.HideMiss =
      j.HideOverload =
        hidden;
    Changed();
  }

  private void LayoutTweakPages(float width)
  {
    float linkHeight = width < 520 ? 100 : 80;
    for (int i = 0; i < _tweakLinks.Count; i++)
    {
      var link = _tweakLinks[i];
      SettingsUiFactory.Place((RectTransform)link.transform, 0, i * (linkHeight + 12), width, linkHeight);
      var labels = link.GetComponentsInChildren<TextMeshProUGUI>();
      // The hidden button caption is excluded from this active-only query.
      SettingsUiFactory.Place(labels[1].rectTransform, 20, 43, width - 84, linkHeight - 51);
    }
    float tweaksTop = _tweakLinks.Count * (linkHeight + 12);
    bool wide = width >= 760;
    float toggleWidth = wide ? (width - 12) / 2 : width;
    for (int i = 0; i < _tweakRows.Count; i++)
      SettingsUiFactory.Place(
        (RectTransform)_tweakRows[i].transform,
        wide ? i * (toggleWidth + 12) : 0,
        tweaksTop + (wide ? 0 : i * 100),
        toggleWidth,
        88
      );
    ((RectTransform)_pages[1].transform).sizeDelta = new Vector2(
      width,
      tweaksTop + (wide ? 88 : _tweakRows.Count * 100 - 12)
    );

    LayoutTabs(_editorTabs, _editorTabButtons, width);
    _selectionPane.gameObject.SetActive(_editorTab == 0);
    _editingPane.gameObject.SetActive(_editorTab == 1);
    int columns = width >= 600 ? 2 : 1;
    float cell = (width - (columns - 1) * 12) / columns;
    float row = width >= 600 ? 88 : 100;
    for (int i = 0; i < 4; i++)
      SettingsUiFactory.Place(
        (RectTransform)_selectionOptions[i].transform,
        i % columns * (cell + 12),
        i / columns * (row + 12),
        cell,
        row
      );
    float selectionBottom = (4 / columns) * (row + 12);
    SettingsUiFactory.Place((RectTransform)_selectionOptions[4].transform, 0, selectionBottom, width, 100);
    SettingsUiFactory.Place(_selectionPane, 0, 60, width, selectionBottom + 100);
    SettingsUiFactory.Place((RectTransform)_editingOptions[0].transform, 0, 0, width, row);
    float angleHeight = width < 480 ? 128 : 88;
    SettingsUiFactory.Place(_rotationRow, 0, row + 12, width, angleHeight);
    for (int i = 0; i < _rotationPresets.Count; i++)
      SettingsUiFactory.Place(
        (RectTransform)_rotationPresets[i].transform,
        16 + i * (width < 480 ? 58 : 72),
        36,
        width < 480 ? 52 : 64,
        36
      );
    SettingsUiFactory.Place(
      (RectTransform)_rotationInput.transform,
      width < 480 ? 16 : 320,
      width < 480 ? 80 : 34,
      92,
      40
    );
    SettingsUiFactory.Place(_rotationError.rectTransform, 16, row + angleHeight + 14, width - 32, 28);
    float syncTop = row + angleHeight + 52;
    SettingsUiFactory.Place((RectTransform)_editingOptions[1].transform, 0, syncTop, width, width < 520 ? 132 : 100);
    SettingsUiFactory.Place(_editingPane, 0, 60, width, syncTop + (width < 520 ? 132 : 100));
    ((RectTransform)_pages[4].transform).sizeDelta = new Vector2(
      width,
      60 + (_editorTab == 0 ? _selectionPane.rect.height : _editingPane.rect.height)
    );

    bool compact = width < 600;
    SettingsUiFactory.Place(_judgementHeader, 0, 0, width, compact ? 92 : 52);
    TextMeshProUGUI hint = _judgementHeader.GetComponentInChildren<TextMeshProUGUI>();
    SettingsUiFactory.Place(hint.rectTransform, 0, 0, compact ? width : width - 244, 40);
    Button[] actions = _judgementHeader.GetComponentsInChildren<Button>();
    for (int i = 0; i < actions.Length; i++)
      SettingsUiFactory.Place(
        (RectTransform)actions[i].transform,
        compact ? i * 120 : width - 232 + i * 120,
        compact ? 44 : 0,
        108,
        40
      );
    int count =
      width >= 660 ? 3
      : width >= 440 ? 2
      : 1;
    float judgmentWidth = (width - (count - 1) * 12) / count;
    for (int i = 0; i < _judgementOptions.Count; i++)
      SettingsUiFactory.Place(
        (RectTransform)_judgementOptions[i].transform,
        i % count * (judgmentWidth + 12),
        _judgementHeader.rect.height + i / count * 98,
        judgmentWidth,
        86
      );
    ((RectTransform)_pages[5].transform).sizeDelta = new Vector2(
      width,
      _judgementHeader.rect.height + Mathf.CeilToInt((float)_judgementOptions.Count / count) * 98 - 12
    );
    LayoutPlanetPage(width);
    LayoutChatterPage(width);
  }
}
