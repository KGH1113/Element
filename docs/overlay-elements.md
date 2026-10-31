# Overlay elements

The overlay editor (Settings → Overlay) places three kinds of elements: **Text**, **Key viewer** and **Shape**. Selecting an element opens a second toolbar with that element's own controls. Drag it by its grip to move it; it keeps its place for the next selection and never blocks the canvas.

## Text

A text is literal characters mixed with **values**. A value is the live text of something the game knows, and it behaves as one character: the caret skips it, a selection takes it whole, one Backspace deletes it.

| Category | Values |
| --- | --- |
| Progress | Progress (`0%-48%`), start (`0%`), current (`48%`) |
| Judgment | X-Accuracy, all judgments (the colored row), Overload, Too Early, Very Early, Early Perfect, Perfect −, Pure Perfect, Perfect +, Late Perfect, Very Late, Too Late, Miss, Combo, Timing scale |
| Time | Time (`1:27~3:48`), elapsed, total |
| Speed | Tile BPM, current BPM, keys per second |
| Song | Song title, attempts, full attempts |
| System | FPS |

- Adding a text, double-clicking one, pressing Enter on a selected one or **Edit text** in its toolbar opens the editor: the screen dims and the text floats in the middle at the size the game draws it. Every value is listed below at once, grouped by category.
- The bar above the text sets the size in pixels and the color of the selected characters, or of the whole text when nothing is selected. Every color is either **Highlight Color** (follows the highlight color, only its opacity is yours) or **Custom Color**.
- The text toolbar's size (− 25px +) sets the whole text at once. Sizes are what the game draws at 1080p, so resizing the element with its handles changes them too.
- ⌘/Ctrl + Enter finishes, Esc cancels. A text left empty is removed.
- Only texts that use a changed value are redrawn, so values that update every hit cost no more than before.
- The combo value grows briefly on every hit, like the old combo.

## Shape

A shape starts as a square. Its toolbar changes the kind (square, triangle, circle), the fill and the border (color and width). Changing the kind after moving or rounding corners asks first, because those edits are dropped.

- Square handles sit on the corners: drag one to move that corner. The box grows to keep every corner.
- The small round handle inside a corner rounds that corner only.
- The white dashed box resizes: a corner keeps the proportions, a side stretches in that direction.
- Clicking a handle without dragging opens its exact values (corner X/Y, radius, width and height).
- Circles have no corners; their box sets the ellipse.
- Shapes are drawn behind all texts.

## Key viewer

Added at once as a hand viewer. Its toolbar switches between hand and foot in the same place. It is available while ElementNote runs with **Show inside ADOFAI** on; otherwise Add Element says what to turn on.

Editor panels over a key viewer cut it out with their own rounded corners, so the viewer never covers the editor.

## Settings from before 1.1

The fixed overlays of earlier versions are converted on the first load into texts with the same values, colors and places. A moved overlay keeps its anchor, offset and size. The combo becomes two texts, "Perfect" and the number, placed where the old combo was. Values that used to fade toward the highlight color (progress, accuracy, BPM) now use the highlight color directly. The labels in front of them ("Progress |", "Music Time |") keep following the game's language until the text is edited; editing turns them into plain words.
