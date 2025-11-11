using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 武器特效核心类 - 提供共享的反射缓存和通用特效播放逻辑
    /// 避免 WeaponEffectsPlayer 和 WeaponFireEffectsPlayer 的代码重复
    /// </summary>
    public static class WeaponEffectsCore
    {
        #region 反射类型缓存
        
        private static Type? _characterMainControlType;
        private static Type? _itemAgentGunType;
        private static Type? _itemSettingGunType;
        private static Type? _levelManagerType;
        private static Type? _audioManagerType;
        private static Type? _gameplayDataSettingsType;

        private static PropertyInfo? _gunItemSettingProperty;
        private static PropertyInfo? _muzzleProperty;
        private static PropertyInfo? _silencedProperty;
        private static PropertyInfo? _bulletSpeedProperty;
        private static PropertyInfo? _bulletDistanceProperty;
        
        private static FieldInfo? _muzzleFxPfbField;
        private static FieldInfo? _shellParticleField;
        private static FieldInfo? _shootKeyField;
        private static FieldInfo? _bulletPfbField;
        
        private static PropertyInfo? _prefabsProperty;
        private static FieldInfo? _defaultBulletField;
        
        private static MethodInfo? _audioManagerPostMethod;

        private static bool _initialized = false;

        #endregion

        /// <summary>
        /// 初始化反射缓存
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // 缓存类型
                _characterMainControlType = AccessTools.TypeByName("CharacterMainControl");
                _itemAgentGunType = AccessTools.TypeByName("ItemAgent_Gun");
                _itemSettingGunType = AccessTools.TypeByName("ItemSetting_Gun");
                _levelManagerType = AccessTools.TypeByName("LevelManager");
                _audioManagerType = AccessTools.TypeByName("AudioManager");
                _gameplayDataSettingsType = AccessTools.TypeByName("Duckov.Utilities.GameplayDataSettings");

                // 缓存 ItemAgent_Gun 的属性和字段
                if (_itemAgentGunType != null)
                {
                    _gunItemSettingProperty = AccessTools.Property(_itemAgentGunType, "GunItemSetting");
                    _muzzleProperty = AccessTools.Property(_itemAgentGunType, "muzzle");
                    _silencedProperty = AccessTools.Property(_itemAgentGunType, "Silenced");
                    _bulletSpeedProperty = AccessTools.Property(_itemAgentGunType, "BulletSpeed");
                    _bulletDistanceProperty = AccessTools.Property(_itemAgentGunType, "BulletDistance");
                    _shellParticleField = AccessTools.Field(_itemAgentGunType, "shellParticle");
                }

                // 缓存 ItemSetting_Gun 的字段
                if (_itemSettingGunType != null)
                {
                    _muzzleFxPfbField = AccessTools.Field(_itemSettingGunType, "muzzleFxPfb");
                    _shootKeyField = AccessTools.Field(_itemSettingGunType, "shootKey");
                    _bulletPfbField = AccessTools.Field(_itemSettingGunType, "bulletPfb");
                }

                // 缓存默认子弹
                if (_gameplayDataSettingsType != null)
                {
                    _prefabsProperty = AccessTools.Property(_gameplayDataSettingsType, "Prefabs");
                    if (_prefabsProperty != null)
                    {
                        object? prefabs = _prefabsProperty.GetValue(null);
                        if (prefabs != null)
                        {
                            _defaultBulletField = AccessTools.Field(prefabs.GetType(), "DefaultBullet");
                        }
                    }
                }

                // 缓存 AudioManager.Post 方法
                if (_audioManagerType != null)
                {
                    _audioManagerPostMethod = AccessTools.Method(_audioManagerType, "Post", 
                        new[] { typeof(string), typeof(Vector3) });
                }

                _initialized = true;
                Debug.Log("[WeaponEffectsCore] ✅ 初始化完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsCore] 初始化失败: {ex.Message}");
            }
        }

        #region 通用特效播放方法

        /// <summary>
        /// 播放枪口火焰特效
        /// </summary>
        public static void PlayMuzzleFlash(object gunAgent)
        {
            try
            {
                EnsureInitialized();
                
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gunAgent);
                if (gunItemSetting == null) return;

                GameObject? muzzleFxPfb = _muzzleFxPfbField?.GetValue(gunItemSetting) as GameObject;
                Transform? muzzle = _muzzleProperty?.GetValue(gunAgent) as Transform;

                if (muzzleFxPfb != null && muzzle != null)
                {
                    GameObject.Instantiate(muzzleFxPfb, muzzle.position, muzzle.rotation);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeaponEffectsCore] 播放枪口火焰失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放弹壳抛出特效
        /// </summary>
        public static void PlayShellEjection(object gunAgent)
        {
            try
            {
                EnsureInitialized();
                
                ParticleSystem? shellParticle = _shellParticleField?.GetValue(gunAgent) as ParticleSystem;
                shellParticle?.Emit(1);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeaponEffectsCore] 播放弹壳失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放开枪音效（3D空间音效）
        /// </summary>
        public static void PlayShootSound(object gunAgent, Vector3 position, bool? isSilencedOverride = null)
        {
            try
            {
                EnsureInitialized();
                
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gunAgent);
                if (gunItemSetting == null) return;

                string? shootKey = _shootKeyField?.GetValue(gunItemSetting) as string;
                if (string.IsNullOrEmpty(shootKey)) return;

                // 构建音效路径
                string soundPath = $"SFX/Combat/Gun/Shoot/{shootKey}";
                
                // 检查消音器（可以被外部参数覆盖）
                bool isSilenced = isSilencedOverride ?? (bool)(_silencedProperty?.GetValue(gunAgent) ?? false);
                if (isSilenced)
                {
                    soundPath += "_mute";
                }

                // 播放 3D 空间音效
                _audioManagerPostMethod?.Invoke(null, new object[] { soundPath, position });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeaponEffectsCore] 播放音效失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建子弹（从对象池获取）
        /// </summary>
        public static void CreateBullet(object gunAgent, Vector3 muzzlePosition, Vector3 direction, 
            object? sourceCharacter = null, float damageMultiplier = 1.0f)
        {
            try
            {
                EnsureInitialized();
                
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gunAgent);
                if (gunItemSetting == null) return;

                // 获取子弹预制体
                object? bulletPfb = GetBulletPrefab(gunItemSetting);
                if (bulletPfb == null) return;

                Transform? muzzle = _muzzleProperty?.GetValue(gunAgent) as Transform;
                if (muzzle == null) return;

                // 从对象池获取子弹
                object? bullet = GetBulletFromPool(bulletPfb);
                if (bullet is Component bulletComponent)
                {
                    bulletComponent.transform.position = muzzlePosition;
                    bulletComponent.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                    
                    // 初始化子弹
                    InitializeBullet(bullet, gunAgent, muzzlePosition, direction, sourceCharacter, damageMultiplier);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeaponEffectsCore] 创建子弹失败: {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取子弹预制体（优先使用自定义，否则使用默认）
        /// </summary>
        private static object? GetBulletPrefab(object gunItemSetting)
        {
            object? bulletPfb = _bulletPfbField?.GetValue(gunItemSetting);
            
            // 如果没有自定义子弹，使用默认子弹
            if (bulletPfb == null && _prefabsProperty != null)
            {
                object? prefabs = _prefabsProperty.GetValue(null);
                if (prefabs != null && _defaultBulletField != null)
                {
                    bulletPfb = _defaultBulletField.GetValue(prefabs);
                }
            }
            
            return bulletPfb;
        }

        /// <summary>
        /// 从对象池获取子弹
        /// </summary>
        private static object? GetBulletFromPool(object bulletPfb)
        {
            if (_levelManagerType == null) return null;

            var instanceProperty = AccessTools.Property(_levelManagerType, "Instance");
            object? levelManager = instanceProperty?.GetValue(null);
            if (levelManager == null) return null;

            var bulletPoolProperty = AccessTools.Property(_levelManagerType, "BulletPool");
            object? bulletPool = bulletPoolProperty?.GetValue(levelManager);
            if (bulletPool == null) return null;

            var getABulletMethod = AccessTools.Method(bulletPool.GetType(), "GetABullet");
            return getABulletMethod?.Invoke(bulletPool, new object[] { bulletPfb });
        }

        /// <summary>
        /// 初始化子弹的 ProjectileContext
        /// </summary>
        private static bool InitializeBullet(object bullet, object gunAgent, Vector3 muzzlePosition, 
            Vector3 direction, object? sourceCharacter, float damageMultiplier)
        {
            try
            {
                var projectileContextType = AccessTools.TypeByName("ProjectileContext");
                if (projectileContextType == null) return false;

                object? context = Activator.CreateInstance(projectileContextType);
                if (context == null) return false;

                // 获取子弹参数（已包含角色加成）
                float bulletSpeed = GetBulletSpeed(gunAgent);
                float bulletDistance = GetBulletDistance(gunAgent);

                // 设置基础参数
                AccessTools.Field(projectileContextType, "direction")?.SetValue(context, direction);
                AccessTools.Field(projectileContextType, "speed")?.SetValue(context, bulletSpeed);
                AccessTools.Field(projectileContextType, "distance")?.SetValue(context, bulletDistance + 0.4f);
                AccessTools.Field(projectileContextType, "damage")?.SetValue(context, 25f * damageMultiplier);

                // 设置第一帧检测
                AccessTools.Field(projectileContextType, "firstFrameCheck")?.SetValue(context, true);
                AccessTools.Field(projectileContextType, "firstFrameCheckStartPoint")?.SetValue(context, muzzlePosition);

                // 设置伤害参数
                AccessTools.Field(projectileContextType, "halfDamageDistance")?.SetValue(context, 50f);
                AccessTools.Field(projectileContextType, "critRate")?.SetValue(context, 0.1f * damageMultiplier);
                AccessTools.Field(projectileContextType, "critDamageFactor")?.SetValue(context, 2.0f);
                AccessTools.Field(projectileContextType, "armorPiercing")?.SetValue(context, 0f);
                AccessTools.Field(projectileContextType, "armorBreak")?.SetValue(context, 0f);
                AccessTools.Field(projectileContextType, "penetrate")?.SetValue(context, 0);

                // 设置来源角色和队伍
                if (sourceCharacter != null)
                {
                    AccessTools.Field(projectileContextType, "fromCharacter")?.SetValue(context, sourceCharacter);
                    
                    if (_characterMainControlType != null)
                    {
                        var teamProperty = AccessTools.Property(_characterMainControlType, "Team");
                        object? team = teamProperty?.GetValue(sourceCharacter);
                        if (team != null)
                        {
                            AccessTools.Field(projectileContextType, "team")?.SetValue(context, team);
                        }
                    }
                }

                // 调用 Projectile.Init() 方法
                var initMethod = AccessTools.Method(bullet.GetType(), "Init", new Type[] { projectileContextType });
                if (initMethod != null)
                {
                    initMethod.Invoke(bullet, new object[] { context });
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsCore] 初始化子弹失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取子弹速度（含角色加成）
        /// </summary>
        private static float GetBulletSpeed(object gunAgent)
        {
            if (_bulletSpeedProperty != null)
            {
                object? speedValue = _bulletSpeedProperty.GetValue(gunAgent);
                if (speedValue is float speed)
                {
                    return speed;
                }
            }
            return 100f; // 默认值
        }

        /// <summary>
        /// 获取子弹飞行距离（含角色加成）
        /// </summary>
        private static float GetBulletDistance(object gunAgent)
        {
            if (_bulletDistanceProperty != null)
            {
                object? distanceValue = _bulletDistanceProperty.GetValue(gunAgent);
                if (distanceValue is float distance)
                {
                    return distance;
                }
            }
            return 200f; // 默认值
        }

        /// <summary>
        /// 确保已初始化
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_initialized) Initialize();
        }

        #endregion

        #region 公开属性（供外部查询）

        public static Type? CharacterMainControlType => _characterMainControlType;
        public static Type? ItemAgentGunType => _itemAgentGunType;
        public static bool IsInitialized => _initialized;

        #endregion
    }
}

