using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 角色预设扫描模块
    /// 扫描游戏中所有的 CharacterRandomPreset 并提取 CharacterModel 信息
    /// </summary>
    public class CharacterPresetScannerModule : IDebugModule
    {
        private Vector2 _scrollPosition;
        private List<PresetInfo> _scannedPresets = new List<PresetInfo>();
        private List<ModelInfo> _scannedModels = new List<ModelInfo>();
        private bool _isScanned = false;
        private string _statusMessage = "";
        private int _selectedTab = 0; // 0=预设列表, 1=模型列表
        private string _searchText = "";

        public string ModuleName => "角色预设扫描器";
        public string Category => "角色";
        public string Description => "扫描所有 CharacterRandomPreset 并提取 CharacterModel";
        public bool IsEnabled { get; set; } = false;

        public void OnGUI()
        {
            if (!IsEnabled) return;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("=== 角色预设扫描器 ===", GUI.skin.box);

            // 控制按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("扫描角色预设", GUILayout.Width(120)))
            {
                ScanCharacterPresets();
            }

            if (GUILayout.Button("清空结果", GUILayout.Width(100)))
            {
                ClearResults();
            }

            if (_isScanned)
            {
                GUILayout.Label($"预设: {_scannedPresets.Count} | 模型: {_scannedModels.Count}", GUILayout.Width(200));
            }
            GUILayout.EndHorizontal();

            // 状态消息
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                GUILayout.Label(_statusMessage);
            }

            // 搜索框
            if (_isScanned)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("搜索:", GUILayout.Width(50));
                _searchText = GUILayout.TextField(_searchText, GUILayout.Width(200));
                if (GUILayout.Button("清空", GUILayout.Width(60)))
                {
                    _searchText = "";
                }
                GUILayout.EndHorizontal();
            }

            // 标签页
            if (_isScanned)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(_selectedTab == 0, $"预设列表 ({_scannedPresets.Count})", GUI.skin.button))
                {
                    _selectedTab = 0;
                }
                if (GUILayout.Toggle(_selectedTab == 1, $"模型列表 ({_scannedModels.Count})", GUI.skin.button))
                {
                    _selectedTab = 1;
                }
                GUILayout.EndHorizontal();

                // 显示列表
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(400));

                if (_selectedTab == 0)
                {
                    DisplayPresetList();
                }
                else if (_selectedTab == 1)
                {
                    DisplayModelList();
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        public void Update()
        {
            // 暂时不需要更新逻辑
        }

        /// <summary>
        /// 扫描所有角色预设
        /// </summary>
        private void ScanCharacterPresets()
        {
            try
            {
                _statusMessage = "正在扫描...";
                _scannedPresets.Clear();
                _scannedModels.Clear();
                _isScanned = false;

                // 查找 CharacterRandomPreset 类型
                var presetType = AccessTools.TypeByName("CharacterRandomPreset");
                if (presetType == null)
                {
                    _statusMessage = "❌ 未找到 CharacterRandomPreset 类型";
                    Debug.LogError("[CharacterPresetScanner] 未找到 CharacterRandomPreset 类型");
                    return;
                }

                // 扫描所有预设
                var allPresets = Resources.FindObjectsOfTypeAll(presetType);
                Debug.Log($"[CharacterPresetScanner] 找到 {allPresets.Length} 个预设对象");

                var modelSet = new HashSet<UnityEngine.Object>();
                var modelField = AccessTools.Field(presetType, "characterModel");

                if (modelField == null)
                {
                    _statusMessage = "❌ 未找到 characterModel 字段";
                    Debug.LogError("[CharacterPresetScanner] 未找到 characterModel 字段");
                    return;
                }

                foreach (var preset in allPresets)
                {
                    if (preset == null) continue;

                    var presetName = (preset as UnityEngine.Object)?.name ?? "Unknown";
                    var model = modelField.GetValue(preset) as UnityEngine.Object;

                    // 记录预设信息
                    var presetInfo = new PresetInfo
                    {
                        Name = presetName,
                        ModelName = model?.name ?? "null",
                        HasModel = model != null
                    };
                    _scannedPresets.Add(presetInfo);

                    // 收集唯一的模型
                    if (model != null && !modelSet.Contains(model))
                    {
                        modelSet.Add(model);
                        Debug.Log($"[CharacterPresetScanner] 找到模型: {model.name} (来自预设: {presetName})");
                    }
                }

                // 转换为模型列表
                _scannedModels = modelSet
                    .Select(m => new ModelInfo
                    {
                        Name = m.name,
                        Type = m.GetType().Name,
                        InstanceID = m.GetInstanceID()
                    })
                    .OrderBy(m => m.Name)
                    .ToList();

                _statusMessage = $"✅ 扫描完成: {_scannedPresets.Count} 个预设, {_scannedModels.Count} 个唯一模型";
                _isScanned = true;

                Debug.Log($"[CharacterPresetScanner] 扫描完成: {_scannedPresets.Count} 预设, {_scannedModels.Count} 模型");
            }
            catch (Exception ex)
            {
                _statusMessage = $"❌ 扫描失败: {ex.Message}";
                Debug.LogError($"[CharacterPresetScanner] 扫描异常: {ex}");
            }
        }

        /// <summary>
        /// 清空扫描结果
        /// </summary>
        private void ClearResults()
        {
            _scannedPresets.Clear();
            _scannedModels.Clear();
            _isScanned = false;
            _statusMessage = "";
            _searchText = "";
        }

        /// <summary>
        /// 显示预设列表
        /// </summary>
        private void DisplayPresetList()
        {
            GUILayout.Label($"=== 预设列表 ({_scannedPresets.Count}) ===", GUI.skin.box);

            var filteredPresets = string.IsNullOrEmpty(_searchText)
                ? _scannedPresets
                : _scannedPresets.Where(p =>
                    p.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.ModelName.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                ).ToList();

            if (filteredPresets.Count == 0)
            {
                GUILayout.Label("没有匹配的预设");
                return;
            }

            foreach (var preset in filteredPresets)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                
                // 左侧：预设信息
                GUILayout.BeginVertical();
                GUILayout.Label($"预设: {preset.Name}");
                GUILayout.Label($"  模型: {preset.ModelName} {(preset.HasModel ? "✓" : "✗")}");
                GUILayout.EndVertical();
                
                GUILayout.FlexibleSpace();
                
                // 右侧：操作按钮
                if (GUILayout.Button("创建 NPC", GUILayout.Width(80)))
                {
                    CreateNPCFromPreset(preset.Name);
                }
                
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(2);
            }
        }

        /// <summary>
        /// 显示模型列表
        /// </summary>
        private void DisplayModelList()
        {
            GUILayout.Label($"=== 模型列表 ({_scannedModels.Count}) ===", GUI.skin.box);

            var filteredModels = string.IsNullOrEmpty(_searchText)
                ? _scannedModels
                : _scannedModels.Where(m =>
                    m.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Type.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                ).ToList();

            if (filteredModels.Count == 0)
            {
                GUILayout.Label("没有匹配的模型");
                return;
            }

            foreach (var model in filteredModels)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"模型: {model.Name}");
                GUILayout.Label($"  类型: {model.Type}");
                GUILayout.Label($"  实例ID: {model.InstanceID}");
                
                // 统计使用此模型的预设数量
                int usageCount = _scannedPresets.Count(p => p.ModelName == model.Name);
                GUILayout.Label($"  使用次数: {usageCount}");
                
                GUILayout.EndVertical();
                GUILayout.Space(2);
            }
        }

        /// <summary>
        /// 使用指定预设创建 NPC
        /// </summary>
        private void CreateNPCFromPreset(string presetName)
        {
            try
            {
                _statusMessage = $"正在创建 NPC: {presetName}...";
                
                // 1. 查找预设类型
                var presetType = AccessTools.TypeByName("CharacterRandomPreset");
                if (presetType == null)
                {
                    _statusMessage = "❌ 未找到 CharacterRandomPreset 类型";
                    Debug.LogError("[CharacterPresetScanner] 未找到 CharacterRandomPreset 类型");
                    return;
                }

                // 2. 查找指定名称的预设
                var allPresets = Resources.FindObjectsOfTypeAll(presetType);
                object? targetPreset = null;
                
                foreach (var preset in allPresets)
                {
                    if ((preset as UnityEngine.Object)?.name == presetName)
                    {
                        targetPreset = preset;
                        break;
                    }
                }

                if (targetPreset == null)
                {
                    _statusMessage = $"❌ 未找到预设: {presetName}";
                    Debug.LogError($"[CharacterPresetScanner] 未找到预设: {presetName}");
                    return;
                }

                // 3. 调用 CreateCharacterAsync 方法
                var createMethod = AccessTools.Method(presetType, "CreateCharacterAsync");
                if (createMethod == null)
                {
                    _statusMessage = "❌ 未找到 CreateCharacterAsync 方法";
                    Debug.LogError("[CharacterPresetScanner] 未找到 CreateCharacterAsync 方法");
                    return;
                }

                // 4. 计算生成位置（在玩家前方 5 米处）
                var playerPos = GetPlayerPosition();
                var playerForward = GetPlayerForward();
                var spawnPos = playerPos + playerForward * 5f;

                // 5. 准备参数
                Vector3 direction = playerForward;
                int relatedScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
                object? group = null; // CharacterSpawnerGroup，可选
                bool isLeader = false;

                Debug.Log($"[CharacterPresetScanner] 开始创建 NPC: {presetName} at {spawnPos}");

                // 6. 调用创建方法（异步方法会在后台执行）
                object? createTask = createMethod.Invoke(targetPreset, new object?[] 
                { 
                    spawnPos, 
                    direction, 
                    relatedScene, 
                    group, 
                    isLeader 
                });

                // 7. 创建任务已提交
                if (createTask != null)
                {
                    _statusMessage = $"✅ 已提交创建请求: {presetName}";
                    Debug.Log($"[CharacterPresetScanner] ✅ 已提交创建 NPC 请求: {presetName}");
                }
                else
                {
                    _statusMessage = $"⚠️ 创建任务返回为空: {presetName}";
                    Debug.LogWarning($"[CharacterPresetScanner] 创建任务返回为空: {presetName}");
                }
            }
            catch (Exception ex)
            {
                _statusMessage = $"❌ 创建失败: {ex.Message}";
                Debug.LogError($"[CharacterPresetScanner] 创建 NPC 失败: {ex}");
            }
        }

        /// <summary>
        /// 获取玩家位置
        /// </summary>
        private Vector3 GetPlayerPosition()
        {
            try
            {
                // 尝试通过 LevelManager 获取主角位置
                var levelManagerType = AccessTools.TypeByName("LevelManager");
                if (levelManagerType != null)
                {
                    var instanceProp = AccessTools.Property(levelManagerType, "Instance");
                    var levelManager = instanceProp?.GetValue(null);
                    
                    if (levelManager != null)
                    {
                        var mainCharProp = AccessTools.Property(levelManagerType, "MainCharacter");
                        var mainChar = mainCharProp?.GetValue(levelManager);
                        
                        if (mainChar is Component component)
                        {
                            return component.transform.position;
                        }
                    }
                }

                // 备选方案：尝试查找 CharacterMainControl
                var mainControlType = AccessTools.TypeByName("CharacterMainControl");
                if (mainControlType != null)
                {
                    var mainProp = AccessTools.Property(mainControlType, "Main");
                    var mainControl = mainProp?.GetValue(null);
                    
                    if (mainControl is Component component)
                    {
                        return component.transform.position;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterPresetScanner] 获取玩家位置失败: {ex.Message}");
            }
            
            return Vector3.zero;
        }

        /// <summary>
        /// 获取玩家朝向
        /// </summary>
        private Vector3 GetPlayerForward()
        {
            try
            {
                // 尝试通过 LevelManager 获取主角朝向
                var levelManagerType = AccessTools.TypeByName("LevelManager");
                if (levelManagerType != null)
                {
                    var instanceProp = AccessTools.Property(levelManagerType, "Instance");
                    var levelManager = instanceProp?.GetValue(null);
                    
                    if (levelManager != null)
                    {
                        var mainCharProp = AccessTools.Property(levelManagerType, "MainCharacter");
                        var mainChar = mainCharProp?.GetValue(levelManager);
                        
                        if (mainChar is Component component)
                        {
                            return component.transform.forward;
                        }
                    }
                }

                // 备选方案：尝试查找 CharacterMainControl
                var mainControlType = AccessTools.TypeByName("CharacterMainControl");
                if (mainControlType != null)
                {
                    var mainProp = AccessTools.Property(mainControlType, "Main");
                    var mainControl = mainProp?.GetValue(null);
                    
                    if (mainControl is Component component)
                    {
                        return component.transform.forward;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterPresetScanner] 获取玩家朝向失败: {ex.Message}");
            }
            
            return Vector3.forward;
        }

        /// <summary>
        /// 预设信息数据结构
        /// </summary>
        private class PresetInfo
        {
            public string Name { get; set; } = "";
            public string ModelName { get; set; } = "";
            public bool HasModel { get; set; }
        }

        /// <summary>
        /// 模型信息数据结构
        /// </summary>
        private class ModelInfo
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public int InstanceID { get; set; }
        }
    }
}

