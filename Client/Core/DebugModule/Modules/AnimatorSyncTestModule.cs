using System;
using UnityEngine;
using DuckyNet.Client.Core.Utils;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 动画同步测试调试模块 - 创建测试单位并模拟远程玩家动画同步
    /// </summary>
    public class AnimatorSyncTestModule : IDebugModule
    {
        public string ModuleName => "动画同步测试";
        public string Category => "测试";
        public string Description => "创建测试单位并实时同步本地玩家的动画状态";
        public bool IsEnabled { get; set; } = true;

        private object? _testCharacter;
        private Animator? _testAnimator;
        private Animator? _localAnimator;
        private string _statusInfo = "";
        private Vector3 _spawnOffset = new Vector3(3f, 0f, 0f); // 默认在右侧3米
        
        // 同步配置
        private bool _autoSync = true;
        private float _syncInterval = 0.033f; // ~33ms（30帧/秒）
        private float _lastSyncTime = 0f;
        
        // 统计信息
        private int _syncCount = 0;
        private int _skippedCount = 0;
        private AnimatorSyncData? _lastSyncData = null;
        private AnimatorSyncData? _currentTargetData = null; // 当前目标数据（用于每帧应用）

        public void OnGUI()
        {
            if (!IsEnabled) return;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("=== 动画同步测试工具 ===", GUI.skin.box);
            
            // 偏移量设置
            GUILayout.BeginHorizontal();
            GUILayout.Label("生成偏移 X:", GUILayout.Width(80));
            if (float.TryParse(GUILayout.TextField(_spawnOffset.x.ToString("F1"), GUILayout.Width(60)), out float x))
                _spawnOffset.x = x;
            GUILayout.Label("Y:", GUILayout.Width(20));
            if (float.TryParse(GUILayout.TextField(_spawnOffset.y.ToString("F1"), GUILayout.Width(60)), out float y))
                _spawnOffset.y = y;
            GUILayout.Label("Z:", GUILayout.Width(20));
            if (float.TryParse(GUILayout.TextField(_spawnOffset.z.ToString("F1"), GUILayout.Width(60)), out float z))
                _spawnOffset.z = z;
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // 同步间隔设置
            GUILayout.BeginHorizontal();
            GUILayout.Label("同步间隔(ms):", GUILayout.Width(100));
            if (float.TryParse(GUILayout.TextField((_syncInterval * 1000).ToString("F0"), GUILayout.Width(60)), out float interval))
                _syncInterval = interval / 1000f;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 按钮区域
            if (_testCharacter == null)
            {
                if (GUILayout.Button("🎭 创建测试单位", GUILayout.Height(50)))
                {
                    CreateTestCharacter();
                }
            }
            else
            {
                // 自动同步开关
                GUILayout.BeginHorizontal();
                bool newAutoSync = GUILayout.Toggle(_autoSync, _autoSync ? "🔄 自动同步中..." : "⏸️ 自动同步(关闭)");
                if (newAutoSync != _autoSync)
                {
                    _autoSync = newAutoSync;
                    if (_autoSync)
                    {
                        _syncCount = 0;
                        _skippedCount = 0;
                        _statusInfo += "\n✅ 自动同步已开启";
                    }
                    else
                    {
                        _statusInfo += "\n⏸️ 自动同步已暂停";
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                if (GUILayout.Button("🎯 手动同步一次", GUILayout.Height(40)))
                {
                    SyncAnimationOnce();
                }

                GUILayout.Space(5);

                if (GUILayout.Button("🗑️ 删除测试单位", GUILayout.Height(40)))
                {
                    DestroyTestCharacter();
                }

                // 统计信息
                GUILayout.Space(10);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"同步次数: {_syncCount}");
                GUILayout.Label($"跳过次数: {_skippedCount} (增量优化)");
                if (_lastSyncData != null)
                {
                    GUILayout.Label($"当前状态: {_lastSyncData.StateHash}");
                    GUILayout.Label($"归一化时间: {_lastSyncData.GetNormalizedTime():F2}");
                }
                GUILayout.EndVertical();
            }

            // 状态信息
            GUILayout.Space(10);
            GUILayout.Box(_statusInfo, GUILayout.ExpandHeight(true));
            
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 创建测试单位
        /// </summary>
        private void CreateTestCharacter()
        {
            try
            {
                var mainChar = CharacterMainControl.Main;
                if (mainChar == null)
                {
                    _statusInfo = "❌ 无法获取本地玩家位置";
                    return;
                }

                // 获取本地玩家的 Animator
                _localAnimator = mainChar.GetComponentInChildren<Animator>();
                if (_localAnimator == null)
                {
                    _statusInfo = "❌ 本地玩家没有 Animator 组件";
                    return;
                }

                // 计算生成位置
                var playerPos = mainChar.transform.position;
                var spawnPos = playerPos + _spawnOffset;

                _statusInfo = "⏳ 正在创建测试单位...";

                // 1. 创建角色数据项
                var characterItem = CharacterCreationUtils.CreateCharacterItem();
                if (characterItem == null)
                {
                    _statusInfo = "❌ 创建角色数据项失败";
                    return;
                }

                // 2. 获取角色模型预制体
                var modelPrefab = CharacterCreationUtils.GetCharacterModelPrefab();
                if (modelPrefab == null)
                {
                    _statusInfo = "❌ 获取角色模型预制体失败";
                    return;
                }

                // 3. 实例化角色
                var newCharacter = CharacterCreationUtils.CreateCharacterInstance(
                    characterItem, modelPrefab, spawnPos, Quaternion.identity
                );
                if (newCharacter == null)
                {
                    _statusInfo = "❌ 实例化角色失败";
                    return;
                }

                // 4. 配置角色
                CharacterCreationUtils.ConfigureCharacter(newCharacter, "TestCharacter_AnimSync", spawnPos, team: 0);
                CharacterCreationUtils.ConfigureCharacterPreset(newCharacter, "动画测试", showName: true);

                // 5. 标记为远程玩家（禁用移动）
                CharacterCreationUtils.MarkAsRemotePlayer(newCharacter);

                // 6. 从距离系统移除
                CharacterCreationUtils.UnregisterFromDistanceSystem(newCharacter);

                // 7. 请求血条
                CharacterCreationUtils.RequestHealthBar(newCharacter, "动画测试", null);

                // 8. 获取测试单位的 Animator
                if (newCharacter is Component comp)
                {
                    _testAnimator = comp.GetComponentInChildren<Animator>();
                    if (_testAnimator == null)
                    {
                        _statusInfo = "❌ 测试单位没有 Animator 组件";
                        UnityEngine.Object.Destroy(comp.gameObject);
                        return;
                    }

                    // 9. 禁用 CharacterAnimationControl（防止本地逻辑覆盖同步的动画参数）
                    DisableAnimationControl(comp);
                }

                _testCharacter = newCharacter;
                _syncCount = 0;
                _skippedCount = 0;
                _statusInfo = $"✅ 测试单位创建成功\n位置: {spawnPos}\n\n可以开启自动同步或手动同步";
            }
            catch (Exception ex)
            {
                _statusInfo = $"❌ 创建测试单位异常:\n{ex.Message}";
                Debug.LogError($"[AnimatorSyncTestModule] {ex}");
            }
        }

        /// <summary>
        /// 禁用动画控制脚本（防止本地逻辑覆盖同步的动画参数）
        /// </summary>
        private void DisableAnimationControl(Component character)
        {
            try
            {
                // 禁用 CharacterAnimationControl
                var animControlType = HarmonyLib.AccessTools.TypeByName("CharacterAnimationControl");
                if (animControlType != null)
                {
                    var animControl = character.GetComponentInChildren(animControlType) as MonoBehaviour;
                    if (animControl != null)
                    {
                        animControl.enabled = false;
                        Debug.Log("[AnimatorSyncTestModule] ✅ 已禁用 CharacterAnimationControl");
                    }
                }

                // 禁用 CharacterAnimationControl_MagicBlend（如果存在）
                var magicBlendType = HarmonyLib.AccessTools.TypeByName("CharacterAnimationControl_MagicBlend");
                if (magicBlendType != null)
                {
                    var magicBlend = character.GetComponentInChildren(magicBlendType) as MonoBehaviour;
                    if (magicBlend != null)
                    {
                        magicBlend.enabled = false;
                        Debug.Log("[AnimatorSyncTestModule] ✅ 已禁用 CharacterAnimationControl_MagicBlend");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AnimatorSyncTestModule] 禁用动画控制脚本失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 同步一次动画状态
        /// </summary>
        private void SyncAnimationOnce()
        {
            if (_localAnimator == null || _testAnimator == null)
            {
                _statusInfo = "❌ Animator 组件缺失";
                return;
            }

            try
            {
                // 1. 采集本地玩家的动画状态
                var syncData = CaptureAnimatorState(_localAnimator);
                if (syncData == null)
                {
                    _statusInfo = "❌ 采集动画状态失败";
                    return;
                }

                // 2. 检查是否需要同步（增量优化）
                if (!HasDataChanged(syncData))
                {
                    _skippedCount++;
                    return;
                }

                // 3. 应用到测试单位
                ApplyAnimatorState(_testAnimator, syncData);

                _lastSyncData = syncData;
                _currentTargetData = syncData; // 保存当前目标
                _syncCount++;
                _statusInfo = $"✅ 同步成功 (#{_syncCount})\n状态: {syncData.StateHash}\n时间: {syncData.GetNormalizedTime():F2}";
            }
            catch (Exception ex)
            {
                _statusInfo = $"❌ 同步异常:\n{ex.Message}";
                Debug.LogError($"[AnimatorSyncTestModule] {ex}");
            }
        }

        /// <summary>
        /// 每帧持续应用动画参数（参考 RemoteAnimatorSmoother.Update）
        /// </summary>
        private void ContinuouslyApplyParameters()
        {
            if (_currentTargetData == null || _testAnimator == null) return;

            try
            {
                // Float 参数 - 每帧直接设置（移除 dampTime）
                var floatParamNames = new string[] { "MoveSpeed", "MoveDirX", "MoveDirY", "", "", "", "", "" };
                for (int i = 0; i < Math.Min(floatParamNames.Length, 3); i++)
                {
                    if (string.IsNullOrEmpty(floatParamNames[i])) continue;
                    
                    try
                    {
                        int hash = Animator.StringToHash(floatParamNames[i]);
                        _testAnimator.SetFloat(hash, _currentTargetData.GetFloatParam(i));
                    }
                    catch { }
                }

                // Integer 参数 - HandState
                try
                {
                    int handStateHash = Animator.StringToHash("HandState");
                    _testAnimator.SetInteger(handStateHash, (int)_currentTargetData.GetFloatParam(3));
                }
                catch { }

                // Bool 参数 - 每帧持续设置
                var boolParamNames = new string[] { "Dashing", "RightHandOut", "Attack", "GunReady" };
                for (int i = 0; i < Math.Min(boolParamNames.Length, 4); i++)
                {
                    if (string.IsNullOrEmpty(boolParamNames[i])) continue;
                    
                    try
                    {
                        int hash = Animator.StringToHash(boolParamNames[i]);
                        _testAnimator.SetBool(hash, _currentTargetData.GetBoolParam(i));
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AnimatorSyncTestModule] ContinuouslyApplyParameters: {ex}");
            }
        }

        /// <summary>
        /// 采集 Animator 状态（参考 AnimatorSyncManager）
        /// </summary>
        private AnimatorSyncData? CaptureAnimatorState(Animator animator)
        {
            try
            {
                var syncData = new AnimatorSyncData();

                // 获取当前状态哈希（主层）
                var currentState = animator.GetCurrentAnimatorStateInfo(0);
                syncData.StateHash = currentState.fullPathHash;
                syncData.SetNormalizedTime(currentState.normalizedTime);

                // 采集 Float 参数（使用游戏实际的参数名）
                var floatParamNames = new string[]
                {
                    "MoveSpeed",    // 0: 移动速度
                    "MoveDirX",     // 1: 移动方向 X
                    "MoveDirY",     // 2: 移动方向 Y
                    "",             // 3: 预留给 HandState (Integer)
                    "",             // 4: 预留
                    "",             // 5: 预留
                    "",             // 6: 预留
                    ""              // 7: 预留
                };

                for (int i = 0; i < Math.Min(floatParamNames.Length, 8); i++)
                {
                    if (string.IsNullOrEmpty(floatParamNames[i])) continue;
                    
                    try
                    {
                        int hash = Animator.StringToHash(floatParamNames[i]);
                        float value = animator.GetFloat(hash);
                        syncData.SetFloatParam(i, value);
                    }
                    catch
                    {
                        // 参数不存在，跳过
                    }
                }

                // 采集 Integer 参数 - HandState
                try
                {
                    int handStateHash = Animator.StringToHash("HandState");
                    int handStateValue = animator.GetInteger(handStateHash);
                    // 存储到预留的 Float 槽位 [3]
                    syncData.SetFloatParam(3, handStateValue);
                }
                catch
                {
                    // 参数不存在，跳过
                }

                // 采集 Bool 参数（使用游戏实际的参数名）
                var boolParamNames = new string[]
                {
                    "Dashing",      // 0: 翻滚/冲刺
                    "RightHandOut", // 1: 右手是否伸出
                    "Attack",       // 2: 攻击状态 (MagicBlend)
                    "GunReady",     // 3: 枪械准备 (MagicBlend)
                    "",             // 4: 预留
                    "",             // 5: 预留
                    "",             // 6: 预留
                    "",             // 7: 预留
                    "",             // 8-31: 更多预留
                };

                for (int i = 0; i < Math.Min(boolParamNames.Length, 32); i++)
                {
                    if (string.IsNullOrEmpty(boolParamNames[i])) continue;
                    
                    try
                    {
                        int hash = Animator.StringToHash(boolParamNames[i]);
                        bool value = animator.GetBool(hash);
                        syncData.SetBoolParam(i, value);
                    }
                    catch
                    {
                        // 参数不存在，跳过
                    }
                }

                return syncData;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AnimatorSyncTestModule] CaptureAnimatorState: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 应用动画状态到 Animator（只负责状态切换，参数由 ContinuouslyApplyParameters 处理）
        /// </summary>
        private void ApplyAnimatorState(Animator animator, AnimatorSyncData syncData)
        {
            try
            {
                // 1. 播放对应状态
                var currentState = animator.GetCurrentAnimatorStateInfo(0);
                if (currentState.fullPathHash != syncData.StateHash)
                {
                    // 状态切换 - 使用 CrossFade 平滑过渡
                    animator.CrossFade(syncData.StateHash, 0.2f, 0, syncData.GetNormalizedTime());
                }
                else
                {
                    // 同步归一化时间
                    float targetTime = syncData.GetNormalizedTime();
                    float currentTime = currentState.normalizedTime % 1f;
                    float timeDiff = Mathf.Abs(targetTime - currentTime);

                    if (timeDiff > 0.1f && timeDiff < 0.9f)
                    {
                        animator.Play(syncData.StateHash, 0, targetTime);
                    }
                }
                
                // 注意：参数由 ContinuouslyApplyParameters() 每帧持续设置
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AnimatorSyncTestModule] ApplyAnimatorState: {ex}");
            }
        }

        /// <summary>
        /// 检查数据是否改变（增量同步优化）
        /// </summary>
        private bool HasDataChanged(AnimatorSyncData newData)
        {
            if (_lastSyncData == null) return true;

            // 状态切换
            if (newData.StateHash != _lastSyncData.StateHash) return true;

            // Bool 参数改变
            if (newData.BoolParams != _lastSyncData.BoolParams) return true;

            // Float 参数改变（阈值 0.02）
            for (int i = 0; i < 8; i++)
            {
                int diff = Math.Abs(newData.FloatParams[i] - _lastSyncData.FloatParams[i]);
                if (diff > 2) // 2 = 0.02 * 100
                {
                    return true;
                }
            }

            // 归一化时间改变（阈值 0.05）
            int timeDiff = Math.Abs(newData.NormalizedTime - _lastSyncData.NormalizedTime);
            if (timeDiff > 3276) // 3276 = 0.05 * 65535
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 删除测试单位
        /// </summary>
        private void DestroyTestCharacter()
        {
            try
            {
                if (_testCharacter != null && _testCharacter is Component component)
                {
                    UnityEngine.Object.Destroy(component.gameObject);
                    _testCharacter = null;
                    _testAnimator = null;
                    _localAnimator = null;
                    _lastSyncData = null;
                    _autoSync = false;
                    _statusInfo = $"✅ 测试单位已删除\n总同步: {_syncCount} 次\n跳过: {_skippedCount} 次";
                }
            }
            catch (Exception ex)
            {
                _statusInfo = $"❌ 删除失败:\n{ex.Message}";
                Debug.LogError($"[AnimatorSyncTestModule] {ex}");
            }
        }

        public void Update()
        {
            if (!IsEnabled || _testCharacter == null) return;

            // 每帧持续应用参数（关键！）
            ContinuouslyApplyParameters();

            // 定期同步（仅在 autoSync 模式）
            if (_autoSync && Time.time - _lastSyncTime >= _syncInterval)
            {
                SyncAnimationOnce();
                _lastSyncTime = Time.time;
            }
        }

        public void OnEnable()
        {
            _statusInfo = "动画同步测试工具\n\n功能:\n- 创建测试单位\n- 实时同步本地玩家动画\n- 验证远程动画同步逻辑\n\n点击创建按钮开始";
        }

        public void OnDisable()
        {
            _autoSync = false;
        }
    }
}
