using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 武器开枪特效播放器
    /// 用于远程玩家的开枪特效（枪口火焰、弹壳、音效、子弹）
    /// </summary>
    public static class WeaponFireEffectsPlayer
    {
        // 缓存反射类型
        private static Type? _itemAgentGunType;
        private static Type? _itemSettingGunType;
        private static Type? _gameplayDataSettingsType;
        private static Type? _audioManagerType;
        private static Type? _levelManagerType;
        private static Type? _characterMainControlType;

        // ✅ 缓存反射成员（避免每次调用都查找，提升性能）
        private static PropertyInfo? _gunItemSettingProperty;
        private static PropertyInfo? _muzzleProperty;
        private static FieldInfo? _muzzleFxPfbField;
        private static FieldInfo? _shellParticleField;
        private static FieldInfo? _shootKeyField;
        private static FieldInfo? _bulletPfbField;
        private static PropertyInfo? _prefabsProperty;
        private static FieldInfo? _defaultBulletField;
        private static MethodInfo? _audioManagerPostMethod;
        
        // 🔥 子弹参数属性（从 ItemAgent_Gun 获取，已包含角色加成）
        private static PropertyInfo? _bulletSpeedProperty;
        private static PropertyInfo? _bulletDistanceProperty;

        private static bool _initialized = false;

        /// <summary>
        /// 初始化反射成员（在 ModBehaviour 启动时调用）
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // 缓存类型
                _itemAgentGunType = AccessTools.TypeByName("ItemAgent_Gun");
                _itemSettingGunType = AccessTools.TypeByName("ItemSetting_Gun");
                _gameplayDataSettingsType = AccessTools.TypeByName("Duckov.Utilities.GameplayDataSettings");
                _audioManagerType = AccessTools.TypeByName("AudioManager");
                _levelManagerType = AccessTools.TypeByName("LevelManager");
                _characterMainControlType = AccessTools.TypeByName("CharacterMainControl");

                // ✅ 预先缓存所有反射成员
                if (_itemAgentGunType != null)
                {
                    _gunItemSettingProperty = AccessTools.Property(_itemAgentGunType, "GunItemSetting");
                    _muzzleProperty = AccessTools.Property(_itemAgentGunType, "muzzle");
                    _shellParticleField = AccessTools.Field(_itemAgentGunType, "shellParticle");
                    
                    // 🔥 缓存子弹参数属性（已包含角色加成）
                    _bulletSpeedProperty = AccessTools.Property(_itemAgentGunType, "BulletSpeed");
                    _bulletDistanceProperty = AccessTools.Property(_itemAgentGunType, "BulletDistance");
                }

                if (_itemSettingGunType != null)
                {
                    _muzzleFxPfbField = AccessTools.Field(_itemSettingGunType, "muzzleFxPfb");
                    _shootKeyField = AccessTools.Field(_itemSettingGunType, "shootKey");
                    _bulletPfbField = AccessTools.Field(_itemSettingGunType, "bulletPfb");
                }

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

                // ✅ 缓存 AudioManager.Post(string, Vector3) 方法
                if (_audioManagerType != null)
                {
                    _audioManagerPostMethod = AccessTools.Method(_audioManagerType, "Post", 
                        new[] { typeof(string), typeof(Vector3) });
                }

                _initialized = true;
                Debug.Log("[WeaponFireEffectsPlayer] ✅ 初始化完成（已缓存反射成员）");
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
                if (_itemAgentGunType == null || !_itemAgentGunType.IsInstanceOfType(currentAgent))
                {
                    Debug.Log("[WeaponFireEffectsPlayer] 当前手持武器不是枪械类型");
                    return;
                }

                // 🔍 调试日志：接收到的 fireData
                Debug.Log($"[WeaponFireEffectsPlayer] 🎬 开始播放开枪特效");
                Debug.Log($"    • fireData.MuzzlePosition: ({fireData.MuzzlePositionX:F3}, {fireData.MuzzlePositionY:F3}, {fireData.MuzzlePositionZ:F3})");
                Debug.Log($"    • fireData.MuzzleDirection: ({fireData.MuzzleDirectionX:F3}, {fireData.MuzzleDirectionY:F3}, {fireData.MuzzleDirectionZ:F3})");
                Debug.Log($"    • fireData.IsSilenced: {fireData.IsSilenced}");

                // 转换位置和方向
                Vector3 muzzlePos = new Vector3(fireData.MuzzlePositionX, fireData.MuzzlePositionY, fireData.MuzzlePositionZ);
                Vector3 muzzleDir = new Vector3(fireData.MuzzleDirectionX, fireData.MuzzleDirectionY, fireData.MuzzleDirectionZ);
                
                Debug.Log($"    • 转换后 muzzlePos: {muzzlePos}");
                Debug.Log($"    • 转换后 muzzleDir: {muzzleDir} (magnitude: {muzzleDir.magnitude:F3})");

                // 1. 播放枪口火焰
                PlayMuzzleFlash(currentAgent, muzzlePos);

                // 2. 播放弹壳抛出
                PlayShellEjection(currentAgent);

                // 3. 播放开枪音效
                PlayShootSound(currentAgent, muzzlePos, fireData.IsSilenced);

                // 4. 创建子弹
                CreateBullet(currentAgent, muzzlePos, muzzleDir);

                Debug.Log("[WeaponFireEffectsPlayer] ✅ 开枪特效播放完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponFireEffectsPlayer] 播放特效失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放枪口火焰
        /// </summary>
        private static void PlayMuzzleFlash(object gunAgent, Vector3 muzzlePosition)
        {
            try
            {
                // ✅ 使用缓存的成员
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gunAgent);

                if (gunItemSetting != null)
                {
                    GameObject? muzzleFxPfb = _muzzleFxPfbField?.GetValue(gunItemSetting) as GameObject;

                    if (muzzleFxPfb != null)
                    {
                        Transform? muzzle = _muzzleProperty?.GetValue(gunAgent) as Transform;

                        if (muzzle != null)
                        {
                            // ✅ 直接调用 Unity API，不使用反射
                            GameObject.Instantiate(muzzleFxPfb, muzzle.position, muzzle.rotation);
                            Debug.Log("[WeaponFireEffectsPlayer] ✅ 枪口火焰已播放");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeaponFireEffectsPlayer] 播放枪口火焰失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放弹壳抛出
        /// </summary>
        private static void PlayShellEjection(object gunAgent)
        {
            try
            {
                // ✅ 使用缓存的成员
                ParticleSystem? shellParticle = _shellParticleField?.GetValue(gunAgent) as ParticleSystem;

                if (shellParticle != null)
                {
                    // 发射一个弹壳粒子
                    shellParticle.Emit(1);
                    Debug.Log("[WeaponFireEffectsPlayer] ✅ 弹壳已抛出");
                    
                    // ℹ️ 弹壳的生命周期由粒子系统的 Lifetime 设置决定
                    // 如果弹壳不消失，可能是粒子系统配置问题：
                    // - 检查 Start Lifetime 是否设置了合理的值（如 2-5 秒）
                    // - 检查 Stop Action 是否为 Destroy
                    // - 可能需要在游戏中检查粒子系统的配置
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeaponFireEffectsPlayer] 播放弹壳失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放开枪音效（使用 FMOD AudioManager）
        /// </summary>
        private static void PlayShootSound(object gunAgent, Vector3 position, bool isSilenced)
        {
            try
            {
                // ✅ 使用缓存的成员
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gunAgent);

                if (gunItemSetting == null)
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] 无法获取 GunItemSetting");
                    return;
                }

                string shootKey = _shootKeyField?.GetValue(gunItemSetting) as string ?? "";

                if (string.IsNullOrEmpty(shootKey))
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] shootKey 为空");
                    return;
                }

                // 🔥 FMOD 音效路径格式: event:/SFX/Combat/Gun/Shoot/{key}
                string soundPath = $"event:/SFX/Combat/Gun/Shoot/{shootKey}";
                if (isSilenced)
                {
                    soundPath += "_mute";
                }

                // ✅ 使用 AudioManager.Post(string, Vector3) - 3D空间音效
                if (_audioManagerPostMethod != null)
                {
                    _audioManagerPostMethod.Invoke(null, new object[] { soundPath, position });
                    Debug.Log($"[WeaponFireEffectsPlayer] ✅ 音效已播放: {soundPath}");
                }
                else
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] AudioManager.Post 方法未找到");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeaponFireEffectsPlayer] 播放音效失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建子弹（从对象池获取并初始化）
        /// </summary>
        private static void CreateBullet(object gunAgent, Vector3 position, Vector3 direction)
        {
            try
            {
                // ✅ 使用缓存的成员
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gunAgent);

                if (gunItemSetting == null)
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] GunItemSetting 为空");
                    return;
                }

                // 获取子弹预制体
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

                if (bulletPfb == null)
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] 找不到子弹预制体");
                    return;
                }

                // 获取枪口 Transform
                Transform? muzzle = _muzzleProperty?.GetValue(gunAgent) as Transform;
                if (muzzle == null)
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] 无法获取枪口 Transform");
                    return;
                }

                // 🔥 从对象池获取子弹（不要直接 Instantiate）
                if (_levelManagerType != null)
                {
                    var instanceProperty = AccessTools.Property(_levelManagerType, "Instance");
                    object? levelManager = instanceProperty?.GetValue(null);
                    
                    if (levelManager != null)
                    {
                        var bulletPoolProperty = AccessTools.Property(_levelManagerType, "BulletPool");
                        object? bulletPool = bulletPoolProperty?.GetValue(levelManager);
                        
                        if (bulletPool != null)
                        {
                            var getABulletMethod = AccessTools.Method(bulletPool.GetType(), "GetABullet");
                            if (getABulletMethod != null)
                            {
                                // 从对象池获取子弹
                                object? bullet = getABulletMethod.Invoke(bulletPool, new object[] { bulletPfb });
                                
                                if (bullet != null && bullet is Component bulletComponent)
                                {
                                    // 🔥 设置子弹位置和旋转（使用散射后的方向）
                                    bulletComponent.transform.position = muzzle.position;
                                    bulletComponent.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                                    
                                    // ⭐ 关键：构建 ProjectileContext 并初始化子弹（传入散射后的方向）
                                    if (!InitializeBullet(bullet, gunAgent, muzzle.position, direction))
                                    {
                                        Debug.LogWarning("[WeaponFireEffectsPlayer] 子弹初始化失败，子弹可能无法飞行");
                                    }
                                    else
                                    {
                                        Debug.Log($"[WeaponFireEffectsPlayer] ✅ 子弹已创建并初始化");
                                        Debug.Log($"    • 位置: {muzzle.position}");
                                        Debug.Log($"    • 方向（含散射）: {direction}");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] LevelManager 未找到");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeaponFireEffectsPlayer] 创建子弹失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化子弹的 ProjectileContext（参考 WeaponEffectsPlayer）
        /// </summary>
        /// <param name="bullet">子弹对象</param>
        /// <param name="gun">枪械对象</param>
        /// <param name="muzzlePosition">枪口位置</param>
        /// <param name="scatteredDirection">散射后的真实方向</param>
        private static bool InitializeBullet(object bullet, object gun, Vector3 muzzlePosition, Vector3 scatteredDirection)
        {
            try
            {
                // 获取 ProjectileContext 类型
                var projectileContextType = AccessTools.TypeByName("ProjectileContext");
                if (projectileContextType == null)
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] 找不到 ProjectileContext 类型");
                    return false;
                }

                // 创建 ProjectileContext 实例
                object? context = Activator.CreateInstance(projectileContextType);
                if (context == null) return false;

                // 获取远程玩家角色（用于获取队伍信息）
                GameObject? characterObject = null;
                if (gun is Component gunComponent)
                {
                    // 从枪械往上找到角色对象
                    characterObject = gunComponent.transform.root.gameObject;
                }

                // 🔥 从 ItemAgent_Gun 获取真实参数（已包含角色加成）
                float bulletSpeed = 100f;     // 默认值
                float bulletDistance = 200f;  // 默认值
                
                // ✅ 使用缓存的属性获取参数
                if (_bulletSpeedProperty != null)
                {
                    object? speedValue = _bulletSpeedProperty.GetValue(gun);
                    if (speedValue is float speed)
                    {
                        bulletSpeed = speed;
                        Debug.Log($"[WeaponFireEffectsPlayer] 子弹速度（含加成）: {bulletSpeed}");
                    }
                }
                
                if (_bulletDistanceProperty != null)
                {
                    object? distanceValue = _bulletDistanceProperty.GetValue(gun);
                    if (distanceValue is float distance)
                    {
                        bulletDistance = distance;
                        Debug.Log($"[WeaponFireEffectsPlayer] 子弹距离（含加成）: {bulletDistance}");
                    }
                }

                // 设置基础参数（使用从枪械获取的真实值）
                AccessTools.Field(projectileContextType, "direction")?.SetValue(context, scatteredDirection); // 🔥 使用散射后的方向
                AccessTools.Field(projectileContextType, "speed")?.SetValue(context, bulletSpeed);
                AccessTools.Field(projectileContextType, "distance")?.SetValue(context, bulletDistance + 0.4f); // ✅ 加上偏移量
                AccessTools.Field(projectileContextType, "damage")?.SetValue(context, 0f); // ⚠️ 远程子弹伤害设为0，避免重复伤害

                // 设置队伍（从角色获取）
                if (characterObject != null && _characterMainControlType != null)
                {
                    var characterMainControl = characterObject.GetComponent<CharacterMainControl>();
                    if (characterMainControl != null)
                    {
                        var teamProperty = AccessTools.Property(_characterMainControlType, "Team");
                        object? team = teamProperty?.GetValue(characterMainControl);
                        if (team != null)
                        {
                            AccessTools.Field(projectileContextType, "team")?.SetValue(context, team);
                        }
                    }
                }

                // 设置第一帧检测（用于近距离命中检测）
                AccessTools.Field(projectileContextType, "firstFrameCheck")?.SetValue(context, true);
                AccessTools.Field(projectileContextType, "firstFrameCheckStartPoint")?.SetValue(context, muzzlePosition);

                // 其他参数（远程子弹只用于视觉，伤害相关参数可忽略）
                AccessTools.Field(projectileContextType, "halfDamageDistance")?.SetValue(context, 50f);
                AccessTools.Field(projectileContextType, "critRate")?.SetValue(context, 0f);
                AccessTools.Field(projectileContextType, "critDamageFactor")?.SetValue(context, 1.0f);
                AccessTools.Field(projectileContextType, "armorPiercing")?.SetValue(context, 0f);
                AccessTools.Field(projectileContextType, "armorBreak")?.SetValue(context, 0f);
                AccessTools.Field(projectileContextType, "penetrate")?.SetValue(context, 0);

                // 设置来源角色（远程玩家）
                if (characterObject != null)
                {
                    var characterMainControl = characterObject.GetComponent<CharacterMainControl>();
                    AccessTools.Field(projectileContextType, "fromCharacter")?.SetValue(context, characterMainControl);
                }

                // 调用 Projectile.Init() 方法
                var initMethod = AccessTools.Method(bullet.GetType(), "Init", new Type[] { projectileContextType });
                if (initMethod != null)
                {
                    initMethod.Invoke(bullet, new object[] { context });
                    Debug.Log("[WeaponFireEffectsPlayer] ✅ 子弹已通过 Init() 初始化");
                    return true;
                }
                else
                {
                    Debug.LogWarning("[WeaponFireEffectsPlayer] 找不到 Projectile.Init() 方法");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponFireEffectsPlayer] 初始化子弹失败: {ex.Message}");
                return false;
            }
        }
    }
}

