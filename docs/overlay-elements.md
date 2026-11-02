# Overlay elements

The overlay editor (Settings → Overlay) places three kinds of elements: **Text**, **Key viewer** and **Shape**.

- **Texts and shapes** only move on the canvas; their size is set in their editor. While one is selected, the toolbar shows **Edit text** or **Edit shape** where the scale was. Double-clicking or pressing Enter opens the same editor. Scales from earlier versions stay as they were.
- **Key viewers** keep the scale and resize handles. Selecting one opens a second toolbar with hand and foot; drag it by its grip. It keeps its place for the next selection and never blocks the canvas.

## Selecting several

- **Dragging over empty canvas** selects every element whose visible content lies fully inside the area. An element only partly inside stays out, so a large background shape isn't caught by accident.
- **Cmd/Ctrl or Shift + click** adds an element to the selection or takes it out. **Cmd/Ctrl or Shift + drag** draws the area even when it starts on an element, and adds what it covers to the selection.
- **Cmd/Ctrl + A** selects every element on the canvas. A plain click on empty canvas clears the selection.
- **Moving.** Dragging any selected element moves them all; snapping uses the outline of the whole group. Arrow keys move them all too. A click on one of them without moving selects it alone.
- **Delete and Reset** act on every selected element, as one undo step.
- **Align and distribute.** With two or more selected, the second toolbar lines them up: left, center, right, top, middle or bottom, against the box they cover together. With three or more, it also spaces them evenly across or down, keeping the outer two in place. Each press is one undo step; when there is nothing to change, such as a column already evenly spaced, the toolbar says so instead.
- While several are selected, the scale greys out and Edit text/shape, the resize handles and the key viewer toolbar step aside. Double-clicking one selects it alone and opens its editor.

## Values

A value is something the game knows, kept live. It behaves as one character: the caret skips it, a selection takes it whole, and one Backspace deletes it.

| Category | Values |
| --- | --- |
| Progress | Start, current |
| Judgment | X-Accuracy, Overload, Too Early, Very Early, Early Perfect, Perfect −, Pure Perfect, Perfect +, Late Perfect, Very Late, Too Late, Miss, Combo, Timing scale |
| Time | Elapsed, total |
| Speed | Tile BPM, current BPM, keys per second |
| Song | Song title, attempts, full attempts |
| System | FPS |

- **Units are words you type.** Values carry no units, so write `%` after progress or accuracy yourself.
- **Composite values are built from parts.** Values that combined others are gone. The old progress `0%-48%` is now start, `%-`, current and `%`. The old time `1:27~3:48` is elapsed, `~` and total.
- **Adding a value.** Every value is laid out by category in the input dock at the bottom of both editors, which is always there. In a text, clicking one adds it at the caret; while a formula is being typed, it goes into the formula. You can also drag it: an accent caret shows where it lands in the text, and a number lights up when the value can go there.
- **Hovering.** Pointing at a value in the text names it, including its calculation.

## Calculating

Clicking a number, or a value in a text, types its formula in the input dock along the bottom of the editor. The preview always sits above the dock, so it is never covered. With nothing being typed, the dock's top line says what to do and the keypad rests.

- **Formula line.** The top line shows what is edited, its formula with values as chips, and the result on the right.
- **Values and keypad.** Below are all values and a large keypad: 0–9, point, + − × ÷, backspace, **AC** to start over and **Done**. The keyboard types the same keys.
- **Results.** Elsewhere, numbers show their results. One worked out from a formula reads in the accent; its formula appears only in the dock.
- **Switching and finishing.** Clicking another number switches the dock to it. Done, Enter, Esc or a click on the empty dim finishes, and the dock rests again.
- **AC** starts over without touching the number until something new is typed; it is its own undo step.

