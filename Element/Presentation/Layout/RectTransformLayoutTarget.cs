using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Presentation.Layout;

internal sealed class RectTransformLayoutTarget : IOverlayLayoutTarget, IOverlayGalleryPreview
{
  private readonly RectTransform _transform;
  private readonly Transform _previewParent;
  private readonly Vector3 _defaultScale;
  private readonly Vector2 _defaultPosition;
  private readonly TextMeshProUGUI _sourceText;
  private readonly TextMeshProUGUI[] _sourceTexts;
  private readonly float[] _restingFontSizes;
  private readonly Vector2[] _restingTextPositions;
  private readonly Vector3[] _restingTextScales;
  private readonly OverlayContentGeometry _contentGeometry;
  private OverlayContentGeometry _sampleGeometry;
  private readonly OverlayLayoutSettings _stagedSettings = new();
  private bool _staged;
  private Rect _stageOrigin;
  private bool _applying;
  private readonly CanvasGroup _visibility;
  private readonly string _sample;
  private readonly Vector3[] _corners = new Vector3[4];
  private OverlayLayoutSettings _settings;
  private TextMeshProUGUI _previewText;
  private GameObject _previewRoot;
  private readonly List<PreviewChild> _previewChildren = new();
  private readonly List<PreviewChild> _mirrorChildren = new();
  private RectTransformLayoutTarget _mirrorSource;
  private bool _preview;
  private int _previewThemeVersion = -1;
  private bool _appliedEnabled = true;

  internal event Action<RectTransformLayoutTarget> EnabledChanged;

  public string Id { get; }
  public string TemplateId { get; }
  public string Label { get; }
  public bool Available => _transform != null;
  public bool Enabled => _settings == null || _settings.Enabled;
  public float EffectiveScale => _transform != null ? _transform.localScale.x / _defaultScale.x : 1f;

