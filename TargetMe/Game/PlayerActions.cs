using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using TargetMe.Core;
using TargetMe.Tracking;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace TargetMe.Game;

/// <summary>What the context menu can do with a viewer. Game calls run on the next framework tick.</summary>
public sealed unsafe class PlayerActions
{
    private readonly ViewerTracker tracker;

    public PlayerActions(ViewerTracker tracker) => this.tracker = tracker;

    public void Target(ViewerRecord record) => WithCharacter(record, pc => Services.TargetManager.Target = pc);

    public void Dote(ViewerRecord record) => WithCharacter(record, pc =>
    {
        Services.TargetManager.Target = pc;
        Services.Framework.RunOnTick(() =>
        {
            if (!ChatSender.Send("/dote", out var error))
                Services.Log.Warning($"Couldn't dote {record.Name}: {error}");
        }, delayTicks: 1);
    });

    public void Examine(ViewerRecord record) => WithCharacter(record, pc =>
    {
        var agent = AgentInspect.Instance();
        if (agent != null)
            agent->ExamineCharacter(pc.EntityId);
    });

    public void AdventurePlate(ViewerRecord record)
    {
        if (record.ContentId != 0)
        {
            Services.Framework.RunOnTick(() =>
            {
                var agent = AgentCharaCard.Instance();
                if (agent != null)
                    agent->OpenCharaCard(record.ContentId);
            });
            return;
        }

        WithCharacter(record, pc =>
        {
            var agent = AgentCharaCard.Instance();
            if (agent != null)
                agent->OpenCharaCard((GameObject*)pc.Address);
        });
    }

    public void GameMenu(ViewerRecord record) => WithCharacter(record, pc =>
    {
        var agent = AgentHUD.Instance();
        if (agent != null)
            agent->OpenContextMenuFromTarget((GameObject*)pc.Address);
    });

    public void CopyName(ViewerRecord record) =>
        ImGui.SetClipboardText(record.WorldName.Length > 0 ? $"{record.Name}@{record.WorldName}" : record.Name);

    /// <summary>Runs on the next tick with the live character, or does nothing if they've gone.</summary>
    private void WithCharacter(ViewerRecord record, Action<IPlayerCharacter> action) =>
        Services.Framework.RunOnTick(() =>
        {
            if (tracker.Resolve(record) is { } pc)
                action(pc);
        });
}
