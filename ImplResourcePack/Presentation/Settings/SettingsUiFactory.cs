using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Settings;

// Retained uGUI controls. Textures, sprites and theme bindings are created once, never in Update.
internal sealed class SettingsUiFactory : IDisposable
{
  public static readonly Color Background = new(0.067f, 0.067f, 0.067f, 1f);
  public static readonly Color Surface = new(0.115f, 0.115f, 0.115f, 1f);
  public static readonly Color Muted = new(0.66f, 0.66f, 0.66f, 1f);
  public static readonly Color Foreground = new(0.96f, 0.96f, 0.96f, 1f);
  private readonly TMP_FontAsset _font;
  private readonly Texture2D _texture;
  private readonly Sprite _rounded;
  private readonly List<Action> _themeBindings = new();
  public Color Accent => Overlay.Shared.OverlayTheme.Accent;
  public Color OnAccent => SettingsPalette.OnAction;
  public Color SoftAccent => SettingsPalette.SoftAccent;
  public Color AccentSurface => SettingsPalette.AccentSurface;
  public Func<bool> ReduceMotion = () => false;

  public SettingsUiFactory(TMP_FontAsset font)
  {
    SettingsCursor.Retain();
    _font = font;
    const int size = 40;
    const float radius = 16f;
    _texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Settings rounded corners" };
    Color[] pixels = new Color[size * size];
    for (int y = 0; y < size; y++)
    for (int x = 0; x < size; x++)
    {
      float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius));
      float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius));
      float distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
      pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - distance));
    }
    _texture.SetPixels(pixels);
    _texture.Apply(false, true);
    _rounded = Sprite.Create(
      _texture,
      new Rect(0, 0, size, size),
      new Vector2(.5f, .5f),
      100,
      0,
      SpriteMeshType.FullRect,
      new Vector4(18, 18, 18, 18)
    );
  }

  public RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
  {
    RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
    rect.SetParent(parent, false);
    rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
    rect.pivot = new Vector2(0, 1);
    rect.anchoredPosition = new Vector2(x, -y);
    rect.sizeDelta = new Vector2(width, height);
    return rect;
  }

  public Image Panel(
    string name,
    Transform parent,
    float x,
    float y,
    float width,
    float height,
    Color color,
    bool rounded = true
  )
  {
    Image image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
    image.color = color;
    if (rounded)
    {
      image.sprite = _rounded;
      image.type = Image.Type.Sliced;
      image.pixelsPerUnitMultiplier = 2f;
    }
    image.raycastTarget = false;
    return image;
  }

  public TextMeshProUGUI Text(
    string text,
    Transform parent,
    float x,
    float y,
    float width,
    float height,
    float size = 16,
    Color? color = null
  )
  {
    TextMeshProUGUI label = Rect(text, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
    label.font = _font;
    label.text = text;
    label.fontSize = size;
    // MapleStory's supplied face is already bold; don't synthesize extra weight.
    label.fontWeight = FontWeight.Regular;
    label.color = color ?? Foreground;
    label.raycastTarget = false;
    label.richText = false;
    label.textWrappingMode = TextWrappingModes.Normal;
    label.overflowMode = TextOverflowModes.Ellipsis;
    label.alignment = TextAlignmentOptions.MidlineLeft;
    return label;
  }

  public Button Button(
    string text,
    Transform parent,
    float x,
    float y,
    float width,
    float height,
    Action onClick,
    bool primary = false,
    bool themed = true
  )
  {
    Image image = Panel(text, parent, x, y, width, height, Surface);
    image.color = Color.white;
    image.raycastTarget = true;
    image.gameObject.AddComponent<CanvasGroup>();
    SettingsButton button = image.gameObject.AddComponent<SettingsButton>();
    button.navigation = new Navigation { mode = Navigation.Mode.None };
    button.targetGraphic = image;
    SettingsCursor.Attach(button.gameObject, SettingsCursorKind.Pointer);
    AddFocusRing(image);
    TextMeshProUGUI label = Text(text, image.transform, 12, 0, width - 24, height, 16);
    Stretch(label.rectTransform, 12, 0, 12, 0);
    label.alignment = TextAlignmentOptions.Midline;
    button.onClick.AddListener(() => onClick());
    if (!themed)
      return button;
    Bind(() =>
    {
      Color fill = primary ? AccentSurface : Surface;
      image.color = Color.white;
      ColorBlock colors = button.colors;
      colors.normalColor = fill;
      colors.highlightedColor = Color.Lerp(fill, Foreground, .13f);
      // Keyboard focus has its own ring; selection must not look like hover.
      colors.selectedColor = fill;
      colors.pressedColor = Color.Lerp(fill, Color.black, .2f);
      colors.disabledColor = Color.Lerp(fill, Background, .45f);
      colors.fadeDuration = ReduceMotion() ? 0 : .1f;
      button.SetPalette(colors);
      label.color = primary ? SettingsPalette.SoftAccent : Foreground;
    });
    return button;
  }

  public Button IconButton(string name, Transform parent, float x, float y, float size, Action onClick, int iconKind)
  {
    Button button = Button("", parent, x, y, size, size, onClick, themed: false);
    button.name = name;
    button.targetGraphic.color = Color.clear;
    button.transition = Selectable.Transition.None;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    float iconSize = Mathf.Min(24, size - 12);
    SettingsNavigationIcon icon = Rect(
      name + " icon",
      button.transform,
      (size - iconSize) * .5f,
      (size - iconSize) * .5f,
      iconSize,
      iconSize
    )
      .gameObject.AddComponent<SettingsNavigationIcon>();
    icon.Kind = iconKind;
    icon.raycastTarget = false;
    Outline focus = icon.gameObject.AddComponent<Outline>();
    focus.effectDistance = new Vector2(1.25f, -1.25f);
    focus.enabled = false;
    button.GetComponent<SettingsFocusRing>().Initialize(focus);
    Bind(() => focus.effectColor = SoftAccent);
    SettingsIconInteraction feedback = button.gameObject.AddComponent<SettingsIconInteraction>();
    feedback.Initialize(button, icon, ReduceMotion);
    Bind(() => feedback.SetPalette(Foreground, SoftAccent));
    return button;
  }

  public Button Switch(string title, string description, Transform parent, float y, Func<bool> read, Action<bool> write)
  {
    Button row = Button(
      title,
      parent,
      0,
      y,
      640,
      88,
      () =>
      {
        write(!read());
        RefreshTheme();
      }
    );
    row.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    StretchWidth((RectTransform)row.transform, 0, 0);
    // Center the title and description as one intrinsic-height group. A wrapped
    // description should grow the group equally above and below the switch.
    RectTransform copy = Rect("Switch copy", row.transform, 16, 0, 532, 0);
    copy.anchorMin = new Vector2(0, .5f);
    copy.anchorMax = new Vector2(1, .5f);
    copy.pivot = new Vector2(0, .5f);
    copy.sizeDelta = new Vector2(-108, 0);
    VerticalLayoutGroup stack = copy.gameObject.AddComponent<VerticalLayoutGroup>();
    stack.spacing = 8;
    stack.childControlWidth = stack.childControlHeight = true;
    stack.childForceExpandWidth = true;
    stack.childForceExpandHeight = false;
    ContentSizeFitter fit = copy.gameObject.AddComponent<ContentSizeFitter>();
    fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    Text(title, copy, 0, 0, 532, 24, 17);
    Text(description, copy, 0, 0, 532, 24, 14, Muted);
    SettingsCapsuleGraphic track = Capsule("Switch", row.transform, 0, 0, 48, 28, Surface);
    track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(1, .5f);
    track.rectTransform.pivot = new Vector2(0, .5f);
    track.rectTransform.anchoredPosition = new Vector2(-64, 0);
    SettingsCapsuleGraphic thumb = Capsule("Thumb", track.transform, 4, 4, 20, 20, Foreground);
    SettingsSwitchMotion motion = thumb.gameObject.AddComponent<SettingsSwitchMotion>();
    Bind(() =>
    {
      bool enabled = read();
      track.color = enabled ? AccentSurface : new Color(.3f, .3f, .3f);
      thumb.color = enabled ? SoftAccent : Foreground;
      motion.Set(enabled ? 24 : 4, ReduceMotion());
    });
    return row;
  }

  public TMP_InputField Input(
    string name,
    string value,
    Transform parent,
    float x,
    float y,
    float width,
    Action<string> changed,
    Action<string> committed = null,
    int characterLimit = 7
  )
  {
    Image box = Panel(name, parent, x, y, width, 40, new Color(.28f, .28f, .28f));
    box.raycastTarget = true;
    Image inset = Panel("Input surface", box.transform, 1, 1, width - 2, 38, new Color(.075f, .075f, .075f));
    Stretch(inset.rectTransform, 1, 1, 1, 1);
    TMP_InputField input = box.gameObject.AddComponent<TMP_InputField>();
    SettingsCursor.Attach(input.gameObject, SettingsCursorKind.Text);
    input.navigation = new Navigation { mode = Navigation.Mode.None };
    RectTransform viewport = Rect("Text area", box.transform, 10, 4, width - 20, 32);
    Stretch(viewport, 10, 4, 10, 4);
    viewport.gameObject.AddComponent<RectMask2D>();
    TextMeshProUGUI text = Text(value, viewport, 0, 0, width - 20, 32, 16);
    // TMP's Geometry/Midline caret is centered on local Y=0, even when the
    // text rect uses a top pivot. Middle alignment derives caret, selection,
    // and IME positions from the same ascender/descender as the visible text.
    text.alignment = TextAlignmentOptions.Left;
    // The viewport clips input while TMP scrolls it to the caret. Ellipsis is
    // appropriate for labels, but would replace editable characters here.
    text.overflowMode = TextOverflowModes.Overflow;
    Stretch(text.rectTransform, 0, 0, 0, 0);
    input.textViewport = viewport;
    input.textComponent = text;
    input.targetGraphic = box;
    AddFocusRing(box);
    input.lineType = TMP_InputField.LineType.SingleLine;
    input.characterLimit = characterLimit;
    input.richText = false;
    input.text = value;
    input.onValueChanged.AddListener(v => changed?.Invoke(v));
    input.onEndEdit.AddListener(v => committed?.Invoke(v));
    Bind(() =>
    {
      input.selectionColor = Color.Lerp(Surface, SoftAccent, .3f);
      input.caretColor = Foreground;
    });
    return input;
  }

  public Slider Slider(string name, Transform parent, float y, float value, Action<float> changed) =>
    Slider(name, parent, 68, y, 540, value, changed);

  public Slider Slider(
    string name,
    Transform parent,
    float x,
    float y,
    float width,
    float value,
    Action<float> changed,
    float step = 1f / 255f
  )
  {
    RectTransform root = Rect(name, parent, x, y, width, 36);
    SettingsSlider slider = root.gameObject.AddComponent<SettingsSlider>();
    SettingsCursor.Attach(slider.gameObject, SettingsCursorKind.Horizontal);
    slider.Step = step;
    slider.navigation = new Navigation { mode = Navigation.Mode.None };
    Image hit = root.gameObject.AddComponent<Image>();
    hit.color = Color.clear;
    // Track and thumb use the same endpoints; caps keep a true half-height radius.
    SettingsCapsuleGraphic track = Capsule("Track", root, 10, 15, width - 20, 6, new Color(.28f, .28f, .28f));
    StretchWidth(track.rectTransform, 10, 10);
    RectTransform fillArea = Rect("Fill area", root, 10, 15, width - 20, 6);
    StretchWidth(fillArea, 10, 10);
    SettingsCapsuleGraphic fill = Capsule("Fill", fillArea, 0, 0, width - 20, 6, SoftAccent);
    fill.rectTransform.anchorMin = Vector2.zero;
    fill.rectTransform.anchorMax = Vector2.one;
    fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
    RectTransform handleArea = Rect("Handle area", root, 10, 8, width - 20, 20);
    StretchWidth(handleArea, 10, 10);
    SettingsCapsuleGraphic handle = Capsule("Handle", handleArea, 0, 0, 20, 20, Foreground);
    handle.rectTransform.pivot = new Vector2(.5f, .5f);
    handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, .5f);
    handle.rectTransform.anchoredPosition = Vector2.zero;
    handle.rectTransform.sizeDelta = new Vector2(20, 0);
    slider.fillRect = fill.rectTransform;
    slider.handleRect = handle.rectTransform;
    slider.targetGraphic = handle;
    AddFocusRing(handle, root.gameObject);
    slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
    slider.value = value;
    slider.onValueChanged.AddListener(v => changed(v));
    Bind(() => fill.color = SoftAccent);
    return slider;
  }

  public SettingsCapsuleGraphic Capsule(
    string name,
    Transform parent,
    float x,
    float y,
    float width,
    float height,
    Color color
  )
  {
    SettingsCapsuleGraphic graphic = Rect(name, parent, x, y, width, height)
      .gameObject.AddComponent<SettingsCapsuleGraphic>();
    graphic.color = color;
    graphic.raycastTarget = false;
    return graphic;
  }

  public void Bind(Action update)
  {
    _themeBindings.Add(update);
    update();
  }

  private void AddFocusRing(Graphic image, GameObject owner = null)
  {
    Outline outline = image.gameObject.AddComponent<Outline>();
    outline.effectDistance = new Vector2(2, -2);
    outline.enabled = false;
    (owner ?? image.gameObject).AddComponent<SettingsFocusRing>().Initialize(outline);
    Bind(() => outline.effectColor = SoftAccent);
  }

  public void RefreshTheme()
  {
    foreach (Action binding in _themeBindings)
      binding();
  }

  public void Dispose()
  {
    SettingsCursor.Release();
    _themeBindings.Clear();
    Destroy(_rounded);
    Destroy(_texture);
  }

  internal static void Destroy(Object value)
  {
    if (value == null)
      return;
    if (UnityEngine.Application.isPlaying)
      Object.Destroy(value);
    else
      Object.DestroyImmediate(value);
  }

  public static void Place(RectTransform rect, float x, float y, float width, float height)
  {
    rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
    rect.pivot = new Vector2(0, 1);
    rect.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
    rect.sizeDelta = new Vector2(Mathf.Round(width), Mathf.Round(height));
  }

  public static void StretchWidth(RectTransform rect, float left, float right)
  {
    rect.anchorMin = new Vector2(0, 1);
    rect.anchorMax = new Vector2(1, 1);
    rect.offsetMin = new Vector2(left, rect.offsetMin.y);
    rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
  }

  public static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
  {
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.offsetMin = new Vector2(left, bottom);
    rect.offsetMax = new Vector2(-right, -top);
  }
}

