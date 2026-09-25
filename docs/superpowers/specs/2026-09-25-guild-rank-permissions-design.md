# Guild rank permissions: switches the leader sets per rank

Designed 2026-09-25. It answers the "Guild Rank Permissions" player thread (`Unora/docs/player-feedback/suggestions/1530549949970776095-guild-rank-permissions.md`), which Iglis bumped on 2026-09-01. It goes further than idea 27 in `Unora/docs/guild-hall-ideas.md`, which only covers rank names and colors.

## Goal

The guild leader chooses what each lower rank may do. Today every rule is a fixed rank check: officers ("Council", tier 1) can withdraw from the guild bank, pay for buffs with guild gold, buy hall upgrades, and admit, kick, promote and demote. Members (tier 2) and applicants (tier 3) can do none of those. Players asked to change this both ways. Some want the bank locked to the leader, and some want members to reach shared potions.

## Decisions

| Question | Decision |
|---|---|
| Which actions get switches | Taking items from the guild bank, taking gold from it, paying for buffs with guild gold, buying hall upgrades, admitting, kicking, and promoting/demoting. Seven switches. |
| Bank items and gold | Separate switches. |
| Member management | Three switches: admit, kick, and promote/demote together. |
| Rank-gap rules | Unchanged. A switch lets a rank try an action; the existing gap rules still decide who it can act on. |
| The leader | Always has every power. The leader rank has no switches. |
| Where the leader edits | A dialog menu at Quill and at Aricin (approach A). No client window. |
| Storage | One flags enum per rank, saved in each rank's tier file (approach 1). |
| Old guilds | A tier file with no permissions gets today's rules. Nothing changes until a leader flips a switch. |

## 1. Switches and storage

### The enum

`GuildPermission` is a `[Flags]` enum in `Chaos.DarkAges/Definitions/Enums.cs`. It lives there because `Chaos.Schemas` can only see `Chaos.DarkAges` and `Chaos.Common`, the same reason `BankInteractionType` lives there.

| Value | Bit | Menu text | Allows |
|---|---|---|---|
| `None` | 0 | | Nothing |
| `WithdrawItems` | 1 | Take items from the bank | Taking items out of the guild bank |
| `WithdrawGold` | 2 | Take gold from the bank | Taking gold out of the guild bank |
| `GuildGoldBuffs` | 4 | Pay for buffs with guild gold | Paying for a guild buff from the guild bank |
| `BuyHallRooms` | 8 | Buy hall upgrades | Buying hall rooms and the guild cloak deed from Tibbs, with the buyer's own gold |
| `Admit` | 16 | Admit new members | Admitting someone in the Abel tavern |
| `Kick` | 32 | Kick lower ranks | Kicking someone of a lower rank |
| `PromoteDemote` | 64 | Promote and demote | Promoting and demoting, within the gap rules |

The enum has no `All` value. The server writes flags as names, so a saved `"All"` would quietly grant any switch added later. The full Council set is a constant in `GuildPermissionRules` instead.

### Which switches each rank can have

A new static class, `Chaos/Collections/GuildPermissionRules.cs`, holds the rules in one place:

- `ForTier(tier)`: the switches that tier can have, in menu order.
  - **Tier 1 (Council):** all seven.
  - **Tier 2 (Member):** all except `PromoteDemote`. A member can't promote anyone, because promotion needs a two-tier gap. A member can't demote anyone either, because applicants are already the lowest rank.
  - **Tier 3 (Applicant):** all except `Kick` and `PromoteDemote`. Nobody ranks below an applicant.
  - **Tier 0 (Leader):** none. The leader already has everything.
- `DefaultFor(tier)`: all seven for tier 1, `None` for every other tier. This matches today's rules.
- `Label(permission)`: the menu text in the table above.

### On the rank

- `GuildRank` gets a `Permissions` property (`GuildPermission`) and `SetPermission(permission, on)`.
- `GuildRankSchema` gets `GuildPermission? Permissions`. It's saved as names, such as `"permissions": "WithdrawItems, Admit"`.
- `GuildMapperProfile` maps both ways. A missing (null) value loads as `GuildPermissionRules.DefaultFor(tier)`. The next save writes the value out.
- Permissions belong to the tier, so renaming a rank keeps them.

### On the guild

Both methods take the guild's existing `Sync` lock.

