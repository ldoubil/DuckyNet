using System;
using DuckyNet.Server.Core;
using DuckyNet.Server.Managers;

namespace DuckyNet.Server.Events.Handlers
{
    /// <summary>
    /// 玩家清理处理器 - 玩家断开连接时清理相关数据
    /// </summary>
    public class PlayerCleanupHandler
    {
        private readonly NpcVisibilityTracker _npcVisibilityTracker;
        private readonly PlayerManager _playerManager;
        private readonly PlayerNpcManager _playerNpcManager;

        public PlayerCleanupHandler(
            IEventBus eventBus, 
            NpcVisibilityTracker npcVisibilityTracker,
            PlayerManager playerManager,
            PlayerNpcManager playerNpcManager)
        {
            _npcVisibilityTracker = npcVisibilityTracker;
            _playerManager = playerManager;
            _playerNpcManager = playerNpcManager;
            
            eventBus.Subscribe<PlayerDisconnectedEvent>(OnPlayerDisconnected);
        }

        private void OnPlayerDisconnected(PlayerDisconnectedEvent evt)
        {
            if (evt.Player == null) return;

            Console.WriteLine($"[PlayerCleanup] 清理玩家数据: {evt.Player.SteamName}");

            // 获取 ClientId
            var clientId = _playerManager.GetClientIdBySteamId(evt.Player.SteamId);

            // 清理 NPC 可见性追踪
            if (clientId != null)
            {
                _npcVisibilityTracker.RemovePlayer(clientId);
                Console.WriteLine($"[PlayerCleanup] ✅ 已清理 NPC 可见性追踪");
            }

            // 🔥 清理玩家的所有 NPC
            _playerNpcManager.ClearPlayerNpcs(evt.Player.SteamId);
            Console.WriteLine($"[PlayerCleanup] ✅ 已清理玩家的所有 NPC");
        }
    }
}

