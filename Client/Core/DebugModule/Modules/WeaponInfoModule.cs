using System;
using System.Reflection;
using UnityEngine;
using DuckyNet.Client.Core.DebugModule;
using DuckyNet.Client.Core.Utils;
using HarmonyLib;

namespace DuckyNet.Client.Core.DebugModule.Modules
{
    /// <summary>
    /// 武器信息调试模块 - 显示当前手持武器的详细参数
    /// </summary>
    public class WeaponInfoModule : IDebugModule
    {
        public string ModuleName => "武器信息";
        public string Category => "玩家";
        public string Description => "显示当前手持武器的详细参数（特效、子弹、音效等）";
        public bool IsEnabled { get; set; } = true;

        // 缓存反射信息
        private static Type? _characterMainControlType;
        private static Type? _itemAgentGunType;
        private static Type? _itemSettingGunType;
        private static Type? _gameplayDataSettingsType;
        private static Type? _projectileType;
        
        private static PropertyInfo? _mainProperty;
        private static MethodInfo? _getGunMethod;
        private static PropertyInfo? _gunItemSettingProperty;
        private static FieldInfo? _shellParticleField;
        private static PropertyInfo? _muzzleProperty;
        private static PropertyInfo? _silencedProperty;

        // 武器信息缓存
        private string _weaponInfo = "等待获取武器信息...";
        private float _lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 0.5f; // 每0.5秒更新一次

        public WeaponInfoModule()
        {
            InitializeReflection();
            WeaponEffectsPlayer.Initialize();
        }

        /// <summary>
        /// 初始化反射类型和方法
        /// </summary>
        private void InitializeReflection()
        {
            try
            {
                // 获取类型
                _characterMainControlType = AccessTools.TypeByName("CharacterMainControl");
                _itemAgentGunType = AccessTools.TypeByName("ItemAgent_Gun");
                _itemSettingGunType = AccessTools.TypeByName("ItemSetting_Gun");
                _gameplayDataSettingsType = AccessTools.TypeByName("GameplayDataSettings");
                _projectileType = AccessTools.TypeByName("Projectile");

                if (_characterMainControlType != null)
                {
                    // CharacterMainControl.Main (静态属性)
                    _mainProperty = AccessTools.Property(_characterMainControlType, "Main");
                    
                    // CharacterMainControl.GetGun() 方法
                    _getGunMethod = AccessTools.Method(_characterMainControlType, "GetGun");
                }

                if (_itemAgentGunType != null)
                {
                    // ItemAgent_Gun.GunItemSetting 属性
                    _gunItemSettingProperty = AccessTools.Property(_itemAgentGunType, "GunItemSetting");
                    
                    // ItemAgent_Gun.shellParticle 私有字段
                    _shellParticleField = AccessTools.Field(_itemAgentGunType, "shellParticle");
                    
                    // ItemAgent_Gun.muzzle 属性
                    _muzzleProperty = AccessTools.Property(_itemAgentGunType, "muzzle");
                    
                    // ItemAgent_Gun.Silenced 属性
                    _silencedProperty = AccessTools.Property(_itemAgentGunType, "Silenced");
                }

                Debug.Log("[WeaponInfoModule] 反射初始化完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WeaponInfoModule] 反射初始化失败: {ex.Message}");
            }
        }

        public void Update()
        {
            // 每隔一段时间更新武器信息
            if (Time.time - _lastUpdateTime > UPDATE_INTERVAL)
            {
                _lastUpdateTime = Time.time;
                UpdateWeaponInfo();
            }
        }

