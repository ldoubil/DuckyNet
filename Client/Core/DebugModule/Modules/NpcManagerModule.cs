using System;
using System.Linq;
using UnityEngine;
using DuckyNet.Client.Core.DebugModule;

namespace DuckyNet.Client.Core.DebugModule.Modules
{
    /// <summary>
    /// NPC 管理调试模块
    /// </summary>
    public class NpcManagerModule : IDebugModule
    {
        public string ModuleName => "NPC 管理器";
        public string Category => "游戏";
        public string Description => "管理和监控所有 NPC 的状态";
        public bool IsEnabled { get; set; } = true;

        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showAliveOnly = true;
        private bool _showDeadOnly = false;
        private NpcInfo? _selectedNpc;

        // GUI 样式
        private GUIStyle? _headerStyle;
        private GUIStyle? _aliveStyle;
        private GUIStyle? _deadStyle;
        private GUIStyle? _selectedStyle;

        public void OnGUI()
        {
            if (!GameContext.IsInitialized) return;

            InitializeStyles();

            var npcManager = GameContext.Instance.NpcManager;
            if (npcManager == null)
            {
                GUILayout.Label("⚠️ NPC 管理器未初始化");
                return;
            }

            DrawControls(npcManager);
            DrawNpcList(npcManager);
            DrawSelectedNpcDetails();
        }

        /// <summary>
        /// 初始化样式
        /// </summary>
        private void InitializeStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _aliveStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { textColor = new Color(0.4f, 1f, 0.4f) },
                fontSize = 10
            };