internal sealed class SettingsSlider : Slider
{
  public float Step = 1f / 255f;

  public override void OnMove(AxisEventData eventData)
  {
    if (!IsActive() || !IsInteractable())
      return;
    float delta = eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Down ? -Step : Step;
    value += delta * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10 : 1);
    eventData.Use();
  }
}

internal sealed class SettingsSwitchMotion : MonoBehaviour
{
  private float _target = float.NaN;

  public void Set(float target, bool immediate)
  {
    if (_target == target && !immediate)
      return;
    RectTransform rect = (RectTransform)transform;
    bool first = float.IsNaN(_target);
    _target = target;
    if (immediate || first)
      rect.anchoredPosition = new Vector2(target, -4);
    enabled = !immediate && !first;
  }

  private void Update()
  {
    RectTransform rect = (RectTransform)transform;
    float x = Mathf.Lerp(rect.anchoredPosition.x, _target, 1 - Mathf.Exp(-24 * Time.unscaledDeltaTime));
    if (Mathf.Abs(x - _target) < .05f)
    {
      x = _target;
      enabled = false;
    }
    rect.anchoredPosition = new Vector2(x, -4);
  }
}

// Theme changes must not start a new hover tween on every slider event. Ordinary
// pointer/focus transitions still use Button's normal animation between states.
internal sealed class SettingsButton : Button
{
  private bool _hovered;
  private CanvasGroup _disabledGroup;
  private static int _pointerSelectionFrame = -1;
  internal static bool PointerSelectedThisFrame => _pointerSelectionFrame == Time.frameCount;

