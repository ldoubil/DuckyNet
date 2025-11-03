using System;
using UnityEngine;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Client.Patches;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 单位生命周期管理器 - 示例实现
    /// 展示如何使用单位生命周期事件
    /// </summary>
    public class CharacterLifecycleManager : IDisposable
    {
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();

        public CharacterLifecycleManager()
        {
            // 订阅 EventBus 事件（事件由 Harmony Patch 自动发布）
            _eventSubscriber.EnsureInitializedAndSubscribe();
            _eventSubscriber.Subscribe<CharacterSpawnedEvent>(OnCharacterSpawned);
            _eventSubscriber.Subscribe<CharacterDestroyedEvent>(OnCharacterDestroyed);
            _eventSubscriber.Subscribe<CharacterDeathEvent>(OnCharacterDeath);

            Debug.Log("[CharacterLifecycleManager] 已初始化单位生命周期管理器");
        }

        /// <summary>
        /// 单位创建事件处理器
        /// </summary>
        private void OnCharacterSpawned(CharacterSpawnedEvent evt)
        {
            try
            {
                Debug.Log($"[CharacterLifecycle] 🟢 单位创建: ID={evt.CharacterId}, Name={evt.GameObject?.name}");
                
                // TODO: 在这里添加你的逻辑
                // 例如：
                // - 记录单位到列表
                // - 附加自定义组件
                // - 同步到网络
                // - 等等...
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterLifecycleManager] 处理单位创建失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 单位销毁事件处理器
        /// </summary>
        private void OnCharacterDestroyed(CharacterDestroyedEvent evt)
        {
            try
            {
                Debug.Log($"[CharacterLifecycle] 🔴 单位销毁: ID={evt.CharacterId}, Name={evt.GameObject?.name}");
                
                // TODO: 在这里添加你的逻辑
                // 例如：
                // - 从列表中移除
                // - 清理资源
                // - 通知网络
                // - 等等...
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterLifecycleManager] 处理单位销毁失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 单位死亡事件处理器
        /// </summary>
        private void OnCharacterDeath(CharacterDeathEvent evt)
        {
            try
            {
                Debug.Log($"[CharacterLifecycle] 💀 单位死亡: ID={evt.CharacterId}, Name={evt.GameObject?.name}");
                
                // TODO: 在这里添加你的逻辑
                // 例如：
                // - 播放死亡特效
                // - 掉落物品
                // - 更新统计
                // - 同步到网络
                // - 等等...
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterLifecycleManager] 处理单位死亡失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            _eventSubscriber?.Dispose();
            CharacterCreationPatch.Clear();
            Debug.Log("[CharacterLifecycleManager] 已清理单位生命周期管理器");
        }
    }
}

