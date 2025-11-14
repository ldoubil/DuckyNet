using DuckyNet.Shared.Data;
using DuckyNet.RPC;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Context;
using System;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 玩家服务接口
    /// 定义客户端可以调用的服务器方法（上行）
    /// </summary>
    [RpcService("PlayerUnitySyncService")]
    public interface IPlayerUnitySyncService
    {
        /// <summary>
        /// 发送自身坐标到服务器/服务器根据当前房间和场景进行分发
        /// </summary>
        [ClientToServer]
        void SendPlayerUnitySync(IClientContext client, UnitySyncData syncData);
    }
}