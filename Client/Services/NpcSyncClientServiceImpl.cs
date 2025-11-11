using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using UnityEngine;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// NPC 同步客户端服务 - 接收服务器广播的 NPC 事件
    /// </summary>
    public class NpcSyncClientServiceImpl : INpcSyncClientService
    {
        /// <summary>
        /// 接收其他客户端的 NPC 生成
        /// </summary>
        public void OnNpcSpawned(NpcSpawnData spawnData)
        {
            try
            {
                if (!GameContext.IsInitialized) return;

                Debug.Log($"[NpcSyncClient] 📦 收到远程 NPC 生成: {spawnData.NpcType} (ID: {spawnData.NpcId})");
                Debug.Log($"    场景: {spawnData.SceneName}/{spawnData.SubSceneName}");
                Debug.Log($"    位置: ({spawnData.PositionX:F2}, {spawnData.PositionY:F2}, {spawnData.PositionZ:F2})");

                // 检查是否在同一场景
                var localSceneData = GameContext.Instance.PlayerManager?.LocalPlayer?.Info?.CurrentScenelData;
                if (localSceneData == null) return;

                bool isSameScene = localSceneData.SceneName == spawnData.SceneName &&
                                  localSceneData.SubSceneName == spawnData.SubSceneName;

                if (!isSameScene)
                {
                    Debug.Log($"[NpcSyncClient] 不在同一场景，跳过创建");
                    return;
                }

                // 从对象池创建影子 NPC
                GameContext.Instance.NpcManager?.AddRemoteNpc(spawnData.NpcId, spawnData);
                
                Debug.Log($"[NpcSyncClient] ✅ 远程 NPC 已创建并注册（使用对象池）");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcSyncClient] 处理 NPC 生成失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 接收 NPC 批量位置更新
        /// </summary>
        public void OnNpcBatchTransform(NpcBatchTransformData batchData)
        {
            try
            {
                if (!GameContext.IsInitialized) return;

                var npcManager = GameContext.Instance.NpcManager;
                if (npcManager == null) return;

                // 批量更新远程 NPC 位置
                for (int i = 0; i < batchData.Count; i++)
                {
                    Vector3 position = new Vector3(
                        batchData.PositionsX[i], 
                        batchData.PositionsY[i], 
                        batchData.PositionsZ[i]
                    );
                    
                    npcManager.UpdateRemoteNpcTransform(batchData.NpcIds[i], position, batchData.RotationsY[i]);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcSyncClient] 处理位置更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 接收 NPC 销毁
        /// </summary>
        public void OnNpcDestroyed(NpcDestroyData destroyData)
        {
            try
            {
                if (!GameContext.IsInitialized) return;

                Debug.Log($"[NpcSyncClient] 🗑️ 收到远程 NPC 销毁: {destroyData.NpcId} (原因: {destroyData.Reason})");

                // 移除远程 NPC
                GameContext.Instance.NpcManager?.RemoveRemoteNpc(destroyData.NpcId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcSyncClient] 处理 NPC 销毁失败: {ex.Message}");
            }
        }
    }
}

