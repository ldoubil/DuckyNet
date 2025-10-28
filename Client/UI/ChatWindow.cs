using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Core;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Services.Generated;

namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 聊天消息数据
    /// </summary>
    public class ChatMessage
    {
        public string SenderName { get; set; } = string.Empty;
        public string SteamId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public MessageType Type { get; set; }
    }

    /// <summary>
    /// 聊天窗口
    /// 支持 Steam 头像、自动渐隐、固定布局、输入屏蔽
    /// </summary>
    public class ChatWindow : IUIWindow
    {
        private readonly RpcClient _rpcClient;
        private readonly List<ChatMessage> _messages = new List<ChatMessage>();
        
        private bool _isVisible = true; // 默认显示
        private bool _showInput = false;
        private bool _isInRoom = false;
        private Vector2 _scrollPosition = Vector2.zero;
        private string _inputMessage = string.Empty;
        
        // 自动渐隐
        private float _lastMessageTime = 0f;
        private const float AUTO_HIDE_DELAY = 5f;
        private float _fadeAlpha = 1f;

        // 固定布局常量
        private const float PANEL_WIDTH = 400f;
        private const float STATUS_HEIGHT = 25f;
        private const float INPUT_AREA_HEIGHT = 80f;
        private const float MESSAGE_AREA_HEIGHT = 320f;
        private const float PANEL_MARGIN_RIGHT = 20f;
        private const float PANEL_MARGIN_BOTTOM = 100f;
        
        // UI 样式
        private GUIStyle? _messageStyle;
        private GUIStyle? _usernameStyle;
        private GUIStyle? _timestampStyle;
        private GUIStyle? _textFieldStyle;
        private GUIStyle? _hintStyle;
        private GUIStyle? _statusConnectedStyle;
        private GUIStyle? _statusDisconnectedStyle;

        public bool IsVisible => _isVisible;
        public bool IsInputActive => _showInput;

        public ChatWindow(RpcClient rpcClient)
        {
            _rpcClient = rpcClient;
        }

        /// <summary>
        /// 设置房间状态
        /// </summary>
        public void SetRoomStatus(bool isInRoom)
        {
            _isInRoom = isInRoom;
        }

        /// <summary>
        /// 获取玩家头像（通过 AvatarManager）
        /// </summary>
        private Texture2D? GetPlayerAvatar(string steamId)
        {
            if (!GameContext.IsInitialized)
            {
                return null;
            }

            return GameContext.Instance.AvatarManager.GetAvatar(steamId);
        }

        /// <summary>
        /// 添加聊天消息
        /// </summary>
        public void AddMessage(PlayerInfo sender, string message)
        {
            _messages.Add(new ChatMessage
            {
                SenderName = sender.SteamName,
                SteamId = sender.SteamId,
                Message = message,
                Time = DateTime.Now,
                Type = MessageType.Info
            });
            
            if (_messages.Count > 100)
            {
                _messages.RemoveAt(0);
            }
            
            _scrollPosition.y = float.MaxValue;
            _lastMessageTime = Time.time;
            _fadeAlpha = 1f;
        }

        public void AddSystemMessage(string message, MessageType type = MessageType.Info)
        {
            _messages.Add(new ChatMessage
            {
                SenderName = "系统",
                SteamId = "system",
                Message = message,
                Time = DateTime.Now,
                Type = type
            });
            
            if (_messages.Count > 100)
            {
                _messages.RemoveAt(0);
            }
            
            _scrollPosition.y = float.MaxValue;
            _lastMessageTime = Time.time;
            _fadeAlpha = 1f;
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
        }

        public void Show()
        {
            _isVisible = true;
        }

        public void Hide()
        {
            _isVisible = false;
            _showInput = false;
        }

        public void OnGUI()
        {
            if (!_isVisible) return;

            InitializeStyles();
            
            // 计算渐隐效果
            if (_messages.Count > 0 && !_showInput)
            {
                float timeSinceLastMessage = Time.time - _lastMessageTime;
                if (timeSinceLastMessage > AUTO_HIDE_DELAY)
                {
                    float fadeProgress = (timeSinceLastMessage - AUTO_HIDE_DELAY) / 1f;
                    _fadeAlpha = Mathf.Clamp01(1f - fadeProgress);
                }
                else
                {
                    _fadeAlpha = 1f;
                }
            }
            else if (_showInput)
            {
                _fadeAlpha = 1f;
            }
            
            // 固定面板位置
            float chatWidth = PANEL_WIDTH;
            float chatHeight = MESSAGE_AREA_HEIGHT + INPUT_AREA_HEIGHT + STATUS_HEIGHT;
            float xPos = Screen.width - chatWidth - PANEL_MARGIN_RIGHT;
            float yPos = Screen.height - chatHeight - PANEL_MARGIN_BOTTOM;

            // 处理输入快捷键
            Event e = Event.current;
            string focusedControl = GUI.GetNameOfFocusedControl();
            
            if (_showInput && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return)
                {
                    if (focusedControl == "ChatInput")
                    {
                        if (!string.IsNullOrWhiteSpace(_inputMessage))
                        {
                            SendMessage();
                        }
                        _showInput = false;
                        _inputMessage = string.Empty;
                        GUI.FocusControl(null);
                    }
                    e.Use();
                    return;
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    _showInput = false;
                    _inputMessage = string.Empty;
                    GUI.FocusControl(null);
                    e.Use();
                    return;
                }
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
            {
                if (_isInRoom && !_showInput && (string.IsNullOrEmpty(focusedControl) || focusedControl == "ChatInput"))
                {
                    _showInput = true;
                    _fadeAlpha = 1f;
                    e.Use();
                }
            }

            // 点击外部关闭输入框
            if (_showInput && e.type == EventType.MouseDown)
            {
                Rect chatArea = new Rect(xPos, yPos, chatWidth, chatHeight);
                if (!chatArea.Contains(e.mousePosition))
                {
                    _showInput = false;
                    _inputMessage = string.Empty;
                    GUI.FocusControl(null);
                    e.Use();
                }
            }

            GUILayout.BeginArea(new Rect(xPos, yPos, chatWidth, chatHeight));

            // 1. 消息区域（可渐隐）
            Color originalColor = GUI.color;
            if (!_showInput)
            {
                GUI.color = new Color(1, 1, 1, _fadeAlpha);
            }
            DrawChatMessages();
            GUI.color = originalColor;

            // 2. 输入区域
            DrawInputArea();

            // 3. 状态栏
            DrawStatusBar();

            GUILayout.EndArea();
        }

        private void DrawChatMessages()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUIStyle.none, GUIStyle.none, GUILayout.Height(MESSAGE_AREA_HEIGHT));
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            
            foreach (var msg in _messages)
            {
                DrawChatMessageItem(msg);
                GUILayout.Space(5);
            }
            
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawChatMessageItem(ChatMessage msg)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUILayout.BeginVertical();
            
            // 头像和昵称
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUILayout.Label(msg.SenderName, _usernameStyle);
            GUILayout.Space(5);
            
            Texture2D? avatar = GetPlayerAvatar(msg.SteamId);
            if (avatar != null)
            {
                GUILayout.Box(avatar, GUIStyle.none, GUILayout.Width(32), GUILayout.Height(32));
            }
            else
            {
                GUILayout.Space(32);
            }
            
            GUILayout.EndHorizontal();
            
            // 消息内容
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            Color msgColor = GetMessageColor(msg.Type);
            Color oldColor = GUI.color;
            GUI.color = msgColor;
            GUILayout.Label(msg.Message, _messageStyle);
            GUI.color = oldColor;
            
            GUILayout.Space(5);
            GUILayout.EndHorizontal();
            
            // 时间戳
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(msg.Time.ToString("HH:mm:ss"), _timestampStyle);
            GUILayout.Space(5);
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawInputArea()
        {
            GUILayout.BeginVertical(GUILayout.Height(INPUT_AREA_HEIGHT));
            GUILayout.FlexibleSpace();
            
            if (_showInput)
            {
                GUI.SetNextControlName("ChatInput");
                _inputMessage = GUILayout.TextField(_inputMessage, _textFieldStyle, GUILayout.Height(34));
                GUI.FocusControl("ChatInput");
                
                GUILayout.Space(2);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Enter 发送  |  ESC 取消", _hintStyle);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(_isInRoom ? "按 Enter 输入消息..." : "未在房间，不能聊天", _hintStyle);
                GUILayout.EndHorizontal();
            }
            
            GUILayout.EndVertical();
        }

        private void DrawStatusBar()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(STATUS_HEIGHT));
            GUILayout.FlexibleSpace();
            
            if (_rpcClient.IsConnected)
            {
                GUILayout.Label("● 已连接", _statusConnectedStyle);
            }
            else
            {
                GUILayout.Label("● 未连接", _statusDisconnectedStyle);
            }
            
            GUILayout.EndHorizontal();
        }

        private void SendMessage()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_inputMessage) || !_rpcClient.IsConnected)
                    return;

                string message = _inputMessage.Trim();
                _inputMessage = string.Empty;

                // 使用自动生成的强类型 Proxy
                var serverContext = new ClientServerContext(_rpcClient);
                var playerService = new PlayerServiceClientProxy(serverContext);
                playerService.SendChatMessage(message);

                Debug.Log($"[ChatWindow] 发送消息: {message}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChatWindow] 发送消息失败: {ex.Message}");
                AddSystemMessage($"发送失败: {ex.Message}", MessageType.Error);
            }
        }

        private Color GetMessageColor(MessageType type)
        {
            return type switch
            {
                MessageType.Warning => Color.yellow,
                MessageType.Error => Color.red,
                MessageType.Success => Color.green,
                _ => Color.white
            };
        }

        private void InitializeStyles()
        {
            if (_messageStyle != null) return;

            _messageStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.MiddleRight,
                padding = new RectOffset(5, 5, 3, 3),
                normal = { textColor = Color.white }
            };

            _usernameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.8f, 0.9f, 1f) }
            };

            _timestampStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 13,
                padding = new RectOffset(8, 8, 8, 8),
                normal = { textColor = Color.white }
            };

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _statusConnectedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.green }
            };

            _statusDisconnectedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.red }
            };
        }

        public void Dispose()
        {
            _messages.Clear();
        }
    }
}
