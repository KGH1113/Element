using UnityEngine.UI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>An icon on a button, with the button looked up once for its state each frame.</summary>
internal readonly struct LayoutIcon
{
  public readonly LayoutActionIcon Icon;
  public readonly Button Button;

  public LayoutIcon(LayoutActionIcon icon, Button button)
  {
    Icon = icon;
    Button = button;
  }
}
