using System;
using System.Collections.Generic;
using ElementKit;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// The page's choices: segments, lists, several on or off, and the nine
/// points. Each is ElementUI's own picker, so the column draws it as its row
/// (<see cref="ModColumnStyle"/>); the points are Element's.
/// </summary>
internal sealed class ModChoiceParts
{
  private readonly ModEditorPage _page;

  public ModChoiceParts(ModEditorPage page) => _page = page;

  public EditorControl Choice(string label, IReadOnlyList<string> options, int selected, Action<int> changed)
  {
    List<string> names = Names(options);
    ModPart part = _page.Part();
    ModValueViewModel<int> current = _page.Value(part, Mathf.Clamp(selected, 0, names.Count - 1), changed);
    part.Control = () => Picker(label ?? "", names, Bind(() => current.Value));
    return _page.Add(part);
  }

  /// <summary>One option out of many, picked from a list.</summary>
  public EditorControl Dropdown(string label, IReadOnlyList<string> options, int selected, Action<int> changed)
  {
    List<string> names = Names(options);
    ModPart part = _page.Part();
    ModValueViewModel<int> current = _page.Value(part, Mathf.Clamp(selected, 0, names.Count - 1), changed);
    part.Control = () => Picker(label ?? "", names, Bind(() => current.Value), PickerAppearance.Dropdown);
    return _page.Add(part);
  }

  /// <summary>Several options, each on or off; each one turned is handed to the mod.</summary>
  public EditorControl MultiChoice(
    string label,
    IReadOnlyList<string> options,
    IReadOnlyList<bool> on,
    Action<int, bool> changed
  )
  {
    List<string> names = Names(options);
    List<bool> start = new();
    for (int i = 0; i < names.Count; i++)
      start.Add(on != null && i < on.Count && on[i]);
    ModPart part = _page.Part();
    List<bool> shown = start;
    ModValueViewModel<List<bool>> current = _page.Value<List<bool>>(
      part,
      start,
      next =>
      {
        for (int i = 0; i < names.Count; i++)
        {
          bool now = MultiPickerConfiguration.IsOn(next, i);
          if (now != MultiPickerConfiguration.IsOn(shown, i))
            changed?.Invoke(i, now);
        }
        shown = next;
      }
    );
    part.Control = () => MultiPicker(label ?? "", names, Bind(() => current.Value));
    return _page.Add(part);
  }

  public EditorControl AnchorPicker(string label, Anchor value, Action<Anchor> changed)
  {
    ModPart part = _page.Part();
    ModValueViewModel<Anchor> current = _page.Value(part, value, changed);
    Binding<Anchor> anchor = Bind(() => current.Value);
    ModEditorContext context = _page.Context;
    part.Control = () => ModParts.AnchorPicker(label, anchor, context);
    return _page.Add(part);
  }

  private static List<string> Names(IReadOnlyList<string> options)
  {
    List<string> names = new();
    if (options != null)
      foreach (string option in options)
        names.Add(option ?? "");
    if (names.Count == 0)
      names.Add("");
    return names;
  }
}
