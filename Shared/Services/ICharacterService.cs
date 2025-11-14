using DuckyNet.RPC;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Context;
using System;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 角色服务接口 - 管理角色外观和数据
    /// </summary>
    [RpcService("CharacterService")]
    public interface ICharacterService
    {
        /// <summary>
        /// 更新玩家角色外观
        /// </summary>
        [ClientToServer]
        Task<bool> UpdateAppearanceAsync(IClientContext client, byte[] appearanceData);

        /// <summary>
        /// 获取玩家角色外观
        /// </summary>
        [ClientToServer]
        Task<byte[]?> GetAppearanceAsync(IClientContext client, string steamId);

    }

    /// <summary>
    /// 角色客户端服务接口
    /// </summary>
    [RpcService("CharacterClientService")]
    public interface ICharacterClientService
    {
        /// <summary>
        /// 玩家外观更新通知
        /// </summary>
        [ServerToClient]
        void OnPlayerAppearanceUpdated(string steamId, byte[] appearanceData);
    }
}