  public Rect Bounds
  {
    get
    {
      if (_transform == null)
        return default;
      _transform.GetWorldCorners(_corners);
      Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(null, _corners[1]);
      Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(null, _corners[3]);
      return new Rect(topLeft.x, Screen.height - topLeft.y, bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
    }
  }

  public Rect SelectionBounds
  {
    get
    {
      if (!Available || !Enabled)
        return default;
      bool found = false;
      Rect bounds = default;
      if (_previewRoot != null && _previewRoot.activeInHierarchy)
      {
        if (_previewChildren.Count == 0)
          IncludeTextBounds(_previewText, ref bounds, ref found);
        else
          foreach (PreviewChild child in _previewChildren)
            IncludeTextBounds(child.Text, ref bounds, ref found);
      }
      else
        foreach (TextMeshProUGUI text in _sourceTexts)
          IncludeTextBounds(text, ref bounds, ref found);
      return found ? bounds : default;
    }
  }

  public RectTransformLayoutTarget(
    string id,
    string label,
    RectTransform transform,
    Transform previewParent,
    TextMeshProUGUI text,
    string sample,
    string templateId = null
  )
  {
    Id = id;
    TemplateId = templateId ?? id;
    Label = label;
    _transform = transform;
    _previewParent = previewParent;
    _defaultScale = transform.localScale;
    _defaultPosition = transform.anchoredPosition;
    _sourceText = text;
    _sourceTexts = transform.GetComponentsInChildren<TextMeshProUGUI>(true);
    _restingFontSizes = new float[_sourceTexts.Length];
    _restingTextPositions = new Vector2[_sourceTexts.Length];
    _restingTextScales = new Vector3[_sourceTexts.Length];
    for (int i = 0; i < _sourceTexts.Length; i++)
    {
      _restingFontSizes[i] = _sourceTexts[i].fontSize;
      _restingTextPositions[i] = _sourceTexts[i].rectTransform.anchoredPosition;
      _restingTextScales[i] = _sourceTexts[i].rectTransform.localScale;
    }
    _contentGeometry = new OverlayContentGeometry(
      transform,
      _sourceTexts,
      _restingFontSizes,
      _restingTextPositions,
      _restingTextScales,
      TemplateId == "combo",
      ContentSizeChanged
    );
    _visibility = transform.GetComponent<CanvasGroup>();
    if (_visibility == null)
      _visibility = transform.gameObject.AddComponent<CanvasGroup>();
    _visibility.interactable = false;
    _visibility.blocksRaycasts = false;
    _sample = sample;
  }

  public void Configure(OverlayLayoutSettings settings)
  {
    _staged = false;
    _settings = settings;
    Apply();
  }

  public void Apply()
  {
    if (_transform == null || _settings == null)
      return;
    _staged = false;
    ApplyPosition();
  }

  private void ApplyPosition()
  {
    if (_transform == null || _settings == null || _applying)
      return;
    _applying = true;
    try
    {
      ApplyVisibility();
      UpdateMirroredVisual();
      OverlayLayoutSettings settings = _staged ? _stagedSettings : _settings;
      if (!settings.Customized)
      {
        _transform.localScale = _defaultScale;
        _transform.anchoredPosition = _defaultPosition;
        UpdatePreview();
        return;
      }
      float scale = ValidScale(settings.Scale, OverlayLayoutPosition.IsAnchored(settings));
      _transform.localScale = _defaultScale * scale;
      FitVisibleSize();
      // Legacy centers refer to the edit envelope. New anchors refer to the
      // stable content, so changing a title/count retains the chosen text edge.
      Rect positioned = OverlayLayoutPosition.IsAnchored(settings) ? AnchorBounds : Bounds;
      LayoutBounds resolved = OverlayLayoutPosition.Resolve(
        settings,
        positioned.width,
        positioned.height,
        Screen.width,
        Screen.height
      );
      _transform.position += new Vector3(resolved.X - positioned.x, positioned.y - resolved.Y, 0);
      Rect bounds = Bounds;
      LayoutBounds visible = OverlayLayoutGeometry.KeepVisible(
        new LayoutBounds(bounds.x, bounds.y, bounds.width, bounds.height),
        Screen.width,
        Screen.height
      );
      MoveCenter(new Vector2(visible.X + visible.Width * .5f, visible.Y + visible.Height * .5f));
      UpdatePreview();
    }
    finally
    {
      _applying = false;
    }
  }

  public void RefreshLayout() => Apply();

  public void SetEnabled(bool enabled)
  {
    if (_settings == null)
      return;
    _settings.Enabled = enabled;
    ApplyVisibility();
    if (enabled)
      UpdateMirroredVisual();
    UpdatePreview();
  }

  private void ApplyVisibility()
  {
    bool enabled = Enabled;
    _visibility.alpha = enabled ? 1 : 0;
    if (_appliedEnabled == enabled)
      return;
    _appliedEnabled = enabled;
    EnabledChanged?.Invoke(this);
  }

  public RectTransformLayoutTarget CreateInstance(string id, Transform parent)
  {
    GameObject root = OverlayTextFactory.CreateRect(
      "Overlay instance: " + id,
      parent,
      new Vector2(.5f, .5f),
      new Vector2(.5f, .5f),
      Vector2.zero,
      _transform.rect.size
    );
    List<PreviewChild> children = new(_sourceTexts.Length);
    TextMeshProUGUI primary = null;
    foreach (TextMeshProUGUI source in _sourceTexts)
    {
      GameObject child = root;
      if (source.rectTransform != _transform)
      {
        RectTransform sourceRect = source.rectTransform;
        child = OverlayTextFactory.CreateRect(
          source.name,
          root.transform,
          sourceRect.anchorMin,
          sourceRect.pivot,
          sourceRect.anchoredPosition,
          sourceRect.sizeDelta
        );
        child.GetComponent<RectTransform>().anchorMax = sourceRect.anchorMax;
      }
      TextMeshProUGUI text = CopyText(child, source, source.text);
      ContentSizeFitter sourceFitter = source.GetComponent<ContentSizeFitter>();
      if (sourceFitter != null)
      {
        ContentSizeFitter fitter = child.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = sourceFitter.horizontalFit;
        fitter.verticalFit = sourceFitter.verticalFit;
      }
      children.Add(new PreviewChild(source, text));
      if (source == _sourceText)
        primary = text;
    }
    RectTransformLayoutTarget instance = new(
      id,
      Label,
      root.GetComponent<RectTransform>(),
      parent,
      primary,
      _sample,
      TemplateId
    );
    instance._mirrorSource = this;
    instance._mirrorChildren.AddRange(children);
    // Gallery samples always use the template's resting pose, even when an
    // instance is created while the combo is in the middle of a bump.
    for (int i = 0; i < _sourceTexts.Length; i++)
    {
      instance._restingFontSizes[i] = _restingFontSizes[i];
      instance._restingTextPositions[i] = _restingTextPositions[i];
      instance._restingTextScales[i] = _restingTextScales[i];
    }
    if (TryGetLocalAnchorBounds(out Rect localContent))
    {
      // A new copy can be committed before its first Canvas render. Reuse the
      // template's measured resting content without forcing a layout rebuild.
      localContent.position += instance._transform.rect.center - _transform.rect.center;
      instance._contentGeometry.Seed(localContent);
    }
    instance.UpdateMirroredVisual();
    return instance;
  }

  public void UpdateMirroredVisual()
  {
    if (_mirrorSource == null || _transform == null || !Enabled)
      return;
    bool visible = _mirrorSource._transform != null && _mirrorSource._transform.gameObject.activeInHierarchy;
    if (_transform.gameObject.activeSelf != visible)
      _transform.gameObject.SetActive(visible);
    if (!visible)
      return;
    foreach (PreviewChild child in _mirrorChildren)
    {
      TextMeshProUGUI source = child.Source;
      TextMeshProUGUI text = child.Text;
      if (text.enabled != source.enabled)
        text.enabled = source.enabled;
      if (text.font != source.font)
        text.font = source.font;
      if (text.text != source.text)
        text.text = source.text;
      if (text.color != source.color)
        text.color = source.color;
      if (text.fontSize != source.fontSize)
        text.fontSize = source.fontSize;
      if (text.fontSharedMaterial != source.fontSharedMaterial)
        text.fontSharedMaterial = source.fontSharedMaterial;
      if (text.rectTransform == _transform)
        continue;
      if (text.gameObject.activeSelf != source.gameObject.activeSelf)
        text.gameObject.SetActive(source.gameObject.activeSelf);
      RectTransform sourceRect = source.rectTransform;
      RectTransform rect = text.rectTransform;
      if (rect.anchoredPosition != sourceRect.anchoredPosition)
        rect.anchoredPosition = sourceRect.anchoredPosition;
      if (rect.sizeDelta != sourceRect.sizeDelta)
        rect.sizeDelta = sourceRect.sizeDelta;
      if (rect.localScale != sourceRect.localScale)
        rect.localScale = sourceRect.localScale;
    }
  }

  public void ResetLayout()
  {
    if (_settings == null)
      return;
    _settings.Customized = false;
    _settings.LayoutVersion = 0;
    _settings.Anchor = Anchor9.Center;
    _settings.OffsetX = _settings.OffsetY = 0;
    _settings.Scale = 1;
    _settings.CenterX = _settings.CenterY = .5f;
    Apply();
    UpdatePreview();
  }

  public void SetBounds(Rect bounds)
  {
    if (_transform == null || _settings == null || bounds.width <= 0f || bounds.height <= 0f)
      return;
    Rect current = Bounds;
    if (
      current.width <= 0f
      || !OverlayLayoutGeometry.Finite(bounds.center.x)
      || !OverlayLayoutGeometry.Finite(bounds.center.y)
    )
      return;
    if (
      Mathf.Abs(current.x - bounds.x) < .001f
      && Mathf.Abs(current.y - bounds.y) < .001f
      && Mathf.Abs(current.width - bounds.width) < .001f
      && Mathf.Abs(current.height - bounds.height) < .001f
    )
      return;
    if (!_staged)
      _stageOrigin = current;
    float scale = ValidScale(_transform.localScale.x / _defaultScale.x * bounds.width / current.width, true);
    _transform.localScale = _defaultScale * scale;
    FitVisibleSize();
    Rect fitted = Bounds;
    LayoutBounds visible = OverlayLayoutGeometry.KeepVisible(
      new LayoutBounds(
        bounds.center.x - fitted.width * .5f,
        bounds.center.y - fitted.height * .5f,
        fitted.width,
        fitted.height
      ),
      Screen.width,
      Screen.height
    );
    Vector2 center = new(visible.X + visible.Width * .5f, visible.Y + visible.Height * .5f);
    MoveCenter(center);
    Rect moved = Bounds;
    if (
      Mathf.Abs(moved.x - _stageOrigin.x) < .001f
      && Mathf.Abs(moved.y - _stageOrigin.y) < .001f
      && Mathf.Abs(moved.width - _stageOrigin.width) < .001f
      && Mathf.Abs(moved.height - _stageOrigin.height) < .001f
    )
    {
      _staged = false;
      UpdatePreview();
      return;
    }
    Rect content = AnchorBounds;
    OverlayLayoutSettings previous = _staged ? _stagedSettings : _settings;
    AnchoredLayoutPosition position = OverlayLayoutPosition.Capture(
      new LayoutBounds(content.x, content.y, content.width, content.height),
      Screen.width,
      Screen.height,
      OverlayLayoutPosition.IsAnchored(previous) ? previous.Anchor : null
    );
    if (!position.IsValid)
      return;
    StorePosition(_stagedSettings, position);
    _stagedSettings.Scale = EffectiveScale;
    _staged = true;
    UpdatePreview();
  }

  public void CommitLayout(bool reselectAnchor, bool initialize = false)
  {
    if ((!_staged && !initialize) || _settings == null || _transform == null)
      return;
    Rect content = AnchorBounds;
    AnchoredLayoutPosition position = OverlayLayoutPosition.Capture(
      new LayoutBounds(content.x, content.y, content.width, content.height),
      Screen.width,
      Screen.height,
      !reselectAnchor && OverlayLayoutPosition.IsAnchored(_settings) ? _settings.Anchor : null
    );
    if (!position.IsValid)
      return;
    bool preserveScale =
      reselectAnchor
      && !initialize
      && OverlayLayoutPosition.IsAnchored(_settings)
      && OverlayLayoutGeometry.Finite(_settings.Scale)
      && _settings.Scale > 0;
    float scale = preserveScale ? _settings.Scale : EffectiveScale;
    StorePosition(_settings, position);
    Rect bounds = Bounds;
    Vector2 center = bounds.center;
    _settings.CenterX = center.x / Mathf.Max(1, Screen.width);
    _settings.CenterY = center.y / Mathf.Max(1, Screen.height);
    _settings.Scale = scale;
    _staged = false;
  }

  private static void StorePosition(OverlayLayoutSettings settings, AnchoredLayoutPosition position)
  {
    settings.Customized = true;
    settings.LayoutVersion = OverlayLayoutPosition.CurrentVersion;
    settings.Anchor = position.Anchor;
    settings.OffsetX = position.OffsetX;
    settings.OffsetY = position.OffsetY;
  }

  public void SetPreview(bool preview)
  {
    _preview = preview;
    if (preview && _previewText == null && _sourceText != null)
    {
      _previewRoot = OverlayTextFactory.CreateRect(
        "Layout preview: " + Id,
        _previewParent,
        Vector2.zero,
        new Vector2(0.5f, 0.5f),
        Vector2.zero,
        _transform.rect.size
      );
      if (_sourceText.rectTransform == _transform)
        _previewText = CopySampleText(_previewRoot, _sourceText);
      else
      {
        // Composite targets (the combo title and value) keep their own child
        // anchors. Copy visuals only: never clone animation or mirroring scripts.
        foreach (TextMeshProUGUI source in _sourceTexts)
        {
          RectTransform sourceRect = source.rectTransform;
          GameObject child = OverlayTextFactory.CreateRect(
            source.name,
            _previewRoot.transform,
            sourceRect.anchorMin,
            sourceRect.pivot,
            sourceRect.anchoredPosition,
            sourceRect.sizeDelta
          );
          RectTransform childRect = child.GetComponent<RectTransform>();
          childRect.anchorMax = sourceRect.anchorMax;
          childRect.localScale = sourceRect.localScale;
          TextMeshProUGUI text = CopySampleText(child, source);
          _previewChildren.Add(new PreviewChild(source, text));
          ContentSizeFitter sourceFitter = source.GetComponent<ContentSizeFitter>();
          if (sourceFitter != null)
          {
            ContentSizeFitter fitter = child.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = sourceFitter.horizontalFit;
            fitter.verticalFit = sourceFitter.verticalFit;
          }
          if (source == _sourceText)
            _previewText = text;
        }
      }
      TextMeshProUGUI[] samples = _previewRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
      _sampleGeometry = new OverlayContentGeometry(
        _previewRoot.GetComponent<RectTransform>(),
        samples,
        _restingFontSizes,
        _restingTextPositions,
        _restingTextScales,
        TemplateId == "combo",
        ContentSizeChanged
      );
    }
    UpdatePreview();
  }

  public void UpdatePreview()
  {
    if (_previewRoot == null || _previewText == null)
      return;
    bool visible =
      _preview
      && Enabled
      && (!_transform.gameObject.activeInHierarchy || _sourceText == null || string.IsNullOrEmpty(_sourceText.text));
    bool switched = _previewRoot.activeSelf != visible;
    _previewRoot.SetActive(visible);
    if (switched)
      ContentSizeChanged();
    if (!visible)
      return;
    if (_previewThemeVersion != OverlayTheme.Version)
    {
      SetSampleVisual(_previewText, _sourceText);
      _previewThemeVersion = OverlayTheme.Version;
    }
    foreach (PreviewChild child in _previewChildren)
    {
      child.Text.rectTransform.anchoredPosition = child.Source.rectTransform.anchoredPosition;
      child.Text.fontSize = child.Source.fontSize;
    }
    Rect bounds = Bounds;
    RectTransform previewTransform = _previewRoot.GetComponent<RectTransform>();
    previewTransform.sizeDelta = _transform.rect.size;
    previewTransform.localScale = _transform.lossyScale / Mathf.Max(0.001f, _previewParent.lossyScale.x);
    previewTransform.position = new Vector3(bounds.center.x, Screen.height - bounds.center.y, 0f);
  }

  public GameObject CreateGalleryPreview(Transform parent)
  {
    if (_transform == null || _sourceText == null)
      return null;
    GameObject root = OverlayTextFactory.CreateRect(
      "Gallery preview: " + Id,
      parent,
      new Vector2(.5f, .5f),
      new Vector2(.5f, .5f),
      Vector2.zero,
      _transform.rect.size
    );
    GameObject content = OverlayTextFactory.CreateRect(
      "Content",
      root.transform,
      new Vector2(.5f, .5f),
      new Vector2(.5f, .5f),
      Vector2.zero,
      _transform.rect.size
    );
    RectTransform contentRect = content.GetComponent<RectTransform>();
    List<TextMeshProUGUI> texts = new(_sourceTexts.Length);
    for (int i = 0; i < _sourceTexts.Length; i++)
    {
      TextMeshProUGUI source = _sourceTexts[i];
      GameObject child = content;
      if (source.rectTransform != _transform)
      {
        RectTransform sourceRect = source.rectTransform;
        child = OverlayTextFactory.CreateRect(
          source.name,
          content.transform,
          sourceRect.anchorMin,
          sourceRect.pivot,
          _restingTextPositions[i],
          sourceRect.sizeDelta
        );
        child.GetComponent<RectTransform>().anchorMax = sourceRect.anchorMax;
      }
      TextMeshProUGUI text = CopySampleText(child, source);
      text.fontSize = _restingFontSizes[i];
      ContentSizeFitter sourceFitter = source.GetComponent<ContentSizeFitter>();
      if (sourceFitter != null)
      {
        // Resolve the sample's preferred size once. No fitters or animation
        // components continue updating after this static gallery is constructed.
        Vector2 size = text.rectTransform.sizeDelta;
        Vector2 preferred = text.GetPreferredValues();
        if (sourceFitter.horizontalFit == ContentSizeFitter.FitMode.PreferredSize)
          size.x = preferred.x;
        if (sourceFitter.verticalFit == ContentSizeFitter.FitMode.PreferredSize)
          size.y = preferred.y;
        text.rectTransform.sizeDelta = size;
      }
      text.ForceMeshUpdate(true, true);
      texts.Add(text);
    }
    bool found = false;
    Rect visible = default;
    foreach (TextMeshProUGUI text in texts)
      IncludeLocalTextBounds(text, contentRect, ref visible, ref found);
    if (found)
    {
      root.GetComponent<RectTransform>().sizeDelta = visible.size;
      contentRect.anchoredPosition = -visible.center;
    }
    return root;
  }

  public void Dispose()
  {
    _contentGeometry.Dispose();
    _sampleGeometry?.Dispose();
    if (_previewRoot != null)
      Object.Destroy(_previewRoot);
    if (_mirrorSource != null && _transform != null)
      Object.Destroy(_transform.gameObject);
  }

  private static TextMeshProUGUI CopyText(GameObject root, TextMeshProUGUI source, string value)
  {
    TextMeshProUGUI text = OverlayTextFactory.AddText(
      root,
      source.font,
      source.fontSharedMaterial,
      source.fontSize,
      source.alignment
    );
    text.text = value;
    text.color = source.color;
    text.textWrappingMode = source.textWrappingMode;
    text.overflowMode = source.overflowMode;
    text.enableAutoSizing = source.enableAutoSizing;
    text.fontSizeMin = source.fontSizeMin;
    text.fontSizeMax = source.fontSizeMax;
    text.fontStyle = source.fontStyle;
    text.characterSpacing = source.characterSpacing;
    text.wordSpacing = source.wordSpacing;
    text.lineSpacing = source.lineSpacing;
    text.paragraphSpacing = source.paragraphSpacing;
    text.margin = source.margin;
    return text;
  }

  private TextMeshProUGUI CopySampleText(GameObject root, TextMeshProUGUI source)
  {
    TextMeshProUGUI text = CopyText(root, source, source == _sourceText ? _sample : source.text);
    SetSampleVisual(text, source);
    return text;
  }

  private void SetSampleVisual(TextMeshProUGUI text, TextMeshProUGUI source)
  {
    if (source != _sourceText)
      return;
    OverlayLabels labels = OverlayLocalization.Get(RDString.language);
    string label = null;
    string value = null;
    Color color = OverlayTheme.Accent;
    switch (TemplateId)
    {
      case "progress":
        label = labels.Progress;
        value = "0%-48%";
        color = OverlayTheme.ProgressColor(.48f);
        break;
      case "accuracy":
        label = labels.XAccuracy;
        value = "99.5%";
        color = OverlayTheme.XAccuracyColor(.995f);
        break;
      case "time":
        label = labels.MusicTime;
        value = "1:27~3:48";
        break;
      case "fps":
        label = "FPS";
        value = "120";
        break;
      case "tile-bpm":
      case "current-bpm":
      case "keys-per-second":
        label =
          TemplateId == "tile-bpm" ? "TBPM"
          : TemplateId == "current-bpm" ? "CBPM"
          : "KPS";
        value = TemplateId == "keys-per-second" ? "6" : "180";
        color = OverlayTheme.BpmColor(180);
        break;
      case "song-title":
      case "timing-scale":
      case "attempt":
      case "full-attempt":
        color = Color.white;
        break;
      case "combo":
        color = OverlayTheme.ComboColor(120);
        break;
      case "judgement":
        color = OverlayTheme.JudgementBase;
        text.text =
          "0 <color=#FF0000>0</color> <color=#FF6F4E>0</color> <color=#A0FF4E>2</color> "
          + "<color=#60FF4E>4 120 4</color> <color=#A0FF4E>2</color> "
          + "<color=#FF6F4E>0</color> <color=#FF0000>0</color> 0";
        break;
    }
    if (label != null)
      text.text = "<color=#FFFFFF>" + label + " |</color> " + value;
    text.color = color;
  }

  private static void IncludeTextBounds(TextMeshProUGUI text, ref Rect bounds, ref bool found)
  {
    if (text == null || !text.isActiveAndEnabled || text.color.a <= 0 || string.IsNullOrEmpty(text.text))
      return;
    if (text.havePropertiesChanged || text.textInfo.characterCount == 0)
      text.ForceMeshUpdate();
    Bounds mesh = text.textBounds;
    if (mesh.size.x <= 0 || mesh.size.y <= 0)
      return;
    Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
    Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
    for (int i = 0; i < 4; i++)
    {
      Vector3 local = new((i & 1) == 0 ? mesh.min.x : mesh.max.x, i < 2 ? mesh.min.y : mesh.max.y, 0);
      Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, text.transform.TransformPoint(local));
      screen.y = Screen.height - screen.y;
      min = Vector2.Min(min, screen);
      max = Vector2.Max(max, screen);
    }
    IncludeRect(Rect.MinMaxRect(min.x, min.y, max.x, max.y), ref bounds, ref found);
  }

