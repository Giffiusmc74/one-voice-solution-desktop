# ONE Voice Solution — Meter Redesign Brief for Hanif

## What needs to be done

Rewrite the `DrawDialMeter` method in `src/MainFormV5.cs` using **pure GDI+ only**.
The target look is the reference image you already have.

---

## GitHub

- **Repo:** `https://github.com/Giffiusmc74/one-voice-solution-desktop`
- **Your branch:** `feature/meter-redesign-hanif`
- **Do all work on that branch.** Do NOT push to `main`.
- When done, open a Pull Request from `feature/meter-redesign-hanif` → `main`.

```
git clone https://github.com/Giffiusmc74/one-voice-solution-desktop.git
cd one-voice-solution-desktop
git checkout feature/meter-redesign-hanif
```

---

## The one method to change

**File:** `src/MainFormV5.cs`
**Method:** `private void DrawDialMeter(Graphics g, Rectangle bounds, float percent, Color baseColor)`

**Do NOT touch anything else.** All audio capture, device selection, bridge server, heartbeat, settings, and backend logic must remain exactly as-is.

---

## Method signature (already correct in the file — do not change it)

```csharp
private void DrawDialMeter(Graphics g, Rectangle bounds, float percent, Color baseColor)
```

- `bounds` — the full panel rectangle (origin 0,0 when called from Paint event)
- `percent` — 0.0 to 100.0 — the current audio level
- `baseColor` — the meter color (red for Agent, green for Customer)

---

## What the meter must look like (match the reference image exactly)

Working from outside to inside:

1. **Outer ambient glow** — soft, wide, very low opacity bloom behind the whole ring. Multiple passes with increasing radius and decreasing alpha. Stays inside/around the ring, does not bleed across the whole panel background.

2. **Outer ring** — solid colored ring, ~3px. This is the hard edge.

3. **LED dots on the ring** — small round dots sitting ON the outer ring. ~72 dots evenly spaced around the full 360°. Lit dots (up to the current level) glow in the meter color. Unlit dots are dark grey. Each lit dot has a small glow halo behind it.

4. **Tick marks** — inside the ring, thin radial lines. Every 6th tick is a major tick (longer, thicker, brighter). Minor ticks are shorter and dimmer.

5. **Progress arc** — inside the tick ring, a thick arc from the top (-90°) sweeping clockwise to the current level. Two passes: a wide semi-transparent glow pass, then a sharp solid pass on top. Round end caps.

6. **Top highlight** — a short white arc (~12°) at the very top of the ring. Simulates a specular reflection.

7. **Inner core** — deep dark filled circle. PathGradientBrush from black at center to very dark gray at edge.

8. **Color bleed** — a very subtle radial color tint inside the core (low opacity, same color as meter). Gives the impression of light bouncing inside.

9. **Inner rim** — faint white ellipse at the edge of the inner core. Adds depth.

10. **Percent text** — centered in the core. Large, bold, `Segoe UI`. Two passes: a slightly larger colored glow pass, then a white sharp pass on top. Shows `{value}%` (e.g. `72%`).

---

## Technical constraints

- **.NET Framework 4.8 WinForms** — no WPF, no WebView2, no HTML, no Canvas.
- **GDI+ only** — `System.Drawing`. No third-party graphics libraries.
- **No Gaussian blur available natively** — simulate glow with multiple stacked semi-transparent `Pen` or `SolidBrush` passes at increasing radii with decreasing alpha.
- **Double-buffered panel** — the meter panel is a `DoubleBufferedPanel` so flicker is handled. Just draw correctly.
- **AntiAlias + CompositingQuality.HighQuality** — set these at the top of the method.

---

## Reference Python render

The file `render_new_design.py` in the repo root is the Python script that produced the reference image. It uses PIL/Pillow with GaussianBlur. Use it as the design specification — not as code to translate line-for-line (the blur approach differs), but as the exact visual target.

Run it locally to see the target:
```
pip install pillow
python render_new_design.py
# outputs: new_design_preview.png
```

---

## Protecting `main` while you work

- All your commits go to `feature/meter-redesign-hanif`
- Other work will continue on `main` during the same period
- When you're done, open a PR — do not merge yourself
- The PR will be reviewed before merging so nothing collides

---

## Questions?

Contact via the project owner. Do not push to `main` under any circumstances.
