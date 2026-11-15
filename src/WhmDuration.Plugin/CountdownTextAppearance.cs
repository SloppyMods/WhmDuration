using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using WhmDuration.Core;

namespace WhmDuration.Plugin;

internal sealed unsafe class CountdownTextAppearance(IGameGui gui, IReadOnlyList<string> bars)
{
    private readonly CountdownLayoutTracker tracker=new();

    private static AtkTextNode* TextNode(ActionBarSlot* slot)
    {
        if(slot==null || slot->ComponentDragDrop==null) return null;
        var icon=slot->ComponentDragDrop->AtkComponentIcon;
        if(icon==null || icon->QuantityText==null || icon->QuantityText->Type!=NodeType.Text) return null;
        return icon->QuantityText;
    }

    private static bool Key(AddonActionBarBase* bar, ActionBarSlot* slot, out TextSlotKey key)
    {
        key=default;
        if(bar==null || slot==null || !UiCooldownOverride.SupportsAddon(bar->NameString,bar->IsCrossHotbar)) return false;
        long index=slot-bar->ActionBarSlotVector.First;
        if(index<0 || index>=bar->SlotCount || index>=bar->ActionBarSlotVector.LongCount) return false;
        key=new(bar->Id,(int)index);
        return true;
    }

    private static void SetLayout(AtkTextNode* node, TextNodeLayout value)
    {
        node->SetPositionFloat(value.X,value.Y);
        node->SetScale(value.ScaleX,value.ScaleY);
    }

    // Restore before the original renderer so layout/style updates begin from game values,
    // not our previous frame's offset/scale. Never dereference a saved node address.
    public void Restore(AddonActionBarBase* bar, ActionBarSlot* slot)
    {
        if(!Key(bar,slot,out var key)) return;
        var node=TextNode(slot);
        var original=tracker.Take(key,(nuint)node);
        if(node!=null && original is {} layout) SetLayout(node,layout);
    }

    public void Apply(AddonActionBarBase* bar, ActionBarSlot* slot, CountdownTextStyle style)
    {
        style=style.Normalize();
        if(style==CountdownTextStyle.Default || !Key(bar,slot,out var key)) return;
        var node=TextNode(slot);
        if(node==null) return;
        var original=new TextNodeLayout(node->X,node->Y,node->ScaleX,node->ScaleY);
        if(!float.IsFinite(original.X) || !float.IsFinite(original.Y) || !float.IsFinite(original.ScaleX) || !float.IsFinite(original.ScaleY)) return;
        tracker.Remember(key,(nuint)node,original);
        SetLayout(node,CountdownTextLayout.Apply(original,style));
    }

    public void ForgetAddon(ushort addonId) => tracker.ForgetAddon(addonId);

    public void RestoreAll()
    {
        foreach(var name in bars)
        {
            var bar=(AddonActionBarBase*)gui.GetAddonByName(name).Address;
            if(bar==null || !bar->IsReady) continue;
            for(int index=0;index<bar->SlotCount && index<bar->ActionBarSlotVector.LongCount;index++)
                Restore(bar,bar->ActionBarSlotVector.First+index);
            tracker.ForgetAddon(bar->Id);
        }
    }
}
