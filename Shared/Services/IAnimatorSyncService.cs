using DuckyNet.Shared.Data;
using DuckyNet.RPC;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Context;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 动画同步服务接口
    /// </summary>
    [RpcService("AnimatorSync")]
    public interface IAnimatorSyncService
    {
        /// <summary>
        /// 上传动画状态到服务器（客户端 -> 服务器）
        /// </summary>
        [ClientToServer]
        void UpdateAnimatorState(IClientContext client, AnimatorSyncData animatorData);
    }

    /// <summary>
    /// 动画同步客户端服务（服务器广播给客户端）
    /// </summary>
    [RpcService("AnimatorSyncClient")]
    public interface IAnimatorSyncClientService
    {
        /// <summary>
        /// 接收其他玩家的动画状态（服务器 -> 客户端）
        /// </summary>
        [ServerToClient]
        void OnAnimatorStateUpdated(string steamId, AnimatorSyncData animatorData);
    }
}
