using System;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.Players;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 远程玩家生成器调试模块 - 用于手动创建测试用的远程玩家
    /// </summary>
    public class RemotePlayerSpawnerModule : IDebugModule
    {
        public string ModuleName => "远程玩家生成器";
        public string Category => "测试";
        public string Description => "手动创建测试用的远程玩家单位";
        public bool IsEnabled { get; set; } = true;

        private string _playerName = "TestPlayer";
        private string _steamId = "76561199999999999";
        private Vector3 _spawnPosition = Vector3.zero;
        private int _testPlayerCount = 0;
        private GameObject? _lastCreatedCharacter = null;
        private Vector3 _lastKnownPosition = Vector3.zero;
        
        // 🔥 新增：头像相关
        private bool _useCustomAvatar = false;
        private Texture2D? _customAvatarTexture = null;
        private string _avatarColorR = "255";
        private string _avatarColorG = "100";
        private string _avatarColorB = "100";

        public RemotePlayerSpawnerModule()
        {
        }

        public void OnGUI()
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("游戏上下文未初始化", GUI.skin.label);
                return;
            }

            GUILayout.BeginVertical("box");
            
            // 标题
            GUILayout.Label("═══ 远程玩家生成器 ═══", new GUIStyle(GUI.skin.label) 
            { 
                fontSize = 14, 
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            });
            
            GUILayout.Space(10);

            // 玩家名称输入
            GUILayout.BeginHorizontal();
            GUILayout.Label("玩家名称:", GUILayout.Width(80));
            _playerName = GUILayout.TextField(_playerName, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            // SteamID 输入
            GUILayout.BeginHorizontal();
            GUILayout.Label("Steam ID:", GUILayout.Width(80));
            _steamId = GUILayout.TextField(_steamId, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            
            // 🔥 头像设置
            GUILayout.Label("头像设置:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            
            _useCustomAvatar = GUILayout.Toggle(_useCustomAvatar, "使用自定义头像");
            
            if (_useCustomAvatar)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("颜色 RGB:", GUILayout.Width(80));
                GUILayout.Label("R:", GUILayout.Width(20));
                _avatarColorR = GUILayout.TextField(_avatarColorR, 3, GUILayout.Width(40));
                GUILayout.Label("G:", GUILayout.Width(20));
                _avatarColorG = GUILayout.TextField(_avatarColorG, 3, GUILayout.Width(40));
                GUILayout.Label("B:", GUILayout.Width(20));
                _avatarColorB = GUILayout.TextField(_avatarColorB, 3, GUILayout.Width(40));
                GUILayout.EndHorizontal();
                
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("生成头像", GUILayout.Width(100)))
                {
                    GenerateCustomAvatar();
                }
                if (_customAvatarTexture != null)
                {
                    GUILayout.Label("✓ 头像已生成", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.green } });
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // 生成位置输入
            GUILayout.Label("生成位置:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("X:", GUILayout.Width(20));
            if (float.TryParse(GUILayout.TextField(_spawnPosition.x.ToString("F2"), GUILayout.Width(60)), out float x))
                _spawnPosition.x = x;
            
            GUILayout.Label("Y:", GUILayout.Width(20));
            if (float.TryParse(GUILayout.TextField(_spawnPosition.y.ToString("F2"), GUILayout.Width(60)), out float y))
                _spawnPosition.y = y;
            
            GUILayout.Label("Z:", GUILayout.Width(20));
            if (float.TryParse(GUILayout.TextField(_spawnPosition.z.ToString("F2"), GUILayout.Width(60)), out float z))
                _spawnPosition.z = z;
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // 快捷按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("原点(0,0,0)", GUILayout.Width(100)))
            {
                _spawnPosition = Vector3.zero;
            }
            if (GUILayout.Button("本地玩家位置", GUILayout.Width(120)))
            {
                SetPositionToLocalPlayer();
            }
            if (GUILayout.Button("相机前方", GUILayout.Width(100)))
            {
                SetPositionToFrontOfCamera();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 创建按钮
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("创建远程玩家", GUILayout.Height(40)))
            {
                CreateTestRemotePlayer();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // 快速创建按钮
            GUILayout.Label("快速创建:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("创建 3 个"))
            {
                CreateMultipleTestPlayers(3);
            }
            if (GUILayout.Button("创建 5 个"))
            {
                CreateMultipleTestPlayers(5);
            }
            if (GUILayout.Button("创建 10 个"))
            {
                CreateMultipleTestPlayers(10);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 统计信息
            GUILayout.Label($"已创建测试玩家数量: {_testPlayerCount}", new GUIStyle(GUI.skin.label) 
            { 
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            });

            GUILayout.Space(10);

            // 位置监控
            GUILayout.Label("位置监控:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
            
            if (_lastCreatedCharacter != null)
            {
                var currentPos = _lastCreatedCharacter.transform.position;
                GUILayout.Label($"最后创建的角色: {_lastCreatedCharacter.name}");
                GUILayout.Label($"当前位置: {currentPos:F3}");
                GUILayout.Label($"初始位置: {_lastKnownPosition:F3}");
                
                var distance = Vector3.Distance(currentPos, _lastKnownPosition);
                var color = distance > 0.1f ? Color.red : Color.green;
                GUILayout.Label($"移动距离: {distance:F3} 米", new GUIStyle(GUI.skin.label) 
                { 
                    normal = { textColor = color }
                });
                
                if (distance > 0.1f)
                {
                    GUILayout.Label("⚠️ 角色正在移动/掉落！", new GUIStyle(GUI.skin.label) 
                    { 
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = Color.red }
                    });
                }
            }
            else
            {
                GUILayout.Label("尚未创建角色");
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 创建测试用的远程玩家
        /// </summary>
        private void CreateTestRemotePlayer()
        {
            try
            {
                // 创建玩家信息
                var playerInfo = new PlayerInfo
                {
                    SteamId = _steamId,
                    SteamName = _playerName,
                    AvatarUrl = string.Empty,
                    CurrentScenelData = new Shared.Data.ScenelData("Base", "Base_SceneV2")
                };

                // 创建远程玩家对象
                var remotePlayer = new RemotePlayer(playerInfo);
                
                // 🔥 如果启用自定义头像,设置头像
                if (_useCustomAvatar && _customAvatarTexture != null)
                {
                    remotePlayer.SetAvatarTexture(_customAvatarTexture);
                    Debug.Log($"[RemotePlayerSpawnerModule] 已设置自定义头像");
                }

                // 🔥 创建角色模型 - 不传名字,让RemotePlayer从Info.SteamName自动获取
                bool success = remotePlayer.CreateCharacter(_spawnPosition);

                if (success)
                {
                    _testPlayerCount++;
                    Debug.Log($"[RemotePlayerSpawnerModule] 成功创建测试玩家: {_playerName} 在位置 {_spawnPosition}");
                    
                    // 打印角色上的所有组件
                    if (remotePlayer.CharacterObject != null)
                    {
                        PrintAllComponents(remotePlayer.CharacterObject);
                    }
                    
                    // 自动生成下一个玩家的信息
                    _playerName = $"TestPlayer{_testPlayerCount + 1}";
                    _steamId = $"7656119999999{_testPlayerCount:D4}";
                }
                else
                {
                    Debug.LogError($"[RemotePlayerSpawnerModule] 创建测试玩家失败: {_playerName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayerSpawnerModule] 创建测试玩家异常: {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        /// <summary>
        /// 生成自定义头像纹理
        /// </summary>
        private void GenerateCustomAvatar()
        {
            try
            {
                // 解析RGB值
                if (!byte.TryParse(_avatarColorR, out byte r)) r = 255;
                if (!byte.TryParse(_avatarColorG, out byte g)) g = 100;
                if (!byte.TryParse(_avatarColorB, out byte b)) b = 100;
                
                // 创建64x64的纯色头像
                _customAvatarTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                Color avatarColor = new Color(r / 255f, g / 255f, b / 255f, 1f);
                
                Color[] pixels = new Color[64 * 64];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = avatarColor;
                }
                
                _customAvatarTexture.SetPixels(pixels);
                _customAvatarTexture.Apply();
                
                Debug.Log($"[RemotePlayerSpawnerModule] 生成自定义头像: RGB({r}, {g}, {b})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayerSpawnerModule] 生成头像失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 打印游戏对象上的所有组件
        /// </summary>
        private void PrintAllComponents(GameObject gameObject)
        {
            if (gameObject == null)
            {
                Debug.LogWarning("[RemotePlayerSpawnerModule] GameObject 为空，无法打印组件");
                return;
            }

            Debug.Log($"[RemotePlayerSpawnerModule] ========== 角色组件列表: {gameObject.name} ==========");
            
            // 获取根对象的所有组件
            var components = gameObject.GetComponents<Component>();
            Debug.Log($"[RemotePlayerSpawnerModule] 根对象 '{gameObject.name}' 上的组件数量: {components.Length}");
            
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp != null)
                {
                    string enabledStatus = "";
                    if (comp is Behaviour behaviour)
                    {
                        enabledStatus = behaviour.enabled ? " [已启用]" : " [已禁用]";
                    }
                    Debug.Log($"[RemotePlayerSpawnerModule]   [{i}] {comp.GetType().FullName}{enabledStatus}");
                }
            }
            
            // 递归打印所有子对象的组件
            Debug.Log($"[RemotePlayerSpawnerModule] 检查子对象组件...");
            var allComponents = gameObject.GetComponentsInChildren<Component>(true);
            Debug.Log($"[RemotePlayerSpawnerModule] 所有组件总数（包括子对象）: {allComponents.Length}");
            
            // 按类型分组统计
            var componentTypes = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var comp in allComponents)
            {
                if (comp != null)
                {
                    string typeName = comp.GetType().Name;
                    if (componentTypes.ContainsKey(typeName))
                        componentTypes[typeName]++;
                    else
                        componentTypes[typeName] = 1;
                }
            }
            
            Debug.Log($"[RemotePlayerSpawnerModule] 组件类型统计:");
            foreach (var kvp in componentTypes)
            {
                Debug.Log($"[RemotePlayerSpawnerModule]   {kvp.Key}: {kvp.Value} 个");
            }
            
            Debug.Log($"[RemotePlayerSpawnerModule] ========================================");
        }

        /// <summary>
        /// 批量创建多个测试玩家
        /// </summary>
        private void CreateMultipleTestPlayers(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // 在原点周围随机生成位置
                Vector3 randomOffset = new Vector3(
                    UnityEngine.Random.Range(-5f, 5f),
                    0f,
                    UnityEngine.Random.Range(-5f, 5f)
                );
                _spawnPosition = randomOffset;
                
                CreateTestRemotePlayer();
            }
        }

        /// <summary>
        /// 设置位置为本地玩家位置
        /// </summary>
        private void SetPositionToLocalPlayer()
        {
            try
            {
                var localPlayer = GameContext.Instance.PlayerManager?.LocalPlayer;
                if (localPlayer?.CharacterObject != null)
                {
                    _spawnPosition = localPlayer.CharacterObject.transform.position;
                    Debug.Log($"[RemotePlayerSpawnerModule] 已设置位置为本地玩家位置: {_spawnPosition}");
                }
                else
                {
                    Debug.LogWarning("[RemotePlayerSpawnerModule] 本地玩家角色不存在");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayerSpawnerModule] 获取本地玩家位置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置位置为相机前方
        /// </summary>
        private void SetPositionToFrontOfCamera()
        {
            try
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    // 在相机前方 5 米处生成
                    _spawnPosition = mainCamera.transform.position + mainCamera.transform.forward * 5f;
                    Debug.Log($"[RemotePlayerSpawnerModule] 已设置位置为相机前方: {_spawnPosition}");
                }
                else
                {
                    Debug.LogWarning("[RemotePlayerSpawnerModule] 主相机不存在");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayerSpawnerModule] 获取相机位置失败: {ex.Message}");
            }
        }

        public void Update()
        {
            // 这个模块不需要每帧更新
        }
    }
}
