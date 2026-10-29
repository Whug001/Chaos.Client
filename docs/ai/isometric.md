## Isometric Rendering Reference

Tile dimensions: 56x27 pixels, half-tile: 28x14 (from `DALib.Definitions.CONSTANTS`).

```
Tile -> Pixel (from Graphics.RenderMap):
  initialDrawX = (mapHeight - 1) * 28
  For each tile (x, y):
    pixelX = initialDrawX + x * 28    (initialDrawX decrements by 28 each y row)
    pixelY = initialDrawY + x * 14    (initialDrawY increments by 14 each y row)

Foreground tile positioning:
  lfgDrawX = same as bgDrawX
  lfgDrawY = bgDrawY + (x+1) * 14 - image.Height + 14  (bottom-aligned)
  Only render if tileIndex.IsRenderedTileIndex() -> (index > 10012) || ((index % 10000) > 12)

Draw order (painter's algorithm -- diagonal stripe, see WorldScreen.Draw.cs):
  1. Background tiles (floor) -- y-major, x-minor order
  2. Tile cursor highlight
  3. Foreground tiles + Entities + Effects -- diagonal stripe (depth = x+y ascending), X ascending within stripe; ground effects in stripe, entity effects after entity
  4. Silhouettes -- blocked-entity outlines behind foreground
  5. DarknessRenderer -- light/darkness overlay (if MapFlags has Darkness; strength follows the Theatre house level)
  5b. SpotlightRenderer -- Theatre spotlight colour (additive), whenever a stage lighting setup has lights
  6. WeatherRenderer -- snow/rain overlay (low nibble 1/2 of MapFlags)
  6b. AmbientEffects -- fog, lightning, mists, particles (ghosts among them), then fly-bys (bats) last (one screen-space batch per active overlay)
  7. Viewport overlays (health bars, chat bubbles, chant text, etc.)
  8. Debug renderer (draw counts, gridlines, toggled via debug flags)
  9. Tab map overlay -- on top of world, under HUD (Tab key toggle)
  10. UI overlay (Root panel) -- popups, HUD; separate SpriteBatch pass, no camera transform
  11. Drag icon -- always topmost
```
