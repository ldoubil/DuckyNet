using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 武器特效播放器 - 封装武器特效播放逻辑
    /// </summary>
    public static class WeaponEffectsPlayer
    {
        // 缓存反射类型
        private static Type? _characterMainControlType;
        private static Type? _itemAgentGunType;
        private static Type? _itemSettingGunType;
        private static Type? _levelManagerType;
        private static Type? _audioManagerType;
        private static Type? _gameplayDataSettingsType;

        private static PropertyInfo? _mainProperty;
        private static MethodInfo? _getGunMethod;
        private static PropertyInfo? _gunItemSettingProperty;
        private static PropertyInfo? _muzzleProperty;
        private static PropertyInfo? _silencedProperty;
        private static FieldInfo? _shellParticleField;

        private static bool _initialized = false;

        /// <summary>
        /// 初始化反射
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                _characterMainControlType = AccessTools.TypeByName("CharacterMainControl");
                _itemAgentGunType = AccessTools.TypeByName("ItemAgent_Gun");
                _itemSettingGunType = AccessTools.TypeByName("ItemSetting_Gun");
                _levelManagerType = AccessTools.TypeByName("LevelManager");
                _audioManagerType = AccessTools.TypeByName("AudioManager");
                _gameplayDataSettingsType = AccessTools.TypeByName("GameplayDataSettings");

                if (_characterMainControlType != null)
                {
                    _mainProperty = AccessTools.Property(_characterMainControlType, "Main");
                    _getGunMethod = AccessTools.Method(_characterMainControlType, "GetGun");
                }

                if (_itemAgentGunType != null)
                {
                    _gunItemSettingProperty = AccessTools.Property(_itemAgentGunType, "GunItemSetting");
                    _muzzleProperty = AccessTools.Property(_itemAgentGunType, "muzzle");
                    _silencedProperty = AccessTools.Property(_itemAgentGunType, "Silenced");
                    _shellParticleField = AccessTools.Field(_itemAgentGunType, "shellParticle");
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

                PlayMuzzleFlash(gun);
                PlayShellEjection(gun);
                PlayShootSound(gun);
                
                if (includeBullet)
                {
                    CreateBullet(gun);
                    Debug.Log("[WeaponEffectsPlayer] ✅ 已播放完整开火特效（含子弹）");
                }
                else
                {
                    Debug.Log("[WeaponEffectsPlayer] ✅ 已播放完整开火特效（不含子弹）");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsPlayer] 播放特效失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放枪口火焰特效
        /// </summary>
        public static void PlayMuzzleFlash(object? gun = null)
        {
            try
            {
                if (!_initialized) Initialize();
                
                gun ??= GetCurrentGun();
                if (gun == null) return;

                // 获取枪口火焰预制体
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gun);
                if (gunItemSetting == null) return;

                var muzzleFxPfbField = AccessTools.Field(_itemSettingGunType, "muzzleFxPfb");
                GameObject? muzzleFxPfb = muzzleFxPfbField?.GetValue(gunItemSetting) as GameObject;

                // 获取枪口位置
                Transform? muzzle = _muzzleProperty?.GetValue(gun) as Transform;

                // 播放特效
                if (muzzleFxPfb != null && muzzle != null)
                {
                    GameObject fx = UnityEngine.Object.Instantiate(muzzleFxPfb, muzzle.position, muzzle.rotation);
                    fx.transform.SetParent(muzzle);
                    Debug.Log($"[WeaponEffectsPlayer] 🔥 已播放枪口火焰: {muzzleFxPfb.name}");
                }
                else
                {
                    Debug.LogWarning("[WeaponEffectsPlayer] 枪口火焰特效或枪口位置未找到");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsPlayer] 播放枪口火焰失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放弹壳抛出特效
        /// </summary>
        public static void PlayShellEjection(object? gun = null)
        {
            try
            {
                if (!_initialized) Initialize();
                
                gun ??= GetCurrentGun();
                if (gun == null) return;

                // 获取弹壳粒子系统（私有字段）
                ParticleSystem? shellParticle = _shellParticleField?.GetValue(gun) as ParticleSystem;

                if (shellParticle != null)
                {
                    shellParticle.Emit(1); // 发射一个弹壳
                    Debug.Log($"[WeaponEffectsPlayer] 🎆 已发射弹壳粒子");
                }
                else
                {
                    Debug.LogWarning("[WeaponEffectsPlayer] 弹壳粒子系统未找到");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsPlayer] 播放弹壳失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放开枪音效（使用 FMOD 3D 空间音效系统）
        /// </summary>
        /// <param name="gun">枪械对象（null 则使用当前持有的枪）</param>
        /// <remarks>
        /// FMOD 3D 音效系统特性：
        /// - 音量随距离自动衰减
        /// - 声音方向性（左右声道）
        /// - 附加到枪械 GameObject，音源位置实时更新
        /// - 音频监听器跟随主角（位置 + 向上偏移 2 米）
        /// - 支持遮挡和环境混响
        /// 
        /// 实现原理：
        /// 1. AudioManager.Post(eventName, gameObject) 播放音效
        /// 2. AudioObject.set3DAttributes() 设置音源的 3D 位置
        /// 3. AudioObject.FixedUpdate() 每帧更新移动音源的位置
        /// 4. AudioManager.UpdateListener() 更新监听器位置（跟随主角）
        /// </remarks>
        public static void PlayShootSound(object? gun = null)
        {
            try
            {
                if (!_initialized) Initialize();
                
                gun ??= GetCurrentGun();
                if (gun == null) return;

                // 获取音效键
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gun);
                if (gunItemSetting == null) return;

                var shootKeyField = AccessTools.Field(_itemSettingGunType, "shootKey");
                string? shootKey = shootKeyField?.GetValue(gunItemSetting) as string;

                if (string.IsNullOrEmpty(shootKey))
                {
                    Debug.LogWarning("[WeaponEffectsPlayer] 开枪音效键未配置");
                    return;
                }

                // 构建音效路径
                string soundPath = $"SFX/Combat/Gun/Shoot/{shootKey.ToLower()}";
                
                // 检查消音器
                bool isSilenced = (bool)(_silencedProperty?.GetValue(gun) ?? false);
                if (isSilenced)
                {
                    soundPath += "_mute";
                }

                // 播放 3D 空间音效（FMOD 系统）
                if (_audioManagerType != null)
                {
                    // AudioManager.Post(eventName, gameObject) - 3D 音效
                    // 音效会附加到 GameObject，位置实时更新，音量随距离衰减
                    var postMethod = AccessTools.Method(_audioManagerType, "Post", new Type[] { typeof(string), typeof(GameObject) });
                    
                    GameObject? gunGameObject = null;
                    if (gun is Component component)
                    {
                        gunGameObject = component.gameObject;
                    }

                    if (postMethod != null && gunGameObject != null)
                    {
                        // 调用 AudioManager.Post()，返回 EventInstance
                        object? eventInstance = postMethod.Invoke(null, new object[] { soundPath, gunGameObject });
                        
                        Debug.Log($"[WeaponEffectsPlayer] 🔊 已播放3D音效: {soundPath}");
                        Debug.Log($"    • 音源位置: {gunGameObject.transform.position}");
                        Debug.Log($"    • 附加对象: {gunGameObject.name}");
                        Debug.Log($"    • 衰减模式: FMOD 3D 空间音效（距离衰减）");
                    }
                    else
                    {
                        Debug.LogWarning("[WeaponEffectsPlayer] AudioManager.Post 方法未找到或 GameObject 为空");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsPlayer] 播放音效失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建子弹（从对象池获取并完整初始化）
        /// </summary>
        /// <remarks>
        /// 完整的子弹初始化流程：
        /// 1. 从对象池获取子弹预制体实例
        /// 2. 设置子弹位置和旋转
        /// 3. 构建 ProjectileContext（包含方向、速度、伤害等参数）
        /// 4. 调用 Projectile.Init(context) 初始化子弹运动
        /// 
        /// ⚠️ 警告：此方法会创建真实的子弹，可能造成伤害，仅用于调试测试
        /// </remarks>
        public static void CreateBullet(object? gun = null)
        {
            try
            {
                if (!_initialized) Initialize();
                
                gun ??= GetCurrentGun();
                if (gun == null) return;

                // 获取子弹预制体
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gun);
                if (gunItemSetting == null) return;

                var bulletPfbField = AccessTools.Field(_itemSettingGunType, "bulletPfb");
                object? bulletPfb = bulletPfbField?.GetValue(gunItemSetting);

                // 如果没有配置，使用默认子弹
                if (bulletPfb == null && _gameplayDataSettingsType != null)
                {
                    var prefabsProperty = AccessTools.Property(_gameplayDataSettingsType, "Prefabs");
                    if (prefabsProperty != null)
                    {
                        object? prefabs = prefabsProperty.GetValue(null);
                        if (prefabs != null)
                        {
                            var defaultBulletField = AccessTools.Field(prefabs.GetType(), "DefaultBullet");
                            bulletPfb = defaultBulletField?.GetValue(prefabs);
                        }
                    }
                }

                // 获取枪口位置
                Transform? muzzle = _muzzleProperty?.GetValue(gun) as Transform;
                if (muzzle == null)
                {
                    Debug.LogWarning("[WeaponEffectsPlayer] 枪口位置未找到");
                    return;
                }

                // 从对象池获取子弹
                if (bulletPfb != null && _levelManagerType != null)
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
                                object? bullet = getABulletMethod.Invoke(bulletPool, new object[] { bulletPfb });
                                
                                if (bullet != null && bullet is Component bulletComponent)
                                {
                                    // 设置子弹位置和旋转
                                    bulletComponent.transform.position = muzzle.position;
                                    bulletComponent.transform.rotation = Quaternion.LookRotation(muzzle.forward, Vector3.up);
                                    
                                    // ⭐ 关键：构建 ProjectileContext 并初始化子弹
                                    if (!InitializeBullet(bullet, gun, muzzle))
                                    {
                                        Debug.LogWarning("[WeaponEffectsPlayer] 子弹初始化失败，子弹可能无法飞行");
                                    }
                                    else
                                    {
                                        Debug.Log($"[WeaponEffectsPlayer] 💥 已创建并初始化子弹");
                                        Debug.Log($"    • 位置: {muzzle.position}");
                                        Debug.Log($"    • 方向: {muzzle.forward}");
                                        Debug.Log($"    • 预制体: {bulletPfb}");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[WeaponEffectsPlayer] 子弹预制体或 LevelManager 未找到");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsPlayer] 创建子弹失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 初始化子弹的 ProjectileContext
        /// </summary>
        private static bool InitializeBullet(object bullet, object gun, Transform muzzle)
        {
            try
            {
                // 获取 ProjectileContext 类型
                var projectileContextType = AccessTools.TypeByName("ProjectileContext");
                if (projectileContextType == null)
                {
                    Debug.LogWarning("[WeaponEffectsPlayer] 找不到 ProjectileContext 类型");
                    return false;
                }

                // 创建 ProjectileContext 实例
                object? context = Activator.CreateInstance(projectileContextType);
                if (context == null) return false;

                // 获取主角（用于获取队伍信息）
                object? mainCharacter = _mainProperty?.GetValue(null);

                // 设置基础参数
                AccessTools.Field(projectileContextType, "direction")?.SetValue(context, muzzle.forward);
                AccessTools.Field(projectileContextType, "speed")?.SetValue(context, 100f); // 默认速度 100
                AccessTools.Field(projectileContextType, "distance")?.SetValue(context, 200f); // 最大飞行距离 200 米
                AccessTools.Field(projectileContextType, "damage")?.SetValue(context, 25f); // 基础伤害 25

                // 设置队伍（从主角获取）
                if (mainCharacter != null && _characterMainControlType != null)
                {
                    var teamProperty = AccessTools.Property(_characterMainControlType, "Team");
                    object? team = teamProperty?.GetValue(mainCharacter);
                    if (team != null)
                    {
                        AccessTools.Field(projectileContextType, "team")?.SetValue(context, team);
                    }
                }

                // 设置第一帧检测（用于近距离命中检测）
                AccessTools.Field(projectileContextType, "firstFrameCheck")?.SetValue(context, true);
                AccessTools.Field(projectileContextType, "firstFrameCheckStartPoint")?.SetValue(context, muzzle.position);

                // 设置伤害衰减参数
                AccessTools.Field(projectileContextType, "halfDamageDistance")?.SetValue(context, 50f); // 50米开始衰减

                // 设置暴击参数
                AccessTools.Field(projectileContextType, "critRate")?.SetValue(context, 0.1f); // 10% 暴击率
                AccessTools.Field(projectileContextType, "critDamageFactor")?.SetValue(context, 2.0f); // 暴击伤害 2倍

                // 设置穿甲和穿透
                AccessTools.Field(projectileContextType, "armorPiercing")?.SetValue(context, 0f);
                AccessTools.Field(projectileContextType, "armorBreak")?.SetValue(context, 0f);
                AccessTools.Field(projectileContextType, "penetrate")?.SetValue(context, 0); // 不穿透

                // 设置来源角色
                AccessTools.Field(projectileContextType, "fromCharacter")?.SetValue(context, mainCharacter);

                // 调用 Projectile.Init() 方法
                var initMethod = AccessTools.Method(bullet.GetType(), "Init", new Type[] { projectileContextType });
                if (initMethod != null)
                {
                    initMethod.Invoke(bullet, new object[] { context });
                    Debug.Log("[WeaponEffectsPlayer] ✅ 子弹已通过 Init() 初始化");
                    return true;
                }
                else
                {
                    Debug.LogWarning("[WeaponEffectsPlayer] 找不到 Projectile.Init() 方法");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponEffectsPlayer] 初始化子弹失败: {ex.Message}");
                return false;
            }
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

