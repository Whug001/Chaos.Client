# Guild tuition: guild-paid training and repairs

Designed 2026-09-25. Idea 32 in `Unora/docs/guild-hall-ideas.md`.

## Goal

Guild members can pay for skill training, spell training and repairs with guild bank gold. Each rank gets two weekly allowances set by the leader: one for training and one for repairs. A dev promised guild-funded training in the "Guild tax rates opt in/out" player thread. Taxes fill the guild bank, and this gives leaders a way to spend it on members.

## Decisions

| Question | Decision |
|---|---|
| How spending is limited | A weekly gold allowance per member, set per rank. |
| Training and repairs | Two separate allowances per rank. An allowance of 0 means "Off". |
| Who chooses the payer | The member chooses each time: pay yourself, or use guild funds. |
| Where repairs count | Every smith that uses the generic repair dialogs, including Fixx in the hall. |
| Allowance smaller than cost | Split the cost. The guild pays what's left of the allowance and the member pays the rest. The prompt shows the split before the member agrees. |
| Storage | Allowances are saved on each rank. Weekly spending is saved on the guild (approach A). |
| Week start | Sunday 20:00 UTC, the same moment as the casino lottery draw. |

## 1. Leader setup at Quill

- `GuildManagementScript` adds an **Allowances** option at Quill for every guild member.
- **Leader view:** pick a rank, then Training or Repairs, then an amount from a fixed list: Off, 100,000, 250,000, 500,000, 1,000,000, 2,500,000 or 5,000,000 per week. There's no typed input, which matches the tax menu.
- **Leader weekly report:** the leader can also list each member's training and repair spending for the current week.
- **Member view:** members see their rank's two allowances and what they have left this week. Only the leader can edit.
- Every rank starts at Off, so nothing changes for a guild until its leader turns an allowance on.
- The leader's own rank (tier 0) has allowances like every other rank.
- A change takes effect right away. Spending already recorded this week still counts against the new amount.

## 2. Member flow at trainers and smiths

The guild-funds option appears only when all of these are true:

- The member is in a guild.
- The member's rank has more than 0 left of the relevant allowance this week.
- The guild bank holds more than 0 gold.
- The action costs more than 0 gold.

Otherwise the dialogs behave exactly as they do today.

### Training (skills and spells)

- `generic_learnskill_showrequirements` and `generic_learnspell_showrequirements` show "Yes" (pay yourself) and "No" today. When guild funds apply, the script adds a third option, **"Use guild funds"**.
- That option opens a new confirmation dialog that states the split. Examples:
  - "Your guild will cover all 150,000."
  - "Your guild will cover 200,000 of the 500,000. You'll pay the other 300,000. That uses the rest of your training allowance this week."
  - When the bank is the limit: "The guild bank only has 50,000. Your guild will cover 50,000 and you'll pay the other 450,000."
- "Yes" on the confirmation dialog goes to the accepted step with a guild-funded flag. "No" returns to the trainer's first menu.
- Item requirements always come from the member's inventory. Only the gold part is split.
- The accepted step works out the split again. If the guild share no longer matches the one shown, nothing is charged. This happens when another member spent from the bank in between. The member sees "Your guild's funds changed. Please try again."

### Repairs

- `generic_repairAllItemInitial` and `generic_repairSingleItemConfirmation` get the same **"Use guild funds"** option and split confirmation.
- The price is the one the smith already charges, including the 10% discount at Fixx. The guild's share is part of that discounted price.
- Saying "Repair All" out loud (`VerbalRepairAllScript`) still uses only the member's gold. That path has no dialog step to show the split and let the member confirm it.

### Payment rules

- The guild's share is the smallest of three amounts: the cost, the member's remaining allowance and the guild bank's gold. The member's share is the rest of the cost.
- Nothing is taken unless both shares can be paid. If the member can't afford their share, the smith or trainer says so and neither side is charged.
- Spending is recorded in the weekly ledger only after both payments succeed.

## 3. Storage and the weekly reset

### Rank allowances

- `GuildRankSchema` gets `TrainingAllowance` and `RepairAllowance` (gold per week, default 0).
- `GuildRank` gets matching properties and a setter. `GuildMapperProfile` maps them both ways.
- Rank files saved before this change load with both allowances at 0.
- An allowance belongs to the rank tier, so renaming a rank keeps it.

### Weekly ledger

- `GuildSchema` gets:
  - `AllowanceWeekStart`: the UTC start of the week the ledger covers.
  - `AllowanceSpending`: member name to `{ Training, Repairs }` gold spent. Name lookups ignore case.
