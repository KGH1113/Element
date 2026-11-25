using System.Globalization;
using Element.Application.Layout;
using Element.Domain.Layout;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Kinds;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Canvas.Toolbar;

/// <summary>
/// What the main toolbar shows and does: what is selected, its scale (typed,
/// or + and −) or its Edit button, undo and redo, reset, snapping, delete, Add
/// Element, the highlight color, the default font, Cancel and Save. The editor
/// changes outside the toolbar every frame, so <see cref="Sync"/> copies what
/// it shows into states once a frame; a state that didn't change costs a compare.
/// </summary>
internal sealed class ToolbarViewModel : ViewModel
{
  private readonly CanvasSession _session;
  private readonly CanvasSelection _selection;
  private readonly ElementKinds _kinds;
  private string _shownScale = "100";
  private float _lastScale = -1;
  private int _scaleSelection = -2;
  private int _countShown = -1;
  private string _countLabel = "";
  private int _highlightVersion = -1;

  public ToolbarViewModel(
    CanvasSession session,
    CanvasSelection selection,
    CanvasStatusLine status,
    ElementKinds kinds,
    ICanvasActions canvas
  )
  {
    _session = session;
    _selection = selection;
    _kinds = kinds;
    Status = status;
    HasFonts = session.Fonts != null;
    Computed<bool> scalable = Computed(() => HasSelection.Value && !Several.Value);
    Computed<bool> selected = Computed(() => HasSelection.Value);
    Scalable = scalable;
    Undo = Command(() => canvas.Undo(), canExecute: Computed(() => CanUndo.Value));
    Redo = Command(() => canvas.Redo(), canExecute: Computed(() => CanRedo.Value));
    Cancel = Command(() => canvas.Cancel());
    Save = Command(() => canvas.Save());
    ResetAll = Command(() => canvas.ResetAll());
    ResetElement = Command(() => canvas.ResetSelected(), canExecute: selected);
    Delete = Command(() => canvas.Delete(), canExecute: selected);
    Add = Command(() => canvas.Add());
    Layers = Command(() => canvas.Layers());
    Color = Command(() => canvas.Color());
    Font = Command(() => canvas.Font(), canExecute: Computed(() => HasFonts));
    Edit = Command(() => canvas.Edit());
    Grow = Command(() => ScaleBy(1.05f), canExecute: scalable);
    Shrink = Command(() => ScaleBy(.95f), canExecute: scalable);
    Snap = Command(() =>
    {
      _session.Preferences.LayoutSnapping = !_session.Preferences.LayoutSnapping;
      Snapping.Value = _session.Preferences.LayoutSnapping;
    });
    FinishTyping = Command(() =>
    {
      if (Pending)
        CommitScale(Scale.Value);
    });
  }

  /// <summary>The status line under the controls.</summary>
  public readonly CanvasStatusLine Status;

  /// <summary>Whether the overlay fonts loaded: the default font can be picked.</summary>
  public readonly bool HasFonts;

  /// <summary>"Text", "3 selected", or "Select overlay" with nothing selected.</summary>
  public readonly State<string> Selection = new("");

  public readonly State<bool> HasSelection = new(false);

  /// <summary>Several selected: they have no one scale.</summary>
  public readonly State<bool> Several = new(false);

  /// <summary>One element sized in its editor is selected: its Edit button stands where the scale is.</summary>
  public readonly State<bool> SizedInEditor = new(false);

  /// <summary>That Edit button's words, such as "Edit text".</summary>
  public readonly State<string> EditCaption = new("");

  /// <summary>The scale field's words: the selected element's percent, a dash for several, or what is being typed.</summary>
  public readonly State<string> Scale = new("100");

  public readonly State<bool> Snapping = new(false);

  public readonly State<bool> CanUndo = new(false);

  public readonly State<bool> CanRedo = new(false);

  /// <summary>The highlight color, on the Highlight button's dot.</summary>
  public readonly State<UiColor> Highlight = new(UiPalette.Accent);

  /// <summary>Whether the scale can be typed or stepped: one element is selected.</summary>
  public Computed<bool> Scalable { get; }

  public Command Undo { get; }
  public Command Redo { get; }
  public Command Cancel { get; }
  public Command Save { get; }
  public Command ResetAll { get; }
  public Command ResetElement { get; }
  public Command Delete { get; }
  public Command Add { get; }

  /// <summary>Opens or closes the layers panel.</summary>
  public Command Layers { get; }

  /// <summary>Whether the layers panel is open: its button stays lit.</summary>
  public readonly State<bool> LayersOpen = new(false);
  public Command Color { get; }
  public Command Font { get; }

  /// <summary>Opens the selected element's editor.</summary>
  public Command Edit { get; }

  public Command Grow { get; }
  public Command Shrink { get; }
  public Command Snap { get; }

  /// <summary>The scale field let go of the keyboard: a typed scale is applied, or refused with the reason.</summary>
  public Command FinishTyping { get; }

  /// <summary>Whether the field holds a typed scale that isn't applied yet.</summary>
  private bool Pending => Scale.Value != _shownScale;

