namespace WhmDuration.Core;

public sealed record PotencyBreakdown(int Initial, int Tick, int TickCount, int Duration, bool Healing, bool PerRecipient)
{
    public int Periodic => Tick * TickCount;
    public int Total => Initial + Periodic;
}

public static class PotencyTooltip
{
    public const string Marker = "[Action Duration]";
    public const string LegacyMarker = "[WHM Duration]";
    private static readonly System.Text.RegularExpressions.Regex InitialDamage = new(@"(?:Deals [^\r\n]+ damage with a potency of|Delivers an attack with a potency of)\s*(\d+)", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex Potencies = new(@"(?:\A|\n)(?:Cure\s+)?Potency:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex Durations = new(@"(?:Duration:\s*|Duration\s*)(\d+)s", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static PotencyBreakdown? Parse(uint actionId, string description)
    {
        var effect = DurationModel.Find(actionId);
        if (effect == null || string.IsNullOrEmpty(description) || description.Length > 8192) return null;
        if (!description.Contains("damage", StringComparison.OrdinalIgnoreCase) &&
            !description.Contains("attack", StringComparison.OrdinalIgnoreCase) &&
            !description.Contains("heal", StringComparison.OrdinalIgnoreCase) &&
            !description.Contains("cure", StringComparison.OrdinalIgnoreCase) &&
            !description.Contains("succor", StringComparison.OrdinalIgnoreCase) &&
            !description.Contains("regen", StringComparison.OrdinalIgnoreCase)) return null;
        int duration = (int)effect.DurationSeconds;
        var durMatches = Durations.Matches(description);
        if (!durMatches.Any(m => int.TryParse(m.Groups[1].Value, out var d) && d == duration)) return null;

        var matches = Potencies.Matches(description);
        int initial = 0, tick = 0;
        bool healing = effect.Target != EffectTarget.Enemy;
        bool perRecipient = healing && (effect.Target == EffectTarget.Self ||
                            description.Contains("all nearby", StringComparison.OrdinalIgnoreCase) ||
                            description.Contains("who enter", StringComparison.OrdinalIgnoreCase) ||
                            description.Contains("nearby party members", StringComparison.OrdinalIgnoreCase));

        if (!healing)
        {
            var first = InitialDamage.Match(description);
            if (first.Success) int.TryParse(first.Groups[1].Value, out initial);
            if (matches.Count == 0 || !int.TryParse(matches[^1].Groups[1].Value, out tick)) return null;
        }
        else
        {
            if (matches.Count == 1)
            {
                initial = 0;
                if (!int.TryParse(matches[0].Groups[1].Value, out tick)) return null;
            }
            else if (matches.Count >= 2)
            {
                if (!int.TryParse(matches[0].Groups[1].Value, out initial) ||
                    !int.TryParse(matches[1].Groups[1].Value, out tick)) return null;
            }
            else return null;
        }

        if (initial < 0 || initial > 100000 || tick <= 0 || tick > 100000) return null;
        return new(initial, tick, duration / 3, duration, healing, perRecipient);
    }

    public static string Append(uint actionId, string description)
    {
        if (description.Contains(Marker, StringComparison.Ordinal) || description.Contains(LegacyMarker, StringComparison.Ordinal)) return description;
        var potency = Parse(actionId, description);
        if (potency == null) return description;
        string type = potency.Healing ? "healing" : "damage";
        string recipient = potency.PerRecipient ? " per recipient" : "";
        return description + $"\n\n{Marker}\nFull-duration {type} potency{recipient}: {potency.Total}\nInitial: {potency.Initial}; periodic: {potency.Periodic} ({potency.TickCount} × {potency.Tick}, every 3s).\nAssumes full duration ({potency.Duration}s). Not guaranteed HP {type}.";
    }
}
