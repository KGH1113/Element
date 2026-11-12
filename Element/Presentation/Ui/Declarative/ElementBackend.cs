using System;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative;

/// <summary>
/// The UGUI backend with Element's own parts: Element's vector icons instead
/// of ElementUI's built-in ones. Every other control takes Element's colors
/// and font from <see cref="ElementTheme"/>.
/// </summary>
internal sealed class ElementBackend : UguiBackend
{
  public ElementBackend(ElementTheme theme)
    : base(theme) => ElementTheme = theme;

  public ElementTheme ElementTheme { get; }

  public override IIconPeer CreateIcon(IContainerPeer parent) => new ElementIconPeer(Parent(parent));
}

/// <summary>An icon drawn with Element's <see cref="UiIcon"/>.</summary>
internal sealed class ElementIconPeer : RectPeer, IIconPeer
{
  private readonly UiIcon _icon;

  public ElementIconPeer(Transform parent)
    : base(parent, "Icon")
  {
    _icon = GameObject.AddComponent<UiIcon>();
    _icon.raycastTarget = false;
  }

  public void SetIcon(Icon icon)
  {
    UiIconKind kind = ElementIcons.KindOf(icon);
    if (_icon.Kind == kind)
      return;
    _icon.Kind = kind;
    _icon.SetVerticesDirty();
  }

  public void SetColor(Color color)
  {
    if (_icon.color != color)
      _icon.color = color;
  }

  public override object GetNative(Type type) => type == typeof(UiIcon) ? _icon : base.GetNative(type);
}

/// <summary>Element's icons as ElementUI icons: <c>Image(ElementIcons.Of(UiIconKind.Overlay))</c>.</summary>
internal static class ElementIcons
{
  private const int First = 1000;

  public static Icon Of(UiIconKind kind) => new(First + (int)kind);

  /// <summary>The Element icon that draws <paramref name="icon"/>: Element's own by number, ElementUI's built-in ones by meaning.</summary>
  public static UiIconKind KindOf(Icon icon)
  {
    if (icon.Id >= First)
      return (UiIconKind)(icon.Id - First);
    return icon.Id switch
    {
      1 => UiIconKind.Plus,
      2 => UiIconKind.Minus,
      3 => UiIconKind.Delete,
      4 => UiIconKind.Reset,
      5 => UiIconKind.Close,
      6 => UiIconKind.Back,
      7 => UiIconKind.Forward,
      8 => UiIconKind.Up,
      9 => UiIconKind.Down,
      10 => UiIconKind.Undo,
      11 => UiIconKind.Redo,
      12 => UiIconKind.Text,
      13 => UiIconKind.Font,
      14 => UiIconKind.Check,
      15 => UiIconKind.PageBack,
      _ => UiIconKind.Plus,
    };
  }
}
