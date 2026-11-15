using System.Numerics;
using Dalamud.Bindings.ImGui;
using WhmDuration.Core;

namespace WhmDuration.Plugin;

internal sealed class CountdownConfigWindow(PluginConfig config, Action save)
{
    private bool open;
    public void Open() => open=true;

    public void Draw()
    {
        if(!open) return;
        ImGui.SetNextWindowSize(new Vector2(420,230),ImGuiCond.FirstUseEver);
        bool visible=ImGui.Begin("Action Duration settings",ref open,ImGuiWindowFlags.NoCollapse);
        try
        {
            if(!visible) return;
            var style=config.Style;
            float x=style.OffsetX,y=style.OffsetY,size=style.Size*100;
            bool changed=ImGui.SliderFloat("Text size (%)",ref size,50,300,"%.0f%%");
            changed |= ImGui.SliderFloat("Horizontal offset",ref x,-100,100,"%.0f px");
            changed |= ImGui.SliderFloat("Vertical offset",ref y,-100,100,"%.0f px");
            if(changed)
            {
                config.SetStyle(new(x,y,size/100));
                save();
            }
            if(ImGui.Button("Reset to defaults"))
            {
                config.SetStyle(CountdownTextStyle.Default);
                save();
            }
            ImGui.Separator();
            ImGui.TextWrapped("Changes apply live to active DoT/HoT countdowns. Offsets are relative to the game's text position; 100% keeps the normal text size.");
            ImGui.TextDisabled("Settings are saved automatically.");
        }
        finally { ImGui.End(); }
    }
}
