using System;
using Element.Configuration;
using Element.Presentation.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

/// <summary>
/// A shortcut shown as keycaps inside a card. Clicking it records the next key
/// pressed with at most one modifier; Escape cancels. One icon action, such as
/// reset or remove, sits beside it.
/// </summary>
internal sealed class SettingsShortcutField
{
  public const float FieldHeight = 44;
  private const float Inset = 16;
  private const float HintGap = 10;
  private const float HintHeight = 48;
  private static readonly KeyCode[] Keys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

  private readonly Func<KeyCode> _key,
    _modifier;
  private readonly Func<KeyCode, KeyCode, string> _assign;
  private readonly string _emptyLabel,
    _modifierExample;
  private readonly bool _actionNeedsShortcut;
  private readonly Action _resized;
  private readonly TextMeshProUGUI _label,
    _modifierKey,
    _modifierSide,
    _keyPlus,
    _mainKey;
  private readonly SettingsKeySymbol _modifierSymbol;
  private int _captureStarted;
  private float _desiredWidth = 240,
    _firstWidth,
    _gap,
    _naturalKeyWidth = 44;

  public RectTransform Frame { get; }
  public Button Capture { get; }
  public Button Action { get; }
  public RectTransform Keycaps { get; }
  public TextMeshProUGUI Hint { get; }
  public bool Capturing { get; private set; }

  /// <param name="assign">Saves a recorded shortcut and returns null, or returns why it cannot be used.</param>
  /// <param name="emptyLabel">Shown when there is no shortcut yet.</param>
  /// <param name="actionNeedsShortcut">The action only applies while a shortcut is set, as removing does.</param>
  /// <param name="resized">The field's height changed; the owner lays out again.</param>
  public SettingsShortcutField(
    SettingsUiFactory ui,
    Transform card,
    Func<KeyCode> key,
    Func<KeyCode> modifier,
    Func<KeyCode, KeyCode, string> assign,
    string actionName,
    LayoutAction actionIcon,
    Action action,
    string modifierExample,
    Action resized,
    string emptyLabel = "",
    bool actionNeedsShortcut = false
  )
  {
    _key = key;
    _modifier = modifier;
    _assign = assign;
    _modifierExample = modifierExample;
    _resized = resized;
    _emptyLabel = emptyLabel;
    _actionNeedsShortcut = actionNeedsShortcut;

    Frame = ui.Panel(
      "Shortcut input border",
      card,
      Inset,
      60,
      240,
      FieldHeight,
      new Color(.3f, .3f, .3f)
    ).rectTransform;
    Capture = ui.Button("", Frame, 1, 1, 238, 42, BeginCapture);
    StyleKeyButton(ui, Capture);
    _label = Capture.GetComponentInChildren<TextMeshProUGUI>();
    // The field is sized to this one line; a rounding wrap would push "…" below.
    _label.textWrappingMode = TextWrappingModes.NoWrap;
    // Registered after the button's own theme so an empty field stays muted.
    ui.Bind(() =>
      _label.color = !Capturing && _key() == KeyCode.None ? SettingsUiFactory.Muted : SettingsUiFactory.Foreground
    );
    Keycaps = ui.Rect("Shortcut keys", Capture.transform, 12, 6, 214, 30);
    SettingsUiFactory.StretchWidth(Keycaps, 12, 12);
    Image modifierCap = ui.Panel("Modifier", Keycaps, 0, 0, 136, 30, new Color(.21f, .21f, .21f));
    _modifierKey = ui.Text("", modifierCap.transform, 8, 0, 120, 30, 15);
    SettingsUiFactory.Stretch(_modifierKey.rectTransform, 8, 0, 8, 0);
    _modifierKey.alignment = TextAlignmentOptions.Midline;
    _modifierSide = ui.Text("L", modifierCap.transform, 8, 0, 14, 30, 11, SettingsUiFactory.Muted);
    _modifierSymbol = ui.Rect("Modifier symbol", modifierCap.transform, 28, 4, 22, 22)
      .gameObject.AddComponent<SettingsKeySymbol>();
    _modifierSymbol.color = SettingsUiFactory.Foreground;
    _modifierSymbol.raycastTarget = false;
    _keyPlus = ui.Text("+", Keycaps, 144, 0, 20, 30, 14, SettingsUiFactory.Muted);
    _keyPlus.alignment = TextAlignmentOptions.Midline;
    Image mainCap = ui.Panel("Key", Keycaps, 172, 0, 80, 30, new Color(.21f, .21f, .21f));
    _mainKey = ui.Text("", mainCap.transform, 8, 0, 64, 30, 15);
    SettingsUiFactory.Stretch(_mainKey.rectTransform, 8, 0, 8, 0);
    _mainKey.alignment = TextAlignmentOptions.Midline;

    Action = ui.Button("", card, 328, 60, FieldHeight, FieldHeight, action);
    Action.gameObject.name = actionName;
    Action.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    LayoutActionIcon icon = ui.Rect(actionName + " icon", Action.transform, 10, 10, 24, 24)
      .gameObject.AddComponent<LayoutActionIcon>();
    icon.Kind = actionIcon;
    icon.color = SettingsUiFactory.Foreground;
    icon.raycastTarget = false;
    StyleKeyButton(ui, Action);

    Hint = ui.Text("", card, Inset, 114, 904, HintHeight, 14, SettingsUiFactory.Muted);
    SettingsUiFactory.StretchWidth(Hint.rectTransform, Inset, Inset);
    Refresh();
  }

