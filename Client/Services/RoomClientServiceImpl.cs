using System;
using UnityEngine;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 房间客户端服务实现（接收服务器调用）
    /// </summary>
    public class RoomClientServiceImpl : IRoomClientService
    {
        public event Action<PlayerInfo, RoomInfo>? OnPlayerJoinedRoomEvent;
        public event Action<PlayerInfo, RoomInfo>? OnPlayerLeftRoomEvent;

        public void OnPlayerJoinedRoom(PlayerInfo player, RoomInfo room)
        {
            Debug.Log($"[RoomClientService] {player.SteamName} joined room {room.RoomName}");
            OnPlayerJoinedRoomEvent?.Invoke(player, room);
        }

        public void OnPlayerLeftRoom(PlayerInfo player, RoomInfo room)
        {
            Debug.Log($"[RoomClientService] {player.SteamName} left room {room.RoomName}");
            OnPlayerLeftRoomEvent?.Invoke(player, room);
        }

        public void OnKickedFromRoom(string reason)
        {
            Debug.LogWarning($"[RoomClientService] Kicked from room: {reason}");
        }
    }
}

