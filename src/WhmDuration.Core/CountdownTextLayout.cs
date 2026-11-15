namespace WhmDuration.Core;

public readonly record struct CountdownTextStyle(float OffsetX, float OffsetY, float Size)
{
    public static CountdownTextStyle Default => new(0, 0, 1);
    public CountdownTextStyle Normalize() => new(
        float.IsFinite(OffsetX) ? Math.Clamp(OffsetX, -100, 100) : 0,
        float.IsFinite(OffsetY) ? Math.Clamp(OffsetY, -100, 100) : 0,
        float.IsFinite(Size) ? Math.Clamp(Size, 0.5f, 3) : 1);
}

public readonly record struct TextNodeLayout(float X, float Y, float ScaleX, float ScaleY);
public readonly record struct TextSlotKey(ushort AddonId, int SlotIndex);

public static class CountdownTextLayout
{
    public static TextNodeLayout Apply(TextNodeLayout baseline, CountdownTextStyle style)
    {
        style=style.Normalize();
        return new(baseline.X+style.OffsetX, baseline.Y+style.OffsetY,
            baseline.ScaleX*style.Size, baseline.ScaleY*style.Size);
    }
}

/// <summary>Stores original layout values and node identity, never a pointer to dereference later.</summary>
public sealed class CountdownLayoutTracker
{
    private readonly Dictionary<TextSlotKey, (nuint Identity, TextNodeLayout Layout)> originals=[];

    public void Remember(TextSlotKey key, nuint identity, TextNodeLayout baseline)
    {
        if(identity==0 || key.SlotIndex<0) return;
        if(!originals.TryGetValue(key,out var previous) || previous.Identity!=identity)
            originals[key]=(identity,baseline);
    }

    public TextNodeLayout? Take(TextSlotKey key, nuint currentIdentity)
    {
        if(!originals.Remove(key,out var previous) || previous.Identity!=currentIdentity) return null;
        return previous.Layout;
    }

    public void ForgetAddon(ushort addonId)
    {
        foreach(var key in originals.Keys.Where(k=>k.AddonId==addonId).ToArray()) originals.Remove(key);
    }
}
