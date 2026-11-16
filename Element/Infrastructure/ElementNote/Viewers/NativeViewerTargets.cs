using System;
using System.Collections.Generic;
using System.Linq;
using Element.Application.KeyViewer;
using Element.Application.Layout;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Configuration.Overlay;
using Element.Domain.KeyViewer;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>
/// The key viewers as the layout editor places them: the two gallery viewers and
/// those a layout list adds. Each is bound to the native host while there is one,
/// and shows an offline sample while it has no live page. Unity main thread.
/// </summary>
internal sealed class NativeViewerTargets : IDisposable
{
  private readonly Func<string, string> _viewerLabel;
  private readonly Action<bool> _editingChanged;
  private readonly Action _enabledChanged;
  private readonly Func<NativeViewerStatus> _status;
  private readonly Func<bool> _hidden;
  private readonly List<NativeViewerLayoutTarget> _targets;
  private readonly NativeViewerLayoutTarget[] _gallery;
  private List<OverlayLayoutSettings> _layouts;
  private IKeyViewerSamples _samples;
  private INativeViewerLayoutHost _host;

  /// <param name="viewerLabel">The name the editor shows for a viewer kind.</param>
  /// <param name="editingChanged">The layout editor started or stopped showing the viewers.</param>
  /// <param name="enabledChanged">A viewer came, went, or was turned on or off.</param>
  /// <param name="status">What the samples say while a viewer has no live page.</param>
  /// <param name="hidden">The editor neither draws nor selects the viewers.</param>
  public NativeViewerTargets(
    Func<string, string> viewerLabel,
    Action<bool> editingChanged,
    Action enabledChanged,
    Func<NativeViewerStatus> status,
    Func<bool> hidden
  )
  {
    _viewerLabel = viewerLabel;
    _editingChanged = editingChanged;
    _enabledChanged = enabledChanged;
    _status = status;
    _hidden = hidden;
    _gallery = new[] { Create("hand", null), Create("foot", null) };
    _targets = new List<NativeViewerLayoutTarget>(_gallery);
  }

  public IReadOnlyList<NativeViewerLayoutTarget> All => _targets;

  /// <summary>The gallery viewers, which always stay.</summary>
  public IReadOnlyCollection<string> DefaultIds => _gallery.Select(target => target.Id).ToArray();

  /// <summary>The layout editor shows the viewers, with samples where no page is attached.</summary>
  public bool Editing { get; private set; }

  public bool AnyEnabled
  {
    get
    {
      for (int i = 0; i < _targets.Count; i++)
        if (_targets[i].Enabled)
          return true;
      return false;
    }
  }

  /// <summary>
  /// Follows <paramref name="layouts"/>, such as a profile's: its viewers come,
  /// and viewers it lacks go instead of being added to it.
  /// </summary>
  /// <param name="legacyEnabled">
  /// At startup only: whether the old single hand and foot viewers were on,
  /// for settings from before each viewer was its own element.
  /// </param>
  public void ConfigureLayouts(List<OverlayLayoutSettings> layouts, bool legacyEnabled)
  {
    if (layouts == null)
      throw new ArgumentNullException(nameof(layouts));
    _layouts = layouts;
    string before = EnabledViewers();
    var removed = new List<string>();
    var added = new List<KeyValuePair<string, string>>();
    NativeViewerLayoutSync.Plan(
      _targets.Select(target => target.Id),
      _gallery.Select(target => target.Id).ToArray(),
      layouts,
      removed,
      added
    );
    foreach (string id in removed)
    {
      NativeViewerLayoutTarget target = _targets.Find(item => item.Id == id);
      _targets.Remove(target);
      target.Dispose();
    }
    foreach (KeyValuePair<string, string> viewer in added)
      _targets.Add(Create(viewer.Value, viewer.Key));
    foreach (NativeViewerLayoutTarget target in _targets)
      target.Configure(layouts, legacyEnabled);
    // The pages on screen follow; viewers that went close their page.
    if (EnabledViewers() != before)
      _enabledChanged();
  }