        /// <summary>
        /// 更新武器信息
        /// </summary>
        private void UpdateWeaponInfo()
        {
            try
            {
                if (_mainProperty == null || _getGunMethod == null)
                {
                    _weaponInfo = "❌ 反射初始化失败";
                    return;
                }

                // 获取主角
                object? mainCharacter = _mainProperty.GetValue(null);
                if (mainCharacter == null)
                {
                    _weaponInfo = "⚠️ 主角不存在（可能未进入游戏）";
                    return;
                }

                // 获取当前手持的枪械
                object? gun = _getGunMethod.Invoke(mainCharacter, null);
                if (gun == null)
                {
                    _weaponInfo = "⚠️ 当前未持有枪械";
                    return;
                }

                // 构建武器信息
                var info = new System.Text.StringBuilder();
                info.AppendLine("═══════════════════════════");
                info.AppendLine("🔫 当前武器信息");
                info.AppendLine("═══════════════════════════");
                info.AppendLine();

                // 获取 GunItemSetting
                object? gunItemSetting = _gunItemSettingProperty?.GetValue(gun);
                if (gunItemSetting != null && _itemSettingGunType != null)
                {
                    // 1. 枪口火焰特效
                    var muzzleFxPfbField = AccessTools.Field(_itemSettingGunType, "muzzleFxPfb");
                    GameObject? muzzleFxPfb = muzzleFxPfbField?.GetValue(gunItemSetting) as GameObject;
                    info.AppendLine($"🔥 枪口特效: {muzzleFxPfb?.name ?? "无"}");

                    // 2. 子弹预制体
                    var bulletPfbField = AccessTools.Field(_itemSettingGunType, "bulletPfb");
                    object? bulletPfb = bulletPfbField?.GetValue(gunItemSetting);
                    
                    if (bulletPfb == null && _gameplayDataSettingsType != null)
                    {
                        // 获取默认子弹
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
                    
                    string bulletName = "无";
                    if (bulletPfb != null)
                    {
                        var nameProperty = bulletPfb.GetType().GetProperty("name");
                        bulletName = nameProperty?.GetValue(bulletPfb) as string ?? bulletPfb.ToString();
                    }
                    info.AppendLine($"💥 子弹预制体: {bulletName}");

                    // 3. 音效配置
                    var shootKeyField = AccessTools.Field(_itemSettingGunType, "shootKey");
                    var reloadKeyField = AccessTools.Field(_itemSettingGunType, "reloadKey");
                    
                    string shootKey = shootKeyField?.GetValue(gunItemSetting) as string ?? "未配置";
                    string reloadKey = reloadKeyField?.GetValue(gunItemSetting) as string ?? "未配置";
                    
                    bool isSilenced = (bool)(_silencedProperty?.GetValue(gun) ?? false);
                    string shootSoundPath = $"SFX/Combat/Gun/Shoot/{shootKey.ToLower()}";
                    if (isSilenced)
                    {
                        shootSoundPath += "_mute";
                    }
                    
                    info.AppendLine();
                    info.AppendLine("🔊 音效配置:");
                    info.AppendLine($"  • 开枪音效键: {shootKey}");
                    info.AppendLine($"  • 完整路径: {shootSoundPath}");
                    info.AppendLine($"  • 换弹音效键: {reloadKey}");
                    info.AppendLine($"  • 消音器: {(isSilenced ? "已安装" : "未安装")}");
                }
                else
                {
                    info.AppendLine("❌ 无法获取 GunItemSetting");
                }

                info.AppendLine();

                // 4. 枪口位置
                Transform? muzzleTransform = _muzzleProperty?.GetValue(gun) as Transform;
                if (muzzleTransform != null)
                {
                    info.AppendLine("📍 枪口位置:");
                    info.AppendLine($"  • 位置: {muzzleTransform.position}");
                    info.AppendLine($"  • 方向: {muzzleTransform.forward}");
                    info.AppendLine($"  • 名称: {muzzleTransform.name}");
                }
                else
                {
                    info.AppendLine("📍 枪口位置: 未找到");
                }

                info.AppendLine();

                // 5. 弹壳粒子系统（私有字段，需要反射）
                ParticleSystem? shellParticle = _shellParticleField?.GetValue(gun) as ParticleSystem;
                if (shellParticle != null)
                {
                    var main = shellParticle.main;
                    info.AppendLine("🎆 弹壳粒子系统:");
                    info.AppendLine($"  • 名称: {shellParticle.name}");
                    info.AppendLine($"  • 最大粒子数: {main.maxParticles}");
                    info.AppendLine($"  • 生命周期: {main.startLifetime.constant}s");
                    info.AppendLine($"  • 播放状态: {(shellParticle.isPlaying ? "播放中" : "已停止")}");
                }
                else
                {
                    info.AppendLine("🎆 弹壳粒子: 未配置");
                }

                info.AppendLine();

                // 6. 枪械基本信息
                var itemComponentField = AccessTools.Field(_itemAgentGunType, "item");
                if (itemComponentField != null)
                {
                    object? itemComponent = itemComponentField.GetValue(gun);
                    if (itemComponent != null)
                    {
                        var itemNameProperty = AccessTools.Property(itemComponent.GetType(), "ItemName");
                        string itemName = itemNameProperty?.GetValue(itemComponent) as string ?? "未知";
                        
                        info.AppendLine("ℹ️ 枪械信息:");
                        info.AppendLine($"  • 名称: {itemName}");
                    }
                }

                info.AppendLine("═══════════════════════════");

                _weaponInfo = info.ToString();
            }
            catch (Exception ex)
            {
                _weaponInfo = $"❌ 获取武器信息失败:\n{ex.Message}\n\n{ex.StackTrace}";
                Debug.LogError($"[WeaponInfoModule] 更新失败: {ex}");
            }
        }

        public void OnGUI()
        {
            if (!IsEnabled) return;

            GUILayout.BeginVertical("box");
            
            // 标题
            GUILayout.Label("🔫 武器参数查看器", new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            });

            GUILayout.Space(10);

            // 刷新按钮
            if (GUILayout.Button("🔄 立即刷新", GUILayout.Height(30)))
            {
                UpdateWeaponInfo();
            }

            GUILayout.Space(5);

            // 第二行：特效播放按钮组
            GUILayout.Label("🎬 特效测试:", new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            });

