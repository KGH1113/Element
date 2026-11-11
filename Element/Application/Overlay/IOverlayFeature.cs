using System;

namespace Element.Application.Overlay;

/// <summary>One kind of overlay value: reads it from its source and shows it in its view.</summary>
internal interface IOverlayFeature : IDisposable
{
  void Show();

  void Hide();

  /// <summary>Reads and shows the value again.</summary>
  void Refresh();
}
