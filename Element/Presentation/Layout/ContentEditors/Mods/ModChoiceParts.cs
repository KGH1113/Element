using System;
using System.Collections.Generic;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Ui.Declarative.Controls;
using ElementKit;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Layout.ContentEditors.Mods.ModParts;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>The page's choices: segments, dropdowns, chips and the anchor picker.</summary>
internal sealed class ModChoiceParts
{
  private readonly ModEditorPage _page;
  private readonly ModCommands _commands;

  public ModChoiceParts(ModEditorPage page)
  {
    _page = page;
    _commands = page.Commands;
  }

  public EditorControl Choice(string label, IReadOnlyList<string> options, int selected, Action<int> changed)
  {
    List<string> names = Names(options);
    ModPart part = _page.Part();
    ModValue<int> current = _page.Value(part, Mathf.Clamp(selected, 0, names.Count - 1), changed);
    part.Control = () => Row(label, Picker(null, names, Bind(() => current.Value)).Named("Choice " + label));
    return _page.Add(part);
  }

  /// <summary>One option out of many, picked from a list that opens under it.</summary>
  public EditorControl Dropdown(string label, IReadOnlyList<string> options, int selected, Action<int> changed)
  {
    List<string> names = Names(options);
    ModPart part = _page.Part();
    ModValue<int> current = _page.Value(part, Mathf.Clamp(selected, 0, names.Count - 1), changed);
    part.Control = () =>
      Row(
        label,
        Picker(null, names, Bind(() => current.Value), PickerAppearance.Dropdown).Named("Dropdown " + label)
      );
    return _page.Add(part);
  }

  /// <summary>Several options, each on or off, as chips that wrap onto more lines.</summary>
  public EditorControl MultiChoice(
    string label,
    IReadOnlyList<string> options,
    IReadOnlyList<bool> on,
    Action<int, bool> changed
  )
  {
    List<string> names = Names(options);
    ModPart part = _page.Part();
    ModValue<bool>[] chips = new ModValue<bool>[names.Count];
    Command[] flips = new Command[names.Count];
    for (int i = 0; i < chips.Length; i++)
    {
      int index = i;
      ModValue<bool> chip = _page.Value<bool>(
        part,
        on != null && index < on.Count && on[index],
        now => changed?.Invoke(index, now)
      );
      chips[i] = chip;
      flips[i] = _commands.Make(() => chip.Value = !chip.Value);
    }
    View[] Chips()
    {
      View[] views = new View[names.Count];
      for (int i = 0; i < views.Length; i++)
      {
        ModValue<bool> chip = chips[i];
        Command flip = flips[i];
        views[i] = Button(UI.Text(names[i]), () => flip, ButtonStyle.Chip, () => chip.Value).Named("Chip " + names[i]);
      }
      return views;
    }
    part.Control = () => Row(label, Flow(6, Chips()).Padding(4, 0));
    return _page.Add(part);
  }

  public EditorControl AnchorPicker(string label, Anchor value, Action<Anchor> changed)
  {
    ModPart part = _page.Part();
    ModValue<Anchor> current = _page.Value(part, value, changed);
    OverlayGrowPickerModel picker = AnchorModel(Bind(() => current.Value), _page.Context);
    part.Control = () => ModParts.AnchorPicker(label, picker);
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