            // FMOD 3D 音效说明
            GUILayout.Label("💡 所有音效使用 FMOD 3D 空间音效系统", new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.3f, 0.8f, 1f) },
                fontStyle = FontStyle.Italic
            });
            GUILayout.Label("   • 音量随距离衰减 • 声音方向性 • 实时位置更新", new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            });

            GUILayout.Space(3);

            GUILayout.BeginHorizontal();
            
            // 播放枪口火焰
            if (GUILayout.Button("🔥 枪口火焰", GUILayout.Height(35)))
            {
                WeaponEffectsPlayer.PlayMuzzleFlash();
            }

            // 播放弹壳抛出
            if (GUILayout.Button("🎆 弹壳抛出", GUILayout.Height(35)))
            {
                WeaponEffectsPlayer.PlayShellEjection();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            // 播放开枪音效
            if (GUILayout.Button("🔊 开枪音效 (3D)", GUILayout.Height(35)))
            {
                WeaponEffectsPlayer.PlayShootSound();
            }

            // 创建子弹
            var oldBgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.5f, 0.3f); // 橙色警告
            
            if (GUILayout.Button("💥 创建子弹", GUILayout.Height(35)))
            {
                WeaponEffectsPlayer.CreateBullet();
            }
            
            GUI.backgroundColor = oldBgColor;

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            // 播放完整特效（不含子弹）
            oldBgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 1f); // 蓝色（安全）
            
            if (GUILayout.Button("✨ 完整特效 (无子弹)", GUILayout.Height(35)))
            {
                WeaponEffectsPlayer.PlayFullFireEffects(includeBullet: false);
            }
            
            GUI.backgroundColor = oldBgColor;

            // 播放完整特效（含子弹）
            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f); // 红色警告
            
            if (GUILayout.Button("⚠️ 完整特效 (含子弹)", GUILayout.Height(35)))
            {
                WeaponEffectsPlayer.PlayFullFireEffects(includeBullet: true);
            }
            
            GUI.backgroundColor = oldBgColor;

            GUILayout.EndHorizontal();

            // 子弹警告提示
            GUILayout.Label("⚠️ 含子弹的特效会造成真实伤害，请谨慎使用！", new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                normal = { textColor = new Color(1f, 0.3f, 0.3f) },
                fontStyle = FontStyle.Bold
            });

            GUILayout.Space(5);

            // 显示武器信息（使用滚动视图）
            GUILayout.BeginVertical("box");
            GUILayout.Label(_weaponInfo, new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = false,
                richText = true
            });
            GUILayout.EndVertical();

            GUILayout.Space(5);

            // 底部提示
            GUILayout.Label($"自动更新间隔: {UPDATE_INTERVAL}秒", new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.gray }
            });

            GUILayout.EndVertical();
        }
    }
}

