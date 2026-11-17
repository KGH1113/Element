using System;
using System.Linq.Expressions;
using Element.Configuration;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Input;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// What a <see cref="ShortcutField"/> shows and does: the shortcut as keycaps,
/// recording the next key pressed with at most one modifier (Esc cancels),
/// why a recorded key can't be used, and one action beside the field, such
/// as removing or resetting the shortcut.
/// </summary>
/// <remarks>
/// The page that owns it calls <see cref="TickCapture"/> from its own
/// <c>TickCapture</c>: the window calls that every frame and gives the page
/// the keyboard while it returns true.
/// </remarks>
internal sealed class ShortcutFieldViewModel : ViewModel
{
  private readonly Func<KeyCode, KeyCode, string> _assign;
  private readonly string _modifierExample;
  private int _startedFrame = -1;

  /// <summary>Recording: the next key pressed becomes the shortcut.</summary>
  public bool Recording;

  /// <summary>Under the field while recording: how to record, or why the last key can't be used.</summary>
  public string Hint = "";

  /// <summary>Shown in the field when there is no shortcut.</summary>
  public readonly string EmptyLabel;

  public Computed<KeyCode> Key { get; }
  public Computed<KeyCode> Modifier { get; }
  public Computed<bool> HasShortcut { get; }

  /// <summary>Keycaps show, rather than words: a shortcut is set and not being recorded again.</summary>
  public Computed<bool> ShowsKeys { get; }
  public Computed<bool> HasModifier { get; }

  /// <summary>The words in the field when keycaps don't show.</summary>
  public Computed<string> Prompt { get; }
  public Computed<string> KeyText { get; }
  public Computed<string> ModifierText { get; }

  /// <summary>The modifier is drawn as its macOS symbol (⌥ ⌘ ⌃ ⇧), with its side beside it.</summary>
  public Computed<bool> ModifierSymbol { get; }
  public Computed<string> ModifierSide { get; }

  /// <summary>Starts recording; a click on the field.</summary>
  public Command Record { get; }

  /// <summary>The action beside the field. It stops recording first.</summary>
  public Command Act { get; }

  /// <param name="key">The shortcut's key, read from the owner: <c>() =&gt; Settings.SettingsShortcutKey</c>.</param>
  /// <param name="modifier">The modifier held with it, or None.</param>
  /// <param name="assign">Saves a recorded shortcut and returns null, or returns why it can't be used.</param>
  /// <param name="act">What the button beside the field does, such as clearing the shortcut.</param>
  /// <param name="modifierExample">A shortcut with one modifier, for the hint when two are held: "Left Alt + 1".</param>
  /// <param name="emptyLabel">Shown while there is no shortcut.</param>
  /// <param name="actNeedsShortcut">The action only means something while a shortcut is set, as removing does.</param>
  public ShortcutFieldViewModel(
    Expression<Func<KeyCode>> key,
    Expression<Func<KeyCode>> modifier,
    Func<KeyCode, KeyCode, string> assign,
    Action act,
    string modifierExample,
    string emptyLabel = "",
    bool actNeedsShortcut = false
  )
  {
    _assign = assign ?? throw new ArgumentNullException(nameof(assign));
    _modifierExample = modifierExample ?? "";
    EmptyLabel = emptyLabel ?? "";
    Key = Computed(key);
    Modifier = Computed(modifier);
    HasShortcut = Computed(() => Key.Value != KeyCode.None);
    ShowsKeys = Computed(() => !Recording && Key.Value != KeyCode.None);
    HasModifier = Computed(() => Modifier.Value != KeyCode.None);
    Prompt = Computed(() => Recording ? L("새 단축키를 입력하세요…", "Press a shortcut…") : EmptyLabel);
    KeyText = Computed(() => KeyName(Key.Value));
    ModifierText = Computed(() => ModifierName(Modifier.Value));
    ModifierSymbol = Computed(() => DrawsSymbol(Modifier.Value));
    ModifierSide = Computed(() => SideOf(Modifier.Value));
    Record = Command(Start);
    Act = Command(
      () =>
      {
        Stop();
        act?.Invoke();
      },
      actNeedsShortcut ? HasShortcut : null
    );
  }

  private void Start()
  {
    Recording = true;
    Hint = L(
      "보조 키 하나와 일반 키를 함께 누를 수 있어요. Esc로 취소합니다.",
      "Use a key with up to one modifier. Esc cancels."
    );
    // The click or Enter that started recording must not be recorded.
    _startedFrame = Time.frameCount;
  }

  private void Stop()
  {
    Recording = false;
    Hint = "";
  }

  /// <summary>
  /// While recording, takes this frame's keys: Esc cancels, a key with at
  /// most one modifier is offered to the owner. True while recording, so the
  /// window leaves the keyboard to the field.
  /// </summary>
  public bool TickCapture()
  {
    if (!Recording)
      return false;
    if (Time.frameCount == _startedFrame)
      return true;
    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
    {
      Cancel();
      return true;
    }
    int index = -1;
    while (KeyboardKeys.NextPressed(ref index, out KeyCode key))
    {
      if (KeyShortcut.IsModifier(key))
        continue;
      KeyCode modifier = ShortcutKeys.HeldModifier(out int count);
      if (count > 1)
      {
        Hint =
          L("보조 키는 하나만 선택해주세요. 예: ", "Choose one modifier, for example ") + _modifierExample + L("", ".");
        Runtime.Probe();
        return true;
      }
      TryAssign(key, modifier);
      return true;
    }
    return true;
  }

  /// <summary>Saves the shortcut and stops recording, or keeps recording and says why it can't be used.</summary>
  public bool TryAssign(KeyCode key, KeyCode modifier)
  {
    string problem = _assign(key, modifier);
    if (problem != null)
      Hint = problem;
    else
      Stop();
    Runtime.Probe();
    return problem == null;
  }

  /// <summary>Stops recording without changing the shortcut.</summary>
  public void Cancel()
  {
    if (!Recording)
      return;
    Stop();
    Runtime.Probe();
  }

  [ElementUI.Pure]
  private static string KeyName(KeyCode key) => key == KeyCode.None ? "" : ShortcutKeys.KeyLabel(key);

  [ElementUI.Pure]
  private static string ModifierName(KeyCode modifier) =>
    modifier == KeyCode.None ? "" : ShortcutKeys.ModifierLabel(modifier);

  [ElementUI.Pure]
  private static bool DrawsSymbol(KeyCode modifier) =>
    modifier != KeyCode.None && ShortcutKeys.IsMac && UiKeySymbol.Symbol(modifier).Length > 0;

  [ElementUI.Pure]
  private static string SideOf(KeyCode modifier) => ShortcutKeys.IsRightModifier(modifier) ? "R" : "L";
}