  private static bool Korean => RDString.language == SystemLanguage.Korean;

  private static string L(string ko, string en) => Korean ? ko : en;

  public void BeginCapture()
  {
    Capturing = true;
    Keycaps.gameObject.SetActive(false);
    _label.gameObject.SetActive(true);
    _label.color = SettingsUiFactory.Foreground;
    EventSystem.current?.SetSelectedGameObject(null);
    _captureStarted = Time.frameCount;
    _label.text = L("새 단축키를 입력하세요…", "Press a shortcut…");
    Hint.text = L(
      "보조 키 하나와 일반 키를 함께 누를 수 있어요. Esc로 취소합니다.",
      "Use a key with up to one modifier. Esc cancels."
    );
    _resized();
  }

  /// <summary>While capturing, records the next shortcut or cancels on Escape.</summary>
  public void Tick()
  {
    if (!Capturing || Time.frameCount == _captureStarted)
      return;
    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
    {
      Cancel();
      return;
    }
    foreach (KeyCode key in Keys)
    {
      if (
        key == KeyCode.None
        || key >= KeyCode.Mouse0
        || KeyShortcut.IsModifier(key)
        || !UnityEngine.Input.GetKeyDown(key)
      )
        continue;
      KeyCode modifier = ShortcutKeys.HeldModifier(out int count);
      if (count > 1)
      {
        Hint.text =
          L("보조 키는 하나만 선택해주세요. 예: ", "Choose one modifier, for example ") + _modifierExample + L("", ".");
        return;
      }
      TryAssign(key, modifier);
      return;
    }
  }

  /// <summary>Saves the shortcut and ends capture, or keeps capturing and shows why it cannot be used.</summary>
  public bool TryAssign(KeyCode key, KeyCode modifier)
  {
    string error = _assign(key, modifier);
    if (error != null)
    {
      Hint.text = error;
      return false;
    }
    Cancel();
    return true;
  }

  public void Cancel()
  {
    if (!Capturing)
      return;
    Capturing = false;
    Hint.text = "";
    Refresh();
    _resized();
    if (Capture.gameObject.activeInHierarchy)
      EventSystem.current?.SetSelectedGameObject(Capture.gameObject);
  }

