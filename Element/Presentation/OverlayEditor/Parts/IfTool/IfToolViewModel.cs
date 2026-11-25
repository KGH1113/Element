using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.IfTool;

/// <summary>
/// What the If tool shows: a card per case, read from the top, then the
/// "otherwise" card, a way to add a case, and a slider that tries a value
/// out. The editor whose text it is shows it after every change;
/// <see cref="IfToolView"/> draws it.
/// </summary>
internal sealed class IfToolViewModel : ViewModel
{
  private static readonly IReadOnlyList<IfCaseViewModel> None = Array.Empty<IfCaseViewModel>();
  private readonly IfCaseViewModel[] _cards = new IfCaseViewModel[OverlayTextIf.MaximumCases];
  private readonly IIfToolActions _editor;
  private readonly State<float> _tried = new(0);

  /// <param name="under">What the cards sit on; their fills are a little lighter than it.</param>
  public IfToolViewModel(IIfToolActions editor, Color under)
  {
    _editor = editor;
    for (int i = 0; i < _cards.Length; i++)
      _cards[i] = Own(new IfCaseViewModel(i, false, editor, under));
    Otherwise = Own(new IfCaseViewModel(_cards.Length, true, editor, under));
    Add = Command(editor.AddCase, canExecute: Computed(() => CanAdd.Value));
  }

  /// <summary>The cases' cards, the first read first.</summary>
  public readonly State<IReadOnlyList<IfCaseViewModel>> Cases = new(None);

  /// <summary>The card for when no case is on.</summary>
  public readonly IfCaseViewModel Otherwise;

  /// <summary>The text has an If here: its cards show. Without one, only the way to make one does.</summary>
  public readonly State<bool> HasIf = new(false);

  /// <summary>There is room for one more case.</summary>
  public readonly State<bool> CanAdd = new(true);

  /// <summary>A value can be tried out: the first case compares one.</summary>
  public readonly State<bool> CanTry = new(false);

  /// <summary>The value the slider moves, and its number now.</summary>
  public readonly State<string> TryName = new("");

  public readonly State<string> TryNumber = new("");

  /// <summary>Where the tried value is along the slider's range, from 0 to 1; moving it tries a value.</summary>
  public float Tried
  {
    [Pure]
    get => _tried.Value;
    set => _editor.Try(value);
  }

  public Command Add { get; }

  /// <summary>Card <paramref name="index"/> of the cases, to show it.</summary>
  public IfCaseViewModel Card(int index) => _cards[index];

  /// <summary>Whether an If is here, and how many cases it has.</summary>
  public void ShowCases(bool hasIf, int count)
  {
    HasIf.Value = hasIf;
    count = hasIf ? Mathf.Clamp(count, 0, _cards.Length) : 0;
    CanAdd.Value = count < _cards.Length;
    if (Cases.Value.Count == count)
      return;
    IfCaseViewModel[] shown = new IfCaseViewModel[count];
    Array.Copy(_cards, shown, count);
    Cases.Value = shown;
  }

  /// <summary>The slider that tries a value: whether it shows, whose it is, its number and where it stands.</summary>
  public void ShowTry(bool can, string name, string number, float position)
  {
    CanTry.Value = can;
    TryName.Value = name ?? "";
    TryNumber.Value = number ?? "";
    _tried.Value = Mathf.Clamp01(position);
  }
}
