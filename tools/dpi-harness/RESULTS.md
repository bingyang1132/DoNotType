# Harness runs

One section per machine. A run on a single display does not supersede a two-display run, because it
cannot reach the `WM_DPICHANGED` path at all — so entries are added, not replaced.

## 2026-08-21 — two displays, 125% and 200%

Windows 10 Pro 19045, .NET SDK 10.0.400, `fix/windows-highdpi-settings-layout` at 209213c, system
UI font Microsoft YaHei UI (a non-Latin default, so the baseline is not Segoe UI 15px).

| | DISPLAY1 (primary) | DISPLAY2 |
|---|---|---|
| Physical | 1920x1030 | 3840x2080 |
| Scale | 125% (DeviceDpi 120) | 200% (DeviceDpi 192) |
| Font | Microsoft YaHei UI 20px | Microsoft YaHei UI 31px |
| Settings window | 811x761 | 1298x1218 |

```
2 display(s) connected

=== display 1: \\.\DISPLAY1 (primary) 1920x1030 -> DeviceDpi=120 (125%) font=Microsoft YaHei UI 20px window=811x761 ===
  scroll reach [General] ok: last is Button "Save", 0px to spare
  ALL CLEAR on \\.\DISPLAY1

=== display 2: \\.\DISPLAY2 3840x2080 -> DeviceDpi=192 (200%) font=Microsoft YaHei UI 31px window=1298x1218 ===
  scroll reach [General] ok: last is Button "Save", 0px to spare
  ALL CLEAR on \\.\DISPLAY2

PASS
```

Exit code 0. The `WM_DPICHANGED` path was exercised: the two displays sit at different scale factors,
and the window and font both tracked the 120→192 ratio (811→1298 wide, 20→31px) rather than being
bitmap-stretched by Windows.

### The Save button's margin is exactly minimal, at every DPI

The scroll-reach probe reported `0px to spare` on both displays. Zero passes, but it is the boundary,
and the run alone does not say whether 18px is comfortably enough or barely enough. Re-run with the
margin changed, to separate the two:

| `save.Margin.Bottom` | 125% | 200% |
|---|---|---|
| 18 (as committed) | 0px to spare | 0px to spare |
| 40 (probe, reverted) | 29px to spare | 45px to spare |

The slack tracks the margin: the 22px baseline increase yields 27.5 and 44 physical px at the two
scales, against 29 and 45 measured. So the slack is governed by the margin and is not a fixed
property of a scrolled-to-end viewport.

That 18px lands on exactly zero at *both* scales is therefore not a coincidence of one DPI. The
margin compensates for the panel's unreachable 18px bottom padding, and compensator and compensated
scale by the same factor, so the result is zero at every DPI. The fix is correct, DPI-stable, and
exactly minimal.

The consequence is cosmetic and left alone: because the slack is zero, scrolling to the end puts the
button's bottom edge flush against the viewport, and the 18px of padding below it never renders as
visible space. Restoring that gap would need `Margin.Bottom = 36` — 18 to reach, 18 to show. Not
done here; reachability is what was broken.

### Not covered by this run

**Only the General tab's scroll reach was checked.** The probe skips panels whose vertical scrollbar
is not visible, and at these window sizes — 1298x1218 at 200% — no other tab scrolled. Checking the
rest means shrinking the window toward `MinimumSize` (620x520) and running again.

**No case where the window outgrew its display.** Both sizes fit their monitor with room left, so
whatever happens when the scaled window exceeds the working area is still untested.

**One font.** Every measurement here is on Microsoft YaHei UI. The 96 DPI Segoe UI baseline the
sizes were authored against was not re-checked on this machine.
