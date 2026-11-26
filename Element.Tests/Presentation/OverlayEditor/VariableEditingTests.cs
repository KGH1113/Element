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
    TheDockStepsOutAndAcross();
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

  // Inside a condition's slot: which condition that is, a step back out of
  // it, and straight across to another of its slots.
  private static void TheDockStepsOutAndAcross()
  {
    (FormulaEditing editing, _) = Field(OverlayValueType.Number, "if({combo};>=;100;60;if({combo};>=;50;30;20))");
    OverlayPiece outer = editing.SelectedPiece;
    Check(
      editing.Depth == 0 && editing.Around == null && editing.AroundSlot == -1 && !editing.Leave() && !editing.Hop(0),
      "at the field there is no condition around, nowhere back to and nowhere across"
    );
    editing.Enter(OverlayPiece.Right);
    Check(
      editing.Depth == 1
        && editing.Around == outer
        && editing.AroundSlot == OverlayPiece.Right
        && editing.AroundGives == OverlayValueType.Number
        && editing.SelectedPiece?.Text == "100",
      "inside a slot the condition around is the one it is a slot of"
    );
    Check(
      editing.Hop(OverlayPiece.Result)
        && editing.Depth == 1
        && editing.Around == outer
        && editing.AroundSlot == OverlayPiece.Result
        && editing.SelectedPiece?.Text == "60",
      "across goes straight into another slot of the same condition"
    );
    editing.Hop(OverlayPiece.Default);
    OverlayPiece inner = editing.SelectedPiece;
    Check(
      inner?.Kind == OverlayPieceKind.Condition && editing.Depth == 1 && editing.AroundSlot == OverlayPiece.Default,
      "an answer that is a condition is picked there, still inside the outer one"
    );
    editing.Enter(OverlayPiece.Left);
    Check(
      editing.Depth == 2 && editing.Around == inner && editing.AroundSlot == OverlayPiece.Left,
      "inside the inner condition, the nearest one is around"
    );
    Check(
      editing.Leave() && editing.Depth == 1 && editing.Around == outer && editing.SelectedPiece == inner,
      "back is one step: the inner condition is picked again, inside the outer one's answer"
    );
    Check(
      editing.Leave() && editing.Depth == 0 && editing.Around == null && editing.SelectedPiece == outer,
      "and one more step is out at the field, the outer condition picked"
    );

    // What keeps the field from being finished shows on its own condition: the dock goes there.
    (FormulaEditing unfinished, _) = Field(OverlayValueType.Number, "if({combo};>=;100;60;if(;>=;50;30;20))");
    OverlayPiece whole = unfinished.SelectedPiece;
    unfinished.Enter(OverlayPiece.Right);
    OverlayPiece lacking = unfinished.Problem?.Condition;
    Check(lacking != null && lacking != whole, "the empty slot is the inner condition's");
    unfinished.FaceProblem();
    Check(
      unfinished.SelectedPiece == lacking
        && unfinished.Depth == 1
        && unfinished.Around == whole
        && unfinished.AroundSlot == OverlayPiece.Default,
      "from another slot the dock goes to the condition that lacks something, picked where it sits"
    );
    unfinished.Enter(OverlayPiece.Right);
    unfinished.FaceProblem();
    Check(
      unfinished.Depth == 2 && unfinished.Around == lacking && unfinished.AroundSlot == OverlayPiece.Right,
      "inside a slot of that condition the dock stays: its strip shows the empty one"
    );
  }
}