  protected override void Awake()
  {
    base.Awake();
    _disabledGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
  }

  protected override void OnEnable()
  {
    _hovered = false;
    base.OnEnable();
  }

  protected override void OnDisable()
  {
    _hovered = false;
    base.OnDisable();
  }

  public override void OnPointerEnter(PointerEventData data)
  {
    _hovered = true;
    base.OnPointerEnter(data);
  }

  public override void OnPointerExit(PointerEventData data)
  {
    _hovered = false;
    base.OnPointerExit(data);
  }

  public override void OnPointerDown(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      _pointerSelectionFrame = Time.frameCount;
    base.OnPointerDown(data);
  }

  public override void OnPointerClick(PointerEventData data)
  {
    // Navigation callbacks select the next page's first control without
    // forwarding PointerEventData. Preserve the source of that selection.
    if (data.button == PointerEventData.InputButton.Left)
      _pointerSelectionFrame = Time.frameCount;
    base.OnPointerClick(data);
  }

  protected override void DoStateTransition(SelectionState state, bool instant)
  {
    // Unity keeps a clicked control selected after the pointer leaves it.
    // Reopened pages can also carry cached pointer state from before hiding.
    if (state == SelectionState.Selected || state == SelectionState.Highlighted)
      state = _hovered ? SelectionState.Highlighted : SelectionState.Normal;
    if (_disabledGroup == null)
      _disabledGroup = GetComponent<CanvasGroup>();
    if (_disabledGroup != null)
      _disabledGroup.alpha = state == SelectionState.Disabled ? .5f : 1;
    base.DoStateTransition(state, instant);
  }

  public void SetPalette(ColorBlock palette)
  {
    colors = palette;
    DoStateTransition(currentSelectionState, true);
  }
}

internal sealed class SettingsFocusRing : MonoBehaviour, ISelectHandler, IDeselectHandler
{
  private Outline _outline;

  public void Initialize(Outline outline) => _outline = outline;

  public void OnSelect(BaseEventData eventData)
  {
    if (_outline != null)
      _outline.enabled = eventData is not PointerEventData && !SettingsButton.PointerSelectedThisFrame;
  }

  public void OnDeselect(BaseEventData eventData)
  {
    if (_outline != null)
      _outline.enabled = false;
  }

  private void OnDisable()
  {
    if (_outline != null)
      _outline.enabled = false;
  }
}
