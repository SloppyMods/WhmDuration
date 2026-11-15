using Dalamud.Game;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Enums;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using WhmDuration.Core;
using Lumina.Text.ReadOnly;

namespace WhmDuration.Plugin;

internal sealed unsafe class NativeTooltips : IDisposable
{
    // ActionDetail.OnRequestedUpdate's native description renderer. Resolve each load.
    private const string Signature="E8 ?? ?? ?? ?? 48 8B 43 28 48 8B AF";
    private delegate void* GenerateTooltip(AtkUnitBase* addon, NumberArrayData* numbers, StringArrayData* strings);
    private readonly Hook<GenerateTooltip> hook;
    private readonly IGameGui gui;
    private readonly IClientState client;
    private readonly IPluginLog log;
    private readonly Func<bool> enabled;
    private readonly GameEffects effects;
    private readonly ISeStringEvaluator evaluator;
    private bool faulted;
    public bool Faulted=>faulted;
    public long Appended {get;private set;}

    public NativeTooltips(IGameInteropProvider interop, ISigScanner scanner, IGameGui gui,
        IClientState client, IPluginLog log, Func<bool> enabled, GameEffects effects, ISeStringEvaluator evaluator)
    {
        this.gui=gui; this.client=client; this.log=log; this.enabled=enabled; this.effects=effects;
        this.evaluator=evaluator;
        hook=interop.HookFromAddress<GenerateTooltip>(scanner.ScanText(Signature),Detour);
        hook.Enable();
    }

    private void* Detour(AtkUnitBase* addon, NumberArrayData* numbers, StringArrayData* strings)
    {
        byte[]? original=null;
        byte[]? replacement=null;
        try
        {
            var agent=AgentActionDetail.Instance();
            if (!faulted && enabled() && effects.Available && client.ClientLanguage==ClientLanguage.English &&
                agent!=null && agent->ActionKind==DetailKind.Action && !agent->IsLovmActionDetail &&
                addon!=null && addon->NameString=="ActionDetail" && strings!=null && strings->StringArray!=null && strings->Size>13)
            {
                var ptr=strings->StringArray[13];
                if(ptr.HasValue)
                {
                    var description=MemoryHelper.ReadSeStringNullTerminated((nint)ptr.Value);
                    uint action=agent->AdjustedId!=0 ? agent->AdjustedId : agent->ActionId;
                    // Installed client descriptions contain global-parameter macros for
                    // Dia/Regen/Medica II potency. Evaluate for parsing only; preserve the
                    // original payloads when rendering. Already-evaluated text is unchanged.
                    var evaluated=evaluator.Evaluate(new ReadOnlySeString(description.Encode())).ExtractText();
                    var augmented=PotencyTooltip.Append(action,evaluated);
                    if(augmented!=evaluated)
                    {
                        original=description.EncodeWithNullTerminator();
                        var suffix=augmented[evaluated.Length..];
                        replacement=new SeStringBuilder().Append(description).AddText(suffix).Build().EncodeWithNullTerminator();
                    }
                }
            }
        }
        catch(Exception ex){Fault(ex);}
        if(original==null || replacement==null) return hook.Original(addon,numbers,strings);
        try
        {
            // Copy into game-owned string storage, never leave a pinned managed pointer behind.
            strings->SetValue(13,replacement,false,true,true);
            Appended++;
            return hook.Original(addon,numbers,strings);
        }
        finally { strings->SetValue(13,original,false,true,true); }
    }

    private void Fault(Exception ex)
    {
        if(!faulted) log.Error(ex,"WHM Duration tooltip adapter disabled after a fault.");
        faulted=true;
    }

    public void Restore()
    {
        var agent=AgentActionDetail.Instance();
        var addon=(AtkUnitBase*)gui.GetAddonByName("ActionDetail").Address;
        if(agent!=null && addon!=null && addon->IsReady && addon->IsVisible && agent->NumberArray!=null && agent->StringArray!=null)
            hook.Original(addon,agent->NumberArray,agent->StringArray);
    }

    public void Dispose()
    {
        try{Restore();} catch(Exception ex){Fault(ex);}
        hook.Dispose();
    }
}
