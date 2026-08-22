# DPI layout harness

Opens the real `SettingsForm` on every connected display and checks its layout on each one. Run by
hand; it is not wired into the solution or CI, because what it checks needs displays a runner does
not have.

```
cd tools/dpi-harness
dotnet run
```

Exit code 0 means every display came back clean.

## What it checks

**Clipping.** Any control drawn past its parent's edge. Children of `AutoScroll` panels are skipped:
they scroll rather than clip, so counting them reports false failures.

**Scroll reach.** Each `AutoScroll` panel is driven to its end and asked whether the bottom-most
control is inside the viewport there. This is a different question, and the clipping walk cannot
answer it — a control can sit within bounds and still be impossible to scroll to. The General tab's
Save button was exactly that case.

Scrollbar arithmetic is deliberately not used as the test: `ScrollBar.Maximum` is not the maximum
scroll position, and predicting the extent rather than driving it produced a confidently wrong answer
the first time round.

## Why two displays matter

Moving a window across a scale-factor boundary raises `WM_DPICHANGED`, and that rescale is a
different code path from being created at a given DPI. With one display connected the harness says
so and skips it. To exercise it, connect two displays set to different scaling percentages and run
again — the harness walks the window onto each in turn.

## Reading the output

```
=== display 1: \\.\DISPLAY1 (primary) 3840x2076 -> DeviceDpi=168 (175%) ... ===
  scroll reach [General] ok: last is Button "Save", 0px to spare
  ALL CLEAR on \\.\DISPLAY1
```

A `CLIPPED` or `UNREACHABLE` line names the tab, the control and the shortfall in pixels.