  /// <summary>
  /// Once a frame: what is selected, which controls apply, the scale (unless
  /// it is being typed), the highlight color and the status line.
  /// </summary>
  public void Sync(bool typing)
  {
    CanUndo.Value = _session.History.CanUndo;
    CanRedo.Value = _session.History.CanRedo;
    bool any = _selection.HasSelection;
    bool several = _selection.Multi;
    IOverlayLayoutTarget primary = _selection.Primary;
    HasSelection.Value = any;
    Several.Value = several;
    // Several selected show the scale, unavailable, rather than one element's Edit.
    bool edit = !several && _kinds.SizedInEditor(primary);
    SizedInEditor.Value = edit;
    if (edit)
      EditCaption.Value = _kinds.EditCaption(primary);
    Selection.Value = SelectionLabel(any, several, primary);
    Snapping.Value = _session.Preferences.LayoutSnapping;
    SyncScale(typing, any, several, primary);
    if (_highlightVersion != UiTheme.Version)
    {
      _highlightVersion = UiTheme.Version;
      Highlight.Value = UiPalette.Accent;
    }
    Status.Tick();
  }

  // Counted labels are made when the count changes, not every frame.
  private string SelectionLabel(bool any, bool several, IOverlayLayoutTarget primary)
  {
    if (!any)
      return L("요소 선택", "Select overlay");
    if (!several)
      return _kinds.Label(primary);
    // A whole group is named, not counted.
    string group = _selection.Group;
    if (group != null)
      return L("묶음 · ", "Group · ") + GroupNames.Of(_session.Host.LayoutSettings, group);
    int count = _selection.Count;
    if (count != _countShown)
    {
      _countShown = count;
      _countLabel = L(count + "개 선택됨", count + " selected");
    }
    return _countLabel;
  }

  private void SyncScale(bool typing, bool any, bool several, IOverlayLayoutTarget primary)
  {
    float shown =
      several ? -2
      : any ? primary.EffectiveScale * 100
      : 100;
    if (_scaleSelection != _selection.Selected)
    {
      _scaleSelection = _selection.Selected;
      Status.Clear();
      ForgetScale();
    }
    if (typing || Pending || shown == _lastScale)
      return;
    // A dash on its own: several elements have no one scale.
    _shownScale = several ? "—" : shown.ToString("0.#", CultureInfo.InvariantCulture);
    Scale.Value = _shownScale;
    _lastScale = shown;
  }

  /// <summary>A scale being typed is dropped; the field shows the element's again once it lets go.</summary>
  public void DropTypedScale() => Scale.Value = _shownScale;

  /// <summary>The scale shown is read again; a typed scale is dropped.</summary>
  public void ForgetScale()
  {
    DropTypedScale();
    _lastScale = -1;
  }

  /// <summary>Applies a scale still being typed, as when the editor saves; false when it can't be used.</summary>
  public bool CommitTyped() => !_selection.HasSelection || !Pending || CommitScale(Scale.Value);

  /// <summary>
  /// Applies a typed percent to the selected element; false, with the range
  /// on the status line, when it isn't a number in range.
  /// </summary>
  public bool CommitScale(string value)
  {
    if (_session.Closing || !_selection.HasSelection)
      return true;
    float current = CanvasSession.ValidScale(_selection.Primary.EffectiveScale);
    float min = Mathf.Min(20, current * 100);
    float max = Mathf.Max(CanvasSession.MaximumScale * 100, current * 100);
    if (
      !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float percent)
      || !OverlayLayoutGeometry.Finite(percent)
      || percent < min
      || percent > max
    )
    {
      string range =
        min.ToString("0.#", CultureInfo.InvariantCulture) + "–" + max.ToString("0.#", CultureInfo.InvariantCulture);
      Status.ShowError(L(range + "%로 입력하세요", "Enter " + range + "%"));
      _lastScale = -1;
      return false;
    }
    SetScale(percent / 100f);
    return true;
  }

  /// <summary>Scales the selected element by <paramref name="ratio"/>: + and −, and the keys.</summary>
  public void ScaleBy(float ratio)
  {
    if (_selection.HasSelection)
      SetScale(_selection.Primary.EffectiveScale * ratio);
  }

  private void SetScale(float scale)
  {
    // The typed words are used; the field shows the element's scale again.
    _shownScale = Scale.Value;
    _lastScale = -1;
    Status.Clear();
    IOverlayLayoutTarget target = _session.Frames[_selection.Selected].Target;
    float current = CanvasSession.ValidScale(target.EffectiveScale);
    float maximum = CanvasSession.MaximumScale;
    target.SetBounds(
      CanvasSession.ToRect(
        OverlayLayoutGeometry.ScaleAtCenter(
          CanvasSession.ToBounds(target.Bounds),
          Mathf.Clamp(scale, Mathf.Min(.2f, current), Mathf.Max(maximum, current)) / current,
          _session.ViewWidth,
          _session.ViewHeight,
          Mathf.Min(1, .2f / current),
          Mathf.Max(1, maximum / current)
        )
      )
    );
    target.CommitLayout(false);
    _session.Commit();
  }
}
