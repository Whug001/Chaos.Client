namespace Chaos.Client.Models;

/// <summary>
///     The per-item stat modifiers an item can carry, in the canonical order shared by the market's search filter (the
///     wire <c>MarketStat</c> enum indexes this order) and every detail pane. Each field is the bonus granted by the
///     item; zero means "not present" and is omitted from display. Wire entries (market listings, bank items) map onto
///     this at the call site — it is a display projection, not a protocol type.
/// </summary>
public readonly record struct ItemStats(
    int Hp,
    int Mp,
    int Str,
    int Int,
    int Wis,
    int Con,
    int Dex,
    int Ac,
    int Hit,
    int Dmg,
    int AtkSpeed,
    int FlatSkillDmg,
    int FlatSpellDmg,
    int SkillDmgPct,
    int SpellDmgPct,
    int Cdr,
    int HealBonus,
    int HealBonusPct,
    int MagicResist)
{
    /// <summary>
    ///     Projects the non-zero stats to inline display blocks like "+10 HP" / "-50 MP" / "+5% Cooldown", in the
    ///     canonical order. Detail panes lay these out as wrapped inline text, keeping each block whole.
    /// </summary>
    public IReadOnlyList<string> ToBlocks()
    {
        var blocks = new List<string>(19);

        //per-stat color via inline legend codes: positive = good (Lime {=q), negative = bad (Scarlet {=b). AC is
        //inverted (lower/negative AC is better in Dark Ages), so it swaps which sign is "good".
        void Add(string label, int value, bool percent = false, bool invertColor = false)
        {
            if (value == 0)
                return;

            var sign = value > 0 ? "+" : ""; //negatives carry their own '-'
            var good = (value > 0) != invertColor;
            var code = good ? "{=q" : "{=b"; //q = Lime, b = Scarlet (LegendPalette text-color codes)
            blocks.Add($"{code}{sign}{value}{(percent ? "%" : "")} {label}");
        }

        Add("HP", Hp);
        Add("MP", Mp);
        Add("STR", Str);
        Add("INT", Int);
        Add("WIS", Wis);
        Add("CON", Con);
        Add("DEX", Dex);
        Add("AC", Ac, invertColor: true);
        Add("Hit", Hit);
        Add("Dmg", Dmg);
        Add("Atk Speed", AtkSpeed, true); //the wire field is a percentage on both the market and bank sides
        Add("Skill Dmg", FlatSkillDmg);
        Add("Spell Dmg", FlatSpellDmg);
        Add("Skill Dmg %", SkillDmgPct, true);
        Add("Spell Dmg %", SpellDmgPct, true);
        Add("Cooldown", Cdr, true);
        Add("Heal Bonus", HealBonus);
        Add("Heal Bonus %", HealBonusPct, true);
        Add("Magic Resist", MagicResist);

        return blocks;
    }
}
