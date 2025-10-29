using System;
using UnityEngine;
using DuckyNet.Client.RPC;

namespace DuckyNet.Client.Core.Deb
{
    /// <summary>
    /// 网络调试模块 - 显示连接状态和 RPC 信息
    /// </summary>
    public class NetworkDebugModule : IDebugModule
    {
        private readonly RpcClient _client;
        private int _totalRpcs = 0;
        private int _pendingRpcs = 0;

        public string ModuleName => "网络状态";
        public string Category => "网络";
        public string Description => "显示 RPC 连接状态和调用统计";
        public bool IsEnabled { get; set; } = true;

        public NetworkDebugModule(RpcClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void OnGUI()
        {
            GUILayout.BeginVertical();

            // 连接状态
            var statusStyle = new GUIStyle(GUI.skin.label);
            if (_client.IsConnected)
            {
                statusStyle.normal.textColor = Color.green;
                GUILayout.Label($"● 已连接", statusStyle);
            }
            else
            {
                statusStyle.normal.textColor = Color.red;
                GUILayout.Label($"● 未连接", statusStyle);
            }

            GUILayout.Label($"连接状态: {_client.ConnectionState}");

            // RPC 统计
            GUILayout.Space(5);
            GUILayout.Label($"总 RPC 调用: {_totalRpcs}", GUI.skin.label);
            GUILayout.Label($"待处理 RPC: {_pendingRpcs}", GUI.skin.label);

            // 操作按钮
            GUILayout.Space(5);
            if (GUILayout.Button("重置统计"))
            {
                _totalRpcs = 0;
                _pendingRpcs = 0;
            }

            GUILayout.EndVertical();
        }

        public void Update()
        {
            // 可以在这里更新统计数据
            // 注意：需要访问 RpcClient 的内部字段才能获取真实统计数据
            // 这里暂时显示示例值
        }
    }
}