- `HasPermission(memberName, permission)`: false for a non-member, true for the leader, and the rank's switch for everyone else.
- `SetRankPermission(tier, permission, on)`: changes one switch. It throws for tier 0 and for a tier that doesn't exist, like `SetRankAllowance` does. It doesn't check `GuildPermissionRules.ForTier`; only the menu limits which switches appear. A switch set on a tier where it doesn't apply, for example by editing a file by hand, does nothing, because the gap rules still block the action.

## 2. Checks

Each script below swaps its `IsOfficerRank` check for `guild.HasPermission(name, …)`. Refusals name the rank's limit, not a rank name, because rank names are custom. "Council member" is often the wrong word.

### Guild bank

- `BankPermissions.CanWithdraw` is renamed `CanWithdrawItems`, and a new `CanWithdrawGold` field is added. `BankPermissions.Personal` allows both, and `BankPermissions.None` allows neither.
- `Guild.GetBankPermissions` fills them from `WithdrawItems` and `WithdrawGold`. View and deposit stay open to every rank.
- In `WorldServer`, the withdraw-item handler checks `CanWithdrawItems` and the withdraw-gold handler checks `CanWithdrawGold`. The old single refusal ("You aren't ranked high enough to withdraw.") becomes:
  - "Your rank can't take items from the guild bank."
  - "Your rank can't take gold from the guild bank."
- Permissions are already re-read on every bank click, so a flip applies to open bank windows at once.
- Tuition allowances are separate. A rank with `WithdrawGold` off can still spend its weekly training and repair allowance.

### Guild buffs (`GuildBuffScript`)

- The payment menu shows "Pay from Guild Bank" only with `GuildGoldBuffs`.
- The payment step checks `GuildGoldBuffs` again before it takes gold. The refusal is "Your rank can't pay for buffs with guild gold."
- **Bug fixed along the way.** `On25ExpPaymentChoiceNext` works out the choice from the option's position, and the option list depends on rank. If the switch goes off between the menu and the click, a click on "Pay from Guild Bank" is read as "Pay from Personal Gold" and charges the member's own gold. The script will read the clicked option's text with `Subject.GetOptionText` instead, as `GuildRankManagementScript` does. A late click on "Pay from Guild Bank" is then refused by the payment step's check, and nobody is charged.

### Hall upgrades (`GuildUpdateHallScript`)

- The `tibbs_initial` check and `HandleUpgrade` use `BuyHallRooms`. This covers the four rooms and the guild cloak deed. The refusal is "Your rank can't buy hall upgrades."
- The house deed stays leader-only. Right now only the `tibbs_buyhouse` menu blocks non-leaders, and `HandleUpgrade` only asks for an officer. `HandleUpgrade` gets its own leader check when the property is `deed`, so opening up the room check can't open up the deed. The refusal is "Only the guild leader can buy the guild house deed."

### Member management

- `GuildMemberManagementScript.OnDisplayingInitial` shows Promote and Demote only with `PromoteDemote`, and Kick only with `Kick`. Admit shows only with `Admit`, and still only in the tavern.
- `GuildMemberAdmitScript`, `GuildMemberKickScript`, `GuildMemberPromoteScript` and `GuildMemberDemoteScript` check the same switch again when the action happens. Refusals say "Your rank can't admit new members.", "…kick members.", "…promote members." or "…demote members."
- The gap rules don't change, including the promote factor (1 for the leader, 2 for everyone else).
- "Promote to Leader" (handing over leadership) stays leader-only.

### No change

Taxes, allowances, rank names, the cloak design, disbanding, leaving, the roster, and the guild board's post, delete and highlight rules.

## 3. The leader's menu

### Where it lives

`GuildManagementScript` adds **Permissions** to both leader menus, right after **Ranks**. That covers Quill in the guild hall and Aricin in the Abel tavern. Aricin matters because a guild without a hall still admits, kicks and promotes there. Only the leader sees the option.

### Dialogs

New folder `Unora/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions/`:

- `generic_guild_permissions_initial.json`, type `Menu`, script key `guildPermissions`. Text: "Which rank do you want to change? The leader can always do everything." The script adds the three lower ranks by their guild names, in tier order. The template's own option is "Back" to `Top`.
- `generic_guild_permissions_rank.json`, type `Menu`, `contextual: true`, script key `guildPermissions`. Text: "What {RankName} may do. Choose a line to switch it on or off." The script adds one line per switch from `GuildPermissionRules.ForTier(tier)`, such as "Take gold from the bank: Off". The last option is "Done", back to `generic_guild_permissions_initial`.

