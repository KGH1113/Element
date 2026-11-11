using System;
using Element.Configuration;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using ElementKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Colors, fonts and shortcuts, picked the way Element's own are.
internal sealed partial class ModEditorPage
{
  private static readonly KeyCode[] Keys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

  // The shortcut part recording the next key, if any.
  private ShortcutCapture _capture;

  public override EditorControl ColorPicker(string label, Color value, Action<Color> changed, bool opacity = true)
  {
    ModEditorControl control = Row("Color", 0);
    float x = Caption(control, label);
    Color current = value;
    if (!opacity)
      current.a = 1;
    TextMeshProUGUI hex = _ui.Text("", control.Rect, x + 40, 0, 160, RowHeight, 14, SettingsUiFactory.Muted);
    hex.textWrappingMode = TextWrappingModes.NoWrap;
    SettingsCapsuleGraphic swatch = null;
    void Show()
    {
      Color opaque = current;
      opaque.a = 1;
      swatch.color = opaque;
      hex.text = "#" + ColorUtility.ToHtmlStringRGB(current) + Percent(opacity ? current.a : 1);
    }
    Button button = null;
    swatch = OverlayEditorUi.Swatch(
      _ui,
      control.Rect,
      "Color " + label,
      x,
      4,
      () =>
        _host.OpenColor(
          new ModColorRequest(
            string.IsNullOrEmpty(label) ? L("색", "Color") : label,
            current,
            opacity,
            (RectTransform)button.transform,
            picked =>
            {
              current = picked;
              Show();
              Color now = current;
              _host.Change(button, () => changed?.Invoke(now), false);
            }
          )
        ),
      _reduceMotion,
      out button
    );
    Show();
    control.Layout = width =>
      SettingsUiFactory.Place(hex.rectTransform, x + 40, 0, Mathf.Max(60, width - x - 40), RowHeight);
    return Add(control);
  }

  public override EditorControl ColorPicker(string label, Paint value, Action<Paint> changed)
  {
    ModEditorControl control = Row("Paint", 0);
    float x = Caption(control, label);
    Paint current = value?.Clone() ?? Paint.Solid(Color.white);
    TextMeshProUGUI shown = _ui.Text("", control.Rect, x + 40, 0, 160, RowHeight, 14, SettingsUiFactory.Muted);
    shown.textWrappingMode = TextWrappingModes.NoWrap;
    shown.overflowMode = TextOverflowModes.Ellipsis;
    SettingsCapsuleGraphic swatch = null;
    void Show()
    {
      Color opaque = current.ToColor(OverlayTheme.Accent);
      opaque.a = 1;
      swatch.color = opaque;
      shown.text =
        current.IsGradient ? L("그라디언트 · 색 ", "Gradient · ") + current.Gradient.Stops.Count + L("개", " colors")
        : current.Highlight ? L("하이라이트 색", "Highlight") + Percent(current.Color.a)
        : "#" + ColorUtility.ToHtmlStringRGB(current.Color) + Percent(current.Color.a);
    }
    Button button = null;
    swatch = OverlayEditorUi.Swatch(
      _ui,
      control.Rect,
      "Paint " + label,
      x,
      4,
      () =>
        _host.OpenPaint(
          new ModPaintRequest(
            string.IsNullOrEmpty(label) ? L("색", "Color") : label,
            current.Clone(),
            (RectTransform)button.transform,
            picked =>
            {
              current = picked?.Clone() ?? Paint.Solid(Color.white);
              Show();
              Paint now = current.Clone();
              _host.Change(button, () => changed?.Invoke(now), false);
            }
          )
        ),
      _reduceMotion,
      out button
    );
    Show();
    control.Layout = width =>
      SettingsUiFactory.Place(shown.rectTransform, x + 40, 0, Mathf.Max(60, width - x - 40), RowHeight);
    return Add(control);
  }

