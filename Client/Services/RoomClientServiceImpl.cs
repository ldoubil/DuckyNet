using UnityEngine;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 房间客户端服务实现（接收服务器调用）
    /// </summary>
    public class RoomClientServiceImpl : IRoomClientService
    {
        public void OnPlayerJoinedRoom(PlayerInfo player, RoomInfo room)
        {
            Debug.Log($"[RoomClientService] ========== 收到 RPC: OnPlayerJoinedRoom ==========");
            Debug.Log($"[RoomClientService] 玩家: {player.SteamName} ({player.SteamId})");
            Debug.Log($"[RoomClientService] 房间: {room.RoomName} ({room.RoomId})");

            // 发布到 EventBus
            if (GameContext.IsInitialized)
            {
                Debug.Log($"[RoomClientService] 发布 PlayerJoinedRoomEvent...");
                GameContext.Instance.EventBus.Publish(new PlayerJoinedRoomEvent(player, room));
                Debug.Log($"[RoomClientService] ✅ PlayerJoinedRoomEvent 已发布");
            }
            else
            {
                Debug.LogError($"[RoomClientService] ❌ GameContext 未初始化，无法发布事件！");
            }
            Debug.Log($"[RoomClientService] ========== 处理完成 ==========");
        }

        public void OnPlayerLeftRoom(PlayerInfo player, RoomInfo room)
        {
            Debug.Log($"[RoomClientService] {player.SteamName} left room {room.RoomName}");

            // 发布到 EventBus
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new PlayerLeftRoomEvent(player, room));
            }
        }

        public void OnKickedFromRoom(string reason)
        {
            Debug.LogWarning($"[RoomClientService] Kicked from room: {reason}");

            // 发布到 EventBus
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new KickedFromRoomEvent(reason));
            }
        }
    }
}

