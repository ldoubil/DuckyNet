using System;
using UnityEngine;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core.Utils;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 武器开枪特效播放器 - 远程玩家专用
    /// 使用 WeaponEffectsCore 提供的共享逻辑
    /// </summary>
    public static class WeaponFireEffectsPlayer
    {
        private static bool _initialized = false;

        /// <summary>
        /// 初始化（远程玩家专用）
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // 初始化共享核心
                WeaponEffectsCore.Initialize();
                
                _initialized = true;
                Debug.Log("[WeaponFireEffectsPlayer] ✅ 初始化完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponFireEffectsPlayer] 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放远程玩家的开枪特效
        /// </summary>
        public static void PlayFireEffects(GameObject characterObject, WeaponFireData fireData)
        {
            try
            {
                if (characterObject == null)
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] 角色对象为空");
                    return;
                }

                var characterMainControl = characterObject.GetComponent<CharacterMainControl>();
                if (characterMainControl == null)
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] 找不到 CharacterMainControl");
                    return;
                }

                // 获取当前手持的枪械 Agent
                var currentAgent = characterMainControl.CurrentHoldItemAgent;
                if (currentAgent == null)
                {
                    Debug.Log("[WeaponFireEffectsPlayer] 当前没有手持武器，跳过特效");
                    return;
                }

                // 检查是否为枪械类型
                if (WeaponEffectsCore.ItemAgentGunType == null || 
                    !WeaponEffectsCore.ItemAgentGunType.IsInstanceOfType(currentAgent))
                {
                    Debug.Log("[WeaponFireEffectsPlayer] 当前手持武器不是枪械类型");
                    return;
                }

                // 转换位置和方向
                Vector3 muzzlePos = new Vector3(fireData.MuzzlePositionX, fireData.MuzzlePositionY, fireData.MuzzlePositionZ);
                Vector3 muzzleDir = new Vector3(fireData.MuzzleDirectionX, fireData.MuzzleDirectionY, fireData.MuzzleDirectionZ);

                // 使用共享核心播放特效
                WeaponEffectsCore.PlayMuzzleFlash(currentAgent);
                WeaponEffectsCore.PlayShellEjection(currentAgent);
                WeaponEffectsCore.PlayShootSound(currentAgent, muzzlePos, fireData.IsSilenced);
                
                // 创建子弹（远程玩家伤害为0）
                WeaponEffectsCore.CreateBullet(currentAgent, muzzlePos, muzzleDir, characterMainControl, 0f);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponFireEffectsPlayer] 播放特效失败: {ex.Message}");
            }
        }
    }
}

