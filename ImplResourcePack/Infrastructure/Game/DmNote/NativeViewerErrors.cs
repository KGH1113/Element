using ImplResourcePack.Domain.DmNote;

namespace ImplResourcePack.Infrastructure.Game.DmNote;

/// <summary>
/// Translates the native hosts' diagnostic text into what the player can do
/// about it. Only the host adapter reads native wording; callers see statuses.
/// </summary>
internal static class NativeViewerErrors
{
  public static NativeViewerStatus Classify(
    string nativeError,
    NativeViewerStatus unknown = NativeViewerStatus.PageUnavailable
  )
  {
    if (string.IsNullOrWhiteSpace(nativeError))
      return NativeViewerStatus.Attached;
    // Fixed when the game starts; retrying or returning to the window cannot help.
    if (nativeError.Contains("COM apartment"))
      return NativeViewerStatus.HostIncompatible;
    if (nativeError.Contains("renderer stopped") || nativeError.Contains("reconnect automatically"))
      return NativeViewerStatus.Reconnecting;
    if (nativeError.Contains("WebView2 Runtime"))
      return NativeViewerStatus.WebViewRuntimeMissing;
    if (nativeError.Contains("macOS 11"))
      return NativeViewerStatus.UnsupportedSystem;
    if (nativeError.Contains("game window") || nativeError.Contains("application is not ready"))
      return NativeViewerStatus.WindowUnavailable;
    return unknown;
  }

  /// <summary>
  /// A page that stopped or failed to load waits for its descriptor, so the
  /// next descriptor must reach the host even when it is unchanged.
  /// </summary>
  public static bool NeedsDescriptor(NativeViewerStatus status) =>
    status is NativeViewerStatus.PageUnavailable or NativeViewerStatus.Reconnecting;
}