  private static void IncludeLocalTextBounds(
    TextMeshProUGUI text,
    RectTransform relativeTo,
    ref Rect bounds,
    ref bool found
  )
  {
    Bounds mesh = text.textBounds;
    if (mesh.size.x <= 0 || mesh.size.y <= 0)
      return;
    Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
    Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
    for (int i = 0; i < 4; i++)
    {
      Vector3 local = new((i & 1) == 0 ? mesh.min.x : mesh.max.x, i < 2 ? mesh.min.y : mesh.max.y, 0);
      Vector2 point = relativeTo.InverseTransformPoint(text.transform.TransformPoint(local));
      min = Vector2.Min(min, point);
      max = Vector2.Max(max, point);
    }
    IncludeRect(Rect.MinMaxRect(min.x, min.y, max.x, max.y), ref bounds, ref found);
  }

  private static void IncludeRect(Rect current, ref Rect bounds, ref bool found)
  {
    bounds = found
      ? Rect.MinMaxRect(
        Mathf.Min(bounds.xMin, current.xMin),
        Mathf.Min(bounds.yMin, current.yMin),
        Mathf.Max(bounds.xMax, current.xMax),
        Mathf.Max(bounds.yMax, current.yMax)
      )
      : current;
    found = true;
  }

  private readonly struct PreviewChild
  {
    public readonly TextMeshProUGUI Source;
    public readonly TextMeshProUGUI Text;

    public PreviewChild(TextMeshProUGUI source, TextMeshProUGUI text)
    {
      Source = source;
      Text = text;
    }
  }

