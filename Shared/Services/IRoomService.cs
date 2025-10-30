using DuckyNet.Shared.RPC;
using System;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 房间数据
    /// </summary>
    [Serializable]
    public class RoomInfo
    {
        /// <summary>
        /// 房间唯一ID
        /// </summary>
        public string RoomId { get; set; } = string.Empty;

        /// <summary>
        /// 房间名称
        /// </summary>
        public string RoomName { get; set; } = string.Empty;

        /// <summary>
        /// 房间描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 房间密码（可选）
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// 是否需要密码
        /// </summary>
        public bool RequirePassword => !string.IsNullOrEmpty(Password);

        /// <summary>
        /// 房主玩家SteamId
        /// </summary>
        public string HostSteamId { get; set; } = string.Empty;

        /// <summary>
        /// 当前玩家数
        /// </summary>
        public int CurrentPlayers { get; set; }

        /// <summary>
        /// 最大玩家数
        /// </summary>
        public int MaxPlayers { get; set; } = 8;

        /// <summary>
        /// 房间创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 是否已满
        /// </summary>
        public bool IsFull => CurrentPlayers >= MaxPlayers;

        /// <summary>
        /// 是否可加入
        /// </summary>
        public bool CanJoin => !IsFull;
    }


    /// <summary>
    /// 创建房间请求
    /// </summary>
    [Serializable]
    public class CreateRoomRequest
    {
        public string RoomName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Password { get; set; }
        public int MaxPlayers { get; set; } = 8;
    }

    /// <summary>
    /// 加入房间请求
    /// </summary>
    [Serializable]
    public class JoinRoomRequest
    {
        public string RoomId { get; set; } = string.Empty;
        public string? Password { get; set; }
    }

    /// <summary>
    /// 房间操作结果
    /// </summary>
    [Serializable]
    public class RoomOperationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public RoomInfo? Room { get; set; }
    }

    /// <summary>
    /// 房间服务接口
    /// </summary>
    [RpcService("RoomService")]
    public interface IRoomService
    {
        /// <summary>
        /// 获取所有房间列表
        /// </summary>
        [ClientToServer]
        Task<RoomInfo[]> GetRoomListAsync(IClientContext client);

        /// <summary>
        /// 创建房间
        /// </summary>
        [ClientToServer]
        Task<RoomOperationResult> CreateRoomAsync(IClientContext client, CreateRoomRequest request);

        /// <summary>
        /// 加入房间
        /// </summary>
        [ClientToServer]
        Task<RoomOperationResult> JoinRoomAsync(IClientContext client, JoinRoomRequest request);

        /// <summary>
        /// 离开房间
        /// </summary>
        [ClientToServer]
        Task<bool> LeaveRoomAsync(IClientContext client);

        /// <summary>
        /// 获取当前房间信息
        /// </summary>
        [ClientToServer]
        Task<RoomInfo?> GetCurrentRoomAsync(IClientContext client);

        /// <summary>
        /// 获取指定房间信息
        /// </summary>
        [ClientToServer]
        Task<RoomInfo?> GetRoomInfoAsync(IClientContext client, string roomId);

        /// <summary>
        /// 获取房间内的玩家列表
        /// </summary>
        [ClientToServer]
        Task<PlayerInfo[]> GetRoomPlayersAsync(IClientContext client, string roomId);

        /// <summary>
        /// 踢出玩家（仅房主）
        /// </summary>
        [ClientToServer]
        Task<bool> KickPlayerAsync(IClientContext client, string SteamId);
    }

    /// <summary>
    /// 房间客户端服务接口
    /// </summary>
    [RpcService("RoomClientService")]
    public interface IRoomClientService
    {
        /// <summary>
        /// 玩家加入房间通知
        /// </summary>
        [ServerToClient]
        void OnPlayerJoinedRoom(PlayerInfo player, RoomInfo room);

        /// <summary>
        /// 玩家离开房间通知
        /// </summary>
        [ServerToClient]
        void OnPlayerLeftRoom(PlayerInfo player, RoomInfo room);

        /// <summary>
        /// 被踢出房间通知
        /// </summary>
        [ServerToClient]
        void OnKickedFromRoom(string reason);
    }
}