- `Guild` holds the ledger and reads and writes it under its existing `Sync` lock.
- **Reset:** no timer runs. Before any ledger read or write, the guild compares `AllowanceWeekStart` with the start of the current week. When a new week has started, it clears the ledger and stores the new week start.
- **Week start:** the most recent Sunday 20:00 UTC at or before now. It lives in its own small helper. The lottery's scheduling code isn't changed.
- A member's entry stays when they leave or are kicked, so rejoining in the same week doesn't give them a fresh allowance.
- On promotion or demotion, spending carries over and the new rank's allowance applies.
- Disbanding a guild removes the ledger with the guild files.

### Shared payment helper

Training and repair scripts both call one helper on `Guild`. Its shape:

- `GetAllowanceStatus(memberName, kind)`: returns the allowance, the amount spent and the amount left. `kind` is `Training` or `Repairs`.
- `QuoteSplit(memberName, kind, cost)`: returns the guild share, the member share, and whether the bank or the allowance limited the guild share.
- `TryPayWithAllowance(aisling, kind, cost, expectedGuildShare, description)`: runs the payment rules above. It takes the guild share from the bank first, then the member share with `TryTakeGold`. If the member share fails, it returns the guild share to the bank. It returns a result the calling script turns into a message.

All three run under the guild's lock, so two members paying at once can't overdraw the bank or the allowance. No code that locks the guild may run inside the helper's call to `TryTakeGold`.

## 4. Logs

- `GuildHouseState.TransactionType` gets a new value, `AllowanceSpend`, added after the existing values.
- Each guild-paid purchase adds a log line with:
  - the member's name;
  - a description, such as "Training: Ambush", "Training: Srad", "Repairs: all items" or "Repairs: Gold Sword";
  - the guild's share as the amount.
- `GuildBankManagementScript` adds **Allowance Spending** to its View Logs menu. It shows the newest 30 lines, like the other logs.
- Each purchase is also written to the server log with guild, member, kind, cost, guild share, member share, and bank gold before and after. This follows the logging in `GuildBuffScript`.

## Files

Server (`Chaos.Client/Chaos-Server`):

- `Chaos/Collections/Guild.cs`: ledger, week start and payment helper.
- `Chaos/Collections/GuildRank.cs`: allowance properties.
- `Chaos.Schemas/Guilds/GuildSchema.cs` and `GuildRankSchema.cs`: new fields.
- `Chaos/Services/MapperProfiles/GuildMapperProfile.cs`: mapping.
- `Chaos/Models/World/GuildHouseState.cs`: `AllowanceSpend`.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildManagementScript.cs`: Allowances option.
- New `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildAllowanceScript.cs`: Quill menus.
- `Chaos/Scripting/DialogScripts/Temuair/GuildScripts/GuildBankManagementScript.cs`: log view.
- `Chaos/Scripting/DialogScripts/Temuair/TrainerScripts/LearnSkillScript.cs` and `LearnSpellScript.cs`: guild-funds option and payment.
- `Chaos/Scripting/DialogScripts/Temuair/Generic/RepairAllItemsScript.cs` and `RepairSingleItemScript.cs`: guild-funds option and payment.

Data (`Unora/Data/Configuration/Templates/Dialogs/Temauir/`):

- New `generic/Guild/GuildAllowanceManagement/` dialogs for the Quill menus.
- New guild-funds confirmation dialogs in `generic/LearnSkill/`, `generic/LearnSpell/` and `generic/RepairItems/`, plus the extra option on the existing dialogs named above.
- A new `stash_guildbankallowancelog` dialog in `Guild Hall/Stash/`, linked from `stash_guildbanklogs`.

## Testing

Unit tests in `Chaos.Tests`, next to `GuildTests` and `Trainers/LearnSpellScriptTests`:

- **Split math:** the allowance, the bank or the cost limits the guild share; a cost of 0; an allowance set to Off.
- **Weekly reset:** a ledger written just before Sunday 20:00 UTC is cleared just after it, and not before.
- **Membership:** leaving and rejoining keeps the week's spending; promotion applies the new rank's allowance to the same spending.
- **Rollback:** a member who can't afford their share is charged nothing, and the guild bank and ledger are unchanged.
- **Stale quote:** the accepted step charges nothing when the guild share changed after the confirmation.
- **Old files:** guild and rank schemas without the new fields load with allowances at 0 and an empty ledger.
- **Trainer flow:** the "Use guild funds" option appears only when the four conditions hold, and a split charges both sides correctly.

Two `Chaos.Tests` failures already exist on master (GiveAbility and OnItemDroppedOn stackable). They're not caused by this work.

In-game check after the unit tests: set allowances at Quill, learn a skill with a split, repair at a town smith and at Fixx, then read the Allowance Spending log.

## Out of scope

- Guild funds for the spoken "Repair All" command.
- Paying item requirements from the guild bank.
- Typed allowance amounts, or different allowances for individual members.
