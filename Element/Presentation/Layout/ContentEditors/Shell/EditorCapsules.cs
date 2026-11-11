using System;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

// Buttons of the full-screen editors, which float on a dim instead of
// sitting on cards: light, see-through capsules over the dark.
internal static class EditorCapsules
{
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
}
