using System.Globalization;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts;

/// <summary>
/// While a color is edited, a small text or shape shows larger, so a gradient's
/// handles have room around it; it eases there and back, and a line says how
/// much larger it is. The element itself keeps its size in the game.
/// </summary>
internal sealed class PreviewZoom
{
  private const float WantedWidth = 240,
    WantedHeight = 120,
    MostZoom = 8,
    SaidFrom = 1.2f,
    LineHeight = 20;

  private readonly EditorGlide _glide = new();
  private TextMeshProUGUI _line;
  private float _said = -1;

  /// <summary>Still easing; the editor redraws every frame until it arrives.</summary>
  public bool Moving => _glide.Moving;

  /// <summary>Takes the next zoom at once, as when the editor is laid out afresh.</summary>
  public void Snap() => _glide.Snap();

  public void Build(UiFactory ui, Transform parent)
  {
    _line = ui.Text("", parent, 0, 0, 240, LineHeight, 12, UiPalette.Muted);
    _line.name = "Preview zoom";
    _line.alignment = TextAlignmentOptions.Midline;
    _line.textWrappingMode = TextWrappingModes.NoWrap;
    _line.raycastTarget = false;
    _line.gameObject.SetActive(false);
    _said = -1;
  }

  /// <summary>
  /// How much larger than its game size to draw an element
  /// <paramref name="width"/> by <paramref name="height"/> frame units: more
  /// than 1 only while <paramref name="editingColor"/> and it is small both ways.
  /// </summary>
  public float For(bool editingColor, float width, float height, bool reduceMotion)
  {
    float wanted = Mathf.Min(WantedWidth / Mathf.Max(1, width), WantedHeight / Mathf.Max(1, height));
    return _glide.Toward(editingColor ? Mathf.Clamp(wanted, 1, MostZoom) : 1, reduceMotion);
  }

  /// <summary>Says how much larger the element is drawn, centered on <paramref name="x"/>; nothing near its own size.</summary>
  public void Say(float shown, float x, float y)
  {
    bool say = shown >= SaidFrom;
    if (_line.gameObject.activeSelf != say)
      _line.gameObject.SetActive(say);
    if (!say)
      return;
    float rounded = Mathf.Round(shown * 2) / 2;
    if (!Mathf.Approximately(rounded, _said))
    {
      _said = rounded;
      string times = rounded.ToString("0.#", CultureInfo.InvariantCulture);
      _line.text = times + "×";
    }
    UiFactory.Place(_line.rectTransform, x - 120, y, 240, LineHeight);
  }
}
