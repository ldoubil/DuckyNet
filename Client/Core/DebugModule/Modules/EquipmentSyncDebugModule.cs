using System;
using System.Collections.Generic;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Client.Core.Utils;
using DuckyNet.Shared.Data;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using UnityEngine;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 装备同步调试模块
    /// 功能：创建测试单位并实时同步本地玩家的装备变更
    /// </summary>
    public class EquipmentSyncDebugModule : IDebugModule
    {
        public string ModuleName => "装备同步测试";
        public string Category => "测试";
        public string Description => "创建测试单位并实时同步本地玩家的装备变更";
        public bool IsEnabled { get; set; } = true;

        // 测试单位列表
        private readonly List<TestCharacter> _testCharacters = new List<TestCharacter>();
        private int _nextCharacterId = 1;

        // 装备同步开关
        private bool _autoSyncEnabled = true;
        private bool _showEquipmentInfo = true;

        // UI 状态
        private Vector2 _scrollPosition;
        private bool _showCreateOptions = false;
        
        // 创建选项
        private int _spawnDistance = 3;
        private bool _syncOnCreate = true;
        private bool _includeArmor = true;
        private bool _includeHelmet = true;
        private bool _includeFaceMask = true;
        private bool _includeBackpack = true;
        private bool _includeHeadset = true;

        // 延迟同步
        private TestCharacter? _pendingSyncCharacter = null;
        private float _syncDelay = 0f;

        // 事件订阅标志
        private bool _isEventSubscribed = false;

        public EquipmentSyncDebugModule()
        {
            // 尝试订阅装备变更事件
            TrySubscribeToEvents();
        }

        public void Update()
        {
            // 尝试订阅事件（如果还未订阅）
            if (!_isEventSubscribed)
            {
                TrySubscribeToEvents();
            }

            // 处理延迟同步
            if (_pendingSyncCharacter != null && _syncDelay > 0f)
            {
                _syncDelay -= Time.deltaTime;
                if (_syncDelay <= 0f)
                {
                    SyncCharacterEquipment(_pendingSyncCharacter);
                    _pendingSyncCharacter = null;
                }
            }
        }

        /// <summary>
        /// 尝试订阅装备变更事件
        /// </summary>
        private void TrySubscribeToEvents()
        {
            if (_isEventSubscribed) return;

            if (GameContext.IsInitialized && GameContext.Instance?.EventBus != null)
            {
                try
                {
                    GameContext.Instance.EventBus.Subscribe<EquipmentSlotChangedEvent>(OnLocalPlayerEquipmentChanged);
                    _isEventSubscribed = true;
                    Debug.Log("[EquipmentSyncDebugModule] ✅ 已订阅装备变更事件");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EquipmentSyncDebugModule] 订阅事件失败: {ex.Message}");
                }
            }
        }

        public void OnGUI()
        {
            GUILayout.Label("=== 装备同步测试工具 ===", GUI.skin.box);
            
            // 全局控制
            DrawGlobalControls();
            
            GUILayout.Space(10);
            
            // 创建测试单位
            DrawCreateSection();
            
            GUILayout.Space(10);
            
            // 测试单位列表
            DrawCharactersList();
        }

        #region UI 绘制

        private void DrawGlobalControls()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("🎮 全局控制", GUI.skin.label);

            // 事件订阅状态
            string eventStatus = _isEventSubscribed ? "✅ 事件已订阅" : "⚠️ 事件未订阅";
            GUILayout.Label(eventStatus, GUI.skin.label);

            GUILayout.BeginHorizontal();
            _autoSyncEnabled = GUILayout.Toggle(_autoSyncEnabled, " 自动同步装备变更");
            _showEquipmentInfo = GUILayout.Toggle(_showEquipmentInfo, " 显示装备详情");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 同步所有单位", GUILayout.Height(30)))
            {
                SyncAllCharacters();
            }
            if (GUILayout.Button("🗑️ 清除所有单位", GUILayout.Height(30)))
            {
                ClearAllCharacters();
            }
            GUILayout.EndHorizontal();

            // 测试按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🧪 测试卸下护甲", GUILayout.Height(25)))
            {
                TestUnequipArmor();
            }
            if (GUILayout.Button("🧪 测试清空装备", GUILayout.Height(25)))
            {
                TestClearEquipment();
            }
            GUILayout.EndHorizontal();

            // 统计信息
            var mainChar = LevelManager.Instance?.MainCharacter;
            if (mainChar != null)
            {
                GUILayout.Label($"📊 统计: 测试单位={_testCharacters.Count}, 本地玩家装备={CountEquippedSlots(mainChar.CharacterItem)}");
            }

            GUILayout.EndVertical();
        }

        private void DrawCreateSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("➕ 创建测试单位", GUI.skin.label);
            _showCreateOptions = GUILayout.Toggle(_showCreateOptions, _showCreateOptions ? "▼" : "▶", GUILayout.Width(30));
            GUILayout.EndHorizontal();

            if (_showCreateOptions)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                
                // 生成距离
                GUILayout.BeginHorizontal();
                GUILayout.Label("生成距离:", GUILayout.Width(80));
                _spawnDistance = (int)GUILayout.HorizontalSlider(_spawnDistance, 1, 10, GUILayout.Width(100));
                GUILayout.Label($"{_spawnDistance}m", GUILayout.Width(50));
                GUILayout.EndHorizontal();

                // 同步选项
                _syncOnCreate = GUILayout.Toggle(_syncOnCreate, " 创建时立即同步装备");

                if (_syncOnCreate)
                {
                    GUILayout.Label("  同步槽位:");
                    GUILayout.BeginHorizontal();
                    _includeArmor = GUILayout.Toggle(_includeArmor, "护甲");
                    _includeHelmet = GUILayout.Toggle(_includeHelmet, "头盔");
                    _includeFaceMask = GUILayout.Toggle(_includeFaceMask, "面罩");
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    _includeBackpack = GUILayout.Toggle(_includeBackpack, "背包");
                    _includeHeadset = GUILayout.Toggle(_includeHeadset, "耳机");
                    GUILayout.EndHorizontal();
                }

                GUILayout.EndVertical();
            }

            // 创建按钮
            if (GUILayout.Button("🎭 创建测试单位", GUILayout.Height(35)))
            {
                CreateTestCharacter(CharacterType.MeleeAI);
            }

            GUILayout.EndVertical();
        }

        private void DrawCharactersList()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"👥 测试单位列表 ({_testCharacters.Count})", GUI.skin.label);

            if (_testCharacters.Count == 0)
            {
                GUILayout.Label("  暂无测试单位", GUI.skin.label);
            }
            else
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.MaxHeight(300));

                for (int i = _testCharacters.Count - 1; i >= 0; i--)
                {
                    var testChar = _testCharacters[i];
                    
                    // 检查单位是否还存在
                    if (testChar.GameObject == null)
                    {
                        _testCharacters.RemoveAt(i);
                        continue;
                    }

                    DrawCharacterItem(testChar);
                    GUILayout.Space(5);
                }

                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();
        }

        private void DrawCharacterItem(TestCharacter testChar)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            // 标题行
            GUILayout.BeginHorizontal();
            GUILayout.Label($"🤖 #{testChar.Id} - {testChar.Name}", GUI.skin.label);
            
            if (GUILayout.Button("🔄", GUILayout.Width(30)))
            {
                SyncCharacterEquipment(testChar);
            }
            if (GUILayout.Button("🗑️", GUILayout.Width(30)))
            {
                RemoveTestCharacter(testChar);
            }
            GUILayout.EndHorizontal();

            // 位置信息
            if (testChar.GameObject != null)
            {
                var pos = testChar.GameObject.transform.position;
                GUILayout.Label($"  位置: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})", GUI.skin.label);
            }

            // 装备信息
            if (_showEquipmentInfo && testChar.CharacterItem != null)
            {
                DrawEquipmentInfo(testChar.CharacterItem);
            }

            GUILayout.EndVertical();
        }

        private void DrawEquipmentInfo(Item characterItem)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("  装备:", GUI.skin.label);

            DrawSlotInfo("护甲", characterItem, CharacterEquipmentController.armorHash);
            DrawSlotInfo("头盔", characterItem, CharacterEquipmentController.helmatHash);
            DrawSlotInfo("面罩", characterItem, CharacterEquipmentController.faceMaskHash);
            DrawSlotInfo("背包", characterItem, CharacterEquipmentController.backpackHash);
            DrawSlotInfo("耳机", characterItem, CharacterEquipmentController.headsetHash);

            GUILayout.EndVertical();
        }

        private void DrawSlotInfo(string slotName, Item characterItem, int slotHash)
        {
            var slot = characterItem.Slots.GetSlot(slotHash);
            string itemName = slot?.Content?.DisplayName ?? "无";
            GUILayout.Label($"    {slotName}: {itemName}", GUI.skin.label);
        }

        #endregion

        #region 核心功能

        /// <summary>
        /// 创建测试单位（使用 CharacterCreationUtils 工具类）
        /// </summary>
        private void CreateTestCharacter(CharacterType type)
        {
            try
            {
                var mainChar = LevelManager.Instance?.MainCharacter;
                if (mainChar == null)
                {
                    Debug.LogError("[EquipmentSyncDebugModule] 主角色未找到");
                    return;
                }

                // 计算生成位置（玩家前方）
                Vector3 spawnPos = mainChar.transform.position + mainChar.transform.forward * _spawnDistance;

                Debug.Log("[EquipmentSyncDebugModule] ⏳ 正在创建测试单位...");

                // 1. 创建角色数据项
                var characterItem = CharacterCreationUtils.CreateCharacterItem();
                if (characterItem == null)
                {
                    Debug.LogError("[EquipmentSyncDebugModule] 创建角色数据项失败");
                    return;
                }

                // 2. 获取角色模型预制体
                var modelPrefab = CharacterCreationUtils.GetCharacterModelPrefab();
                if (modelPrefab == null)
                {
                    Debug.LogError("[EquipmentSyncDebugModule] 获取角色模型预制体失败");
                    return;
                }

                // 3. 实例化角色
                var newCharacter = CharacterCreationUtils.CreateCharacterInstance(
                    characterItem, 
                    modelPrefab, 
                    spawnPos, 
                    Quaternion.identity
                );
                if (newCharacter == null)
                {
                    Debug.LogError("[EquipmentSyncDebugModule] 实例化角色失败");
                    return;
                }

                // 4. 配置角色
                string testCharName = $"装备测试-{_nextCharacterId}";
                CharacterCreationUtils.ConfigureCharacter(newCharacter, testCharName, spawnPos, team: 0);
                CharacterCreationUtils.ConfigureCharacterPreset(newCharacter, testCharName, showName: true);

                // 5. 标记为远程玩家（禁用输入控制）
                CharacterCreationUtils.MarkAsRemotePlayer(newCharacter);

                // 6. 从距离系统移除（避免被自动清理）
                CharacterCreationUtils.UnregisterFromDistanceSystem(newCharacter);

                // 7. 请求血条
                CharacterCreationUtils.RequestHealthBar(newCharacter, testCharName, null);

                // 8. 获取 GameObject
                GameObject? characterObj = null;
                if (newCharacter is Component component)
                {
                    characterObj = component.gameObject;
                }

                if (characterObj == null)
                {
                    Debug.LogError("[EquipmentSyncDebugModule] 无法获取角色 GameObject");
                    return;
                }

                // 创建测试单位记录
                var testChar = new TestCharacter
                {
                    Id = _nextCharacterId++,
                    Name = testCharName,
                    GameObject = characterObj,
                    CharacterMainControl = newCharacter as CharacterMainControl,
                    CharacterItem = characterItem as Item,
                    CreatedTime = DateTime.Now
                };

                _testCharacters.Add(testChar);

                Debug.Log($"[EquipmentSyncDebugModule] ✅ 创建测试单位成功: {testChar.Name} at {spawnPos}");

                // 如果启用创建时同步，设置延迟同步
                if (_syncOnCreate)
                {
                    _pendingSyncCharacter = testChar;
                    _syncDelay = 0.5f; // 延迟0.5秒同步
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentSyncDebugModule] 创建测试单位失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 同步单个角色的装备
        /// </summary>
        private void SyncCharacterEquipment(TestCharacter testChar)
        {
            if (testChar == null || testChar.CharacterItem == null)
            {
                Debug.LogWarning("[EquipmentSyncDebugModule] 测试角色无效");
                return;
            }

            var mainChar = LevelManager.Instance?.MainCharacter;
            if (mainChar == null || mainChar.CharacterItem == null)
            {
                Debug.LogError("[EquipmentSyncDebugModule] 主角色未找到");
                return;
            }

            Debug.Log($"[EquipmentSyncDebugModule] 开始同步装备: {testChar.Name}");

            int count = EquipmentTools.CopyAllEquipment(
                mainChar.CharacterItem,
                testChar.CharacterItem,
                _includeArmor,
                _includeHelmet,
                _includeFaceMask,
                _includeBackpack,
                _includeHeadset
            );

            Debug.Log($"[EquipmentSyncDebugModule] ✅ 同步完成: {testChar.Name}, 装备数={count}");
        }

        /// <summary>
        /// 同步所有测试单位的装备
        /// </summary>
        private void SyncAllCharacters()
        {
            Debug.Log($"[EquipmentSyncDebugModule] 开始同步所有单位: 共{_testCharacters.Count}个");

            int successCount = 0;
            foreach (var testChar in _testCharacters)
            {
                if (testChar.GameObject != null)
                {
                    SyncCharacterEquipment(testChar);
                    successCount++;
                }
            }

            Debug.Log($"[EquipmentSyncDebugModule] ✅ 全部同步完成: {successCount}/{_testCharacters.Count}");
        }

        /// <summary>
        /// 移除测试单位
        /// </summary>
        private void RemoveTestCharacter(TestCharacter testChar)
        {
            if (testChar.GameObject != null)
            {
                UnityEngine.Object.Destroy(testChar.GameObject);
            }
            _testCharacters.Remove(testChar);
            Debug.Log($"[EquipmentSyncDebugModule] 移除测试单位: {testChar.Name}");
        }

        /// <summary>
        /// 清除所有测试单位
        /// </summary>
        private void ClearAllCharacters()
        {
            foreach (var testChar in _testCharacters)
            {
                if (testChar.GameObject != null)
                {
                    UnityEngine.Object.Destroy(testChar.GameObject);
                }
            }
            _testCharacters.Clear();
            Debug.Log("[EquipmentSyncDebugModule] 已清除所有测试单位");
        }

        /// <summary>
        /// 本地玩家装备变更事件处理
        /// </summary>
        private void OnLocalPlayerEquipmentChanged(EquipmentSlotChangedEvent evt)
        {
            if (!_autoSyncEnabled) return;

            try
            {
                string action = evt.IsEquipped ? "装备" : "卸下";
                string itemName = "无";
                
                if (evt.EquippedItem is UnityEngine.Object unityObj)
                {
                    itemName = unityObj.name;
                }

                Debug.Log($"[EquipmentSyncDebugModule] 🎯 检测到装备变更: {evt.SlotType} - {action} - {itemName}");

                // 同步到所有测试单位
                SyncSlotToAllCharacters(evt.SlotType, evt.EquippedItem as Item);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquipmentSyncDebugModule] 处理装备变更失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将特定槽位同步到所有测试单位
        /// </summary>
        private void SyncSlotToAllCharacters(EquipmentSlotType slotType, Item? equippedItem)
        {
            if (_testCharacters.Count == 0) return;

            int slotHash = GetSlotHash(slotType);
            if (slotHash == 0) return;

            foreach (var testChar in _testCharacters)
            {
                if (testChar.GameObject == null || testChar.CharacterItem == null) continue;

                try
                {
                    var targetSlot = testChar.CharacterItem.Slots.GetSlot(slotHash);
                    if (targetSlot == null) continue;

                    if (equippedItem != null)
                    {
                        // 装备了新物品 - 复制
                        Debug.Log($"[EquipmentSyncDebugModule] 同步装备: {slotType} -> {equippedItem.DisplayName}");
                        
                        bool success = EquipmentTools.CreateAndEquip(
                            equippedItem.TypeID,
                            targetSlot,
                            unpluggedItem => 
                            {
                                Debug.Log($"[EquipmentSyncDebugModule] 销毁旧装备: {unpluggedItem.DisplayName}");
                                unpluggedItem.DestroyTree();
                            }
                        );
                        
                        if (!success)
                        {
                            Debug.LogWarning($"[EquipmentSyncDebugModule] 装备失败: {slotType}");
                        }
                    }
                    else
                    {
                        // 卸下装备 - 清空槽位
                        if (targetSlot.Content != null)
                        {
                            Debug.Log($"[EquipmentSyncDebugModule] 卸下装备: {slotType} - {targetSlot.Content.DisplayName}");
                            
                            Item removedItem = targetSlot.Unplug();
                            if (removedItem != null)
                            {
                                removedItem.DestroyTree();
                                Debug.Log($"[EquipmentSyncDebugModule] ✅ 已卸下并销毁装备");
                            }
                        }
                        else
                        {
                            Debug.Log($"[EquipmentSyncDebugModule] 槽位 {slotType} 已经为空，无需卸下");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EquipmentSyncDebugModule] 同步槽位失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region 测试方法

        /// <summary>
        /// 测试卸下所有测试单位的护甲
        /// </summary>
        private void TestUnequipArmor()
        {
            Debug.Log("[EquipmentSyncDebugModule] 🧪 开始测试卸下护甲...");
            
            foreach (var testChar in _testCharacters)
            {
                if (testChar.CharacterItem != null)
                {
                    var armorSlot = testChar.CharacterItem.Slots.GetSlot(CharacterEquipmentController.armorHash);
                    if (armorSlot?.Content != null)
                    {
                        Debug.Log($"[EquipmentSyncDebugModule] 卸下 {testChar.Name} 的护甲: {armorSlot.Content.DisplayName}");
                        Item removed = armorSlot.Unplug();
                        if (removed != null)
                        {
                            removed.DestroyTree();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 测试清空所有测试单位的装备
        /// </summary>
        private void TestClearEquipment()
        {
            Debug.Log("[EquipmentSyncDebugModule] 🧪 开始测试清空装备...");
            
            foreach (var testChar in _testCharacters)
            {
                if (testChar.CharacterItem != null)
                {
                    int count = EquipmentTools.ClearAllEquipment(testChar.CharacterItem, destroyItems: true);
                    Debug.Log($"[EquipmentSyncDebugModule] 已清空 {testChar.Name} 的 {count} 件装备");
                }
            }
        }

        #endregion

        #region 辅助方法

        private int GetSlotHash(EquipmentSlotType slotType)
        {
            return slotType switch
            {
                EquipmentSlotType.Armor => CharacterEquipmentController.armorHash,
                EquipmentSlotType.Helmet => CharacterEquipmentController.helmatHash,
                EquipmentSlotType.FaceMask => CharacterEquipmentController.faceMaskHash,
                EquipmentSlotType.Backpack => CharacterEquipmentController.backpackHash,
                EquipmentSlotType.Headset => CharacterEquipmentController.headsetHash,
                _ => 0
            };
        }

        private int CountEquippedSlots(Item characterItem)
        {
            if (characterItem == null) return 0;

            int count = 0;
            int[] slotHashes = new[]
            {
                CharacterEquipmentController.armorHash,
                CharacterEquipmentController.helmatHash,
                CharacterEquipmentController.faceMaskHash,
                CharacterEquipmentController.backpackHash,
                CharacterEquipmentController.headsetHash
            };

            foreach (var hash in slotHashes)
            {
                var slot = characterItem.Slots.GetSlot(hash);
                if (slot?.Content != null) count++;
            }

            return count;
        }

        #endregion

        #region 内部类

        private enum CharacterType
        {
            MeleeAI
        }

        private class TestCharacter
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public GameObject? GameObject { get; set; }
            public CharacterMainControl? CharacterMainControl { get; set; }
            public Item? CharacterItem { get; set; }
            public DateTime CreatedTime { get; set; }
        }

        #endregion
    }
}
