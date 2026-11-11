namespace Element.Domain.DmNote;

/// <summary>Why a native key viewer is or is not showing, in user terms.</summary>
internal enum NativeViewerStatus
{
  WaitingForApp,
  Connecting,
  Attached,
  NoViewerTab,
  ObsModeOff,
  HiddenInApp,
  Preparing,
  ReplayRendering,
  Reconnecting,
  Incompatible,
  PageUnavailable,
  WindowUnavailable,
  ComponentMissing,
  WebViewRuntimeMissing,
  HostIncompatible,
  UnsupportedSystem,
  UnsupportedPlatform,
}