  /// <summary>Shows the current shortcut. A field that is recording keeps its prompt.</summary>
  public void Refresh()
  {
    if (Capturing)
      return;
    KeyCode key = _key(),
      modifier = _modifier();
    bool empty = key == KeyCode.None;
    Action.interactable = !_actionNeedsShortcut || !empty;
    _label.text = empty ? _emptyLabel : ShortcutKeys.Label(key, modifier);
    _label.color = empty ? SettingsUiFactory.Muted : SettingsUiFactory.Foreground;
    _label.gameObject.SetActive(empty);
    Keycaps.gameObject.SetActive(!empty);
    if (empty)
    {
      _desiredWidth = _label.preferredWidth + 30;
      return;
    }
    bool hasModifier = modifier != KeyCode.None;
    _modifierKey.transform.parent.gameObject.SetActive(hasModifier);
    _keyPlus.gameObject.SetActive(hasModifier);
    _modifierKey.text = ShortcutKeys.ModifierLabel(modifier);
    bool symbol = hasModifier && ShortcutKeys.IsMac && SettingsKeySymbol.Symbol(modifier).Length > 0;
    _modifierKey.gameObject.SetActive(!symbol);
    _modifierSymbol.gameObject.SetActive(symbol);
    _modifierSide.gameObject.SetActive(symbol);
    _modifierSymbol.SetKey(modifier);
    _modifierSide.text = ShortcutKeys.IsRightModifier(modifier) ? "R" : "L";
    _mainKey.text = ShortcutKeys.KeyLabel(key);
    _firstWidth = hasModifier ? (symbol ? 58 : Mathf.Min(112, _modifierKey.preferredWidth + 20)) : 0;
    _gap = hasModifier ? 28 : 0;
    _naturalKeyWidth = Mathf.Max(44, _mainKey.preferredWidth + 20);
    _desiredWidth = _firstWidth + _gap + _naturalKeyWidth + 26;
  }

  /// <summary>
  /// Places the field <paramref name="top"/> pixels into a card <paramref name="width"/>
  /// wide. The action moves under the field when both do not fit on one line.
  /// </summary>
  /// <returns>The bottom of the field, below the hint while recording.</returns>
  public float Layout(float top, float width)
  {
    float desired = Capturing ? _label.preferredWidth + 30 : _desiredWidth;
    float captureWidth = Mathf.Max(0, Mathf.Min(desired, width - Inset * 2));
    bool stacked = captureWidth + FieldHeight + 8 > width - Inset * 2;
    SettingsUiFactory.Place(Frame, Inset, top, captureWidth, FieldHeight);
    SettingsUiFactory.Place((RectTransform)Capture.transform, 1, 1, Mathf.Max(0, captureWidth - 2), FieldHeight - 2);
    SettingsUiFactory.Place(
      (RectTransform)Action.transform,
      stacked ? Inset : Inset + captureWidth + 8,
      stacked ? top + FieldHeight + 12 : top,
      FieldHeight,
      FieldHeight
    );
    float bottom = top + FieldHeight + (stacked ? FieldHeight + 12 : 0);
    SettingsUiFactory.Place(Hint.rectTransform, Inset, bottom + HintGap, width - Inset * 2, HintHeight);
    float secondWidth = Mathf.Clamp(_naturalKeyWidth, 44, Mathf.Max(44, captureWidth - 26 - _firstWidth - _gap));
    SettingsUiFactory.Place((RectTransform)_modifierKey.transform.parent, 0, 0, _firstWidth, 30);
    SettingsUiFactory.Place(_keyPlus.rectTransform, _firstWidth + 4, 0, 20, 30);
    SettingsUiFactory.Place((RectTransform)_mainKey.transform.parent, _firstWidth + _gap, 0, secondWidth, 30);
    return Capturing ? bottom + HintGap + HintHeight : bottom;
  }

  private static void StyleKeyButton(SettingsUiFactory ui, Button button)
  {
    ui.Bind(() =>
    {
      ColorBlock colors = button.colors;
      colors.normalColor = colors.selectedColor = new Color(.075f, .075f, .075f);
      colors.highlightedColor = new Color(.16f, .16f, .16f);
      colors.pressedColor = new Color(.05f, .05f, .05f);
      ((SettingsButton)button).SetPalette(colors);
    });
  }
}