  private string EnabledViewers() =>
    string.Join(",", _targets.Where(target => target.Enabled).Select(target => target.NativeId));

  /// <param name="templateId">OverlayElementKinds.HandKeyViewer or FootKeyViewer.</param>
  public IOverlayLayoutTarget Add(string templateId)
  {
    string kind =
      templateId == "native-hand" ? "hand"
      : templateId == "native-foot" ? "foot"
      : null;
    if (kind == null || _layouts == null)
      return null;
    string id = templateId + ":" + Guid.NewGuid().ToString("N");
    NativeViewerLayoutTarget target = Create(kind, id);
    _layouts.Add(
      new OverlayLayoutSettings
      {
        Id = id,
        TemplateId = templateId,
        Enabled = true,
        // Centered at full size, the size the default viewers start at.
        Customized = true,
        LayoutVersion = OverlayLayoutPosition.CurrentVersion,
        Anchor = Anchor9.Center,
      }
    );
    _targets.Add(target);
    target.Configure(_layouts);
    target.SetPreview(Editing);
    _enabledChanged();
    return target;
  }

  /// <summary>
  /// Replaces <paramref name="target"/> with the other kind in the same place.
  /// The old viewer is hidden, not removed, so undo can bring it back.
  /// </summary>
  public IOverlayLayoutTarget Switch(IOverlayLayoutTarget target, string templateId)
  {
    if (target is not NativeViewerLayoutTarget old || old.TemplateId == templateId || _layouts == null)
      return null;
    OverlayLayoutSettings from = _layouts.Find(layout => layout != null && layout.Id == old.Id);
    if (Add(templateId) is not NativeViewerLayoutTarget created)
      return null;
    OverlayLayoutSettings to = _layouts.Find(layout => layout != null && layout.Id == created.Id);
    if (from != null && to != null)
    {
      to.CopyPlacementFrom(from);
      created.Configure(_layouts);
    }
    old.SetEnabled(false);
    _enabledChanged();
    return created;
  }

  /// <summary>Where the samples shown while a viewer has no live page come from.</summary>
  public void ConfigurePreview(IKeyViewerSamples samples)
  {
    _samples = samples;
    foreach (NativeViewerLayoutTarget target in _targets)
      target.ConfigurePreview(samples);
  }

  /// <summary>Binds every viewer to <paramref name="host"/>; null while there is no host.</summary>
  public void Bind(INativeViewerLayoutHost host)
  {
    _host = host;
    // Read the field each time: binding can end the host, and the rest then bind to none.
    foreach (NativeViewerLayoutTarget target in _targets)
      target.Bind(_host);
  }

  /// <summary>Every frame: geometry follows the game view and the samples follow the editor.</summary>
  public void TickPreview()
  {
    foreach (NativeViewerLayoutTarget target in _targets)
      target.TickPreview();
  }

  /// <summary>The page each enabled viewer shows, from what ElementNote described.</summary>
  public (string Id, NativeViewerSurface Surface)[] EnabledSurfaces(NativeViewerSurface[] known) =>
    _targets
      .Where(target => target.Enabled)
      .SelectMany(target =>
        known.Where(surface => surface.ViewerKind == target.Kind).Select(surface => (target.NativeId, surface))
      )
      .ToArray();

  private NativeViewerLayoutTarget Create(string kind, string id)
  {
    NativeViewerLayoutTarget target = new(
      kind,
      () => _viewerLabel(kind),
      SetEditing,
      _enabledChanged,
      id,
      _status,
      _hidden
    );
    target.ConfigurePreview(_samples);
    target.Bind(_host);
    return target;
  }

  private void SetEditing(bool editing)
  {
    Editing = editing;
    _editingChanged(editing);
  }

  public void Dispose()
  {
    foreach (NativeViewerLayoutTarget target in _targets)
      target.Dispose();
  }
}
