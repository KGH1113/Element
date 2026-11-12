using System.Linq;
using Element.Configuration.Overlay;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Ui.Theme;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  // The text and shape editors' chrome (the top line with Cancel and Done,
  // the bar, Grow from's popover) is ElementUI screens on panels the
  // editor's frame makes: these reach their view models and the controls
  // they draw, found by the names the views give them.
  public static partial class SettingsPreview
  {
    private static OverlayEditorFrame EditorScreen(object editor) => Field<OverlayEditorFrame>(editor, "_screen");

    private static OverlayEditorBar EditorBar(object editor) => Field<OverlayEditorBar>(editor, "_bar");

    /// <summary>The control a bar button is drawn as, once the bar caught up.</summary>
    private static Pressable BarButton(object editor, OverlayBarButtonModel button)
    {
      Settle();
      RectTransform rect = EditorBar(editor).RectOf(button);
      return rect != null ? rect.GetComponent<Pressable>() : null;
    }

    private static Rect BarButtonRect(object editor, OverlayBarButtonModel button) =>
      ScreenRectOf((RectTransform)BarButton(editor, button).transform);

    /// <summary>Clicks a bar button as the player does, and lets the screen catch up.</summary>
    private static void PressBar(object editor, OverlayBarButtonModel button)
    {
      Pressable drawn = BarButton(editor, button);
      Assert(drawn != null, "the bar draws " + button.Name);
      Press(drawn);
      Settle();
    }

    /// <summary>Presses the open editor's Cancel or Done, named after the editor.</summary>
    private static void PressEditorButton(object editor, string name) =>
      Press(ByName<Pressable>(EditorScreen(editor).Frame, name));

    /// <summary>
    /// The top line: the editor's shortcuts, then Cancel and Done at its right
    /// end; a notice takes the shortcuts' place for a while, warm for a
    /// warning; its buttons never keep the keyboard, so Enter and Esc stay the editor's.
    /// </summary>
    private static void CheckEditorTopLine(object editor, string name, string title)
    {
      OverlayEditorFrame screen = EditorScreen(editor);
      // A notice an earlier step left has been read.
      SetField(screen.Top, "_noticeUntil", -1f);
      screen.Tick();
      Settle();
      RectTransform frame = screen.Frame;
      TextMeshProUGUI line = ByName<TextMeshProUGUI>(frame, "Shortcuts");
      Pressable cancel = ByName<Pressable>(frame, name + " cancel"), done = ByName<Pressable>(frame, name + " done");
      string hint = Field<string>(screen.Top, "_hint");
      Assert(line != null && line.text == hint && hint.StartsWith(title) && line.color == UiPalette.Muted && Shown(cancel) && Shown(done)
        && cancel.GetComponentInChildren<TextMeshProUGUI>().text == L("취소", "Cancel")
        && done.GetComponentInChildren<TextMeshProUGUI>().text == L("완료", "Done"),
        "the top line shows the editor's shortcuts, Cancel and Done: " + line?.text);
      Rect lineRect = ScreenRectOf(line.rectTransform), cancelRect = ScreenRectOf((RectTransform)cancel.transform),
        doneRect = ScreenRectOf((RectTransform)done.transform);
      Assert(lineRect.xMax <= cancelRect.xMin && cancelRect.xMax < doneRect.xMin && Mathf.Abs(cancelRect.center.y - doneRect.center.y) < .5f
        && Mathf.Abs(lineRect.center.y - doneRect.center.y) < 2 && InsideScreen(doneRect)
        && Mathf.Abs(Screen.width - doneRect.xMax - OverlayEditorFrame.Margin * screen.Scale) < 1,
        "the shortcuts on the left, Cancel and Done at the right end, on one line: " + cancelRect + " / " + doneRect);
      Assert(doneRect.yMin > ScreenRectOf(EditorBar(editor).Root).yMax, "the top line sits above the bar");
      Assert(done.GetComponent<RoundedRect>().color.maxColorComponent < .5f && cancel.navigation.mode == Navigation.Mode.None
        && done.navigation.mode == Navigation.Mode.None, "Done is the tinted main action; neither keeps the keyboard");
      foreach (TextMeshProUGUI label in new[] { line }.Concat(cancel.GetComponentsInChildren<TextMeshProUGUI>()).Concat(done.GetComponentsInChildren<TextMeshProUGUI>()))
      {
        label.ForceMeshUpdate();
        Assert(label.font == screen.Measure.font && !label.isTextTruncated, "top line label fits: " + label.text);
      }
      screen.Notice("Preview warning");
      Settle();
      Assert(screen.NoticeText == "Preview warning" && line.text == "Preview warning" && line.color == UiPalette.Error,
        "a warning shows where the shortcuts are, warm");
      screen.Notice("Preview note", false);
      Settle();
      Assert(line.text == "Preview note" && line.color == UiPalette.Foreground, "a note reads white");
      SetField(screen.Top, "_noticeUntil", -1f);
      screen.Tick();
      Settle();
      Assert(screen.NoticeText == "" && line.text == hint && line.color == UiPalette.Muted, "the shortcuts come back once a notice has been read");
    }

    /// <summary>
    /// Grow from's popover as drawn: what it picks, the nine points, Auto and
    /// a line saying what the choice does; a point's press picks it and Auto
    /// puts Auto back. Leaves the growth as it found it.
    /// </summary>
    private static void CheckGrowPopover(OverlayGrowFrom grow)
    {
      OverlayGrowPopover popover = Field<OverlayGrowPopover>(grow, "_popover");
      OverlayGrowth before = grow.Growth;
      Settle();
      Pressable[] points = popover.Root.GetComponentsInChildren<Pressable>(true).Where(point => point.name.StartsWith("Grow from ")).ToArray();
      Assert(points.Select(point => point.name).SequenceEqual(Enumerable.Range(0, 9).Select(i => "Grow from " + (OverlayGrowth)i)),
        "the popover offers the nine points, row by row");
      Pressable auto = ByName<Pressable>(popover.Root, "Grow automatically");
      TextMeshProUGUI caption = ByName<TextMeshProUGUI>(popover.Root, "Caption");
      Assert(Shown(auto) && caption != null && caption.text == popover.Caption && caption.text.Length > 0
        && popover.Root.GetComponentsInChildren<TextMeshProUGUI>(false).Any(label => label.text == L("크기가 바뀔 때 고정할 점", "Point that stays when the size changes")),
        "it says what it picks, with Auto and what the choice does");
      foreach (TextMeshProUGUI label in popover.Root.GetComponentsInChildren<TextMeshProUGUI>(false))
      {
        label.ForceMeshUpdate();
        Assert(!label.isTextTruncated, "grow popover label fits: " + label.text);
      }
      Rect box = ScreenRectOf(popover.Root);
      Assert(points.All(point => box.Contains(ScreenRectOf((RectTransform)point.transform).center)) && box.Contains(ScreenRectOf(caption.rectTransform).min),
        "its points and caption sit inside it");
      Press(points[(int)OverlayGrowth.BottomRight]);
      Settle();
      OverlayGrowPickerModel model = popover.Model;
      Assert(grow.Growth == OverlayGrowth.BottomRight && model.Points[8].Look.Value == GrowLook.Chosen
        && model.Points[4].Look.Value == GrowLook.Arrow && model.Points[5].Look.Value == GrowLook.Arrow
        && model.Points[0].Look.Value == GrowLook.Dot && caption.text.Contains(L("오른쪽 아래", "bottom right")),
        "a point's press picks it: filled, arrows on the points around it, the caption names it: " + caption.text);
      Press(auto);
      Settle();
      Assert(grow.Growth == OverlayGrowth.Auto && caption.text.StartsWith(L("자동", "Auto")) 
        && auto.GetComponentInChildren<TextMeshProUGUI>().color == UiPalette.Foreground,
        "Auto follows where the element sits, and lights up: " + caption.text);
      Call(grow, "Pick", before);
    }
  }
}
