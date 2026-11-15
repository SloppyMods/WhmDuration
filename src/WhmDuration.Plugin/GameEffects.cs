using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using WhmDuration.Core;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace WhmDuration.Plugin;

internal sealed unsafe class GameEffects(IObjectTable objects, ITargetManager targets, IClientState client)
{
    public bool Available => !client.IsPvP && objects.LocalPlayer != null;

    public float GetRemaining(uint actionId)
    {
        var effect = DurationModel.Find(actionId);
        var player = objects.LocalPlayer;
        if (!Available || player == null || effect == null) return 0;

        if (effect.Kind == TimerKind.GroundRecast)
        {
            var mgr = ActionManager.Instance();
            if (mgr == null || !mgr->IsRecastTimerActive(ActionType.Action, actionId)) return 0;
            float el = mgr->GetRecastTimeElapsed(ActionType.Action, actionId);
            return Math.Max(0, effect.DurationSeconds - el);
        }

        var target = targets.SoftTarget ?? targets.Target;
        TargetKind kind = TargetKind.None;
        if (target is IBattleChara)
        {
            var category = ActionManager.ClassifyTarget((NativeCharacter*)target.Address);
            kind = category == ActionManager.TargetCategory.Enemy ? TargetKind.Hostile : TargetKind.Friendly;
        }
        uint selected = DurationModel.SelectTarget(effect.Target, player.EntityId, target?.EntityId ?? 0, kind);
        var recipient = selected == player.EntityId ? player : selected == target?.EntityId ? target as IBattleChara : null;
        if (recipient == null) return 0;
        return DurationModel.SelectRemaining(effect, player.EntityId,
            recipient.StatusList.Select(s => new StatusSnapshot(s.StatusId, s.SourceId, s.RemainingTime)));
    }

    public IReadOnlyList<string> Diagnostics()
    {
        var player=objects.LocalPlayer;
        var target=targets.SoftTarget ?? targets.Target;
        var lines=new List<string> { $"WHM Duration diag: available={Available}, job={player?.ClassJob.RowId}, PvP={client.IsPvP}, self={player?.EntityId:X8}, target={target?.EntityId:X8}, soft={targets.SoftTarget?.EntityId:X8}." };
        if(player==null) return lines;
        var manager=ActionManager.Instance();
        if(manager!=null)
        {
            foreach(var act in new uint[]{ 137, 16532, 3569, 16540, 188, 16554, 24314, 7489, 7407, 23 })
            {
                var eff = DurationModel.Find(act);
                if(eff != null)
                {
                    lines.Add($"WHM Duration diag act {act}: rem={GetRemaining(act):F3}, cdActive={manager->IsRecastTimerActive(ActionType.Action,act)}, el={manager->GetRecastTimeElapsed(ActionType.Action,act):F3}, tot={manager->GetRecastTime(ActionType.Action,act):F3}, vis={GetVisual(act)}.");
                }
            }
        }
        return lines;
    }

    public CooldownVisual? GetVisual(uint actionId)
    {
        var effect = DurationModel.Find(actionId);
        var manager = ActionManager.Instance();
        if (!Available || effect == null || manager == null) return null;

        if (effect.Kind == TimerKind.GroundRecast)
        {
            if (!manager->IsRecastTimerActive(ActionType.Action, actionId)) return null;
            float elapsed = manager->GetRecastTimeElapsed(ActionType.Action, actionId);
            float total = manager->GetRecastTime(ActionType.Action, actionId);
            return DurationModel.GetGroundVisual(elapsed, effect.DurationSeconds, total);
        }

        // Read only. Never fabricate ActionManager recast state or intercept UseAction.
        return DurationModel.GetVisual(GetRemaining(actionId), effect.DurationSeconds,
            manager->IsRecastTimerActive(ActionType.Action, actionId));
    }
}
