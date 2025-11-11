using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 武器特效播放器 - 本地玩家专用
    /// 使用 WeaponEffectsCore 提供的共享逻辑
    /// </summary>
    public static class WeaponEffectsPlayer
    {
        // 本地玩家专用的反射成员
        private static PropertyInfo? _mainProperty;
        private static MethodInfo? _getGunMethod;
        private static bool _initialized = false;

        /// <summary>
        /// 初始化反射（本地玩家专用）
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // 初始化共享核心
                WeaponEffectsCore.Initialize();

                // 初始化本地玩家专用成员
                var characterMainControlType = WeaponEffectsCore.CharacterMainControlType;
                if (characterMainControlType != null)
                {
                    _mainProperty = AccessTools.Property(characterMainControlType, "Main");
                    _getGunMethod = AccessTools.Method(characterMainControlType, "GetGun");
                }

                _initialized = true;
                Debug.Log("[WeaponEffectsPlayer] 初始化完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsPlayer] 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放完整的开火特效（枪口火焰 + 弹壳 + 音效 + 子弹）
        /// </summary>
        /// <param name="includeBullet">是否包含子弹创建（默认为 true，⚠️ 会造成真实伤害）</param>
        public static void PlayFullFireEffects(bool includeBullet = true)
        {
            try
            {
                if (!_initialized) Initialize();

                var gun = GetCurrentGun();
                if (gun == null)
                {
                    Debug.LogWarning("[WeaponEffectsPlayer] 当前未持有枪械");
                    return;
                }

                // 使用共享核心的方法
                var muzzle = GetMuzzleTransform(gun);
                if (muzzle != null)
                {
                    WeaponEffectsCore.PlayMuzzleFlash(gun);
                    WeaponEffectsCore.PlayShellEjection(gun);
                    WeaponEffectsCore.PlayShootSound(gun, muzzle.position);
                    
                    if (includeBullet)
                    {
                        // 获取主角作为来源角色
                        object? mainCharacter = _mainProperty?.GetValue(null);
                        WeaponEffectsCore.CreateBullet(gun, muzzle.position, muzzle.forward, mainCharacter, 1.0f);
                        Debug.Log("[WeaponEffectsPlayer] ✅ 已播放完整开火特效（含子弹）");
                    }
                    else
                    {
                        Debug.Log("[WeaponEffectsPlayer] ✅ 已播放完整开火特效（不含子弹）");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsPlayer] 播放特效失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放枪口火焰特效（向后兼容 API）
        /// </summary>
        public static void PlayMuzzleFlash(object? gun = null)
        {
            if (!_initialized) Initialize();
            
            gun ??= GetCurrentGun();
            if (gun == null) return;

            WeaponEffectsCore.PlayMuzzleFlash(gun);
        }

        /// <summary>
        /// 播放弹壳抛出特效（向后兼容 API）
        /// </summary>
        public static void PlayShellEjection(object? gun = null)
        {
            if (!_initialized) Initialize();
            
            gun ??= GetCurrentGun();
            if (gun == null) return;

            WeaponEffectsCore.PlayShellEjection(gun);
        }

        /// <summary>
        /// 播放开枪音效（向后兼容 API）
        /// </summary>
        public static void PlayShootSound(object? gun = null)
        {
            if (!_initialized) Initialize();
            
            gun ??= GetCurrentGun();
            if (gun == null) return;

            var muzzle = GetMuzzleTransform(gun);
            if (muzzle != null)
            {
                WeaponEffectsCore.PlayShootSound(gun, muzzle.position);
            }
        }

        /// <summary>
        /// 创建子弹（向后兼容 API）
        /// ⚠️ 警告：此方法会创建真实的子弹，可能造成伤害
        /// </summary>
        public static void CreateBullet(object? gun = null)
        {
            if (!_initialized) Initialize();
            
            gun ??= GetCurrentGun();
            if (gun == null) return;

            var muzzle = GetMuzzleTransform(gun);
            if (muzzle != null)
            {
                object? mainCharacter = _mainProperty?.GetValue(null);
                WeaponEffectsCore.CreateBullet(gun, muzzle.position, muzzle.forward, mainCharacter, 1.0f);
            }
        }

        /// <summary>
        /// 获取枪口 Transform
        /// </summary>
        private static Transform? GetMuzzleTransform(object gun)
        {
            var muzzleProperty = AccessTools.Property(WeaponEffectsCore.ItemAgentGunType, "muzzle");
            return muzzleProperty?.GetValue(gun) as Transform;
        }

        /// <summary>
        /// 获取当前手持的枪械
        /// </summary>
        private static object? GetCurrentGun()
        {
            try
            {
                if (_mainProperty == null || _getGunMethod == null) return null;

                object? mainCharacter = _mainProperty.GetValue(null);
                if (mainCharacter == null) return null;

                return _getGunMethod.Invoke(mainCharacter, null);
            }
            catch
            {
                return null;
            }
        }
    }
}

