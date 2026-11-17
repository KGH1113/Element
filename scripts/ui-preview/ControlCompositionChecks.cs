using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.OverlayEditor.Parts.Property;
using System;
using System.Linq;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Input;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Editor
{
  internal static class ControlCompositionChecks
  {
    [PageCheck("_general")]
    private static int NativeControlComposition(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message) { check.Assert(condition, message); count++; }
      GameObject fixture = new GameObject("Control composition smoke", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
      fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
      ScreenPanel panel = new ScreenPanel("Composition panel", fixture.transform, Color.clear, keyboard: false);
      try
      {
        TMP_FontAsset font = check.Root.GetComponentsInChildren<TMP_Text>(true).First(text => text.font != null).font;
        var theme = new ElementTheme(font, reduceMotion: true);
        State<bool> on = new State<bool>(false), disabled = new State<bool>(true);
        panel.Mount(new ElementBackend(theme),
          SwitchCards.SwitchCard("Inherited switch", "The card and its switch share one value", on.ToBinding())
            .Disabled(() => disabled.Value), null, 280);
        panel.Root.anchoredPosition = new Vector2(30, -30);
        check.Settle();
        Pressable toggle = panel.Root.GetComponentsInChildren<Pressable>().Single(control => control.name == "Switch");
        TMP_Text title = panel.Root.GetComponentsInChildren<TMP_Text>().Single(text => text.text == "Inherited switch");
        Pressable card = title.GetComponentInParent<Pressable>();
        Assert(!card.IsInteractable() && !toggle.IsInteractable(), "inherited Disabled reaches both the outer card and nested switch");
        Assert(title.color == theme.Color(ColorToken.Muted), "inherited Disabled updates the EnvironmentReader title color");
        GameObject titleHit = PageUi.HitTarget(title), toggleHit = PageUi.HitTarget(toggle);
        Assert(titleHit == card.gameObject && toggleHit == toggle.gameObject,
          "the card title and nested switch have distinct native hit targets: title=" + (titleHit?.name ?? "none")
          + ", switch=" + (toggleHit?.name ?? "none") + "; "
          + string.Join("; ", panel.Root.GetComponentsInChildren<Graphic>().Select(graphic => graphic.name + " depth=" + graphic.depth
            + " hit=" + graphic.raycastTarget + " rect=" + PageUi.World(graphic))));
        PageUi.PressHit(title);
        PageUi.PressHit(toggle);
        Assert(!on.Value, "neither disabled hit target writes the binding");
        disabled.Value = false;
        check.Settle();
        Assert(card.IsInteractable() && toggle.IsInteractable() && title.color == theme.Color(ColorToken.Foreground),
          "reenabling restores both targets and inherited title color");
        PageUi.PressHit(title);
        check.Settle();
        Assert(on.Value, "a card press toggles once");
        PageUi.PressHit(toggle);
        check.Settle();
        Assert(!on.Value, "a nested switch press toggles once without also pressing its card");
        RectTransform thumb = (RectTransform)toggle.transform.Find("Thumb");
        float offX = thumb.anchoredPosition.x;
        on.Value = true;
        check.Settle();
        Assert(thumb.anchoredPosition.x > offX, "an external binding update reaches the retained native switch");

        var row = new PropertyButtonViewModel("Fixture width", "Width", () => { }, value: true, unit: "px");
        var second = new PropertyButtonViewModel("Fixture height", "Height", () => { }, value: true, unit: "px");
        var inspector = new InspectorViewModel("Geometry", new[] { row, second }, () => { });
        panel.Mount(new ElementBackend(theme), new InspectorView(inspector, 280), inspector, 280, followsContent: true);
        check.Settle();
        float rowsHeight = panel.Size.y;
        int scans = panel.ControlsVersion;
        int sizeChanges = 0;
        panel.Host.Root.ContentSizeChanged += _ => sizeChanges++;
        Assert(Mathf.Approximately(panel.Root.rect.height, rowsHeight) && rowsHeight > InspectorRowStyle.BackHeight,
          "the inspector native rect and ScreenPanel.Size fit its rows");
        row.Set("200", false, false);
        check.Settle();
        Assert(panel.ControlsVersion == scans, "changing a row value does not rescan the unchanged control tree");
        inspector.ShowOpen("Size");
        check.Settle();
        Assert(Mathf.Approximately(panel.Size.y, InspectorRowStyle.BackHeight)
          && Mathf.Approximately(panel.Root.rect.height, panel.Size.y), "opening a tool fits the native inspector to its back row");
        Pressable back = panel.Root.GetComponentsInChildren<Pressable>().Single(control => control.name == "Inspector back");
        Assert(panel.ControlsVersion > scans && back.navigation.mode == Navigation.Mode.None && back.GetComponent<UiCursor>() != null,
          "a newly mounted back row receives cursor and toolbar keyboard policy without manual Sync");
        inspector.ShowOpen(null);
        check.Settle();
        Assert(Mathf.Approximately(panel.Size.y, rowsHeight) && Mathf.Approximately(panel.Root.rect.height, rowsHeight),
          "closing a tool restores the fitted rows height");
        int settledChanges = sizeChanges, settledScans = panel.ControlsVersion;
        check.Settle();
        Assert(sizeChanges == settledChanges && panel.ControlsVersion == settledScans,
          "idle flushes do not refit or rescan the inspector");
      }
      finally
      {
        panel.Dispose();
        RectPeer.DestroyObject(fixture);
      }
      return count;
    }
  }
}
