using Element.Presentation.OverlayEditor;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Canvas.Toolbar;
using System.Linq;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;

namespace Element.Editor
{
  // The layout editor's panels (toolbar, element toolbar, add menu, dialog)
  // are ElementUI screens in frames of their own: these reach their view
  // models and the controls they draw, found by the names the views give them.
  public static partial class SettingsPreview
  {
    /// <summary>Runs ElementUI's pending updates and layout, as the end of a frame would.</summary>
    private static void Settle()
    {
      for (int i = 0; i < 4; i++)
      {
        ElementUI.Runtime.Flush(1);
        Canvas.ForceUpdateCanvases();
      }
    }

    private static ToolbarViewModel ToolbarModel(OverlayEditorScreen editor) => Toolbar(editor).Model;

    private static CanvasStatusLine Status(OverlayEditorScreen editor) => Field<CanvasStatusLine>(editor, "_status");

    /// <summary>The status line as the toolbar draws it.</summary>
    private static TextMeshProUGUI StatusLabel(OverlayEditorScreen editor)
    {
      Settle();
      return ByName<TextMeshProUGUI>(Toolbar(editor).Root, "Status");
    }

    /// <summary>The <typeparamref name="T"/> on the object named <paramref name="name"/> under <paramref name="root"/>, shown or not; null when none.</summary>
    private static T ByName<T>(Transform root, string name) where T : Component =>
      root == null ? null : root.GetComponentsInChildren<T>(true).FirstOrDefault(component => component.name == name);

    private static Pressable ToolbarButton(OverlayEditorScreen editor, string name) => ByName<Pressable>(Toolbar(editor).Root, name);

    private static TMP_InputField ScaleField(OverlayEditorScreen editor) => ByName<TMP_InputField>(Toolbar(editor).Root, "Scale percent");

    private static bool Shown(Component component) => component != null && component.gameObject.activeInHierarchy;

    /// <summary>Presses a button as a click does: only while it can be pressed.</summary>
    private static void Press(Pressable button)
    {
      if (button.IsInteractable())
        button.Click?.Invoke();
    }

    /// <summary>Presses the selected element's Edit button once the toolbar caught up with the selection.</summary>
    private static void PressEdit(OverlayEditorScreen editor)
    {
      Toolbar(editor).Refresh();
      Settle();
      Press(ToolbarButton(editor, "Edit element"));
    }

    /// <summary>A list's rows: the add menu's, or one beside it.</summary>
    private static Pressable[] Rows(RectTransform menu) =>
      menu.GetComponentsInChildren<Pressable>(true).Where(button => button.name.StartsWith("Add ")).ToArray();

    /// <summary>The middle of <paramref name="rect"/> in screen pixels from the top left, as the editor reads the pointer.</summary>
    private static Vector2 PointerOn(RectTransform rect)
    {
      Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
      return new Vector2(screen.x, Screen.height - screen.y);
    }

    /// <summary>
    /// The main toolbar as a frame: drags from its grip but not from its
    /// controls, follows the pointer one to one, is drawn where it says it is
    /// and leaves its cutout to native viewers; hints, undo state and
    /// snapping as its controls show them.
    /// </summary>
    private static void CheckToolbarChrome(OverlayEditorScreen editor)
    {
      ToolbarPanel toolbar = Toolbar(editor);
      Status(editor).Clear();
      Call(editor, "UpdateFrames");
      Settle();
      Rect bounds = editor.ToolbarBounds;
      Rect drawn = DrawnRect(toolbar.Root);
      Assert(Vector2.Distance(drawn.position, bounds.position) < .5f && Mathf.Abs(drawn.width - bounds.width) < .5f
        && Mathf.Abs(drawn.height - bounds.height) < .5f, "the toolbar is drawn where it says it is: " + drawn + " / " + bounds);
      Vector2 grip = PointerOn(ByName<RectTransform>(toolbar.Root, "Drag panel"));
      Assert(!toolbar.ControlAt(grip) && toolbar.ControlAt(PointerOn((RectTransform)ToolbarButton(editor, "Save").transform))
        && toolbar.ControlAt(PointerOn((RectTransform)ScaleField(editor).transform)),
        "the toolbar drags from its grip; its buttons and field take their presses");
      toolbar.BeginDrag(grip);
      // Up, and a little sideways: the toolbar nearly fills the fixture's width and stays in the view.
      toolbar.Move(grip + new Vector2(4, -50));
      toolbar.Dragging = false;
      Assert(Vector2.Distance(editor.ToolbarBounds.position, bounds.position + new Vector2(4, -50)) < .5f,
        "the toolbar follows its grip one to one");
      Call(editor, "UpdateFrames");
      Settle();
      drawn = DrawnRect(toolbar.Root);
      var cutout = editor.NativeExclusions.Single();
      Assert(Vector2.Distance(drawn.position, editor.ToolbarBounds.position) < .5f
        && Mathf.Abs(cutout.Bounds.X - editor.ToolbarBounds.x) < .5f && Mathf.Abs(cutout.Bounds.Y - editor.ToolbarBounds.y) < .5f,
        "the moved toolbar is drawn and cut out where it went");
      SetField(toolbar, "_placed", false);
      Call(editor, "UpdateFrames");

      // Hovering a control says what it does on the status line, until the pointer leaves.
      Pressable undo = ToolbarButton(editor, "Undo");
      PointerArea area = undo.GetComponentInParent<PointerArea>();
      area.Hover(true);
      Assert(Status(editor).Text.Value == L("실행 취소", "Undo") && StatusLabel(editor).text == L("실행 취소", "Undo")
        && StatusLabel(editor).color == UiPalette.Muted, "a hovered button's hint shows on the status line");
      area.Hover(false);
      Assert(Status(editor).Text.Value == "", "the hint goes when the pointer leaves");

      // Undo and redo follow the history; their icons grey out with them.
      Toolbar(editor).Refresh();
      Settle();
      CanvasSession session = Session(editor);
      Pressable redo = ToolbarButton(editor, "Redo");
      Assert(undo.IsInteractable() == session.History.CanUndo && redo.IsInteractable() == session.History.CanRedo
        && undo.GetComponentInChildren<UiIcon>().color == (session.History.CanUndo ? UiPalette.Foreground : UiPalette.Disabled)
        && redo.GetComponentInChildren<UiIcon>().color == (session.History.CanRedo ? UiPalette.Foreground : UiPalette.Disabled),
        "undo and redo are on while there is something to undo or redo");

      // Snapping is a switch: its icon takes the highlight while it is on.
      Pressable snap = ToolbarButton(editor, "Snapping");
      bool snapping = session.Preferences.LayoutSnapping;
      Press(snap);
      Settle();
      Assert(session.Preferences.LayoutSnapping == !snapping
        && snap.GetComponentInChildren<UiIcon>().color == (snapping ? UiPalette.Muted : UiPalette.SoftAccent),
        "Snapping turns snapping over and shows it");
      Press(snap);
      Settle();
      Assert(session.Preferences.LayoutSnapping == snapping, "a second press puts snapping back");
    }

    /// <summary>Where a panel is drawn, in screen pixels from the top left.</summary>
    private static Rect DrawnRect(RectTransform rect) => Element.Presentation.Ui.Popovers.PopoverPlacement.ScreenRect(rect);
  }
}
