using System;

namespace Element.Application.Overlay;

internal interface IOverlayView<TSnapshot> : IDisposable
{
  void Show();

  void Hide();

  void Render(TSnapshot snapshot);
}
