using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Configuration.Gameplay;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>Tweaks → Editor: what the selection shows, and fine rotation with event positions.</summary>
internal sealed class EditorTweaksPage : SettingsPage
{
  private readonly List<Button> _selectionOptions = new();
  private readonly List<Button> _editingOptions = new();
  private readonly List<Button> _rotationPresets = new();
  private readonly SettingsSegmentedControl _editorTabs;
  private readonly RectTransform _selectionPane,
    _editingPane,
    _rotationRow;
  private readonly TMP_InputField _rotationInput;
  private readonly TextMeshProUGUI _rotationError;

  public EditorTweaksPage(ISettingsWindow window)
    : base(window, "Editor tweaks")
  {
    EditorTweaksOptions editor = Settings.Gameplay.Editor;
    _editorTabs = Tabs(Root, new[] { L("선택 정보", "Selection"), L("편집 동작", "Editing") }, _ => Window.FitWindow());
    _selectionPane = Ui.Rect("Selection options", Root, 0, 60, 936, 320);
    _editingPane = Ui.Rect("Editing options", Root, 0, 60, 936, 320);
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
    Ui.Bind(() => _selectionOptions[4].interactable = editor.ShowAngle || editor.ShowBeats || editor.ShowDuration);

    _editingOptions.Add(
      Ui.Switch(
        L("정밀 회전", "Fine rotation"),
        (ShortcutKeys.IsMac ? "Cmd + Option" : "Ctrl + Alt")
          + L(" + 쉼표·마침표로 선택한 각도만큼 회전합니다.", " + comma or period rotates by your chosen step."),
        _editingPane,
        0,
        () => editor.FineRotation,
        v =>
        {
          editor.FineRotation = v;
          Window.Changed();
        }
      )
    );
    _rotationRow = Ui.Panel("Rotation step", _editingPane, 0, 100, 936, 76, UiPalette.Surface).rectTransform;
    Ui.Text(L("회전 각도", "Step angle"), _rotationRow, 16, 8, 200, 24, 14, UiPalette.Muted);
    float[] steps = { 22.5f, 15, 7.5f, 1 };
    for (int i = 0; i < steps.Length; i++)
    {
      float step = steps[i];
      _rotationPresets.Add(
        Ui.Button(
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
            Window.Changed();
          }
        )
      );
    }
    _rotationInput = Ui.Input(
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
    _rotationError = Ui.Text("", _editingPane, 16, 190, 900, 24, 13, UiPalette.Error);
    _editingOptions.Add(
      Ui.Switch(
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
          Window.Changed();
        }
      )
    );
    Ui.Bind(() =>
    {
      foreach (Button button in _rotationPresets)
        button.interactable = editor.FineRotation;
      _rotationInput.interactable = editor.FineRotation;
    });
  }

  public override string Title => L("에디터", "Editor tweaks");

  private void AddSelection(string title, string detail, Func<bool> read, Action<bool> write) =>
    _selectionOptions.Add(
      Ui.Switch(
        title,
        detail,
        _selectionPane,
        0,
        read,
        v =>
        {
          write(v);
          Window.Changed();
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
    Settings.Gameplay.Editor.RotationStep = value;
    _rotationError.text = "";
    Window.Changed();
  }

  public override void SettingsReplaced()
  {
    _rotationInput.SetTextWithoutNotify(Settings.Gameplay.Editor.RotationStep.ToString(CultureInfo.InvariantCulture));
    _rotationError.text = "";
  }

  public override void Layout(float width, float height)
  {
    _editorTabs.Place(0, 0, width, 44);
    _selectionPane.gameObject.SetActive(_editorTabs.Selected == 0);
    _editingPane.gameObject.SetActive(_editorTabs.Selected == 1);
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
    Root.sizeDelta = new Vector2(
      width,
      60 + (_editorTabs.Selected == 0 ? _selectionPane.rect.height : _editingPane.rect.height)
    );
  }
}
