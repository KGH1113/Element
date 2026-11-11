using System;
using System.Collections.Generic;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Presentation.Ui.Widgets;

// Retained uGUI controls. The rounded sprite is shared; theme bindings are created once, never in Update.
internal sealed class UiFactory : IDisposable
{
  private readonly TMP_FontAsset _font;
  private readonly Sprite _rounded;
  private readonly List<Action> _themeBindings = new();
  private bool _disposed;
  public Color Accent => UiTheme.Accent;
  public Color OnAccent => UiPalette.OnAction;
  public Color SoftAccent => UiPalette.SoftAccent;
  public Color AccentSurface => UiPalette.AccentSurface;
  public Func<bool> ReduceMotion = () => false;

  public UiFactory(TMP_FontAsset font)
  {
    UiCursor.Retain();
    _font = font;
    _rounded = UiRoundedSprite.Retain();
  }

  /// <summary>A factory whose motion follows <paramref name="reduceMotion"/>, as the editors use it.</summary>
  public UiFactory(TMP_FontAsset font, Func<bool> reduceMotion)
    : this(font) => ReduceMotion = reduceMotion;

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
    label.color = color ?? UiPalette.Foreground;
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
    Image image = Panel(text, parent, x, y, width, height, UiPalette.Surface);
    image.color = Color.white;
    image.raycastTarget = true;
    image.gameObject.AddComponent<CanvasGroup>();
    UiButton button = image.gameObject.AddComponent<UiButton>();
    button.navigation = new Navigation { mode = Navigation.Mode.None };
    button.targetGraphic = image;
    UiCursor.Attach(button.gameObject, UiCursorKind.Pointer);
    AddFocusRing(image);
    TextMeshProUGUI label = Text(text, image.transform, 12, 0, width - 24, height, 16);
    Stretch(label.rectTransform, 12, 0, 12, 0);
    label.alignment = TextAlignmentOptions.Midline;
    button.onClick.AddListener(() => onClick());
    if (!themed)
      return button;
    Bind(() =>
    {
      Color fill = primary ? AccentSurface : UiPalette.Surface;
      image.color = Color.white;
      ColorBlock colors = button.colors;
      colors.normalColor = fill;
      colors.highlightedColor = Color.Lerp(fill, UiPalette.Foreground, .13f);
      // Keyboard focus has its own ring; selection must not look like hover.
      colors.selectedColor = fill;
      colors.pressedColor = Color.Lerp(fill, Color.black, .2f);
      colors.disabledColor = Color.Lerp(fill, UiPalette.Background, .45f);
      colors.fadeDuration = ReduceMotion() ? 0 : .1f;
      button.SetPalette(colors);
      label.color = primary ? UiPalette.SoftAccent : UiPalette.Foreground;
    });
    return button;
  }

  public Button IconButton(
    string name,
    Transform parent,
    float x,
    float y,
    float size,
    Action onClick,
    UiIconKind iconKind
  )
  {
    Button button = Button("", parent, x, y, size, size, onClick, themed: false);
    button.name = name;
    button.targetGraphic.color = Color.clear;
    button.transition = Selectable.Transition.None;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    float iconSize = Mathf.Min(24, size - 12);
    UiIcon icon = Rect(
      name + " icon",
      button.transform,
      (size - iconSize) * .5f,
      (size - iconSize) * .5f,
      iconSize,
      iconSize
    )
      .gameObject.AddComponent<UiIcon>();
    icon.Kind = iconKind;
    icon.raycastTarget = false;
    Outline focus = icon.gameObject.AddComponent<Outline>();
    focus.effectDistance = new Vector2(1.25f, -1.25f);
    focus.enabled = false;
    button.GetComponent<UiFocusRing>().Initialize(focus);
    Bind(() => focus.effectColor = SoftAccent);
    UiIconInteraction feedback = button.gameObject.AddComponent<UiIconInteraction>();
    feedback.Initialize(button, icon, ReduceMotion);
    Bind(() => feedback.SetPalette(UiPalette.Foreground, SoftAccent));
    return button;
  }

  /// <summary>
  /// A button that is only its glyph, for actions inside a card: hover and
  /// press change the glyph, so no second surface lights up inside the card.
  /// </summary>
  public Button GlyphButton(
    string name,
    Transform parent,
    float size,
    float glyphSize,
    UiIconKind icon,
    Action onClick,
    Color normal,
    Color hover
  )
  {
    Button button = Button("", parent, 0, 0, size, size, onClick, themed: false);
    button.name = name;
    button.targetGraphic.color = Color.clear;
    button.transition = Selectable.Transition.None;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    float inset = (size - glyphSize) * .5f;
    UiIcon glyph = Rect(name + " glyph", button.transform, inset, inset, glyphSize, glyphSize)
      .gameObject.AddComponent<UiIcon>();
    glyph.Kind = icon;
    glyph.raycastTarget = false;
    Outline focus = glyph.gameObject.AddComponent<Outline>();
    focus.effectDistance = new Vector2(1.25f, -1.25f);
    focus.enabled = false;
    button.GetComponent<UiFocusRing>().Initialize(focus);
    Bind(() => focus.effectColor = SoftAccent);
    UiIconInteraction feedback = button.gameObject.AddComponent<UiIconInteraction>();
    feedback.Initialize(button, glyph, ReduceMotion);
    feedback.SetPalette(normal, hover);
    return button;
  }

  public Button Switch(
    string title,
    string description,
    Transform parent,
    float y,
    Func<bool> read,
    Action<bool> write
  ) => Switch(title, description, parent, y, read, write, out _);

  /// <summary>A switch row whose <paramref name="descriptionText"/> the caller keeps current.</summary>
  public Button Switch(
    string title,
    string description,
    Transform parent,
    float y,
    Func<bool> read,
    Action<bool> write,
    out TextMeshProUGUI descriptionText
  )
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
    descriptionText = Text(description, copy, 0, 0, 532, 24, 14, UiPalette.Muted);
    UiCapsuleGraphic track = Capsule("Switch", row.transform, 0, 0, 48, 28, UiPalette.Surface);
    track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(1, .5f);
    track.rectTransform.pivot = new Vector2(0, .5f);
    track.rectTransform.anchoredPosition = new Vector2(-64, 0);
    UiCapsuleGraphic thumb = Capsule("Thumb", track.transform, 4, 4, 20, 20, UiPalette.Foreground);
    UiSwitchMotion motion = thumb.gameObject.AddComponent<UiSwitchMotion>();
    Bind(() =>
    {
      bool enabled = read();
      track.color = enabled ? AccentSurface : new Color(.3f, .3f, .3f);
      thumb.color = enabled ? SoftAccent : UiPalette.Foreground;
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
    // TMP makes the caret when the field turns on with its text already set; a
    // field built on and never hidden would have none. It turns on below.
    box.gameObject.SetActive(false);
    Image inset = Panel("Input surface", box.transform, 1, 1, width - 2, 38, new Color(.075f, .075f, .075f));
    Stretch(inset.rectTransform, 1, 1, 1, 1);
    TMP_InputField input = box.gameObject.AddComponent<TMP_InputField>();
    UiCursor.Attach(input.gameObject, UiCursorKind.Text);
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
      input.selectionColor = Color.Lerp(UiPalette.Surface, SoftAccent, .3f);
      input.caretColor = UiPalette.Foreground;
    });
    box.gameObject.SetActive(true);
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
    UiSlider slider = root.gameObject.AddComponent<UiSlider>();
    UiCursor.Attach(slider.gameObject, UiCursorKind.Horizontal);
    slider.Step = step;
    slider.navigation = new Navigation { mode = Navigation.Mode.None };
    Image hit = root.gameObject.AddComponent<Image>();
    hit.color = Color.clear;
    // Track and thumb use the same endpoints; caps keep a true half-height radius.
    UiCapsuleGraphic track = Capsule("Track", root, 10, 15, width - 20, 6, new Color(.28f, .28f, .28f));
    StretchWidth(track.rectTransform, 10, 10);
    RectTransform fillArea = Rect("Fill area", root, 10, 15, width - 20, 6);
    StretchWidth(fillArea, 10, 10);
    UiCapsuleGraphic fill = Capsule("Fill", fillArea, 0, 0, width - 20, 6, SoftAccent);
    fill.rectTransform.anchorMin = Vector2.zero;
    fill.rectTransform.anchorMax = Vector2.one;
    fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
    RectTransform handleArea = Rect("Handle area", root, 10, 8, width - 20, 20);
    StretchWidth(handleArea, 10, 10);
    UiCapsuleGraphic handle = Capsule("Handle", handleArea, 0, 0, 20, 20, UiPalette.Foreground);
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

  public UiCapsuleGraphic Capsule(
    string name,
    Transform parent,
    float x,
    float y,
    float width,
    float height,
    Color color
  )
  {
    UiCapsuleGraphic graphic = Rect(name, parent, x, y, width, height).gameObject.AddComponent<UiCapsuleGraphic>();
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
    (owner ?? image.gameObject).AddComponent<UiFocusRing>().Initialize(outline);
    Bind(() => outline.effectColor = SoftAccent);
  }

  public void RefreshTheme()
  {
    foreach (Action binding in _themeBindings)
      binding();
  }

  public void Dispose()
  {
    // A second Dispose must not release what other factories still share.
    if (_disposed)
      return;
    _disposed = true;
    UiCursor.Release();
    _themeBindings.Clear();
    UiRoundedSprite.Release();
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

  /// <summary>The height <paramref name="text"/> needs when <paramref name="width"/> wide, and at least <paramref name="minimum"/>.</summary>
  public static float TextHeight(TextMeshProUGUI text, float width, float minimum) =>
    Mathf.Max(minimum, Mathf.Ceil(text.GetPreferredValues(width, 0).y) + 1);
}
