using System;
using System.Linq;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.Players;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 远程玩家调试模块 - 显示所有远程玩家信息并提供调试功能
    /// </summary>
    public class RemotePlayerDebugModule : IDebugModule
    {
        public string ModuleName => "远程玩家调试";
        public string Category => "调试";
        public string Description => "显示远程玩家信息,提供传送、显示/隐藏等调试功能";
        public bool IsEnabled { get; set; } = true;

        private Vector2 _scrollPosition = Vector2.zero;
        private bool _showDetails = true;
        private bool _autoRefresh = true;
        private float _refreshTimer = 0f;
        private float _refreshInterval = 0.5f; // 每0.5秒刷新一次

        public void OnGUI()
        {
            if (!GameContext.IsInitialized)
            {
                GUILayout.Label("游戏上下文未初始化", GUI.skin.label);
                return;
            }

            var playerManager = GameContext.Instance.PlayerManager;
            if (playerManager == null)
            {
                GUILayout.Label("玩家管理器未初始化", GUI.skin.label);
                return;
            }

            GUILayout.BeginVertical("box");
            
            // 标题
            GUILayout.Label("═══ 远程玩家调试 ═══", new GUIStyle(GUI.skin.label) 
            { 
                fontSize = 14, 
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            });
            
            GUILayout.Space(10);

            // 统计信息
            int remotePlayerCount = playerManager.RemotePlayers.Count();
            int withCharacter = playerManager.RemotePlayers.Count(p => p.CharacterObject != null);
            
            GUILayout.BeginHorizontal("box");
            GUILayout.Label($"远程玩家总数: {remotePlayerCount}", new GUIStyle(GUI.skin.label) 
            { 
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            });
            GUILayout.FlexibleSpace();
            GUILayout.Label($"有角色模型: {withCharacter}", new GUIStyle(GUI.skin.label) 
            { 
                normal = { textColor = withCharacter > 0 ? Color.green : Color.red }
            });
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // 控制选项
            GUILayout.BeginHorizontal();
            _showDetails = GUILayout.Toggle(_showDetails, "显示详细信息");
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "自动刷新");
            if (GUILayout.Button("手动刷新", GUILayout.Width(100)))
            {
                // 强制刷新
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 本地玩家信息
            if (playerManager.LocalPlayer?.CharacterObject != null)
            {
                GUILayout.Label("本地玩家:", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                var localPos = playerManager.LocalPlayer.CharacterObject.transform.position;
                GUILayout.Label($"位置: {localPos.x:F2}, {localPos.y:F2}, {localPos.z:F2}");
                GUILayout.Space(5);
            }

            if (remotePlayerCount == 0)
            {
                GUILayout.Label("当前没有远程玩家", new GUIStyle(GUI.skin.label) 
                { 
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                });
            }
            else
            {
                // 远程玩家列表（滚动视图）
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(400));
                
                foreach (var remotePlayer in playerManager.RemotePlayers)
                {
                    if (playerManager.LocalPlayer != null)
                    {
                        DrawRemotePlayerPanel(remotePlayer, playerManager.LocalPlayer);
                    }
                    GUILayout.Space(5);
                }
                
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        private void DrawRemotePlayerPanel(RemotePlayer remotePlayer, LocalPlayer localPlayer)
        {
            GUILayout.BeginVertical("box");
            
            // 玩家名称和状态
            GUILayout.BeginHorizontal();
            
            bool hasCharacter = remotePlayer.CharacterObject != null;
            Color statusColor = hasCharacter ? Color.green : Color.red;
            string statusText = hasCharacter ? "●" : "○";
            
            GUILayout.Label(statusText, new GUIStyle(GUI.skin.label) 
            { 
                fontSize = 16,
                normal = { textColor = statusColor }
            }, GUILayout.Width(20));
            
            GUILayout.Label(remotePlayer.Info.SteamName, new GUIStyle(GUI.skin.label) 
            { 
                fontSize = 12,
                fontStyle = FontStyle.Bold
            });
            
            GUILayout.FlexibleSpace();
            
            // Steam ID
            GUILayout.Label($"ID: {remotePlayer.Info.SteamId.Substring(0, 8)}...", 
                new GUIStyle(GUI.skin.label) { fontSize = 10 });
            
            GUILayout.EndHorizontal();

            // 场景信息
            string remoteSceneName = string.IsNullOrEmpty(remotePlayer.CurrentSceneName) 
                ? "未进入场景" 
                : remotePlayer.CurrentSceneName;
            string localSceneName = GameContext.Instance?.SceneClientManager?._scenelDataList?.SceneName ?? "未知";
            
            // 场景匹配状态
            bool sceneMatches = !string.IsNullOrEmpty(remotePlayer.CurrentSceneName) 
                && remotePlayer.CurrentSceneName == localSceneName;
            Color sceneColor = sceneMatches ? Color.green : Color.yellow;
            
            GUILayout.BeginHorizontal();
            GUILayout.Label($"场景: {remoteSceneName}", 
                new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = sceneColor } });
            
            if (!sceneMatches)
            {
                GUILayout.Label($"(本地: {localSceneName})", 
                    new GUIStyle(GUI.skin.label) { fontSize = 9, normal = { textColor = Color.gray } });
            }
            GUILayout.EndHorizontal();

            if (hasCharacter && _showDetails && remotePlayer.CharacterObject != null)
            {
                var characterPos = remotePlayer.CharacterObject.transform.position;
                var characterRot = remotePlayer.CharacterObject.transform.rotation.eulerAngles;
                
                // 位置信息
                GUILayout.BeginHorizontal();
                GUILayout.Label($"位置: ", GUILayout.Width(40));
                GUILayout.Label($"X: {characterPos.x:F2}", GUILayout.Width(80));
                GUILayout.Label($"Y: {characterPos.y:F2}", GUILayout.Width(80));
                GUILayout.Label($"Z: {characterPos.z:F2}", GUILayout.Width(80));
                GUILayout.EndHorizontal();

                // 旋转信息
                GUILayout.BeginHorizontal();
                GUILayout.Label($"旋转: ", GUILayout.Width(40));
                GUILayout.Label($"Y: {characterRot.y:F1}°", GUILayout.Width(80));
                GUILayout.EndHorizontal();

                // 距离信息
                if (localPlayer != null && localPlayer.CharacterObject != null)
                {
                    var localPos = localPlayer.CharacterObject.transform.position;
                    float distance = Vector3.Distance(localPos, characterPos);
                    Color distanceColor = distance < 10 ? Color.green : 
                                        distance < 50 ? Color.yellow : Color.red;
                    
                    GUILayout.Label($"距离: {distance:F2}米", new GUIStyle(GUI.skin.label) 
                    { 
                        normal = { textColor = distanceColor }
                    });
                }

                // 激活状态
                GUILayout.BeginHorizontal();
                GUILayout.Label($"激活: {remotePlayer.CharacterObject.activeSelf}", GUILayout.Width(100));
                GUILayout.Label($"Layer: {remotePlayer.CharacterObject.layer}", GUILayout.Width(80));
                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                // 操作按钮
                GUILayout.BeginHorizontal();
                
                // 传送到远程玩家
                if (GUILayout.Button("传送过去", GUILayout.Height(25)))
                {
                    if (localPlayer != null)
                    {
                        TeleportToRemotePlayer(remotePlayer, localPlayer);
                    }
                }
                
                // 传送远程玩家到自己
                if (GUILayout.Button("传送过来", GUILayout.Height(25)))
                {
                    if (localPlayer != null)
                    {
                        TeleportRemotePlayerToLocal(remotePlayer, localPlayer);
                    }
                }
                
                // 显示/隐藏
                string toggleText = remotePlayer.CharacterObject.activeSelf ? "隐藏" : "显示";
                if (GUILayout.Button(toggleText, GUILayout.Height(25)))
                {
                    ToggleRemotePlayerVisibility(remotePlayer);
                }
                
                // 销毁角色
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("销毁", GUILayout.Height(25), GUILayout.Width(60)))
                {
                    DestroyRemotePlayerCharacter(remotePlayer);
                }
                GUI.backgroundColor = Color.white;
                
                GUILayout.EndHorizontal();
            }
            else if (!hasCharacter)
            {
                GUILayout.Label("未创建角色模型", new GUIStyle(GUI.skin.label) 
                { 
                    normal = { textColor = Color.yellow }
                });
                
                if (GUILayout.Button("尝试创建角色", GUILayout.Height(25)))
                {
                    TryCreateRemotePlayerCharacter(remotePlayer);
                }
            }

            GUILayout.EndVertical();
        }

        private void TeleportToRemotePlayer(RemotePlayer remotePlayer, LocalPlayer localPlayer)
        {
            if (remotePlayer.CharacterObject == null || localPlayer?.CharacterObject == null)
            {
                Debug.LogWarning("[RemotePlayerDebugModule] 无法传送：角色对象不存在");
                return;
            }

            try
            {
                var targetPos = remotePlayer.CharacterObject.transform.position;
                localPlayer.CharacterObject.transform.position = targetPos + new Vector3(2, 0, 0); // 偏移2米避免重叠
                Debug.Log($"[RemotePlayerDebugModule] 已传送到 {remotePlayer.Info.SteamName} 附近");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayerDebugModule] 传送失败: {ex.Message}");
            }
        }

        private void TeleportRemotePlayerToLocal(RemotePlayer remotePlayer, LocalPlayer localPlayer)
        {
            if (remotePlayer.CharacterObject == null || localPlayer?.CharacterObject == null)
            {
                Debug.LogWarning("[RemotePlayerDebugModule] 无法传送：角色对象不存在");
                return;
            }

            try
            {
                var targetPos = localPlayer.CharacterObject.transform.position;
                remotePlayer.CharacterObject.transform.position = targetPos + new Vector3(-2, 0, 0); // 偏移-2米避免重叠
                Debug.Log($"[RemotePlayerDebugModule] 已将 {remotePlayer.Info.SteamName} 传送到身边");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayerDebugModule] 传送失败: {ex.Message}");
            }
        }

        private void ToggleRemotePlayerVisibility(RemotePlayer remotePlayer)
        {
            if (remotePlayer.CharacterObject == null)
            {
                Debug.LogWarning("[RemotePlayerDebugModule] 无法切换显示：角色对象不存在");
                return;
            }

            try
            {
                bool newState = !remotePlayer.CharacterObject.activeSelf;
                remotePlayer.CharacterObject.SetActive(newState);
                Debug.Log($"[RemotePlayerDebugModule] {remotePlayer.Info.SteamName} 已{(newState ? "显示" : "隐藏")}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayerDebugModule] 切换显示失败: {ex.Message}");
            }
        }

        private void DestroyRemotePlayerCharacter(RemotePlayer remotePlayer)
        {
            if (remotePlayer.CharacterObject == null)
            {
                Debug.LogWarning("[RemotePlayerDebugModule] 无法销毁：角色对象不存在");
                return;
            }

            try
            {
                remotePlayer.DestroyCharacter();
                Debug.Log($"[RemotePlayerDebugModule] 已销毁 {remotePlayer.Info.SteamName} 的角色");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayerDebugModule] 销毁失败: {ex.Message}");
            }
        }

        private void TryCreateRemotePlayerCharacter(RemotePlayer remotePlayer)
        {
            if (remotePlayer.CharacterObject != null)
            {
                Debug.LogWarning("[RemotePlayerDebugModule] 角色已存在");
                return;
            }

            try
            {
                // 在本地玩家前方5米处创建
                Vector3 spawnPos = Vector3.zero;
                
                if (GameContext.Instance.PlayerManager?.LocalPlayer?.CharacterObject != null)
                {
                    var localTransform = GameContext.Instance.PlayerManager.LocalPlayer.CharacterObject.transform;
                    spawnPos = localTransform.position + localTransform.forward * 5f;
                }

                bool success = remotePlayer.CreateCharacter(spawnPos);
                if (success)
                {
                    Debug.Log($"[RemotePlayerDebugModule] 已创建 {remotePlayer.Info.SteamName} 的角色");
                }
                else
                {
                    Debug.LogWarning($"[RemotePlayerDebugModule] 创建 {remotePlayer.Info.SteamName} 的角色失败");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemotePlayerDebugModule] 创建失败: {ex.Message}");
            }
        }

        public void Update()
        {
            if (!_autoRefresh) return;

            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= _refreshInterval)
            {
                _refreshTimer = 0f;
                // 自动刷新逻辑（如果需要）
            }
        }
    }
}
