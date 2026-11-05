using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using NativeFramework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;

namespace DuckbillFisher.Plugin;

internal enum ChatItemContextState { Ready, NotDefaultMenu, WrongThread, AddressUnavailable, InvalidState, OtherOwner, NotItem }
internal readonly record struct ChatItemContext(ChatItemContextState State, uint RawItemId = 0);

/// <summary>Copies the current ChatLog item context during its own menu callback. No address survives the call.</summary>
internal sealed class NativeChatItemContextReader(IFramework framework)
{
    public unsafe ChatItemContext Read(IMenuOpenedArgs args)
    {
        if (args.MenuType != ContextMenuType.Default || args.Target is not MenuTargetDefault target)
            return new(ChatItemContextState.NotDefaultMenu);
        if (!framework.IsInFrameworkUpdateThread) return new(ChatItemContextState.WrongThread);
        var address = NativeFramework.StaticAddressPointers.ppInstance;
        if (address == null || *address == null) return new(ChatItemContextState.AddressUnavailable);
        var ui = (*address)->UIModule;
        if (ui == null || ui->VirtualTable == null || ui->VirtualTable->GetAgentModule == null)
            return new(ChatItemContextState.AddressUnavailable);
        var agents = ui->GetAgentModule();
        if (agents == null || agents->UIModule != ui || (int)AgentId.ChatLog >= agents->Agents.Length)
            return new(ChatItemContextState.InvalidState);
        var chat = (AgentChatLog*)agents->Agents[(int)AgentId.ChatLog].Value;
        if (chat == null || chat->AgentInterface.UIModuleInterface != &ui->UIModuleInterface)
            return new(ChatItemContextState.InvalidState);
        // Chat text, player names and other addons must not inherit an earlier fish item's context.
        if (!args.EventInterfaces.Contains((nint)(&chat->AgentInterface.AtkEventInterface)))
            return new(ChatItemContextState.OtherOwner);
        if (target.TargetContentId != 0 || target.TargetObject is not null || chat->ContextStatusId != 0 || chat->ContextItemId == 0)
            return new(ChatItemContextState.NotItem);
        return new(ChatItemContextState.Ready, chat->ContextItemId);
    }
}
