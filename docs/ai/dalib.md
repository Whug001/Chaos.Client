## DALib Key Types

- **`MapFile`** -- `Tiles[x,y]` returns `MapTile` with `.Background`, `.LeftForeground`, `.RightForeground`
- **`Tileset`** -- `Collection<Tile>`, indexed by background tile ID
- **`Tile`** -- 56x27 palettized pixel data
- **`HpfFile`** -- Foreground tile, 28px wide, variable height
- **`Palette`** -- 256 SKColors. `Dye(colorTableEntry)` returns new palette with dye colors at index 98+.
- **`PaletteLookup`** -- Maps tile IDs to palettes via PaletteTable. Khan archives use `KhanPalOverrideType.Male`/`.Female`.
- **`ColorTable`** -- Dye color table from `.tbl` files. `ColorTableEntry` has `Colors[]` (6 SKColors for palette dye slots).
- **`EpfFile/MpfFile/EfaFile`** -- Sprite/animation formats with frame collections
- **`Palettized<T>`** -- Generic wrapper: `.Entity` + `.Palette`, implements IDisposable
- **`DataArchive`** -- DAT file container. `archive["filename"]` or `archive.TryGetValue(name, out entry)`
