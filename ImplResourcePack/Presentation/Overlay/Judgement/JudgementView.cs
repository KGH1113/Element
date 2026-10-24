using System;
using System.Text;
using ImplResourcePack.Application;
using ImplResourcePack.Domain.Judgement;
using ImplResourcePack.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Overlay.Judgement;

internal sealed class JudgementView : IOverlayView<JudgementSnapshot>
{
  private const string PerfectColorHex = "60FF4E";

  private readonly StringBuilder _builder = new(256);
  private GameObject _root;
  private TextMeshProUGUI _text;

  public JudgementView(OverlayCanvasHost host, TMP_FontAsset font, Material material)
  {
    _root = OverlayTextFactory.CreateRect(
      "Judgement",
      host.Root.transform,
      new Vector2(0.5f, 0f),
      new Vector2(0.5f, 0f),
      new Vector2(0f, 85f),
      new Vector2(1000f, 30f)
    );
    _text = OverlayTextFactory.AddText(_root, font, material, 25f, TextAlignmentOptions.Bottom);
    _text.color = OverlayTheme.JudgementBase;
    host.RegisterLayout("judgement", "판정", _root, _text, "0  0  0  2  4  120  4  2  0  0  0");
    _root.SetActive(false);
  }

  public void Show()
  {
    if (_root != null)
      _root.SetActive(true);
  }

  public void Hide()
  {
    if (_root != null)
      _root.SetActive(false);
  }

  public void Render(JudgementSnapshot snapshot)
  {
    if (_text == null)
      return;

    _text.color = OverlayTheme.JudgementBase;

    string xPerfectColorHex = GetXPerfectColorHex();
    _builder.Clear();
    _builder
      .Append(snapshot.FailOverload)
      .Append(" <color=#FF0000>")
      .Append(snapshot.TooEarly)
      .Append("</color> <color=#FF6F4E>")
      .Append(snapshot.VeryEarly)
      .Append("</color> <color=#A0FF4E>")
      .Append(snapshot.EarlyPerfect)
      .Append("</color> <color=#")
      .Append(PerfectColorHex)
      .Append(">")
      .Append(snapshot.PerfectMinus)
      .Append("</color> <color=#")
      .Append(xPerfectColorHex)
      .Append(">")
      .Append(snapshot.XPerfectAndAuto)
      .Append("</color> <color=#")
      .Append(PerfectColorHex)
      .Append(">")
      .Append(snapshot.PerfectPlus)
      .Append("</color> <color=#A0FF4E>")
      .Append(snapshot.LatePerfect)
      .Append("</color> <color=#FF6F4E>")
      .Append(snapshot.VeryLate)
      .Append("</color> <color=#FF0000>")
      .Append(snapshot.TooLate)
      .Append("</color> ")
      .Append(snapshot.FailMiss);
    _text.text = _builder.ToString();
  }

  private static string GetXPerfectColorHex()
  {
    if (RDC.data == null)
      return PerfectColorHex;

    ColourSchemeHitMargin colors = RDC.hitMarginColoursBySettings;
    return colors != null ? OverlayTheme.ColorToHex(colors.colourXPerfect) : PerfectColorHex;
  }

  public void Dispose()
  {
    if (_root != null)
      Object.Destroy(_root);

    _root = null;
    _text = null;
  }
}
