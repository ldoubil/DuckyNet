using DuckyNet.Shared.Data;
using DuckyNet.RPC;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Context;
using System.Threading.Tasks;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// 血量同步服务接口（客户端调用服务端）
    /// </summary>
    [RpcService("HealthSyncService")]
    public interface IHealthSyncService
    {
        /// <summary>
        /// 客户端发送血量数据到服务器
        /// </summary>
        [ClientToServer]
        void SendHealthSync(IClientContext client, HealthSyncData healthData);
    }

    /// <summary>
    /// 血量同步客户端服务接口（服务端调用客户端）
    /// </summary>
    [RpcService("HealthSyncClientService")]
    public interface IHealthSyncClientService
    {
        /// <summary>
        /// 服务器广播血量数据到客户端
        /// </summary>
        [ServerToClient]
        void OnHealthSyncReceived(HealthSyncData healthData);
    }
}

