using System;

namespace ImplResourcePack.Domain.DmNote;

internal sealed class NativeViewerSurface
{
  public string ViewerKind { get; set; }
  public string Url { get; set; }
  public int Width { get; set; }
  public int Height { get; set; }

  public bool IsValid()
  {
    return (ViewerKind == "hand" || ViewerKind == "foot")
      && Width > 0
      && Height > 0
      && Width <= 4096
      && Height <= 4096
      && Url != null
      && Url.Length <= 8192
      && Uri.TryCreate(Url, UriKind.Absolute, out Uri address)
      && address.Scheme == Uri.UriSchemeHttp
      && address.Host == "127.0.0.1"
      && address.UserInfo.Length == 0
      && address.Fragment.Length == 0;
  }

  public bool SameAs(NativeViewerSurface other) =>
    other != null
    && ViewerKind == other.ViewerKind
    && Url == other.Url
    && Width == other.Width
    && Height == other.Height;

  /// <summary>An unchanged descriptor needs no native layout or page work.</summary>
  public static bool SameSet(NativeViewerSurface[] left, NativeViewerSurface[] right)
  {
    if (left == null || right == null || left.Length != right.Length)
      return false;
    foreach (NativeViewerSurface surface in left)
    {
      bool found = false;
      foreach (NativeViewerSurface candidate in right)
        if (surface != null && surface.SameAs(candidate))
        {
          found = true;
          break;
        }
      if (!found)
        return false;
    }
    return true;
  }
}
