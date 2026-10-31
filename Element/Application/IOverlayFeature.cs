using System;

namespace Element.Application;

internal interface IOverlayFeature : IDisposable
{
  void Show();

  void Hide();
}