  public override EditorControl FontPicker(string label, string font, Action<string> changed)
  {
    ModEditorControl control = Row("Font", 0);
    float x = Caption(control, label);
    string current = OverlayFontKey.Normalize(font);
    Button button = null;
    TextMeshProUGUI shown = null;
    void Show()
    {
      OverlayFontStatus status =
        current.Length == 0 ? OverlayFontStatus.Default : _host.Fonts?.Status(current) ?? OverlayFontStatus.Missing;
      shown.text =
        current.Length == 0 ? L("기본 폰트", "Default font")
        : status == OverlayFontStatus.Missing
          ? OverlayFonts.DisplayName(current) + L(" · 이 PC에 없음", " · not on this PC")
        : OverlayFonts.DisplayName(current);
      shown.color = status == OverlayFontStatus.Missing ? OverlayEditorUi.Warning : SettingsUiFactory.Foreground;
    }
    button = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "Font " + label,
      "",
      x,
      2,
      240,
      36,
      14,
      () =>
        _host.OpenFont(
          new ModFontRequest(
            string.IsNullOrEmpty(label) ? L("폰트", "Font") : label,
            current,
            (RectTransform)button.transform,
            picked =>
            {
              string next = OverlayFontKey.Normalize(picked);
              if (OverlayFontKey.Same(next, current))
                return;
              current = next;
              Show();
              string now = current;
              _host.Change(button, () => changed?.Invoke(now), false);
            }
          )
        ),
      false,
      _reduceMotion
    );
    Icon(button.transform, LayoutAction.Font, 10, 8, 20, SettingsUiFactory.Muted);
    shown = _ui.Text("", button.transform, 38, 0, 200, 36, 14, SettingsUiFactory.Foreground);
    shown.alignment = TextAlignmentOptions.MidlineLeft;
    shown.textWrappingMode = TextWrappingModes.NoWrap;
    shown.overflowMode = TextOverflowModes.Ellipsis;
    shown.raycastTarget = false;
    Show();
    control.Layout = width =>
    {
      float room = Mathf.Max(120, width - x);
      SettingsUiFactory.Place((RectTransform)button.transform, x, 2, room, 36);
      shown.rectTransform.sizeDelta = new Vector2(room - 50, 36);
    };
    return Add(control);
  }

  public override EditorControl Shortcut(string label, ElementKit.Shortcut value, Action<ElementKit.Shortcut> changed)
  {
    ModEditorControl control = Row("Shortcut", 0);
    float x = Caption(control, label);
    ShortcutCapture capture = new() { Current = value };
    Button field = null;
    field = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "Shortcut " + label,
      "",
      x,
      2,
      200,
      36,
      14,
      () =>
      {
        // A second click stops recording.
        if (_capture == capture)
          StopCapture();
        else
        {
          StopCapture();
          _capture = capture;
          capture.Show();
        }
      },
      false,
      _reduceMotion
    );
    TextMeshProUGUI shown = _ui.Text("", field.transform, 12, 0, 176, 36, 14, SettingsUiFactory.Foreground);
    shown.alignment = TextAlignmentOptions.Midline;
    shown.textWrappingMode = TextWrappingModes.NoWrap;
    shown.overflowMode = TextOverflowModes.Ellipsis;
    shown.raycastTarget = false;
    Button clear = null;
    clear = OverlayEditorUi.CapsuleButton(
      _ui,
      control.Rect,
      "Shortcut clear " + label,
      "",
      x + 204,
      2,
      36,
      36,
      14,
      () =>
      {
        StopCapture();
        capture.Assign(default);
      },
      true,
      _reduceMotion
    );
    LayoutActionIcon cross = Icon(clear.transform, LayoutAction.Close, 8, 8, 20, SettingsUiFactory.Muted);
    capture.Show = () =>
    {
      bool recording = _capture == capture;
      ElementKit.Shortcut current = capture.Current;
      shown.text =
        recording ? capture.Prompt ?? L("새 단축키를 누르세요… (Esc 취소)", "Press a shortcut… (Esc cancels)")
        : current.IsSet ? ShortcutKeys.Label(current.Key, current.Modifier)
        : L("없음", "None");
      shown.color = recording || current.IsSet ? SettingsUiFactory.Foreground : SettingsUiFactory.Muted;
      clear.interactable = current.IsSet;
      // Nothing to clear: the cross fades with its button.
      cross.color = current.IsSet ? SettingsUiFactory.Foreground : new Color(1, 1, 1, .22f);
    };
    capture.Assign = next =>
    {
      bool same = next.Key == capture.Current.Key && next.Modifier == capture.Current.Modifier;
      capture.Current = next;
      capture.Show();
      if (!same)
        _host.Change(field, () => changed?.Invoke(next), false);
    };
    capture.Show();
    control.Layout = width =>
    {
      float room = Mathf.Max(120, width - x - 40);
      SettingsUiFactory.Place((RectTransform)field.transform, x, 2, room, 36);
      shown.rectTransform.sizeDelta = new Vector2(room - 24, 36);
      SettingsUiFactory.Place((RectTransform)clear.transform, x + room + 4, 2, 36, 36);
    };
    return Add(control);
  }

  // While a shortcut part records: the next key with at most one modifier,
  // or Esc to stop, as Element's own shortcut fields do.
  private void TickShortcut()
  {
    ShortcutCapture capture = _capture;
    if (capture == null)
      return;
    if (Input.GetKeyDown(KeyCode.Escape))
    {
      StopCapture();
      return;
    }
    foreach (KeyCode key in Keys)
    {
      if (key == KeyCode.None || key >= KeyCode.Mouse0 || KeyShortcut.IsModifier(key) || !Input.GetKeyDown(key))
        continue;
      KeyCode modifier = ShortcutKeys.HeldModifier(out int count);
      if (count > 1)
      {
        capture.Prompt = L("보조 키는 하나만 함께 누를 수 있어요", "Hold at most one modifier");
        capture.Show();
        return;
      }
      _capture = null;
      capture.Prompt = null;
      capture.Assign(new ElementKit.Shortcut(key, modifier));
      return;
    }
  }

  private void StopCapture()
  {
    ShortcutCapture capture = _capture;
    _capture = null;
    if (capture == null)
      return;
    capture.Prompt = null;
    capture.Show();
  }

  private static string Percent(float alpha) => alpha < .995f ? "  " + Mathf.RoundToInt(alpha * 100) + "%" : "";

  private sealed class ShortcutCapture
  {
    public ElementKit.Shortcut Current;
    public string Prompt;
    public Action Show;
    public Action<ElementKit.Shortcut> Assign;
  }
}
