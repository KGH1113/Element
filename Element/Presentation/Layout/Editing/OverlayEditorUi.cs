using System;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout;

// Pieces shared by the text and shape editors, which float on a dim instead of
// sitting on cards: light, see-through capsules over the dark.
internal static class OverlayEditorUi
{
  public static readonly Color Bar = new(.13f, .13f, .13f, .96f);
  public static readonly Color Warning = new(1, .66f, .53f);

  /// <summary>A see-through capsule button; <paramref name="flat"/> hides it until hovered.</summary>
  public static Button CapsuleButton(
    SettingsUiFactory ui,
    Transform parent,
    string name,
    string text,
    float x,
    float y,
    float width,
    float height,
    float fontSize,
    Action click,
    bool flat,
    bool reduceMotion
  )
  {
    SettingsCapsuleGraphic surface = ui.Capsule(name, parent, x, y, width, height, Color.white);
    surface.raycastTarget = true;
    SettingsButton button = surface.gameObject.AddComponent<SettingsButton>();
    button.navigation = new Navigation { mode = Navigation.Mode.None };
    button.targetGraphic = surface;
    float rest = flat ? 0 : .07f;
    button.SetPalette(
      new ColorBlock
      {
        normalColor = new Color(1, 1, 1, rest),
        highlightedColor = new Color(1, 1, 1, .15f),
        pressedColor = new Color(1, 1, 1, .22f),
        selectedColor = new Color(1, 1, 1, rest),
        disabledColor = new Color(1, 1, 1, flat ? 0 : .03f),
        colorMultiplier = 1,
        fadeDuration = reduceMotion ? 0 : .08f,
      }
    );
    SettingsCursor.Attach(button.gameObject, SettingsCursorKind.Pointer);
    // Let go of the selection, or Enter would press the last clicked key again
    // while it finishes a number.
    button.onClick.AddListener(() =>
    {
      click?.Invoke();
      if (
        button != null
        && EventSystem.current != null
        && EventSystem.current.currentSelectedGameObject == button.gameObject
      )
        EventSystem.current.SetSelectedGameObject(null);
    });
    button.name = name;
    if (text.Length > 0)
    {
      TextMeshProUGUI label = ui.Text(text, surface.transform, 0, 0, width, height, fontSize);
      label.alignment = TextAlignmentOptions.Midline;
      label.textWrappingMode = TextWrappingModes.NoWrap;
      label.overflowMode = TextOverflowModes.Overflow;
      SettingsUiFactory.Stretch(label.rectTransform, 0, 0, 0, 0);
    }
    return button;
  }

  /// <summary>A color dot in a ring, for opening a color picker.</summary>
  public static SettingsCapsuleGraphic Swatch(
    SettingsUiFactory ui,
    Transform parent,
    string name,
    float x,
    float y,
    Action click,
    bool reduceMotion,
    out Button button
  )
  {
    button = CapsuleButton(ui, parent, name, "", x, y, 32, 32, 13, click, true, reduceMotion);
    ui.Capsule("Ring", button.transform, 6, 6, 20, 20, new Color(1, 1, 1, .45f));
    return ui.Capsule("Swatch", button.transform, 8, 8, 16, 16, Color.white);
  }

  public static string CategoryName(Domain.Elements.OverlayValueCategory category) =>
    category switch
    {
      Domain.Elements.OverlayValueCategory.Progress => SettingsLocale.L("진행", "Progress"),
      Domain.Elements.OverlayValueCategory.Judgement => SettingsLocale.L("판정", "Judgment"),
      Domain.Elements.OverlayValueCategory.Time => SettingsLocale.L("시간", "Time"),
      Domain.Elements.OverlayValueCategory.Speed => SettingsLocale.L("속도", "Speed"),
      Domain.Elements.OverlayValueCategory.Song => SettingsLocale.L("곡·기록", "Song"),
      _ => SettingsLocale.L("시스템", "System"),
    };

  /// <summary>
  /// A color picker opens right above the input dock's keypad, in its
  /// column: the keypad below types its opacity and the values beside it
  /// can be dropped on it. On a screen too short for both, it stops under
  /// the top bar.
  /// </summary>
  public static void PlaceAboveKeypad(SettingsColorPicker picker, OverlayInputDock dock)
  {
    if (picker == null || !picker.Visible)
      return;
    Rect keypad = dock.KeypadArea;
    float height = picker.Height;
    float y = Mathf.Max(OverlayEditorFrame.TopBar + 8, dock.Top - 12 - height);
    SettingsUiFactory.Place(picker.Root, keypad.x, y, keypad.width, height);
    picker.Root.SetAsLastSibling();
  }

  /// <summary>
  /// Places a popover right under the control that opened it (frame units),
  /// lined up with its left edge; above it when there is no room below.
  /// </summary>
  public static void PlaceUnder(
    RectTransform popover,
    Rect control,
    float width,
    float height,
    float viewWidth,
    float viewHeight
  )
  {
    float y = control.yMax + 8;
    if (y + height > viewHeight - 8 && control.y - 8 - height >= 8)
      y = control.y - 8 - height;
    SettingsUiFactory.Place(
      popover,
      Mathf.Clamp(control.x, 8, Mathf.Max(8, viewWidth - width - 8)),
      Mathf.Clamp(y, 8, Mathf.Max(8, viewHeight - height - 8)),
      width,
      height
    );
    popover.SetAsLastSibling();
  }

  /// <summary>Whether a screen point (origin bottom left) is over <paramref name="rect"/>.</summary>
  public static bool Contains(RectTransform rect, Vector2 screen) =>
    rect != null
    && rect.gameObject.activeInHierarchy
    && RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null);
}