- **Order.** × and ÷ come before + and −.
- **Combining values.** Type an operator, then pick another value (or drop it on the formula line). Without an operator, a value replaces what was there.
- **Display.** The text shows the result as the game will: up to two decimals, or m:ss when the result is a time. Total − elapsed shows the time left. A time divided by a time is a plain number.
- **Missing data.** A result needs every value it uses. Until the game has them, the overlay shows nothing there, and the editor uses each value's example.
- **Words.** The song title can't be calculated with, so number fields refuse it with a short reason.
- **Song title.** The game's level name, "artist - song", with the level's own bold, italic, colors and sizes. Parts the level hides stay hidden (a see-through artist, a dash shrunk to nothing). A name hidden entirely shows as plain text, so the overlay still says what is playing. Broken tags, such as a seven-digit color, are left out instead of shown.

## Text

- Adding a text, double-clicking one, pressing Enter on a selected one or **Edit text** in the toolbar opens the editor. The screen dims and the text floats in the middle at the size the game draws it.
- The bar above the text has three labeled buttons: **Size**, **Text color** and **Grow from**. Size and color act on the selected characters, or on the whole text when nothing is selected. Size takes values too.
- A color button opens the color picker right above the input dock's keypad and stays lit while it is open. The text stays in sight; the dock's line says which color is picked. Clicking the button again, Esc or a click elsewhere closes it.
- The picker's opacity works like the editor's other numbers: clicking it types its formula on the keypad right below, and a value from the dock can be dropped on it. The slider shows the result; moving it makes the opacity a plain number again.
- Every color is either **Highlight Color** (follows the highlight color; only its opacity is yours) or **Custom Color**.
- Sizes are what the game draws at 1080p.
- ⌘/Ctrl + Enter finishes, Esc cancels. A text left empty is removed.
- Only texts that use a changed value are redrawn, so values that update every hit cost no more than before.
- A combo, even inside a calculation, grows briefly on every hit like the old combo.

## Shape

Add Element → Shape asks for a square, triangle or circle, then opens the shape editor. The kind stays fixed afterwards.

The default layout has a **progress bar** made of three shapes, as JipperResourcePack draws it: a black bar centered along the top, a light gray track 2 px inside it, and a fill in the highlight color over the track whose width is current progress × 6.36, growing from its left. Select both to move them together. Deleting them keeps them deleted; Reset all brings them back. Layouts from earlier versions get the bar once.

- The shape sits in the middle at its in-game size, with **W** under it and **H** to its right.
- Each corner of a square or triangle has an arc just outside it. Clicking the arc edits that corner's rounding and shows its value beside the arc.
- The bar above has **Fill**, **Border**, **Border width** and **Grow from**. Fill and Border open the color picker above the keypad; clicking the other one switches it. Their opacity in the picker and the border width are typed in the input dock and take values.
- Squares whose corners were dragged in 1.1.0 keep that shape; corners can no longer be moved.
- Every number takes values. For example, width = current × 3 makes a progress bar.
- On the canvas a shape only moves, like a text. Shapes are drawn behind all texts.
- ⌘/Ctrl + Z undoes inside the editor. Done is one undo step in the overlay editor. Cancelling a new shape removes it.

## Grow from

When a text or shape changes size (a value grows longer, a width follows progress), one point of it stays put and the rest grows away from it. The **Grow from** button in either editor's bar names the current point and picks it from nine, with arrows showing where it grows; the editor marks the point on the preview. **Auto** uses the point nearest to where the element sits on screen: an element near the right edge grows to the left. The choice is saved with the element when you press Done.

## Key viewer

Added at once as a hand viewer. Its toolbar switches between hand and foot in the same place. It is available while ElementNote runs with **Show inside ADOFAI** on; otherwise Add Element says what to turn on.

Editor panels over a key viewer cut it out with their own rounded corners, so the viewer never covers the editor.

## Settings from earlier versions

- **Before 1.1:** the fixed overlays are converted on the first load into texts with the same values, colors and places. A moved overlay keeps its anchor, offset and size. The combo becomes two texts, "Perfect" and the number, placed where the old combo was. Values that used to fade toward the highlight color (progress, accuracy, BPM) now use the highlight color directly. The labels in front of them ("Progress |", "Music Time |") keep following the game's language until the text is edited; editing turns them into plain words.
- **1.1.0:** texts are rewritten once so they read the same. Progress, time and the judgment row become their parts, and values that lost their `%` get it back as text. The judgment row's Pure Perfect takes a fixed green instead of following the game's hit color.
