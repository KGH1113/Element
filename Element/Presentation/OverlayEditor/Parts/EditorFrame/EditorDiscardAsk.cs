using System;
using Element.Presentation.OverlayEditor.Parts.Confirm;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.Parts.EditorFrame;

/// <summary>
/// Asks before Cancel throws an edit away: the editor dims and a dialog
/// stands in its middle, the same as the layout editor's questions. Keep
/// editing is focused, so Enter is safe; Esc keeps editing too. Discard is
/// the only way on to Cancel.
/// </summary>
internal sealed class EditorDiscardAsk : IDisposable
{
  private const float Width = 460;

  // Where the frame's key presses go while the question is open.
  private static readonly Event Drained = new();

  private readonly EditorFramePanel _frame;
  private GameObject _root;
  private ScreenPanel _dialog;
  private int _closedFrame = -1;

  public EditorDiscardAsk(EditorFramePanel frame) => _frame = frame;

  /// <summary>The question is open.</summary>
  public bool Open => _root != null;

  /// <summary>The question is open, or was answered this frame: the editor leaves this frame's keys and clicks alone.</summary>
  public bool Holds => _root != null || _closedFrame == Time.frameCount;

  /// <summary>The open dialog; null while it is closed.</summary>
  public RectTransform Dialog => _dialog?.Root;

  /// <summary>Asks; <paramref name="discard"/> runs only if the edit is to be thrown away.</summary>
  public void Ask(Action discard)
  {
    Close();
    _root = _frame
      .Ui.Rect("Discard question", _frame.Root.transform, 0, 0, _frame.ViewWidth, _frame.ViewHeight)
      .gameObject;
    Image backdrop = _frame.Ui.Panel(
      "Backdrop",
      _root.transform,
      0,
      0,
      _frame.ViewWidth,
      _frame.ViewHeight,
      new Color(0, 0, 0, .6f),
      false
    );
    backdrop.raycastTarget = true;
    // Its controls in frame units, as the rest of the editor is.
    RectTransform area = _frame.Ui.Rect("Dialog area", _root.transform, 0, 0, _frame.Width, _frame.Height);
    area.localScale = Vector3.one * _frame.Scale;
    ConfirmViewModel model = new(
      DiscardWords.Title,
      "",
      DiscardWords.Discard,
      Close,
      () =>
      {
        Close();
        discard();
      },
      DiscardWords.KeepEditing
    );
    _dialog = new ScreenPanel("Dialog", area, UiPalette.Background, keyboard: true, radius: 16);
    Vector2 size = _dialog.Mount(_frame.NewBackend(), new ConfirmView(model), model, Width);
    _dialog.Root.anchoredPosition = new Vector2(
      Mathf.Round((_frame.Width - size.x) * .5f),
      -Mathf.Round((_frame.Height - size.y) * .5f)
    );
    Selectable keep = _dialog.Control<Selectable>("Cancel");
    if (keep != null)
      EventSystem.current?.SetSelectedGameObject(keep.gameObject);
  }

  /// <summary>Keeps editing: the question goes.</summary>
  public void Close()
  {
    if (_root == null)
      return;
    _dialog.Dispose();
    _dialog = null;
    UiFactory.Destroy(_root);
    _root = null;
    _closedFrame = Time.frameCount;
    EventSystem.current?.SetSelectedGameObject(null);
  }

  /// <summary>Once a frame: Esc keeps editing, and the keys of the frame are the question's alone.</summary>
  public void Tick()
  {
    if (_root == null)
      return;
    if (Input.GetKeyDown(KeyCode.Escape))
    {
      Close();
      return;
    }
    // The editor reads key presses from the event queue: none of them is its own while the question is open.
    while (EditorKeys.NextKeyDown(Drained)) { }
  }

  public void Dispose() => Close();
}
