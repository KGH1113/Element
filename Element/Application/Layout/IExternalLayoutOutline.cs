namespace Element.Application.Layout;

// An external renderer owns this target's selection border. Unity must not
// draw a second copy underneath native/WebKit content.
internal interface IExternalLayoutOutline
{
  bool OwnsSelectionOutline { get; }
  void SetSelected(bool selected);
}