  private void MoveCenter(Vector2 center)
  {
    Rect bounds = Bounds;
    _transform.position += new Vector3(center.x - bounds.center.x, bounds.center.y - center.y, 0f);
  }

  private void FitVisibleSize()
  {
    Rect bounds = Bounds;
    if (bounds.width <= 0 || bounds.height <= 0 || Screen.width <= 0 || Screen.height <= 0)
      return;
    float visible = OverlayLayoutGeometry.MinimumVisibleFraction;
    float fit = Mathf.Min(
      1,
      Mathf.Min(Screen.width / (bounds.width * visible), Screen.height / (bounds.height * visible))
    );
    _transform.localScale *= fit;
  }

  private static float ValidScale(float scale, bool anchored) =>
    OverlayLayoutGeometry.Finite(scale) && scale > 0 ? (anchored ? scale : Mathf.Clamp(scale, 0.2f, 5f)) : 1f;

  internal Rect AnchorBounds
  {
    get
    {
      if (!TryGetLocalAnchorBounds(out Rect local))
        return Bounds;
      Vector2 minimum = new(float.PositiveInfinity, float.PositiveInfinity);
      Vector2 maximum = new(float.NegativeInfinity, float.NegativeInfinity);
      for (int i = 0; i < 4; i++)
      {
        Vector3 point = new((i & 1) == 0 ? local.xMin : local.xMax, i < 2 ? local.yMin : local.yMax, 0);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, _transform.TransformPoint(point));
        screen.y = Screen.height - screen.y;
        minimum = Vector2.Min(minimum, screen);
        maximum = Vector2.Max(maximum, screen);
      }
      return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }
  }

  private bool TryGetLocalAnchorBounds(out Rect local)
  {
    OverlayContentGeometry geometry =
      _previewRoot != null && _previewRoot.activeInHierarchy && _sampleGeometry?.HasBounds == true
        ? _sampleGeometry
        : _contentGeometry;
    local = geometry.Bounds;
    if (!geometry.HasBounds)
      return false;
    if (geometry == _sampleGeometry)
      local.position += _transform.rect.center - _previewRoot.GetComponent<RectTransform>().rect.center;
    return true;
  }

  private void ContentSizeChanged()
  {
    // Pointer gestures own the edit envelope until commit. New text geometry
    // is still cached, but cannot pull the element away from the pointer.
    if (!_applying && !_staged && _settings != null && OverlayLayoutPosition.IsAnchored(_settings))
      ApplyPosition();
  }
}
