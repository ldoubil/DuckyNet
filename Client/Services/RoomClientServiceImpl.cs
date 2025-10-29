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
            Debug.Log($"[RoomClientService] {player.SteamName} joined room {room.RoomName}");

            // 发布到 EventBus
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new PlayerJoinedRoomEvent(player, room));
            }
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

