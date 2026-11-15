namespace WhmDuration.Core;

/// <summary>Scoped substitution of native UI rendering inputs, never game cooldown state.</summary>
public static unsafe class UiCooldownOverride
{
    // Standard addons report the cross flag as true on the observed client.
    // Exact addon identity is authoritative; the native flag is advisory only.
    public static bool SupportsAddon(string? name, bool reportedCrossHotbar) =>
        name is "_ActionBar" or "_ActionBar01" or "_ActionBar02" or "_ActionBar03" or "_ActionBar04" or
            "_ActionBar05" or "_ActionBar06" or "_ActionBar07" or "_ActionBar08" or "_ActionBar09";

    public static bool Invoke(int* values, int length, int index, CooldownVisual visual, Action render)
    {
        const int slotSize = 17;
        // The native UI SlotType.Action value is 48, NOT ActionType.Action (1).
        if (values == null || index < 0 || length < slotSize || index > length - slotSize ||
            values[index] != 48 || DurationModel.Find(unchecked((uint)values[index + 3])) == null ||
            visual.ProgressPercent is < 0 or > 99 || visual.Seconds is < 1 or > 3600)
        {
            render();
            return false;
        }
        int* slot = values + index;
        int oldTextMode = slot[2], oldSweepMode = slot[7], oldProgress = slot[8],
            oldChargeProgress = slot[9], oldSeconds = slot[10], oldShowText = slot[12];
        try
        {
            // Verified against the installed game renderer: mode 3 prints integer timer
            // from index 10, index 12 shows it; mode 0 uses the normal single-charge sweep.
            // Metadata currently calls index 7 MaxCharges, but its consumer switches on
            // cooldown display modes 0/1/2/3, NOT a count of charges.
            slot[2] = 3;
            slot[7] = 0;
            slot[8] = Math.Max(1, visual.ProgressPercent);
            slot[9] = 100;
            slot[10] = visual.Seconds;
            slot[12] = 1;
            render();
            return true;
        }
        finally
        {
            slot[2] = oldTextMode;
            slot[7] = oldSweepMode;
            slot[8] = oldProgress;
            slot[9] = oldChargeProgress;
            slot[10] = oldSeconds;
            slot[12] = oldShowText;
        }
    }
}
