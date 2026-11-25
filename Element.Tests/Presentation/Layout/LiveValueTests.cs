using System.Text;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Presentation.Layout;

// An editor open over a level that plays (autoplay) follows the values it
// shows, without taking the changes the canvas behind it redraws from.
internal static class LiveValueTests
{
  [Suite("open editors follow live values without taking the canvas's changes")]
  public static void Run()
  {
    OverlayValues values = new();
    EditorLiveValues editor = new(values);
    OverlayValueMask progress = OverlayValueCatalog.Bit(OverlayValueId.ProgressCurrent);
    OverlayValueMask combo = OverlayValueCatalog.Bit(OverlayValueId.Combo);
    Check(!editor.Changed(progress), "nothing changed yet");

    values.Set(OverlayValueId.ProgressCurrent, new StringBuilder("48"), 48);
    Check(editor.Changed(progress), "a value the editor shows changed");
    Check(!editor.Changed(progress), "it is seen once");

    values.Set(OverlayValueId.Combo, new StringBuilder("12"), 12);
    Check(!editor.Changed(progress), "a value the editor doesn't use doesn't redraw it");
    values.Set(OverlayValueId.Combo, new StringBuilder("12"), 12);
    Check(!editor.Changed(combo), "setting the same value again changes nothing");

    values.Set(OverlayValueId.Combo, new StringBuilder("13"), 13);
    Check(editor.Changed(combo | progress), "any value in the mask counts");
    Check((values.TakeChanges() & (progress | combo)) == (progress | combo), "the canvas still gets every change");
    values.Clear(OverlayValueId.ProgressCurrent);
    Check(editor.Changed(progress), "a value going away counts as a change");
  }
}
