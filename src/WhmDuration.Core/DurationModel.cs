namespace WhmDuration.Core;

public enum EffectTarget { Enemy, FriendlyOrSelf, Self }
public enum TargetKind { None, Hostile, Friendly }
public enum TimerKind { TargetStatus, SelfStatus, GroundRecast }
public readonly record struct StatusSnapshot(uint StatusId, uint SourceId, float RemainingSeconds);
public sealed record EffectDefinition(uint ActionId, uint StatusId, float DurationSeconds, EffectTarget Target, TimerKind Kind = TimerKind.TargetStatus);
public readonly record struct CooldownVisual(int ProgressPercent, int Seconds);

public static class DurationModel
{
    private static readonly Dictionary<uint, EffectDefinition> Effects = new()
    {
        // White Mage
        [121] = new(121, 143, 30, EffectTarget.Enemy),
        [132] = new(132, 144, 30, EffectTarget.Enemy),
        [16532] = new(16532, 1871, 30, EffectTarget.Enemy),
        [137] = new(137, 158, 18, EffectTarget.FriendlyOrSelf),
        [133] = new(133, 150, 15, EffectTarget.Self, TimerKind.SelfStatus),
        [37010] = new(37010, 3880, 15, EffectTarget.Self, TimerKind.SelfStatus),
        [3569] = new(3569, 0, 24, EffectTarget.Self, TimerKind.GroundRecast),
        [25862] = new(25862, 0, 20, EffectTarget.Self, TimerKind.GroundRecast),

        // Scholar
        [17864] = new(17864, 179, 30, EffectTarget.Enemy),
        [17865] = new(17865, 189, 30, EffectTarget.Enemy),
        [16540] = new(16540, 1895, 30, EffectTarget.Enemy),
        [37012] = new(37012, 3882, 15, EffectTarget.Enemy),
        [188] = new(188, 0, 15, EffectTarget.Self, TimerKind.GroundRecast),
        [803] = new(803, 1943, 21, EffectTarget.Self, TimerKind.SelfStatus),
        [16537] = new(16537, 1943, 21, EffectTarget.Self, TimerKind.SelfStatus),

        // Astrologian
        [3599] = new(3599, 843, 30, EffectTarget.Enemy),
        [3608] = new(3608, 844, 30, EffectTarget.Enemy),
        [16554] = new(16554, 1881, 30, EffectTarget.Enemy),
        [3595] = new(3595, 835, 15, EffectTarget.FriendlyOrSelf),
        [3601] = new(3601, 836, 15, EffectTarget.Self, TimerKind.SelfStatus),
        [37030] = new(37030, 3888, 15, EffectTarget.Self, TimerKind.SelfStatus),
        [16553] = new(16553, 1879, 15, EffectTarget.Self, TimerKind.SelfStatus),
        [16556] = new(16556, 1880, 15, EffectTarget.FriendlyOrSelf),
        [3613] = new(3613, 847, 15, EffectTarget.Self, TimerKind.SelfStatus),
        [7439] = new(7439, 0, 20, EffectTarget.Self, TimerKind.GroundRecast),

        // Sage
        [24293] = new(24293, 2614, 30, EffectTarget.Enemy),
        [24308] = new(24308, 2615, 30, EffectTarget.Enemy),
        [24314] = new(24314, 2616, 30, EffectTarget.Enemy),
        [37032] = new(37032, 3894, 30, EffectTarget.Enemy),
        [24288] = new(24288, 2620, 15, EffectTarget.Self, TimerKind.SelfStatus),
        [24302] = new(24302, 2682, 15, EffectTarget.Self, TimerKind.SelfStatus),
        [24298] = new(24298, 2936, 15, EffectTarget.Self, TimerKind.SelfStatus),

        // Paladin
        [23] = new(23, 248, 15, EffectTarget.Enemy),
        [25750] = new(25750, 2721, 21, EffectTarget.Enemy),

        // Warrior
        [3552] = new(3552, 2681, 15, EffectTarget.Self, TimerKind.SelfStatus),

        // Dark Knight
        [3639] = new(3639, 0, 15, EffectTarget.Self, TimerKind.GroundRecast),

        // Gunbreaker
        [16153] = new(16153, 1839, 30, EffectTarget.Enemy),
        [16159] = new(16159, 1838, 15, EffectTarget.Enemy),
        [16151] = new(16151, 1835, 18, EffectTarget.FriendlyOrSelf),

        // Dragoon
        [88] = new(88, 118, 24, EffectTarget.Enemy),
        [25772] = new(25772, 2714, 24, EffectTarget.Enemy),

        // Ninja
        [2270] = new(2270, 501, 18, EffectTarget.Self, TimerKind.SelfStatus),

        // Samurai
        [7489] = new(7489, 1228, 60, EffectTarget.Enemy),

        // Bard
        [100] = new(100, 129, 30, EffectTarget.Enemy),
        [113] = new(113, 124, 30, EffectTarget.Enemy),
        [7406] = new(7406, 1200, 45, EffectTarget.Enemy),
        [7407] = new(7407, 1201, 45, EffectTarget.Enemy),

        // Machinist
        [16499] = new(16499, 1860, 15, EffectTarget.Enemy),

        // Black Mage
        [144] = new(144, 161, 30, EffectTarget.Enemy),
        [7447] = new(7447, 162, 24, EffectTarget.Enemy),
        [153] = new(153, 163, 30, EffectTarget.Enemy),
        [7420] = new(7420, 1210, 24, EffectTarget.Enemy),
        [36986] = new(36986, 3871, 30, EffectTarget.Enemy),
        [36987] = new(36987, 3872, 24, EffectTarget.Enemy),

        // Summoner
        [25823] = new(25823, 2706, 15, EffectTarget.Self, TimerKind.SelfStatus),

        // Blue Mage
        [11386] = new(11386, 1714, 30, EffectTarget.Enemy),
        [11390] = new(11390, 1718, 12, EffectTarget.Enemy),
        [23290] = new(23290, 2496, 60, EffectTarget.Enemy),
        [34567] = new(34567, 3712, 60, EffectTarget.Enemy),
    };

