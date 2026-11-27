using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Element.Presentation.Ui.Graphics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Editor
{
  /// <summary>
  /// Looks at every frame the checks save for what a person would call
  /// sloppy: words not centred in their control, a control lying half over
  /// another, words spilling out of their box, an icon off the middle of its
  /// button, neighbours out of line, something off the screen, a hover that
  /// lights a shape other than the surface it is on or hugs its letters. What it finds
  /// goes to ui-audit.txt in the output folder, worst first; the kinds with no
  /// reason to happen at all fail the checks (<see cref="Broken"/>).
  /// </summary>
  internal static class UiTidyAudit
  {
    // What TextMesh Pro reports for the top of letters runs high: words measured
    // as about 2 off stand within a pixel of the middle on screen.
    private const float CentreSlack = 3f,
      OverlapSlack = 1.5f,
      SpillSlack = 1f,
      IconSlack = 1f,
      TallestControl = 64;

    private sealed class Finding
    {
      public string Rule, Where, Detail, Frame;
      public int Frames;
      public float Worst;
    }

    private struct Item
    {
      public Component Part;
      public Rect Box;
      public float Unit;
    }

    private static readonly Dictionary<string, Finding> Found = new Dictionary<string, Finding>();
    private static readonly Vector3[] Corners = new Vector3[4];

    private static readonly string[] NeverAllowed =
    {
      "audit-failed", "words-off-centre", "overlap-control", "overlap-words", "words-over-words", "words-spill", "icon-off-centre",
      "hover-shape", "hover-tight",
    };

    /// <summary>What must not be on any screen, one line each; empty when all is tidy.</summary>
    public static IEnumerable<string> Broken =>
      Found.Values.Where(finding => NeverAllowed.Contains(finding.Rule))
        .Select(finding => finding.Rule + ": " + finding.Where + " " + finding.Detail + " (" + finding.Frame + ")");

    public static void Inspect(string frame)
    {
      try
      {
        Canvas.ForceUpdateCanvases();
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
          if (canvas.isRootCanvas)
            InspectCanvas(canvas, frame);
      }
      catch (Exception exception)
      {
        Report("audit-failed", frame, exception.GetType().Name + ": " + exception.Message + " " + exception.StackTrace?.Split('\n')[0], frame, 0);
      }
    }

    public static void Write(string path)
    {
      StringBuilder text = new StringBuilder();
      foreach (IGrouping<string, Finding> rule in Found.Values.GroupBy(finding => finding.Rule).OrderBy(group => group.Key))
      {
        text.Append("## ").Append(rule.Key).Append(" (").Append(rule.Count()).Append(")\n");
        foreach (Finding finding in rule.OrderByDescending(item => item.Worst))
          text.Append(finding.Worst.ToString("0.0")).Append("u  ×").Append(finding.Frames).Append("  ").Append(finding.Where)
            .Append("  | ").Append(finding.Detail).Append("  | first: ").Append(finding.Frame).Append('\n');
        text.Append('\n');
      }
      File.WriteAllText(path, text.ToString());
    }

    private static void InspectCanvas(Canvas canvas, string frame)
    {
      Rect screen = BoxOf((RectTransform)canvas.transform);
      List<Item> controls = new List<Item>();
      foreach (Selectable control in canvas.GetComponentsInChildren<Selectable>(false))
        if (control.isActiveAndEnabled && Opacity(control.transform) > .05f && control.transform is RectTransform rect)
        {
          // What a scroll view or mask shows of it.
          Rect box = VisibleBox(rect);
          if (box.width > .5f && box.height > .5f)
            controls.Add(new Item { Part = control, Box = box, Unit = Unit(rect) });
        }
      if (controls.Count == 0)
        return;

      List<Item> words = new List<Item>();
      foreach (TextMeshProUGUI label in canvas.GetComponentsInChildren<TextMeshProUGUI>(false))
        if (label.isActiveAndEnabled && label.color.a > .05f && Opacity(label.transform) > .05f && !Clipped(label.transform)
          && TryWords(label, out Rect box))
          words.Add(new Item { Part = label, Box = box, Unit = Unit(label.rectTransform) });

      List<Item> shapes = new List<Item>();
      foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(false))
        if (!(graphic is TMP_Text) && Painted(graphic) && !Clipped(graphic.transform))
        {
          Rect box = BoxOf(graphic.rectTransform);
          if (box.width > .5f && box.height > .5f && !(box.width >= screen.width * .8f && box.height >= screen.height * .8f))
            shapes.Add(new Item { Part = graphic, Box = box, Unit = Unit(graphic.rectTransform) });
        }

      foreach (Item label in words)
        CheckWords(label, words, screen, frame);
      foreach (Item control in controls)
      {
        CheckIcon(control, frame);
        CheckOverlaps(control, controls, shapes, words, screen, frame);
        if (!Clipped(control.Part.transform) && !Inside(screen, control.Box, control.Unit))
          Report("off-screen", Name(control.Part.transform), "", frame, Outside(screen, control.Box) / control.Unit);
      }
      CheckNeighbours(controls, frame);
      CheckWordOverlaps(words, screen, frame);
      CheckHovers(canvas, controls, screen, frame);
    }

    // A control lights up as the thing a person sees: its hover covers the
    // surface it is drawn on, corner for corner, or stays well inside it.
    private static void CheckHovers(Canvas canvas, List<Item> controls, Rect screen, string frame)
    {
      Graphic[] drawn = canvas.GetComponentsInChildren<Graphic>(false);
      Dictionary<Graphic, int> order = new Dictionary<Graphic, int>();
      for (int i = 0; i < drawn.Length; i++)
        order[drawn[i]] = i;
      foreach (Item control in controls)
      {
        // A surface shown all the time lights itself; one that only comes up
        // under the pointer has to come up as the shape a person already sees.
        if (!TryHover(control.Part, out Graphic lit, out float litRadius, out bool idleShown) || idleShown
          || !order.TryGetValue(lit, out int litOrder))
          continue;
        Rect box = BoxOf(lit.rectTransform);
        float unit = control.Unit;
        // The tightest painted surface behind the lit one that holds it.
        Graphic beneath = null;
        Rect under = default;
        for (int i = 0; i < litOrder; i++)
        {
          Graphic graphic = drawn[i];
          if (graphic is TMP_Text || graphic is UiVectorGraphic || graphic.transform.IsChildOf(lit.transform) || !Painted(graphic))
            continue;
          if (graphic.GetComponent<Mask>() != null || graphic.GetComponent<RectMask2D>() != null)
            continue;
          Rect rect = BoxOf(graphic.rectTransform);
          if (!Holds(rect, box, unit) || (rect.width >= screen.width * .8f && rect.height >= screen.height * .8f))
            continue;
          if (beneath == null || rect.width * rect.height < under.width * under.height)
          {
            beneath = graphic;
            under = rect;
          }
        }
        string what = Size(box, unit) + " r" + Corner(litRadius, box, unit).ToString("0");
        if (beneath == null)
        {
          // Lit on nothing: it needs room around what it holds, or the light hugs the letters.
          float room = Room(control.Part.transform, box, unit);
          if (room < 3.5f)
            Report("hover-tight", Name(control.Part.transform), what + " room " + room.ToString("0"), frame, 4 - room);
          continue;
        }
        float left = (box.xMin - under.xMin) / unit,
          right = (under.xMax - box.xMax) / unit,
          bottom = (box.yMin - under.yMin) / unit,
          top = (under.yMax - box.yMax) / unit;
        float widest = Mathf.Max(Mathf.Max(left, right), Mathf.Max(bottom, top));
        float underRadius = beneath is ElementUI.Ugui.RoundedRect round ? round.Radius : float.NaN;
        string on = beneath.name + " " + Size(under, unit) + (float.IsNaN(underRadius) ? "" : " r" + Corner(underRadius, under, unit).ToString("0"));
        float cornerOff = float.IsNaN(underRadius) ? 0 : Mathf.Abs(Corner(litRadius, box, unit) - Corner(underRadius, under, unit));
        // Set in evenly with its corner set in as much reads as one shape inside the other.
        float narrowest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
        bool inset = widest - narrowest <= 1 && !float.IsNaN(underRadius)
          && Mathf.Abs(Corner(litRadius, box, unit) - (Corner(underRadius, under, unit) - narrowest)) <= 1.5f;
        if (widest <= 8 && !inset && (widest > 1.5f || cornerOff > 1.5f))
          // Nearly the surface, but not quite: a second outline inside the first.
          Report("hover-shape", Name(control.Part.transform), what + " on " + on, frame, Mathf.Max(widest, cornerOff));
        else if (Mathf.Max(left, right) <= 8 != Mathf.Max(bottom, top) <= 8)
          // Across the surface one way, a slice of it the other.
          Report("hover-part", Name(control.Part.transform), what + " on " + on, frame, widest);
      }
    }

    // What lights up when the pointer is over a control, and its corner.
    private static bool TryHover(Component control, out Graphic lit, out float radius, out bool idleShown)
    {
      lit = null;
      radius = float.NaN;
      idleShown = false;
      if (control is ElementUI.Ugui.Pressable pressable && pressable.StateChanged?.Target is ElementUI.Ugui.ButtonPeer peer)
      {
        const System.Reflection.BindingFlags Any =
          System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        Type type = typeof(ElementUI.Ugui.ButtonPeer);
        bool owned = (bool)type.GetField("_surfaceOwnedByView", Any).GetValue(peer);
        if (!owned)
        {
          ElementUI.Ugui.UguiTheme theme = (ElementUI.Ugui.UguiTheme)type.GetField("Theme", Any).GetValue(peer);
          ElementUI.ButtonStyle style = (ElementUI.ButtonStyle)type.GetField("Style", Any).GetValue(peer);
          bool selected = (bool)type.GetField("_selected", Any).GetValue(peer);
          Color hover = theme.ButtonFill(
            style,
            ElementUI.Ugui.ButtonState.Hovered | (selected ? ElementUI.Ugui.ButtonState.Selected : 0)
          );
          Color idle = theme.ButtonFill(style, selected ? ElementUI.Ugui.ButtonState.Selected : 0);
          if (hover.a <= .02f || hover == idle)
            return false;
          idleShown = idle.a > .02f;
          lit = peer.Fill;
          radius = peer.Fill.Radius;
          return true;
        }
        // A style draws the surface: the box the size of the button, inside it,
        // lit when it is told the pointer is over it.
        Rect whole = BoxOf((RectTransform)control.transform);
        float unit = Unit(control.transform);
        bool picked = (bool)type.GetField("_selected", Any).GetValue(peer);
        Action<ElementUI.ButtonInteraction> told =
          (Action<ElementUI.ButtonInteraction>)type.GetField("_interactionChanged", Any).GetValue(peer);
        if (told == null)
          return false;
        ElementUI.ButtonInteraction now = (ElementUI.ButtonInteraction)((int)pressable.State | (picked ? 4 : 0));
        foreach (ElementUI.Ugui.RoundedRect box in control.GetComponentsInChildren<ElementUI.Ugui.RoundedRect>(false))
          if (box != peer.Fill && Holds(BoxOf(box.rectTransform), whole, unit) && Holds(whole, BoxOf(box.rectTransform), unit))
          {
            Color idle = box.color;
            told(now | ElementUI.ButtonInteraction.Hovered);
            ElementUI.Runtime.Flush();
            Color hovered = box.color;
            told(now);
            ElementUI.Runtime.Flush();
            if (hovered == idle)
              return false;
            idleShown = idle.a > .02f;
            lit = box;
            radius = box.Radius;
            return true;
          }
        return false;
      }
      // A Unity control tints its own graphic.
      if (control is Selectable plain && plain.transition == Selectable.Transition.ColorTint && plain.targetGraphic != null
        && plain.colors.highlightedColor != plain.colors.normalColor && Painted(plain.targetGraphic))
      {
        lit = plain.targetGraphic;
        idleShown = (lit.color * plain.colors.normalColor).a > .02f;
        radius = lit is ElementUI.Ugui.RoundedRect round ? round.Radius : float.NaN;
        return true;
      }
      return false;
    }

    // The least space between what a control holds and the edge of its light.
    private static float Room(Transform control, Rect light, float unit)
    {
      float room = float.PositiveInfinity;
      void Measure(Rect part)
      {
        float least = Mathf.Min(
          Mathf.Min(part.xMin - light.xMin, light.xMax - part.xMax),
          Mathf.Min(part.yMin - light.yMin, light.yMax - part.yMax)
        );
        room = Mathf.Min(room, least / unit);
      }
      foreach (TextMeshProUGUI words in control.GetComponentsInChildren<TextMeshProUGUI>(false))
        if (words.color.a > .05f && TryWords(words, out Rect box))
          Measure(box);
      foreach (UiVectorGraphic icon in control.GetComponentsInChildren<UiVectorGraphic>(false))
        if (icon.color.a > .05f)
          Measure(BoxOf(icon.rectTransform));
      return room;
    }

    // A corner as drawn: no rounder than half the shorter side.
    private static float Corner(float radius, Rect box, float unit) =>
      float.IsNaN(radius) ? float.NaN : Mathf.Min(radius, Mathf.Min(box.width, box.height) / unit / 2);

    private static string Size(Rect box, float unit) => (box.width / unit).ToString("0") + "×" + (box.height / unit).ToString("0");

    // Words in a control sit in its middle; they stay inside the box around them.
    private static void CheckWords(Item item, List<Item> words, Rect screen, string frame)
    {
      TextMeshProUGUI label = (TextMeshProUGUI)item.Part;
      string said = Short(label.text);
      if (label.isTextTruncated)
        Report("cut-off-words", Name(label.transform), said, frame, 0);
      Selectable control = label.GetComponentInParent<Selectable>();
      if (control != null && !(control is TMP_InputField) && control.transform is RectTransform rect)
      {
        Rect box = BoxOf(rect);
        float unit = item.Unit;
        bool alone = !words.Any(other =>
          other.Part != label && other.Part.GetComponentInParent<Selectable>() == control
          && Mathf.Abs(other.Box.center.y - item.Box.center.y) > 3 * unit);
        // A dot or a minus has no letter height to centre.
        if (alone && box.height <= TallestControl * unit && label.textInfo.lineCount == 1 && label.text.Any(char.IsLetterOrDigit)
          && TryCapMiddle(label, out float middle))
        {
          float off = (middle - box.center.y) / unit;
          if (Mathf.Abs(off) > CentreSlack)
            Report("words-off-centre", Name(label.transform), said + " by " + off.ToString("0.0"), frame, Mathf.Abs(off));
        }
      }
      if (label.GetComponentInParent<TMP_InputField>() != null)
        return;
      Graphic around = BoxAround(label.transform);
      if (around == null)
        return;
      Rect outer = BoxOf(around.rectTransform);
      float spill = Mathf.Max(outer.xMin - item.Box.xMin, item.Box.xMax - outer.xMax, outer.yMin - item.Box.yMin, item.Box.yMax - outer.yMax)
        / item.Unit;
      if (spill > SpillSlack)
        Report("words-spill", Name(label.transform), said + " out of " + around.name, frame, spill);
    }

    private static void CheckIcon(Item control, string frame)
    {
      Transform root = control.Part.transform;
      if (root.GetComponentsInChildren<TMP_Text>(false).Any(text => text.color.a > .05f && !string.IsNullOrEmpty(text.text)))
        return;
      UiIcon[] icons = root.GetComponentsInChildren<UiIcon>(false);
      if (icons.Length != 1)
        return;
      Rect icon = BoxOf(icons[0].rectTransform);
      Rect whole = BoxOf((RectTransform)root);
      float off = Mathf.Max(Mathf.Abs(icon.center.x - whole.center.x), Mathf.Abs(icon.center.y - whole.center.y)) / control.Unit;
      if (off > IconSlack)
        Report("icon-off-centre", Name(root), "", frame, off);
    }

    private static void CheckOverlaps(Item control, List<Item> controls, List<Item> shapes, List<Item> words, Rect screen, string frame)
    {
      void Against(Item other, string kind)
      {
        if (other.Part == control.Part || !PartlyOver(control.Box, other.Box, control.Unit))
          return;
        Transform a = control.Part.transform, b = other.Part.transform;
        if (a.IsChildOf(b) || b.IsChildOf(a))
          return;
        // Two things on one small group; what floats over the screen is meant to cover.
        RectTransform group = Common(a, b);
        if (group == null || Floats(a, group) != Floats(b, group))
          return;
        Rect shared = BoxOf(group);
        if (shared.width >= screen.width * .8f && shared.height >= screen.height * .8f)
          return;
        // A control's own parts aren't another thing.
        Selectable owner = b.GetComponentInParent<Selectable>();
        if (owner != null && owner == control.Part)
          return;
        Rect meet = Rect.MinMaxRect(
          Mathf.Max(control.Box.xMin, other.Box.xMin), Mathf.Max(control.Box.yMin, other.Box.yMin),
          Mathf.Min(control.Box.xMax, other.Box.xMax), Mathf.Min(control.Box.yMax, other.Box.yMax));
        Report("overlap-" + kind, Name(a) + "  ×  " + Name(b), kind == "words" ? Short(((TMP_Text)other.Part).text) : "", frame,
          Mathf.Min(meet.width, meet.height) / control.Unit);
      }
      foreach (Item other in controls)
        if (other.Part.GetInstanceID() < control.Part.GetInstanceID())
          Against(other, "control");
      foreach (Item other in shapes)
        if (other.Part.GetComponent<Selectable>() == null)
          Against(other, "shape");
      foreach (Item other in words)
        Against(other, "words");
    }

    private static void CheckWordOverlaps(List<Item> words, Rect screen, string frame)
    {
      for (int i = 0; i < words.Count; i++)
        for (int j = i + 1; j < words.Count; j++)
        {
          Rect a = words[i].Box, b = words[j].Box;
          float slack = 1.5f * words[i].Unit;
          if (Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) <= slack || Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) <= slack)
            continue;
          RectTransform group = Common(words[i].Part.transform, words[j].Part.transform);
          if (group == null || Floats(words[i].Part.transform, group) != Floats(words[j].Part.transform, group))
            continue;
          Rect shared = BoxOf(group);
          if (shared.width >= screen.width * .8f && shared.height >= screen.height * .8f)
            continue;
          Report("words-over-words", Name(words[i].Part.transform) + "  ×  " + Name(words[j].Part.transform),
            Short(((TMP_Text)words[i].Part).text) + " / " + Short(((TMP_Text)words[j].Part).text), frame, 0);
        }
    }

    // Controls side by side in one parent line up on their middles.
    private static void CheckNeighbours(List<Item> controls, string frame)
    {
      foreach (IGrouping<Transform, Item> row in controls.GroupBy(item => item.Part.transform.parent))
      {
        Item[] items = row.OrderBy(item => item.Box.xMin).ToArray();
        for (int i = 1; i < items.Length; i++)
        {
          Rect a = items[i - 1].Box, b = items[i].Box;
          float unit = items[i].Unit;
          float shared = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
          if (b.xMin < a.xMax - unit || shared < Mathf.Min(a.height, b.height) * .5f)
            continue;
          float off = Mathf.Abs(a.center.y - b.center.y) / unit;
          if (off > 1 && off <= 8)
            Report("neighbours-out-of-line", Name(items[i - 1].Part.transform) + "  ×  " + Name(items[i].Part.transform), "", frame, off);
        }
      }
    }

    private static bool PartlyOver(Rect a, Rect b, float unit)
    {
      float slack = OverlapSlack * unit;
      if (Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) <= slack || Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) <= slack)
        return false;
      return !Holds(a, b, slack) && !Holds(b, a, slack);
    }

    private static bool Holds(Rect outer, Rect inner, float slack) =>
      inner.xMin >= outer.xMin - slack && inner.xMax <= outer.xMax + slack && inner.yMin >= outer.yMin - slack && inner.yMax <= outer.yMax + slack;

    private static bool Inside(Rect screen, Rect box, float unit) => Holds(screen, box, unit);

    private static float Outside(Rect screen, Rect box) =>
      Mathf.Max(screen.xMin - box.xMin, box.xMax - screen.xMax, screen.yMin - box.yMin, box.yMax - screen.yMax);

    private static RectTransform Common(Transform a, Transform b)
    {
      for (Transform parent = a.parent; parent != null; parent = parent.parent)
        if (b.IsChildOf(parent))
          return parent as RectTransform;
      return null;
    }

    // Whether a part hangs in a popover or an overlay layer below group.
    private static bool Floats(Transform part, Transform group)
    {
      for (Transform parent = part; parent != null && parent != group; parent = parent.parent)
        if (parent.name == "Overlays" || parent.name.IndexOf("popover", StringComparison.OrdinalIgnoreCase) >= 0)
          return true;
      return false;
    }

    private static bool Painted(Graphic graphic)
    {
      if (!graphic.isActiveAndEnabled || graphic.color.a <= .05f || Opacity(graphic.transform) <= .05f)
        return false;
      Mask mask = graphic.GetComponent<Mask>();
      return mask == null || mask.showMaskGraphic;
    }

    private static Graphic BoxAround(Transform from)
    {
      for (Transform parent = from.parent; parent != null; parent = parent.parent)
      {
        Graphic graphic = parent.GetComponent<Graphic>();
        if (graphic != null && !(graphic is TMP_Text) && Painted(graphic))
          return graphic;
        if (parent.GetComponent<Canvas>() != null)
          return null;
      }
      return null;
    }

    private static bool Clipped(Transform from)
    {
      for (Transform parent = from; parent != null; parent = parent.parent)
        if (parent.GetComponent<RectMask2D>() != null || parent.GetComponent<ScrollRect>() != null)
          return true;
      return false;
    }

    private static Rect VisibleBox(RectTransform rect)
    {
      Rect box = BoxOf(rect);
      for (Transform parent = rect.parent; parent != null; parent = parent.parent)
        if ((parent.GetComponent<RectMask2D>() != null || parent.GetComponent<Mask>() != null) && parent is RectTransform mask)
        {
          Rect shown = BoxOf(mask);
          box = Rect.MinMaxRect(
            Mathf.Max(box.xMin, shown.xMin), Mathf.Max(box.yMin, shown.yMin),
            Mathf.Max(Mathf.Max(box.xMin, shown.xMin), Mathf.Min(box.xMax, shown.xMax)),
            Mathf.Max(Mathf.Max(box.yMin, shown.yMin), Mathf.Min(box.yMax, shown.yMax)));
        }
      return box;
    }

    private static float Opacity(Transform from)
    {
      float opacity = 1;
      for (Transform parent = from; parent != null; parent = parent.parent)
      {
        CanvasGroup group = parent.GetComponent<CanvasGroup>();
        if (group != null && group.enabled)
        {
          opacity *= group.alpha;
          if (group.ignoreParentGroups)
            break;
        }
      }
      return opacity;
    }

    private static bool TryWords(TextMeshProUGUI label, out Rect box)
    {
      box = default;
      if (string.IsNullOrEmpty(label.text))
        return false;
      if (label.havePropertiesChanged)
        label.ForceMeshUpdate();
      TMP_TextInfo info = label.textInfo;
      if (info == null || info.characterInfo == null)
        return false;
      bool any = false;
      Vector2 min = default, max = default;
      for (int i = 0; i < info.characterCount; i++)
      {
        TMP_CharacterInfo character = info.characterInfo[i];
        if (!character.isVisible)
          continue;
        Vector2 low = label.transform.TransformPoint(character.bottomLeft), high = label.transform.TransformPoint(character.topRight);
        min = any ? Vector2.Min(min, low) : low;
        max = any ? Vector2.Max(max, high) : high;
        any = true;
      }
      if (!any)
        return false;
      box = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
      return true;
    }

    // The middle of the line from its baseline to the top of its letters, so a
    // letter hanging below the line doesn't move it.
    private static bool TryCapMiddle(TextMeshProUGUI label, out float middle)
    {
      middle = 0;
      TMP_TextInfo info = label.textInfo;
      float top = float.MinValue, baseline = float.MaxValue;
      for (int i = 0; i < info.characterCount; i++)
      {
        TMP_CharacterInfo character = info.characterInfo[i];
        if (!character.isVisible)
          continue;
        top = Mathf.Max(top, character.topRight.y);
        baseline = Mathf.Min(baseline, character.baseLine);
      }
      if (top == float.MinValue)
        return false;
      middle = label.transform.TransformPoint(new Vector3(0, (top + baseline) / 2, 0)).y;
      return true;
    }

    private static Rect BoxOf(RectTransform rect)
    {
      rect.GetWorldCorners(Corners);
      return Rect.MinMaxRect(
        Mathf.Min(Corners[0].x, Corners[2].x), Mathf.Min(Corners[0].y, Corners[2].y),
        Mathf.Max(Corners[0].x, Corners[2].x), Mathf.Max(Corners[0].y, Corners[2].y));
    }

    private static float Unit(Transform transform) => Mathf.Max(.0001f, Mathf.Abs(transform.lossyScale.y));

    private static string Short(string text)
    {
      text = (text ?? "").Replace('\n', ' ');
      return text.Length > 32 ? text.Substring(0, 32) + "…" : text;
    }

    private static string Name(Transform transform)
    {
      List<string> names = new List<string>();
      for (Transform part = transform; part != null && names.Count < 4; part = part.parent)
        names.Insert(0, part.name);
      return string.Join("/", names);
    }

    private static void Report(string rule, string where, string detail, string frame, float size)
    {
      string key = rule + "|" + where;
      if (!Found.TryGetValue(key, out Finding finding))
        Found[key] = finding = new Finding { Rule = rule, Where = where, Detail = detail, Frame = frame };
      finding.Frames++;
      if (size > finding.Worst)
      {
        finding.Worst = size;
        finding.Detail = detail;
        finding.Frame = frame;
      }
    }
  }
}
