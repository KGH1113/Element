namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// Values that arrive from outside the game's own events, such as other mods'
/// values. The canvas pumps it once per frame it draws, before redrawing what
/// changed; a pump with nothing new must cost no allocation.
/// </summary>
internal interface IOverlayValueFeed
{
  void Pump(OverlayValues values);
}
