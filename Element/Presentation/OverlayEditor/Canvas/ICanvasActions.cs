using System.Collections.Generic;
using Element.Application.Layout;

namespace Element.Presentation.OverlayEditor.Canvas;

/// <summary>
/// Everything the layout editor can be asked to do, in one list: what the
/// player's toolbar buttons and keys do, then what its parts and element
/// kinds ask of it. <see cref="OverlayEditorScreen"/> does each one. What they
/// read from it goes the other way, through <see cref="ICanvasContext"/>.
/// </summary>
internal interface ICanvasActions
{
  // ---- The player's commands: the main toolbar and the canvas's keys ----

  void Undo();
  void Redo();

  /// <summary>Float view: the elements stand apart in depth; the order panel steps aside meanwhile.</summary>
  void OpenFloatView();

  /// <summary>Back from Float view to the flat canvas.</summary>
  void CloseFloatView();

  /// <summary>Leaves without saving.</summary>
  void Cancel();

  /// <summary>Saves and leaves.</summary>
  void Save();

  void ResetAll();
  void ResetSelected();
  void Delete();

  /// <summary>Opens or closes Add Element's menu.</summary>
  void Add();

  /// <summary>Opens or closes the highlight color.</summary>
  void Color();

  /// <summary>Opens or closes the default font.</summary>
  void Font();

  /// <summary>Opens the selected element's editor.</summary>
  void Edit();

  // ---- Layers and groups ----

  /// <summary>Opens or closes the layers panel above the toolbar.</summary>
  void Layers();

  /// <summary>Makes the selected elements one group.</summary>
  void Group();

  /// <summary>Takes the selected group apart; its elements stay where they are.</summary>
  void Ungroup();

  /// <summary>
  /// Moves the selection to the front (<paramref name="forward"/>) or the back:
  /// past the nearest element it overlaps, or all the way with <paramref name="toEnd"/>.
  /// </summary>
  void Arrange(bool forward, bool toEnd);

  /// <summary>
  /// Moves <paramref name="ids"/> just in front of <paramref name="behindId"/>
  /// (null: the very back), in <paramref name="group"/> or in none for null; one undo step.
  /// </summary>
  void MoveLayers(IReadOnlyCollection<string> ids, string behindId, string group);

  /// <summary>
  /// Shows on the canvas the order a carried layer card would make, without
  /// keeping it; null <paramref name="ids"/> brings the order back as it was.
  /// </summary>
  void PreviewLayers(IReadOnlyCollection<string> ids, string behindId);

  /// <summary>Names <paramref name="group"/>; one undo step.</summary>
  void RenameGroup(string group, string name);

  /// <summary>Selects what a layer card stands for: an element, or a group by its id.</summary>
  void SelectLayer(string id, string group, bool adding);

  /// <summary>Shows only <paramref name="ids"/> clearly while the pointer is over their card; null shows all.</summary>
  void PeekLayers(IReadOnlyCollection<string> ids);

  // ---- What the parts and kinds ask for ----

  /// <summary>
  /// Before a full-screen editor or a dialog: applies typed popover values,
  /// closes the popovers and ends drags. False when a typed value can't be
  /// used; nothing else changes then.
  /// </summary>
  bool Settle();

  /// <summary>A new element in the middle of the view, selected; one undo step with <paramref name="commit"/>.</summary>
  void PlaceNew(IOverlayLayoutTarget target, bool commit);

  /// <summary>Places every element again from its settings.</summary>
  void RefreshLayouts();

  /// <summary>Updates the outlines, toolbars and popovers to what changed.</summary>
  void UpdateFrames();
}
