using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace WhmDuration.Plugin;

public sealed class Plugin : IDalamudPlugin
{
    private readonly ICommandManager commands;
    private readonly IChatGui chat;
    private readonly IPluginLog log;
    private readonly HotbarCooldowns hotbar;
    private readonly NativeTooltips tooltips;
    private readonly GameEffects effects;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly PluginConfig config;
    private readonly CountdownConfigWindow configWindow;
    private bool enabled=true;

    public Plugin(ICommandManager commands, IChatGui chat, IObjectTable objects, ITargetManager targets,
        IClientState client, IGameInteropProvider interop, IAddonLifecycle lifecycle, IGameGui gui,
        IPluginLog log, ISigScanner scanner, ISeStringEvaluator evaluator, IDalamudPluginInterface pluginInterface)
    {
        this.commands=commands;this.chat=chat;this.log=log;
        this.pluginInterface=pluginInterface;
        try { config=pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig(); }
        catch(Exception ex)
        {
            log.Warning(ex,"WHM Duration: unreadable settings, using defaults.");
            config=new PluginConfig();
        }
        config.SetStyle(config.Style);
        configWindow=new(config,()=>pluginInterface.SavePluginConfig(config));
        effects=new GameEffects(objects,targets,client);
        hotbar=new(interop,lifecycle,gui,log,effects,()=>enabled,()=>config.Style);
        try { tooltips=new(interop,scanner,gui,client,log,()=>enabled,effects,evaluator); }
        catch { hotbar.Dispose();throw; }
        var cmdInfo = new CommandInfo(OnCommand)
        {
            HelpMessage = "Action Duration: /duration [on|off|status|diag|config]. No argument toggles.",
            ShowInHelp = true,
        };
        commands.AddHandler("/duration", cmdInfo);
        commands.AddHandler("/actionduration", cmdInfo);
        commands.AddHandler("/whmduration", cmdInfo);
        pluginInterface.UiBuilder.Draw += configWindow.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += configWindow.Open;
        pluginInterface.UiBuilder.OpenMainUi += configWindow.Open;
        log.Information("Action Duration: native adapters loaded.");
    }

    private void OnCommand(string command, string args)
    {
        switch(args.Trim().ToLowerInvariant())
        {
            case "": enabled=!enabled;break;
            case "on":enabled=true;break;
            case "off":enabled=false;break;
            case "status":break;
            case "config":configWindow.Open();return;
            case "diag":
                foreach(var line in effects.Diagnostics().Concat(hotbar.Diagnostics()))
                {
                    chat.Print(line);
                    log.Information("{Diagnostic}",line);
                }
                break;
            default:chat.Print("Action Duration: /duration [on|off|status|diag|config]");return;
        }
        if(!enabled){hotbar.Restore();tooltips.Restore();}
        var status=$"Action Duration: {(enabled ? "on" : "off")}; hotbar fault={hotbar.Faulted}, tooltip fault={tooltips.Faulted}; native renders={hotbar.Draws}, overlays={hotbar.Overlays}, tooltip additions={tooltips.Appended}.";
        chat.Print(status);
        log.Information("{Status}",status);
    }

    public void Dispose()
    {
        commands.RemoveHandler("/duration");
        commands.RemoveHandler("/actionduration");
        commands.RemoveHandler("/whmduration");
        pluginInterface.UiBuilder.Draw -= configWindow.Draw;
        pluginInterface.UiBuilder.OpenConfigUi -= configWindow.Open;
        pluginInterface.UiBuilder.OpenMainUi -= configWindow.Open;
        enabled=false;
        tooltips.Dispose();
        hotbar.Dispose();
    }
}