    public static EffectDefinition? Find(uint actionId) => Effects.GetValueOrDefault(actionId);

    public static float SelectRemaining(EffectDefinition effect, uint localSource, IEnumerable<StatusSnapshot> statuses)
    {
        if (localSource is 0 or 0xE0000000 || !float.IsFinite(effect.DurationSeconds) || effect.DurationSeconds <= 0) return 0;
        float remaining = 0;
        foreach (var status in statuses)
            if (status.StatusId == effect.StatusId && status.SourceId == localSource && float.IsFinite(status.RemainingSeconds))
                remaining = Math.Max(remaining, status.RemainingSeconds);
        return Math.Clamp(remaining, 0, effect.DurationSeconds);
    }

    public static CooldownVisual? GetVisual(float remaining, float duration, bool realCooldown)
    {
        if (realCooldown || !float.IsFinite(remaining) || !float.IsFinite(duration) || remaining <= 0 || duration <= 0 || duration > 3600) return null;
        remaining = Math.Min(remaining, duration);
        return new((int)Math.Clamp(100 * ((duration - (double)remaining) / duration), 0, 99), (int)Math.Ceiling(remaining));
    }

    public static CooldownVisual? GetGroundVisual(float elapsed, float duration, float totalRecast)
    {
        if (!float.IsFinite(elapsed) || !float.IsFinite(duration) || duration <= 0 || elapsed < 0) return null;
        if (elapsed >= duration) return null; // Duration phase completed, let real cooldown display
        return new((int)Math.Clamp(100 * ((double)elapsed / duration), 0, 99), (int)Math.Ceiling(duration - elapsed));
    }

    public static uint SelectTarget(EffectTarget mode, uint self, uint target, TargetKind kind) => mode switch
    {
        EffectTarget.Enemy => kind == TargetKind.Hostile ? target : 0,
        EffectTarget.FriendlyOrSelf => kind == TargetKind.Friendly && target != 0 ? target : self,
        EffectTarget.Self => self,
        _ => 0,
    };
}
