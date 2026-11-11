using System;
using Element.Application.KeyViewer;
using Element.Infrastructure.ElementNote.App;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>
/// Brings ElementNote in front of the game on the tab a key viewer shows, for the
/// layout editor's button. Unity main thread, while the game is in front.
/// </summary>
internal sealed class ElementNoteTabOpener
{
  private readonly NativeViewerSession _session;
  private readonly NativeViewerIpcEndpoint _endpoint;
  private readonly IElementNoteAppFocus _appFocus;
  private readonly NativeViewerErrorLog _log;

  /// <param name="appFocus">Null where the mod has no ElementNote.</param>
  public ElementNoteTabOpener(
    NativeViewerSession session,
    NativeViewerIpcEndpoint endpoint,
    IElementNoteAppFocus appFocus,
    NativeViewerErrorLog log
  )
  {
    _session = session;
    _endpoint = endpoint;
    _appFocus = appFocus;
    _log = log;
  }

  public KeyViewerEditResult Open(string viewerKind)
  {
    if (!_session.Connected)
      return KeyViewerEditResult.AppClosed;
    string peer = _session.EditPeer;
    if (peer == null)
      return KeyViewerEditResult.AppOutdated;
    if (!_endpoint.Attached)
      return KeyViewerEditResult.Failed;
    try
    {
      _appFocus?.PassFocus();
    }
    catch (Exception error)
    {
      // The app still switches its tab; the player brings it forward.
      _log.Warning("Couldn't bring ElementNote forward: " + error.Message);
    }
    try
    {
      return _endpoint.SendEdit(peer, viewerKind) ? KeyViewerEditResult.Opened : KeyViewerEditResult.Failed;
    }
    catch (Exception error)
    {
      _log.Error("Open the key viewer in ElementNote", error);
      return KeyViewerEditResult.Failed;
    }
  }
}
