using Dalamud.Configuration;
using Newtonsoft.Json;
using WhmDuration.Core;

namespace WhmDuration.Plugin;

public sealed class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public float TextOffsetX { get; set; }
    public float TextOffsetY { get; set; }
    public float TextSize { get; set; } = 1;

    [JsonIgnore]
    public CountdownTextStyle Style => new CountdownTextStyle(TextOffsetX,TextOffsetY,TextSize).Normalize();

    public void SetStyle(CountdownTextStyle style)
    {
        style=style.Normalize();
        TextOffsetX=style.OffsetX;
        TextOffsetY=style.OffsetY;
        TextSize=style.Size;
    }
}