            _deadStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { textColor = new Color(1f, 0.4f, 0.4f) },
                fontSize = 10
            };

            _selectedStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0.3f, 0.6f, 1f, 0.3f)) }
            };
        }

        /// <summary>
        /// 绘制控制面板
        /// </summary>
        private void DrawControls(NpcManager npcManager)
        {
            GUILayout.BeginHorizontal();

            // 统计信息
            var allNpcs = npcManager.GetAllNpcs().ToList();
            var aliveCount = allNpcs.Count(n => n.IsAlive);
            var deadCount = allNpcs.Count(n => !n.IsAlive);

            GUILayout.Label($"📊 总计: {allNpcs.Count} | ❤️ 存活: {aliveCount} | 💀 死亡: {deadCount}", _headerStyle);

            // 可见性统计
            var visStats = npcManager.VisibilityManager.GetStats();
                GUILayout.Label($"🔍 可见性: 追踪{visStats.TrackedNpcs} | 远程{visStats.VisibleRemoteNpcs} | 范围{visStats.SyncRange}m");

            // 对象池统计
            var poolStats = npcManager.NpcPool.GetStats();
            GUILayout.Label($"♻️ 对象池: 活动{poolStats.ActiveNpcs} | 池中{poolStats.PooledNpcs} | 复用率{poolStats.ReuseRate:F1}% | 类型{poolStats.PoolTypes}");

            GUILayout.FlexibleSpace();

            // 清理按钮
            if (GUILayout.Button("🗑️ 清空", GUILayout.Width(60), GUILayout.Height(25)))
            {
                npcManager.Clear();
                _selectedNpc = null;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // 过滤器
            GUILayout.BeginHorizontal();
            GUILayout.Label("🔍 搜索:", GUILayout.Width(50));
            _searchFilter = GUILayout.TextField(_searchFilter, GUILayout.Width(150));

            GUILayout.Space(10);
            _showAliveOnly = GUILayout.Toggle(_showAliveOnly, "只显示存活", GUILayout.Width(100));
            _showDeadOnly = GUILayout.Toggle(_showDeadOnly, "只显示死亡", GUILayout.Width(100));

            GUILayout.EndHorizontal();

            GUILayout.Space(5);
        }

        /// <summary>
        /// 绘制 NPC 列表
        /// </summary>
        private void DrawNpcList(NpcManager npcManager)
        {
            var npcs = npcManager.GetAllNpcs();

            // 应用过滤器
            if (_showAliveOnly && !_showDeadOnly)
            {
                npcs = npcs.Where(n => n.IsAlive);
            }
            else if (_showDeadOnly && !_showAliveOnly)
            {
                npcs = npcs.Where(n => !n.IsAlive);
            }

            if (!string.IsNullOrEmpty(_searchFilter))
            {
                npcs = npcs.Where(n => n.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var npcList = npcs.OrderByDescending(n => n.IsAlive).ThenBy(n => n.Name).ToList();

            // 列表
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(300));

            foreach (var npc in npcList)
            {
                DrawNpcItem(npc);
            }

            if (npcList.Count == 0)
            {
                GUILayout.Label("没有找到 NPC", GUILayout.Height(50));
            }

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 绘制单个 NPC 项
        /// </summary>
        private void DrawNpcItem(NpcInfo npc)
        {
            var style = npc.IsAlive ? _aliveStyle : _deadStyle;
            var isSelected = _selectedNpc?.Id == npc.Id;

            if (isSelected)
            {
                GUILayout.BeginVertical(_selectedStyle);
            }
            else
            {
                GUILayout.BeginVertical(GUI.skin.box);
            }

            if (GUILayout.Button($"{(npc.IsAlive ? "❤️" : "💀")} ID:{npc.Id} - {npc.Name}", style, GUILayout.Height(25)))
            {
                _selectedNpc = npc;
            }

            GUILayout.BeginHorizontal();

            // 血量条
            if (npc.IsAlive)
            {
                DrawHealthBar(npc.CurrentHealth, npc.MaxHealth, 150, 15);
                GUILayout.Label($"{npc.CurrentHealth:F0}/{npc.MaxHealth:F0} ({npc.HealthPercent:F1}%)", GUILayout.Width(120));
            }
            else
            {
                GUILayout.Label($"💀 死亡时间: {npc.AliveTime:F1}s", GUILayout.Width(150));
            }

            GUILayout.FlexibleSpace();

            // 位置
            GUILayout.Label($"📍 ({npc.Position.x:F1}, {npc.Position.y:F1}, {npc.Position.z:F1})", GUILayout.Width(180));

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(2);
        }

        /// <summary>
        /// 绘制选中的 NPC 详情
        /// </summary>
        private void DrawSelectedNpcDetails()
        {
            if (_selectedNpc == null) return;

            GUILayout.Space(10);
            GUILayout.Label("📋 选中 NPC 详情", _headerStyle);

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.Label($"🆔 ID: {_selectedNpc.Id}");
            GUILayout.Label($"📛 名称: {_selectedNpc.Name}");
            GUILayout.Label($"❤️ 状态: {(_selectedNpc.IsAlive ? "存活" : "死亡")}");
            
            if (_selectedNpc.IsAlive)
            {
                GUILayout.Label($"💚 血量: {_selectedNpc.CurrentHealth:F0}/{_selectedNpc.MaxHealth:F0} ({_selectedNpc.HealthPercent:F1}%)");
            }
            
            GUILayout.Label($"📍 位置: ({_selectedNpc.Position.x:F2}, {_selectedNpc.Position.y:F2}, {_selectedNpc.Position.z:F2})");
            GUILayout.Label($"⏱️ {(_selectedNpc.IsAlive ? "存活时间" : "生存时长")}: {_selectedNpc.AliveTime:F2}s");

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();

            // 定位按钮
            if (_selectedNpc.GameObject != null && GUILayout.Button("📌 定位到 NPC", GUILayout.Height(30)))
            {
                // 让摄像机看向 NPC（如果需要可以实现）
                Debug.Log($"[NpcManagerModule] 定位到 NPC: {_selectedNpc.Name} at {_selectedNpc.Position}");
            }

            // 取消选择
            if (GUILayout.Button("❌ 取消选择", GUILayout.Height(30)))
            {
                _selectedNpc = null;
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制血量条
        /// </summary>
        private void DrawHealthBar(float current, float max, float width, float height)
        {
            Rect barRect = GUILayoutUtility.GetRect(width, height);
            
            // 背景
            GUI.DrawTexture(barRect, MakeTexture(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.8f)));
            
            // 前景
            float percent = max > 0 ? current / max : 0f;
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * percent, barRect.height);
            
            Color barColor = percent > 0.6f ? new Color(0.2f, 0.8f, 0.2f) : 
                             percent > 0.3f ? new Color(0.8f, 0.8f, 0.2f) : 
                             new Color(0.8f, 0.2f, 0.2f);
            
            GUI.DrawTexture(fillRect, MakeTexture(2, 2, barColor));
        }

        /// <summary>
        /// 创建纹理
        /// </summary>
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public void Update()
        {
            // 每帧更新
        }
    }
}

