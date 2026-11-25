using System;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// One value in the dock: a chip that a click picks. After its name it shows
/// what the value reads in the overlay now, while the game has one. It is
/// listed only while it fits where the dock puts values now.
/// </summary>
internal sealed class ValueChipViewModel : ViewModel
{
  private readonly OverlayValueMask _bit;
  private readonly bool _reserves;
  private string _live = "";

  /// <param name="index">What a search reads: every name the value can be found by (<see cref="OverlayValueSearch.Index"/>).</param>
  public ValueChipViewModel(OverlayValueDefinition value, string index, Action<OverlayValueDefinition> pick)
  {
    Value = value;
    Index = index;
    Name = "Value " + value.Key;
    Label = ValueSectionNames.ValueName(value);
    _bit = OverlayValueMask.Of(value.Id);
    _reserves = value.IsNumeric;
    Pick = Command(() => pick(value));
  }

  public readonly OverlayValueDefinition Value;

  /// <summary>Names the drawn chip, for the checks: "Value combo".</summary>
  public readonly string Name;

  public readonly string Label;

  /// <summary>Every name the value can be found by, folded for searching.</summary>
  public readonly string Index;

  /// <summary>What the value reads now, as plain words; empty while the game has none.</summary>
  public readonly State<string> Live = new("");

  public readonly State<bool> HasLive = new(false);

  /// <summary>
  /// Room the number keeps: its widest shape so far, every digit an 8. A
  /// number ticking up or down keeps its chip as wide, so the chips after it
  /// don't shift as it changes.
  /// </summary>
  public readonly State<string> Room = new("");

  /// <summary>It can go where the dock puts values now: it is listed.</summary>
  public readonly State<bool> Fits = new(true);

  /// <summary>Whether the search finds it; only one that fits is found.</summary>
  public readonly State<bool> Found = new(false);

  public Command Pick { get; }

  /// <summary>Whether it shows a value in <paramref name="changed"/>.</summary>
  public bool Shows(OverlayValueMask changed) => changed.Overlaps(_bit);

  /// <summary>
  /// Takes what the value reads in <paramref name="values"/> now, written
  /// plain into <paramref name="scratch"/> first: nothing is made unless it changed.
  /// </summary>
  public void Follow(OverlayValues values, StringBuilder scratch)
  {
    scratch.Clear();
    if (values != null && values.TryGet(Value.Id, out StringBuilder shown))
      OverlayPlainText.Append(scratch, shown);
    Trim(scratch);
    if (Same(scratch, _live))
      return;
    _live = scratch.ToString();
    Live.Value = _live;
    HasLive.Value = _live.Length > 0;
    if (_reserves && Wider(_live, Room.Value))
      Room.Value = Shape(_live);
  }

  private static void Trim(StringBuilder text)
  {
    int end = text.Length;
    while (end > 0 && text[end - 1] == ' ')
      end--;
    int start = 0;
    while (start < end && text[start] == ' ')
      start++;
    text.Length = end;
    if (start > 0)
      text.Remove(0, start);
  }

  private static bool Same(StringBuilder text, string other)
  {
    if (text.Length != other.Length)
      return false;
    for (int i = 0; i < other.Length; i++)
      if (text[i] != other[i])
        return false;
    return true;
  }

  // A longer number, or one as long laid out differently ("1:05" after "10.5").
  private static bool Wider(string live, string room)
  {
    if (live.Length != room.Length)
      return live.Length > room.Length;
    for (int i = 0; i < live.Length; i++)
      if (ShapeOf(live[i]) != room[i])
        return true;
    return false;
  }

  private static string Shape(string live)
  {
    char[] shape = new char[live.Length];
    for (int i = 0; i < shape.Length; i++)
      shape[i] = ShapeOf(live[i]);
    return new string(shape);
  }

  private static char ShapeOf(char c) => c >= '0' && c <= '9' ? '8' : c;
}
