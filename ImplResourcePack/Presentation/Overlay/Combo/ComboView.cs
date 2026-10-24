using System.Globalization;
using ImplResourcePack.Application;
using ImplResourcePack.Domain.Combo;
using ImplResourcePack.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Overlay.Combo;

internal sealed class ComboView : IOverlayView<ComboSnapshot>
{
  private GameObject _root;
  private Material _comboMaterial;
  private ComboAnimationDriver _animation;
  private SongTitleMirror _songTitleMirror;
  private TextMeshProUGUI _valueText;
  private int _lastCount = -1;
  private int _lastThemeVersion = -1;
  private GameObject _songTitleRoot;

  public ComboView(OverlayCanvasHost host, TMP_FontAsset font, Material material)
  {
    _comboMaterial = new Material(material) { name = "ImplResourcePack Combo Material" };
    _comboMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
    _comboMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.7f));

    _root = OverlayTextFactory.CreateRect(
      "Combo",
      host.Root.transform,
      new Vector2(0.5f, 1f),
      new Vector2(0.5f, 1f),
      new Vector2(0f, -57f),
      new Vector2(300f, 200f)
    );

    _songTitleRoot = OverlayTextFactory.CreateRect(
      "SongTitle",
      host.Root.transform,
      new Vector2(0.5f, 1f),
      new Vector2(0.5f, 1f),
      new Vector2(0f, -21f),
      new Vector2(600f, 32f)
    );
    TextMeshProUGUI songTitleText = OverlayTextFactory.AddText(
      _songTitleRoot,
      font,
      _comboMaterial,
      24f,
      TextAlignmentOptions.Center
    );
    songTitleText.text = string.Empty;
    songTitleText.color = Color.white;
    songTitleText.enableAutoSizing = true;
    songTitleText.fontSizeMin = 12f;
    songTitleText.fontSizeMax = 24f;
    songTitleText.textWrappingMode = TextWrappingModes.NoWrap;
    songTitleText.overflowMode = TextOverflowModes.Overflow;

    _songTitleMirror = _songTitleRoot.AddComponent<SongTitleMirror>();
    _songTitleMirror.Initialize(songTitleText);

    GameObject title = OverlayTextFactory.CreateRect(
      "ComboTitle",
      _root.transform,
      new Vector2(0.5f, 0.45f),
      new Vector2(0.5f, 0f),
      Vector2.zero,
      Vector2.zero
    );
    TextMeshProUGUI titleText = OverlayTextFactory.AddText(
      title,
      font,
      _comboMaterial,
      40f,
      TextAlignmentOptions.Center
    );
    titleText.text = "Perfect";
    titleText.color = Color.white;
    AddContentSizeFitter(title);

    GameObject value = OverlayTextFactory.CreateRect(
      "ComboValue",
      _root.transform,
      new Vector2(0.5f, 0.45f),
      new Vector2(0.5f, 0.5f),
      Vector2.zero,
      Vector2.zero
    );
    _valueText = OverlayTextFactory.AddText(value, font, _comboMaterial, 108f, TextAlignmentOptions.Top);
    AddContentSizeFitter(value);

    _animation = _root.AddComponent<ComboAnimationDriver>();
    _animation.Initialize(title.GetComponent<RectTransform>(), value.GetComponent<RectTransform>(), _valueText);
    host.RegisterLayout("song-title", "곡 제목", _songTitleRoot, songTitleText, "A Dance of Fire and Ice");
    host.RegisterLayout("combo", "콤보", _root, _valueText, "120");
    _root.SetActive(false);
    _songTitleRoot.SetActive(false);
  }

  public void Show()
  {
    _root?.SetActive(true);
    _songTitleRoot?.SetActive(true);
  }

  public void Hide()
  {
    _animation?.ResetVisual();
    _root?.SetActive(false);
    _songTitleRoot?.SetActive(false);
  }

  public void Render(ComboSnapshot snapshot)
  {
    if (
      _valueText == null
      || (_lastCount == snapshot.Count && !snapshot.Bump && _lastThemeVersion == OverlayTheme.Version)
    )
      return;

    _valueText.text = snapshot.Count.ToString(CultureInfo.InvariantCulture);
    _valueText.color = OverlayTheme.ComboColor(snapshot.Count);
    _lastCount = snapshot.Count;
    _lastThemeVersion = OverlayTheme.Version;

    if (snapshot.Bump)
      _animation.Play();
    else
      _animation.ResetVisual();
  }

  public void Dispose()
  {
    _songTitleMirror?.Release();
    if (_root != null)
      Object.Destroy(_root);
    if (_songTitleRoot != null)
      Object.Destroy(_songTitleRoot);
    if (_comboMaterial != null)
      Object.Destroy(_comboMaterial);

    _root = null;
    _songTitleRoot = null;
    _comboMaterial = null;
    _animation = null;
    _valueText = null;
  }

  private static void AddContentSizeFitter(GameObject gameObject)
  {
    ContentSizeFitter fitter = gameObject.AddComponent<ContentSizeFitter>();
    fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
  }
}
