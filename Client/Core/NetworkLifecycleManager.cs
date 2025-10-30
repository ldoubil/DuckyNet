using System;
using System.Threading.Tasks;
using UnityEngine;
using DuckyNet.Client.Core.Helpers;


namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 网络生命周期管理器 - 统一管理网络连接、房间加入/离开等生命周期事件
    /// </summary>
    public class NetworkLifecycleManager : IDisposable
    {
        private readonly GameContext _context;
        public NetworkLifecycleManager(GameContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// 处理连接成功
        /// </summary>
        public void HandleConnected()
        {
            try
            {
                Debug.Log("[NetworkLifecycleManager] 处理连接成功事件");
                // 发布 EventBus 事件
                if (GameContext.IsInitialized)
                {
                    _context.EventBus.Publish(NetworkConnectedEvent.Instance);
                }

            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLifecycleManager] 处理连接事件失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 处理断开连接
        /// </summary>
        public void HandleDisconnected(string reason)
        {
            try
            {
                Debug.LogWarning($"[NetworkLifecycleManager] 与服务器断开连接: {reason}");

                // 发布 EventBus 事件
                if (GameContext.IsInitialized)
                {
                    _context.EventBus.Publish(new NetworkDisconnectedEvent(reason));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkLifecycleManager] 处理断开连接事件失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

    }
}


