using System.Windows.Forms;
using DoNotType.App;

// Opens the settings window on every connected display and checks its layout on each one.
//
// Two checks, because they answer different questions and the first cannot find what the second
// finds. A bounds walk reports controls drawn outside their parent; it has to skip the children of
// AutoScroll panels, since those scroll rather than clip. A scroll-reach probe then drives each
// AutoScroll panel to its end and asks whether the bottom-most control is inside the viewport there
// — a control can be perfectly within bounds and still be impossible to scroll to.
//
// Moving the window across a scale-factor boundary raises WM_DPICHANGED, which is a different code
// path from being created at a given DPI. Only a machine with two differently-scaled displays
// exercises it, which is why this is a hand-run tool and not a test.

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var settings = AppSettings.Load();
        var controller = new DictationController(settings);
        var form = new SettingsForm(settings, controller) { TopMost = true };
        var total = 0;

        form.Shown += async (_, _) =>
        {
            var screens = Screen.AllScreens;
            Console.WriteLine($"{screens.Length} display(s) connected");
            if (screens.Length < 2)
            {
                Console.WriteLine("NOTE: one display only — the WM_DPICHANGED path is not exercised.");
            }

            for (var s = 0; s < screens.Length; s++)
            {
                var screen = screens[s];
                var area = screen.WorkingArea;

                // Land squarely on this monitor so Windows attributes the window to it.
                form.Location = new Point(
                    area.X + Math.Max(0, (area.Width - form.Width) / 2),
                    area.Y + Math.Max(0, (area.Height - form.Height) / 2));
                form.Activate();
                await Task.Delay(900);   // let WM_DPICHANGED land and the relayout settle
                Application.DoEvents();

                Console.WriteLine();
                Console.WriteLine($"=== display {s + 1}: {screen.DeviceName}"
                                + $"{(screen.Primary ? " (primary)" : "")} {area.Width}x{area.Height}"
                                + $" -> DeviceDpi={form.DeviceDpi} ({form.DeviceDpi / 96.0:P0})"
                                + $" font={form.Font.Name} {form.Font.Height}px"
                                + $" window={form.Width}x{form.Height} ===");

                var tabs = form.Controls.OfType<TabControl>().First();
                var failures = 0;
                for (var i = 0; i < tabs.TabPages.Count; i++)
                {
                    tabs.SelectedIndex = i;
                    form.Refresh();
                    Application.DoEvents();
                    await Task.Delay(220);

                    var page = tabs.TabPages[i];
                    failures += ReportClipping(page);
                    foreach (var panel in Scrollables(page))
                    {
                        failures += ReportScrollReach(page.Text, panel);
                    }
                }

                total += failures;
                Console.WriteLine(failures == 0
                    ? $"  ALL CLEAR on {screen.DeviceName}"
                    : $"  {failures} FAILURE(S) on {screen.DeviceName}");
            }

            Console.WriteLine();
            Console.WriteLine(total == 0 ? "PASS" : $"FAIL — {total} finding(s)");
            form.Close();
        };

        Application.Run(form);
        Environment.Exit(total == 0 ? 0 : 1);
    }

    private static IEnumerable<ScrollableControl> Scrollables(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is ScrollableControl { AutoScroll: true } s) yield return s;
            foreach (var nested in Scrollables(child)) yield return nested;
        }
    }

    /// <summary>Children drawn past a parent's edge. AutoScroll parents scroll, so they are skipped.</summary>
    private static int ReportClipping(Control page)
    {
        var found = 0;
        Walk(page, (parent, child) =>
        {
            if (parent is ScrollableControl { AutoScroll: true }) return;
            if (parent.ClientSize.Height <= 0) return;
            var down = child.Bottom - parent.ClientSize.Height;
            var right = child.Right - parent.ClientSize.Width;
            if (down <= 1 && right <= 1) return;
            found++;
            Console.WriteLine($"  CLIPPED [{page.Text}] {child.GetType().Name} \"{Trim(child.Text)}\""
                            + $" by {Math.Max(0, down)}px down / {Math.Max(0, right)}px right");
        });
        return found;
    }

    /// <summary>
    /// Scrolls to the end and reads the result off the laid-out children. Scrollbar arithmetic is
    /// not the test — ScrollBar.Maximum is not the maximum scroll position — so this drives the real
    /// thing instead of predicting it.
    /// </summary>
    private static int ReportScrollReach(string tab, ScrollableControl panel)
    {
        if (!panel.VerticalScroll.Visible) return 0;

        panel.AutoScrollPosition = new Point(0, int.MaxValue);
        panel.PerformLayout();
        Application.DoEvents();

        Control last = null;
        foreach (Control c in panel.Controls)
        {
            if (!c.Visible) continue;
            if (last is null || c.Bottom > last.Bottom) last = c;
        }
        if (last is null) return 0;

        var overshoot = last.Bottom - panel.ClientSize.Height;
        if (overshoot > 0)
        {
            Console.WriteLine($"  UNREACHABLE [{tab}] {last.GetType().Name} \"{Trim(last.Text)}\""
                            + $" by {overshoot}px past the scroll end");
            return 1;
        }

        Console.WriteLine($"  scroll reach [{tab}] ok: last is {last.GetType().Name} "
                        + $"\"{Trim(last.Text)}\", {-overshoot}px to spare");
        return 0;
    }

    private static string Trim(string s) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length > 28 ? s[..28] + "…" : s).Replace("\n", " ");

    private static void Walk(Control parent, Action<Control, Control> visit)
    {
        foreach (Control child in parent.Controls)
        {
            if (!child.Visible) continue;
            visit(parent, child);
            Walk(child, visit);
        }
    }
}