### Script

New `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildPermissionsScript.cs`, built like `GuildAllowanceScript`:

- Picking a rank stores its tier in `Subject.Context`.
- Picking a switch line flips that switch with `SetRankPermission`, saves with `GuildStore.Save(guild)` and shows the rank menu again with the new values.
- A line's position maps back to its switch through `ForTier(tier)`. That list's order never changes, so the mapping holds even though the On/Off text does.
- Every display and every click checks that the player is still the leader. If not, the reply is "Only the leader can change permissions." and nothing changes.
- The save happens at once, like kicks and admissions. Without it, a flip waits for the timed guild save, and a server crash would undo a bank lock.

## 4. Logs

Each flip writes a server log line with the guild, the leader's name, the rank name and tier, the switch, and the new value. This follows the logging in `GuildBuffScript`. There's no guild chat message, because a member who hits a switch that's off gets a clear refusal.

## Files

Server (`Chaos.Client/Chaos-Server`):

- `Chaos.DarkAges/Definitions/Enums.cs`: `GuildPermission`.
- New `Chaos/Collections/GuildPermissionRules.cs`: tiers, defaults and labels.
- `Chaos/Collections/GuildRank.cs`: `Permissions`, `SetPermission`.
- `Chaos/Collections/Guild.cs`: `HasPermission`, `SetRankPermission`, `GetBankPermissions`.
- `Chaos/Collections/BankPermissions.cs`: `CanWithdrawItems`, `CanWithdrawGold`.
- `Chaos.Schemas/Guilds/GuildRankSchema.cs`: `Permissions`.
- `Chaos/Services/MapperProfiles/GuildMapperProfile.cs`: mapping and defaults.
- `Chaos/Services/Servers/WorldServer.cs`: separate item and gold withdraw checks and messages.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBuffScript.cs`: switch check and option-text mapping.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildUpdateHallScript.cs`: switch check and the house deed leader check.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildMemberManagementScript.cs`, `GuildMemberAdmitScript.cs`, `GuildMemberKickScript.cs`, `GuildMemberPromoteScript.cs`, `GuildMemberDemoteScript.cs`: switch checks.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs`: Permissions option.
- New `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildPermissionsScript.cs`: the leader's menu.

Data (`Unora/Data/Configuration/Templates/Dialogs/Temauir/generic/Guild/GuildPermissions/`):

- New `generic_guild_permissions_initial.json` and `generic_guild_permissions_rank.json`.

## Testing

Unit tests in `Chaos.Tests`, next to `GuildTests`, `GuildRankTests`, `BankPermissionsTests` and `GuildAllowances/`:

- **Old files:** a tier file with no permissions loads as today's rules for each tier.
- **Round trip:** permissions save and load back as names, such as `"WithdrawItems, Admit"`.
- **`HasPermission`:** true for the leader, false for a non-member, and the rank's switch otherwise.
- **`SetRankPermission`:** refuses tier 0.
- **Rules table:** seven switches for Council, six for Member, five for Applicant, in a fixed order.
- **Bank:** items on and gold off allows items only, and the reverse. Each refusal has its own message. Existing bank and `ComplexActionHelper` tests are updated for the renamed field.
- **Buffs:** the menu shows with the switch on, the leader turns it off, and the member clicks "Pay from Guild Bank". The payment is refused, and neither the member's gold nor the guild's changes.
- **Hall:** a member with `BuyHallRooms` buys a room; one without it is refused; a non-leader can't buy the house deed even with the switch.
- **Members:** each option shows only with its switch; kick is refused without it; a Member with `Kick` can kick an Applicant but not a Member.
- **Menu:** a leader's click flips the switch and saves; a non-leader's click changes nothing; the Member menu has no promote/demote line and the Applicant menu has no kick line; only the leader gets Permissions, at Quill and at Aricin.

Two `Chaos.Tests` failures already exist on master (GiveAbility and OnItemDroppedOn stackable). They're not caused by this work.

In-game check after deploy: the leader turns off Council's gold withdrawal and a Council member is refused; the leader turns on Member's item withdrawal and a Member takes a potion; Aricin shows Permissions to the leader only.

## Out of scope

- A client window for permissions (approach B).
- Guild board permissions.
- Limits on how many items a rank may withdraw per week. That's a separate idea that could build on these switches.
- More than four ranks, or permissions for individual members.
- A guild chat message when a switch changes.
