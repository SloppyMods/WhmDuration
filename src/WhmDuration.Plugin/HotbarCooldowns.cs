using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using WhmDuration.Core;

namespace WhmDuration.Plugin;

internal sealed unsafe class HotbarCooldowns : IDisposable
{
    private static readonly string[] Bars = ["_ActionBar", "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04", "_ActionBar05", "_ActionBar06", "_ActionBar07", "_ActionBar08", "_ActionBar09"];
    private readonly Hook<AddonActionBarBase.Delegates.UpdateHotbarSlot> hook;
    private readonly IAddonLifecycle lifecycle;
    private readonly IGameGui gui;
    private readonly IPluginLog log;
    private readonly GameEffects effects;
    private readonly Func<bool> enabled;
    private readonly Func<CountdownTextStyle> textStyle;
    private readonly CountdownTextAppearance appearance;
    // Keep indices, not native pointers, across frames. Finalization invalidates bindings.
    private readonly Dictionary<ushort, Dictionary<int, (int Number, int String)>> bindings = [];
    private bool faulted;
    public long Draws { get; private set; }
    public long Overlays { get; private set; }
    public bool Faulted => faulted;

    public HotbarCooldowns(IGameInteropProvider interop, IAddonLifecycle lifecycle, IGameGui gui,
        IPluginLog log, GameEffects effects, Func<bool> enabled, Func<CountdownTextStyle> textStyle)
    {
        this.lifecycle=lifecycle; this.gui=gui; this.log=log; this.effects=effects; this.enabled=enabled;
        this.textStyle=textStyle;
        appearance=new(gui,Bars);
        hook = interop.HookFromAddress<AddonActionBarBase.Delegates.UpdateHotbarSlot>(
            (nint)AddonActionBarBase.MemberFunctionPointers.UpdateHotbarSlot, Detour);
        hook.Enable();
        lifecycle.RegisterListener(AddonEvent.PostUpdate, Bars, Update);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, Bars, FinalizeBar);
    }

    private void Detour(AddonActionBarBase* bar, ActionBarSlot* slot, NumberArrayData* numbers,
        StringArrayData* strings, int numberIndex, int stringIndex)
    {
        CooldownVisual? visual = null;
        try
        {
            if (bar != null && slot != null && UiCooldownOverride.SupportsAddon(bar->NameString,bar->IsCrossHotbar))
            {
                long offset = slot - bar->ActionBarSlotVector.First;
                if (offset >= 0 && offset < bar->SlotCount && offset < bar->ActionBarSlotVector.LongCount)
                {
                    if (!bindings.TryGetValue(bar->Id, out var slots)) bindings[bar->Id]=slots=[];
                    slots[(int)offset]=(numberIndex,stringIndex);
                    visual=Visual(numbers,numberIndex);
                }
            }
        }
        catch(Exception ex) { Fault(ex); }
        Render(bar,slot,numbers,strings,numberIndex,stringIndex,visual);
    }

    private CooldownVisual? Visual(NumberArrayData* numbers, int index)
    {
        if (faulted || !enabled() || numbers == null || numbers->IntArray == null || index < 0 || numbers->Size < 17 || index > numbers->Size-17) return null;
        var values=numbers->IntArray+index;
        if (values[0]!=48) return null;
        return effects.GetVisual(unchecked((uint)values[3]));
    }

    private void Render(AddonActionBarBase* bar, ActionBarSlot* slot, NumberArrayData* numbers,
        StringArrayData* strings, int numberIndex, int stringIndex, CooldownVisual? visual)
    {
        appearance.Restore(bar,slot);
        Draws++;
        if (visual is {} cooldown && numbers != null)
        {
            if (UiCooldownOverride.Invoke(numbers->IntArray,numbers->Size,numberIndex,cooldown,
                () => hook.Original(bar,slot,numbers,strings,numberIndex,stringIndex)))
            {
                Overlays++;
                appearance.Apply(bar,slot,textStyle());
            }
        }
        else hook.Original(bar,slot,numbers,strings,numberIndex,stringIndex);
    }

    private void Update(AddonEvent type, AddonArgs args)
    {
        try { Redraw((AddonActionBarBase*)args.Addon.Address, false); }
        catch(Exception ex) { Fault(ex); }
    }

    private void Redraw(AddonActionBarBase* bar, bool restore)
    {
        var stage=AtkStage.Instance();
        if (bar==null || stage==null || !bar->IsReady || !bar->IsVisible || !UiCooldownOverride.SupportsAddon(bar->NameString,bar->IsCrossHotbar)) return;
        if (!bindings.TryGetValue(bar->Id,out var slots))
        {
            // Seed from the game's actual caller/indices, rather than guessing array layout.
            if (!restore) bar->OnRequestedUpdate(stage->GetNumberArrayData(),stage->GetStringArrayData());
            return;
        }
        var numbers=stage->GetNumberArrayData(NumberArrayType.ActionBar);
        var strings=stage->GetStringArrayData(StringArrayType.ActionBar);
        if(numbers==null || strings==null || numbers->IntArray==null || strings->StringArray==null) return;
        foreach(var (index,position) in slots)
        {
            if(index<0 || index>=bar->SlotCount || index>=bar->ActionBarSlotVector.LongCount ||
               position.Number<0 || numbers->Size<17 || position.Number>numbers->Size-17 ||
               position.String<0 || position.String>=strings->Size-1) continue;
            // Render also when effect expires/toggle disables, so no stale sweep survives.
            Render(bar,bar->ActionBarSlotVector.First+index,numbers,strings,position.Number,position.String,
                restore ? null : Visual(numbers,position.Number));
        }
    }

    private void FinalizeBar(AddonEvent type, AddonArgs args)
    {
        var id=((AddonActionBarBase*)args.Addon.Address)->Id;
        appearance.ForgetAddon(id);
        bindings.Remove(id);
    }
    private void Fault(Exception ex)
    {
        if(!faulted) log.Error(ex,"WHM Duration hotbar adapter disabled after a fault.");
        faulted=true;
    }

    public IReadOnlyList<string> Diagnostics()
    {
        var lines=new List<string> { $"WHM Duration diag hotbars: boundBars={bindings.Count}, boundSlots={bindings.Values.Sum(s=>s.Count)}." };
        var stage=AtkStage.Instance();
        if(stage==null) return lines;
        var numbers=stage->GetNumberArrayData(NumberArrayType.ActionBar);
        if(numbers==null || numbers->IntArray==null) return lines;
        int samples=0;
        foreach(var name in Bars)
        {
            var bar=(AddonActionBarBase*)gui.GetAddonByName(name).Address;
            if(bar==null || !bar->IsReady || !bar->IsVisible) continue;
            bool bound=bindings.TryGetValue(bar->Id,out var slots);
            lines.Add($"WHM Duration diag bar: name={name}, slots={bar->SlotCount}, vector={bar->ActionBarSlotVector.LongCount}, cross={bar->IsCrossHotbar}, bindings={(bound ? slots!.Count : 0)}.");
            if(!bound) continue;
            foreach(var (index,position) in slots!)
            {
                if(index<0 || index>=bar->SlotCount || index>=bar->ActionBarSlotVector.LongCount || position.Number<0 || numbers->Size<17 || position.Number>numbers->Size-17) continue;
                var slot=bar->ActionBarSlotVector.First+index;
                var value=numbers->IntArray+position.Number;
                if(DurationModel.Find(unchecked((uint)value[3]))==null && DurationModel.Find(unchecked((uint)slot->ActionId))==null) continue;
                if(samples++>=12) continue;
                lines.Add($"WHM Duration diag slot: bar={name}, slot={index}, numberIndex={position.Number}, uiType={value[0]}, arrayAction={value[3]}, slotAction={slot->ActionId}, nativePercent={value[8]}, visual={Visual(numbers,position.Number)}.");
            }
        }
        return lines;
    }

    public void Restore()
    {
        try { appearance.RestoreAll(); }
        catch(Exception ex) { Fault(ex); }
        foreach(var name in Bars)
            try { Redraw((AddonActionBarBase*)gui.GetAddonByName(name).Address, true); }
            catch(Exception ex) { Fault(ex); }
    }

    public void Dispose()
    {
        lifecycle.UnregisterListener(Update,FinalizeBar);
        Restore();
        hook.Dispose();
        bindings.Clear();
    }
}
