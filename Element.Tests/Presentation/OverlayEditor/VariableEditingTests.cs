using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Presentation.OverlayEditor;

// The dock's variable: the keypad types into the picked number, values go
// after the picked piece or in its place, a condition's slots open in turn,
// and the field takes a change only while it fits.
internal static class VariableEditingTests
{
  [Suite("the dock edits a variable piece by piece, into slots, and keeps a field that doesn't fit")]
  public static void Run()
  {
    TheKeypadTypesNumbers();
    ValuesGoInPlace();
    ConditionsOpenSlotByslot();
  }

  private static (FormulaEditing Editing, OverlayVariable[] Saved) Field(OverlayValueType type, string start)
  {
    OverlayVariable[] saved = { OverlayVariable.Parse(start) };
    FormulaEditing editing = new();
    editing.Activate(new FormulaSlot("field", type, () => saved[0], variable => saved[0] = variable));
    return (editing, saved);
  }

  private static void TheKeypadTypesNumbers()
  {
    (FormulaEditing editing, OverlayVariable[] saved) = Field(OverlayValueType.Number, "40");
    Check(editing.SelectedPiece?.Text == "40", "a field opens with its last piece picked");
    editing.Press('5');
    Check(saved[0].Serialize() == "405", "digits go into the picked number");
    editing.Press('*');
    editing.Press('2');
    Check(saved[0].Serialize() == "405*2", "an operator joins the next number");
    editing.Press('.');
    Check(!editing.CanPress('.') && editing.Press('5') && saved[0].Serialize() == "405*2.5", "one point per number");
    editing.Backspace();
    editing.Backspace();
    Check(saved[0].Serialize() == "405*2", "backspace takes digits: " + saved[0].Serialize());
    editing.Backspace();
    Check(saved[0].Serialize() == "405*0" && editing.SelectedPiece?.Text == "0", "its last digit leaves 0");
    editing.Backspace();
    Check(saved[0].Serialize() == "405", "and 0 goes with its operator, so it still calculates");
    Check(!editing.CanPress('≥'), "comparisons only go in a condition's operator");

    // An operator brings the number after it, picked, so nothing ends in an operator.
    editing.Press('+');
    Check(
      editing.Root.Serialize() == "405+0" && editing.SelectedPiece?.Text == "0" && editing.Problem == null,
      "+ brings its 0, picked: " + editing.Root.Serialize()
    );
    editing.Press('7');
    Check(saved[0].Serialize() == "405+7", "a digit types over that 0");
    editing.SelectAt(null, 1);
    Check(
      editing.SelectedPiece?.Kind == OverlayPieceKind.Operator && editing.Remove(),
      "a picked operator can be removed"
    );
    Check(saved[0].Serialize() == "405", "and it takes the number it joined: " + saved[0].Serialize());
  }

  private static void ValuesGoInPlace()
  {
    (FormulaEditing editing, OverlayVariable[] saved) = Field(OverlayValueType.Number, "40");
    editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.Combo));
    Check(saved[0].Serialize() == "combo", "in a number field a value replaces the picked one");
    Check(!editing.CanDrop(OverlayValueCatalog.Get(OverlayValueId.SongTitle)), "words don't fit a number");
    Check(
      editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.SongTitle)) == FormulaDropResult.Refused,
      "and are refused"
    );
    (FormulaEditing text, OverlayVariable[] words) = Field(OverlayValueType.Any, "\"Acc \"");
    text.Drop(text.Slot, OverlayValueCatalog.Get(OverlayValueId.Accuracy));
    Check(words[0].Serialize() == "\"Acc \" {accuracy}", "in Any a value goes beside the picked one");
    text.Drop(text.Slot, OverlayValueCatalog.Get(OverlayValueId.MakeText));
    Check(text.SelectedPiece?.Kind == OverlayPieceKind.Text, "made words are picked, for the keyboard");
    text.SetText("%");
    Check(words[0].Serialize() == "\"Acc \" {accuracy} \"%\"", "the keyboard types into them");
  }

  private static void ConditionsOpenSlotByslot()
  {
    (FormulaEditing editing, OverlayVariable[] saved) = Field(OverlayValueType.Number, "40");
    editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.MakeCondition));
    Check(
      saved[0].Serialize() == "40" && editing.Problem?.Slot == OverlayPiece.Left,
      "an unfinished condition isn't taken: the field keeps 40, and the dock says which slot is empty"
    );
    // Every slot is laid out on the line: a click picks it straight away.
    FormulaStep[] Into(int slot) => new[] { new FormulaStep(0, slot) };
    editing.SelectAt(Into(OverlayPiece.Left), -1);
    Check(editing.Path.Count == 2 && editing.Kind == OverlayValueType.Number, "a slot is picked as a number variable");
    editing.Drop(editing.Slot, OverlayValueCatalog.Get(OverlayValueId.Combo));
    editing.SelectAt(null, 0);
    Check(
      editing.Path.Count == 1 && editing.SelectedPiece?.Kind == OverlayPieceKind.Condition,
      "the condition itself is a click away too"
    );
    editing.SelectAt(Into(OverlayPiece.Compare), 0);
    Check(
      editing.Kind == OverlayValueType.Compare
        && editing.Working[0].Op == '≥'
        && editing.Press('>')
        && editing.Press('≥')
        && !editing.CanRemove,
      "the comparison starts as ≥, takes another, and is chosen rather than removed"
    );
    foreach (
      (int slot, string typed) in new[]
      {
        (OverlayPiece.Right, "100"),
        (OverlayPiece.Result, "60"),
        (OverlayPiece.Default, "40"),
      }
    )
    {
      editing.SelectAt(Into(slot), -1);
      foreach (char key in typed)
        editing.Press(key);
    }
    Check(
      editing.Problem == null && saved[0].Serialize() == "if({combo};>=;100;60;40)",
      "once every slot fits, the field takes it: " + saved[0].Serialize()
    );
  }
}
