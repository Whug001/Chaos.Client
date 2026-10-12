# Illustria's Closet — fitting room design

Date: 2026-10-12. Approved in chat by the user; build without further check-ins.

## Goal

A fitting room in Abel where players try on any overcoat, overhelmet or accessory in the game. Nothing is sold.
It shows how a piece looks before the player goes hunting for it.

## Decisions

- **Fitting room only.** No buying, no prices, no "where to find it".
- **Racks.** Every item template whose equipment type is OverArmor, OverHelmet or Accessory. Excluded: mounts,
  anything under the Admin item folder (none today), and template keys on a hide list in Unora data.
- **Gender.** Only looks the player's gender can wear. Unisex looks show for everyone.
- **One tile per look.** Dye variants (same slot, sprite, gender and draws-over-hair flag) are grouped into one tile.
  Picking a tile shows swatches for the colors that exist as real items.
- **Starting look.** The player's real outfit. A "Take everything off" button returns to base armor and hair.
- **Trying on.** Click a tile to put it on; click again to take it off.
  - Overcoat replaces the overcoat layer (shows over armor).
  - OverHelmet replaces the head layer, unless it draws over hair; then it goes into an accessory slot the way the
    server places it.
  - Accessories fill slots 1-3 in order; a fourth replaces the oldest.
  - The Wearing tab lists what is on, each with an "x".
- **Nothing reaches the server.** Closing changes nothing.
- **Colors.** A tile's swatches are the colors that exist as items; a dyeable look offers every dye color.
- **Window layout C ("standing at the mirror").** Left column: a framed mirror showing the front view, and below it the
  character seen from behind. The character walks up from the bottom when the window opens. Turn buttons rotate both
  views together. Right column: tabs Overcoats / Hats / Accessories / Wearing, a search box, an icon grid with paging,
  and dye swatches. Same frame and drawing code as Josephine's beauty shop, 2x scale.

## The room — Illustria's Closet (Abel)

- Map instance `abel_empty_room3` (template 76, 13x13) is renamed "Illustria's Closet".
- A door from Abel town leads in. Its exit returns through that door. If Abel has no unused building door, the room
  stays linked by data only and the user is asked before any town art changes.
- A north-wall mirror via `mirrors.json` (the Mirror Maze / Suomi theatre system). It shows the real outfit.
- The floor tile in front of the mirror is a reactor. Stepping on it opens the fitting room; stepping off closes it.
- NPC **Illustria**, a tailor beside the mirror. One line of flavor text and an option "Let me try things on" that
  opens the same window.

## Server (Chaos-Server)

- `FittingRoomCatalog` built once at boot from all item templates: grouping, exclusions, hide list. Each look:
  slot, name, display sprite, panel sprite, gender, draws-over-hair, colors. Serialized once and cached.
- Packet: `FittingRoomDisplay` (server→client: Open with catalog plus the player's gender, hair style and hair
  color; Close). No client→server packet: the window sends nothing, and the server closes it when the player steps
  off the tile. `CLIENT_VERSION` 780 → 781.
- `TownImportLayer` gains `OverHair` for overhelmets that draw over hair; town import previews use it too.
- Door: Abel has no free building door, so Abel door (7,21), the second door of the unused Empty Room 1, is
  repointed to the closet.
- Reactor script opens on step-on, closes on step-off. Dialog script opens from Illustria's option.

## Unora data

Room rename, door + exit, `mirrors.json`, trigger reactor, Illustria merchant + dialog + spawn, hide list (empty).

## Client (Chaos.Client)

- `FittingRoom` view model: catalog, current outfit, tried-on outfit.
- `TryOnLook`: the one place that puts a look onto an `AislingAppearance`. Town import previews switch to it, which
  fixes their crown/tiara preview.
- `FittingRoomControl` window with the standing-at-the-mirror scene.

## Testing

- Server: catalog grouping and exclusions, packet round trips, version test 781.
- Client: TryOnLook rules (4th accessory, crowns, overcoat over armor), view model toggling and filtering.
- In-game checklist for the user: door, mirror reflection, trigger tile, Illustria, trying on each kind of item.
