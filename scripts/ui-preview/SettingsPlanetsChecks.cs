using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Element.Configuration.Gameplay;
using Element.Presentation.Settings.Pages;
using Element.Presentation.Settings.Window;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Editor
{
  /// <summary>
  /// Planets: the switches, the tab per planet, each part's map or custom
  /// color and the color picker it opens, and the game's planet preview,
  /// which draws only while the page shows. Presses go through the controls
  /// on screen; what reaches the renderer is read from the preview probe.
  /// </summary>
  internal static class SettingsPlanetsChecks
  {
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [PageCheck("_planets")]
    private static int Check(SettingsCheck check)
    {
      int count = 0;
      void Assert(bool condition, string message)
      {
        check.Assert(condition, message);
        count++;
      }

      SettingsWindowViewModel window = check.Model;
      PlanetsModel page = window.Planets;
      PlanetAppearanceOptions planets = check.Settings.Gameplay.Planets;
      Renderer preview = new Renderer(check.Features.RenderPlanetPreview);
      UiHost host = Host(check);
      int edits = 0;
      host.Root.Edited += () => edits++;
      // A frame of the open window: the page's per-frame poll, then ElementUI's updates.
      void Frame()
      {
        check.Settle();
        window.Current.Tick();
        check.Settle();
      }

      Assert(preview.Calls > 0 && preview.Index == 0 && ReferenceEquals(preview.Profile, planets.Red),
        "opening Planets draws the Fire planet through the game's renderer");
      FitScreen(check, host);
      Assert(Mathf.Abs(page.Width - (WindowRect(check).rect.width - 64)) < 1,
        "the page knows the width it is laid out in: " + page.Width);
      count += CheckTexture(check, preview);

      // Off: nothing on the planet can be edited.
      planets.Enabled = false;
      ElementUI.Runtime.Probe();
      Frame();
      Assert(Swatches(check).Length == 3 && Swatches(check).All(button => !button.interactable)
        && Sources(check).Length == 3 && Sources(check).All(button => !button.interactable),
        "with custom planets off, no part's color source or color can be edited");
      Assert(!Switches(check)[1].interactable, "with custom planets off, the planet's own switch is off too");
      Assert(Caption(check) == L("미리보기 · 게임에는 적용 안 됨", "Preview · not applied"),
        "the preview says it isn't applied while custom planets are off");
      int before = preview.Calls;
      Press(Switches(check)[0]);
      Frame();
      Assert(planets.Enabled && preview.Calls > before, "turning custom planets on draws the planet again at once");
      if (!planets.Red.Enabled)
      {
        Press(Switches(check)[1]);
        Frame();
      }
      Assert(Swatches(check).All(button => button.interactable) && Sources(check).All(button => button.interactable),
        "with the planet customized, its body, trail and ring can be edited");
      Assert(Caption(check) == L("색상 미리보기", "Color preview"), "the preview names what it shows");

      before = preview.Calls;
      Press(Sources(check)[0]);
      Frame();
      Assert(preview.Calls > before && !preview.Snapshot.Body.OverrideColor && !planets.Red.Body.OverrideColor,
        "switching Body to the map's color reaches the renderer at once");
      Assert(Sources(check)[0].GetComponentInChildren<TextMeshProUGUI>().text == L("맵 색상", "Map color"),
        "Body's source button says it follows the map");

      // Body: the picker opens under its button, over a layer that closes it.
      Press(Swatches(check)[0]);
      check.Settle();
      UiColorPicker picker = Picker(host);
      Assert(picker != null && picker.Visible && host.Root.HasOverlay && Backdrop(check) != null,
        "Body's color opens the color picker with a layer behind it that closes it");
      Assert(Title(picker) == L("공 색상", "Body color"), "the picker names the part it colors");
      Transform layer = Overlays(check);
      Assert(layer.GetSiblingIndex() == layer.parent.childCount - 1, "the open picker draws, and takes clicks, in front of the page");
      if (check.Name == "planets-ko-720")
        check.Save("planets-ko-picker");
      count += CheckPickerPlacement(check, picker, Swatches(check)[0]);
      before = preview.Calls;
      int editsBefore = edits;
      Input(picker, "_opacity").text = "50";
      Frame();
      Assert(preview.Calls > before && preview.Snapshot.Body.Opacity == 50 && !preview.Snapshot.Body.OverrideColor,
        "Body's opacity reaches the renderer and keeps the map's color");
      Assert(edits > editsBefore, "a color picked in the picker is applied like any control's edit");
      Assert(Percent(Swatches(check)[0]) == "50%", "Body's button shows the new opacity");
      before = preview.Calls;
      Input(picker, "_hex").text = "2468AC";
      Frame();
      Assert(preview.Calls > before && preview.Snapshot.Body.ColorHex == "#2468AC"
        && preview.Snapshot.Body.Opacity == 50 && preview.Snapshot.Body.OverrideColor,
        "a new Body color becomes a custom color at once and keeps its opacity");
      Assert(Sources(check)[0].GetComponentInChildren<TextMeshProUGUI>().text == L("직접 지정", "Custom color"),
        "Body's source button says the color is custom after a color is picked");
      Assert(Near(Dot(Swatches(check)[0]).color, new Color32(0x24, 0x68, 0xAC, 0xFF)),
        "Body's button shows the picked color, opaque");
      Assert(picker.Commit(), "the picked color and opacity commit");
      Close(picker);
      check.Settle();
      Assert(!host.Root.HasOverlay && Picker(host) == null && picker.Root == null,
        "closing the picker takes it and its layer away");
      Sweep(check, host);

      string[] partHex = { "2468AC", "426688", "6842AA" };
      string[] partOpacity = { "50", "25", "0" };
      for (int i = 1; i < 3; i++)
      {
        Press(Swatches(check)[i]);
        check.Settle();
        picker = Picker(host);
        before = preview.Calls;
        Input(picker, "_hex").text = partHex[i];
        Input(picker, "_opacity").text = partOpacity[i];
        Frame();
        PlanetComponentAppearance part = i == 1 ? preview.Snapshot.Tail : preview.Snapshot.Ring;
        Assert(preview.Calls > before && part.ColorHex == "#" + partHex[i] && part.Opacity == float.Parse(partOpacity[i]),
          "each part's color and opacity reach the renderer at once: " + i);
        Assert(preview.Snapshot.Body.ColorHex == "#2468AC" && preview.Snapshot.Body.Opacity == 50,
          "editing Trail or Ring leaves Body as it was");
        // Escape closes the picker, as the window's Escape does.
        Assert(host.HandleEscape(), "Escape closes the open picker");
        check.Settle();
        Assert(!host.Root.HasOverlay, "the picker is gone after Escape");
        Sweep(check, host);
      }

      PlanetAppearanceProfile[] profiles = { planets.Red, planets.Blue, planets.Green };
      for (int i = 1; i < 3; i++)
      {
        before = preview.Calls;
        Press(Tabs(check)[i]);
        Frame();
        Assert(page.Planet == i && preview.Calls > before && preview.Index == i && ReferenceEquals(preview.Profile, profiles[i]),
          "a planet's tab draws that planet at once: " + i);
        count += CheckTexture(check, preview);
        string fire = planets.Red.Body.ColorHex;
        Press(Swatches(check)[0]);
        check.Settle();
        before = preview.Calls;
        string hex = i == 1 ? "80A0C0" : "20C080";
        Input(Picker(host), "_hex").text = hex;
        Frame();
        Assert(preview.Calls > before && preview.Index == i && preview.Snapshot.Body.ColorHex == "#" + hex
          && planets.Red.Body.ColorHex == fire && profiles[i].Body.ColorHex == "#" + hex,
          "editing the picked planet leaves Fire alone and draws the picked one");
        Close(Picker(host));
        check.Settle();
        Sweep(check, host);
      }

      before = preview.Calls;
      Press(Switches(check)[1]);
      Frame();
      Assert(preview.Calls > before && !preview.Snapshot.Enabled && !planets.Green.Enabled
        && Swatches(check).All(button => !button.interactable) && page.Planet == 2,
        "turning the planet's own colors off shows at once and keeps the planet picked");
      Press(Switches(check)[1]);
      Frame();

      preview.Available = false;
      page.RefreshPreview();
      check.Settle();
      RawImage image = Image(check);
      Assert(!image.enabled && image.texture == null && Caption(check) == L("공 미리보기를 불러올 수 없습니다.", "Planet preview is unavailable."),
        "without the game's drawing the preview clears and says it is unavailable");
      preview.Available = true;
      page.RefreshPreview();
      check.Settle();
      count += CheckTexture(check, preview);

      // Leaving the page hides its picker and stops drawing; coming back finds it closed.
      Press(Swatches(check)[1]);
      check.Settle();
      picker = Picker(host);
      Assert(host.Root.HasOverlay && picker.Root.gameObject.activeInHierarchy, "Trail's picker is open before leaving");
      Show(window, window.Tweaks);
      check.Settle();
      // Outside play mode ElementUI destroys a closed picker at once; in the game it hides first.
      Assert((picker.Root == null || !picker.Root.gameObject.activeInHierarchy) && Backdrop(check) == null,
        "leaving Planets leaves no picker or layer on screen");
      before = preview.Calls;
      window.Current.Tick();
      page.RefreshPreview();
      Assert(preview.Calls == before, "Planets doesn't draw while another page shows");
      Show(window, page);
      check.Settle();
      Assert(preview.Calls > before && !host.Root.HasOverlay && Picker(host) == null,
        "coming back draws the planet, with the picker closed");
      Sweep(check, host);

      // Closing the window closes an open picker; nothing draws until the window opens again.
      Press(Swatches(check)[2]);
      check.Settle();
      Assert(host.Root.HasOverlay, "Ring's picker is open before the window closes");
      check.Root.SetActive(false);
      foreach (SettingsPageModel each in window.Pages)
        each.Closed();
      check.Settle();
      Assert(!host.Root.HasOverlay && Picker(host) == null, "closing the window closes the picker");
      before = preview.Calls;
      page.RefreshPreview();
      page.Tick();
      Assert(preview.Calls == before && page.Preview == null, "a closed window draws no planet and lets go of the drawing");
      check.Root.SetActive(true);
      Sweep(check, host);
      window.Opened();
      Assert(preview.Calls == before, "opening the window on Home draws no planet");
      Show(window, page);
      Assert(preview.Calls > before, "showing Planets after opening draws the planet");
      check.Settle();

      // A narrow window: the preview goes above the parts.
      count += CheckNarrow(check);

      // The frame the screenshot keeps: Fire, every part custom.
      Press(Tabs(check)[0]);
      Frame();
      Assert(page.Planet == 0 && preview.Index == 0, "Fire is picked for the screenshot");
      count += CheckTexture(check, preview);
      return count;
    }

    // ---- Texture and layout ----

    private static int CheckTexture(SettingsCheck check, Renderer preview)
    {
      RawImage image = Image(check);
      Texture texture = preview.Texture;
      Rect room = ((RectTransform)image.transform.parent).rect;
      Rect artwork = image.rectTransform.rect;
      check.Assert(image.enabled && image.texture == texture && image.color == Color.white && !image.raycastTarget,
        "the preview shows the renderer's own texture, untinted, and lets clicks through");
      check.Assert(Mathf.Abs(artwork.width / artwork.height - texture.width / (float)texture.height) < .01f
        && artwork.width <= room.width + .1f && artwork.height <= room.height + .1f,
        "wide, tall and square drawings fit the preview without stretching");
      TextMeshProUGUI caption = CaptionText(check);
      check.Assert(Bottom(image.rectTransform) >= Top(caption.rectTransform) - .5f,
        "the drawing doesn't cover the caption under it");
      return 3;
    }

    private static int CheckPickerPlacement(SettingsCheck check, UiColorPicker picker, Pressable anchor)
    {
      Vector3[] corners = new Vector3[4];
      picker.Root.GetWorldCorners(corners);
      Vector3[] frame = new Vector3[4];
      WindowRect(check).GetWorldCorners(frame);
      check.Assert(corners[0].x >= frame[0].x - .5f && corners[2].x <= frame[2].x + .5f
        && corners[0].y >= frame[0].y - .5f && corners[2].y <= frame[2].y + .5f,
        "the picker stays inside the window");
      Vector3[] button = new Vector3[4];
      ((RectTransform)anchor.transform).GetWorldCorners(button);
      // Below the button when it fits, else above, else as near as the window allows; from its left edge.
      check.Assert(corners[0].x <= button[0].x + 7 && corners[2].x >= button[0].x,
        "the picker opens at its button");
      return 2;
    }

    private static int CheckNarrow(SettingsCheck check)
    {
      PlanetsModel page = check.Model.Planets;
      UiHost host = Host(check);
      check.Panel.Resize(420, check.Height);
      FitScreen(check, host);
      RawImage image = Image(check);
      Pressable[] switches = Switches(check);
      Pressable[] swatches = Swatches(check);
      check.Assert(page.Width < 600 && Bottom(image.rectTransform) > Top((RectTransform)switches[1].transform),
        "in a narrow window the preview sits above the parts: " + page.Width);
      Vector3[] corners = new Vector3[4];
      ((RectTransform)swatches[2].transform).GetWorldCorners(corners);
      Vector3[] frame = new Vector3[4];
      WindowRect(check).GetWorldCorners(frame);
      if (check.Name == "planets-ko-720")
        check.Save("planets-ko-stacked");
      check.Assert(corners[2].x <= frame[2].x, "in a narrow window every part's buttons stay inside the window: "
        + corners[2].x + " > " + frame[2].x + " page " + page.Width + " window " + WindowRect(check).rect.width);
      check.Panel.Resize(check.Width, check.Height);
      FitScreen(check, host);
      check.Assert(page.Width >= 600, "back at full size the preview goes beside the parts");
      return 3;
    }

    // ---- The window ----

    private static UiHost Host(SettingsCheck check) =>
      (UiHost)check.Panel.GetType().GetField("_host", Private).GetValue(check.Panel);

    private static RectTransform WindowRect(SettingsCheck check) =>
      (RectTransform)check.Panel.GetType().GetField("_window", Private).GetValue(check.Panel);

    /// <summary>
    /// The screen takes the window's size. In the game the window's host
    /// follows its rect by itself; the editor doesn't send it that message.
    /// </summary>
    private static void FitScreen(SettingsCheck check, UiHost host)
    {
      Rect rect = WindowRect(check).rect;
      host.Root.Resize(new ElementUI.Size(rect.width, rect.height));
      check.Settle();
      check.Settle();
    }

    /// <summary>
    /// Destroys what closed popovers left: in the game ElementUI destroys it,
    /// but the editor refuses Destroy outside play mode.
    /// </summary>
    private static void Sweep(SettingsCheck check, UiHost host)
    {
      if (host.Root.HasOverlay)
        return;
      Transform overlays = Overlays(check);
      for (int i = overlays.childCount - 1; i >= 0; i--)
        UnityEngine.Object.DestroyImmediate(overlays.GetChild(i).gameObject);
    }

    /// <summary>ElementUI's layer for popovers.</summary>
    private static Transform Overlays(SettingsCheck check) =>
      check.Root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Overlays");

    // ---- What is on screen ----

    private static T[] Active<T>(SettingsCheck check) where T : Component =>
      check.Root.GetComponentsInChildren<T>(false);

    /// <summary>The switches shown: custom planets, then the picked planet's own.</summary>
    private static Pressable[] Switches(SettingsCheck check) =>
      Active<Pressable>(check).Where(button => button.name == "Switch").ToArray();

    private static Pressable[] Tabs(SettingsCheck check)
    {
      string[] names = { L("불", "Fire"), L("얼음", "Ice"), L("세 번째 공", "Third planet") };
      Pressable[] all = Active<Pressable>(check);
      return names.Select(name => all.Single(button => button.name == "Option " + name)).ToArray();
    }

    /// <summary>Each part's map/custom button, Body first.</summary>
    private static Pressable[] Sources(SettingsCheck check)
    {
      string custom = L("직접 지정", "Custom color"),
        map = L("맵 색상", "Map color");
      return Active<TextMeshProUGUI>(check)
        .Where(text => text.text == custom || text.text == map)
        .Select(text => text.GetComponentInParent<Pressable>())
        .ToArray();
    }

    /// <summary>Each part's color button, Body first: a button with a color dot in it.</summary>
    private static Pressable[] Swatches(SettingsCheck check) =>
      Active<Pressable>(check).Where(button => button.name == "Button" && Dot(button) != null).ToArray();

    private static RoundedRect Dot(Pressable button) =>
      button.GetComponentsInChildren<RoundedRect>(false).FirstOrDefault(box => box.name == "Box");

    private static string Percent(Pressable swatch)
    {
      TextMeshProUGUI text = swatch.GetComponentsInChildren<TextMeshProUGUI>(false).Single();
      text.ForceMeshUpdate();
      return text.GetParsedText();
    }

    private static RawImage Image(SettingsCheck check) => Active<RawImage>(check).Single();

    private static TextMeshProUGUI CaptionText(SettingsCheck check)
    {
      string[] captions =
      {
        L("색상 미리보기", "Color preview"),
        L("미리보기 · 게임에는 적용 안 됨", "Preview · not applied"),
        L("공 미리보기를 불러올 수 없습니다.", "Planet preview is unavailable."),
      };
      return Active<TextMeshProUGUI>(check).Single(text => captions.Contains(text.text));
    }

    private static string Caption(SettingsCheck check) => CaptionText(check).text;

    private static GameObject Backdrop(SettingsCheck check) =>
      check.Root.GetComponentsInChildren<Transform>(false).Select(t => t.gameObject).FirstOrDefault(o => o.name == "Backdrop" && o.GetComponent<ClickArea>() != null);

    /// <summary>Shows a page as a page's command does: the window shows it, then the screen catches up.</summary>
    private static void Show(SettingsWindowViewModel window, SettingsPageModel page)
    {
      window.Show(page);
      ElementUI.Runtime.Probe();
    }

    private static void Press(Pressable button)
    {
      if (button.IsInteractable())
        button.Click?.Invoke();
    }

    // ---- The open picker ----

    private static UiColorPicker Picker(UiHost host)
    {
      ColorPickerView view = Find<ColorPickerView>(host.Root.RootView);
      return view?.Picker;
    }

    private static T Find<T>(View view) where T : View
    {
      if (view is T found)
        return found;
      var children = (List<View>)typeof(View).GetProperty("Children", Private).GetValue(view);
      foreach (View child in children)
      {
        T inside = Find<T>(child);
        if (inside != null)
          return inside;
      }
      return null;
    }

    private static TMP_InputField Input(UiColorPicker picker, string field) =>
      (TMP_InputField)typeof(UiColorPicker).GetField(field, Private).GetValue(picker);

    private static string Title(UiColorPicker picker) =>
      ((TextMeshProUGUI)typeof(UiColorPicker).GetField("_title", Private).GetValue(picker)).text;

    private static void Close(UiColorPicker picker) =>
      picker.Root.GetComponentsInChildren<Button>(false).Single(button => button.name == "Close color picker").onClick.Invoke();

    // ---- Geometry, in world units (y up) ----

    private static float Top(RectTransform rect)
    {
      Vector3[] corners = new Vector3[4];
      rect.GetWorldCorners(corners);
      return corners[1].y;
    }

    private static float Bottom(RectTransform rect)
    {
      Vector3[] corners = new Vector3[4];
      rect.GetWorldCorners(corners);
      return corners[0].y;
    }

    private static bool Near(Color a, Color b) =>
      Mathf.Abs(a.r - b.r) < .01f && Mathf.Abs(a.g - b.g) < .01f && Mathf.Abs(a.b - b.b) < .01f && Mathf.Abs(a.a - b.a) < .01f;

    /// <summary>The preview's fake renderer, which lives in SettingsPreview: what it was asked to draw.</summary>
    private sealed class Renderer
    {
      private readonly object _probe;

      public Renderer(Func<int, PlanetAppearanceProfile, Texture> render) => _probe = render.Target;

      private object Get(string name) => _probe.GetType().GetField(name).GetValue(_probe);

      public int Calls => (int)Get("Calls");
      public int Index => (int)Get("Index");
      public PlanetAppearanceProfile Profile => (PlanetAppearanceProfile)Get("Profile");
      public PlanetAppearanceProfile Snapshot => (PlanetAppearanceProfile)Get("Snapshot");
      public Texture Texture => (Texture)_probe.GetType().GetProperty("Texture").GetValue(_probe);

      public bool Available
      {
        set => _probe.GetType().GetField("Available").SetValue(_probe, value);
      }
    }
  }
}
