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
    /// 大厅页面
    /// <para>负责展示房间列表、刷新房间、创建房间与加入房间的 UI 与交互逻辑。</para>
    /// <para>通过 <see cref="RpcClient"/> 调用服务器端的房间服务接口 <see cref="IRoomService"/>。</para>
    /// </summary>
    public class LobbyPage
    {
        /// <summary>
        /// RPC 客户端，用于与服务器进行交互
        /// </summary>
        private readonly RpcClient _client;

        /// <summary>
        /// 房间服务客户端代理（复用，避免重复创建）
        /// </summary>
        private readonly RoomServiceClientProxy _roomService;

        /// <summary>
        /// 主菜单窗口引用，用于页面切换与房间页联动
        /// </summary>
        private readonly MainMenuWindow _mainWindow;

        /// <summary>
        /// 房间列表滚动视图位置
        /// </summary>
        private Vector2 _scrollPos;

        /// <summary>
        /// 当前获取到的房间列表（来自服务器）
        /// </summary>
        private List<RoomInfo> _roomList = new List<RoomInfo>();

        // 创建房间输入
        /// <summary>
        /// 待创建房间名称
        /// </summary>
        private string _newRoomName = "我的房间";
        /// <summary>
        /// 待创建房间密码（可为空）
        /// </summary>
        private string _newRoomPassword = "";
        /// <summary>
        /// 待创建房间描述
        /// </summary>
        private string _newRoomDescription = "";
    /// <summary>
    /// 待创建房间最大人数（2-16）
    /// </summary>
    private int _newRoomMaxPlayers = 8;

    // 加入房间密码输入（每个房间独立的密码输入框）
    /// <summary>
    /// 房间密码输入字典（RoomId -> Password）
    /// </summary>
    private Dictionary<string, string> _roomPasswords = new Dictionary<string, string>();

    /// <summary>
    /// 构造函数
    /// </summary>
        /// <param name="client">用于与服务器交互的 RPC 客户端</param>
        /// <param name="mainWindow">主菜单窗口，用于页面切换</param>
        public LobbyPage(RpcClient client, MainMenuWindow mainWindow)
        {
            _client = client;
            _mainWindow = mainWindow;

            // 预先创建并缓存服务代理，后续各方法直接复用
            var serverContext = new ClientServerContext(_client);
            _roomService = new RoomServiceClientProxy(serverContext);
        }

        /// <summary>
        /// 绘制大厅页面（在 OnGUI 中被调用）
        /// </summary>
        public void Draw()
        {
            DrawHeaderAndActions();
            DrawRoomListSection();
            DrawCreateRoomSection();
        }

        /// <summary>
        /// 绘制标题与顶部操作（刷新按钮）
        /// </summary>
        private void DrawHeaderAndActions()
        {
            GUILayout.Label("游戏大厅", GUI.skin.box);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            
            // 刷新房间列表按钮
            if (GUILayout.Button("刷新房间列表"))
            {
                RefreshRoomListAsync();
            }

            GUILayout.FlexibleSpace();
            
            // 断开连接按钮
            if (GUILayout.Button("断开连接", GUILayout.Width(100)))
            {
                _client.Disconnect();
                _mainWindow.SwitchToPage(MainMenuPage.Connect);
            }
            
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        /// <summary>
        /// 绘制房间列表区域
        /// </summary>
        private void DrawRoomListSection()
        {
            // 房间列表
            GUILayout.Label($"房间列表 ({_roomList.Count})", GUI.skin.box);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));

            foreach (var room in _roomList)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                
                // 第一行：房间名称 + 是否加密
                GUILayout.BeginHorizontal();
                string lockIcon = room.RequirePassword ? "🔒" : "🔓";
                GUILayout.Label($"{lockIcon} {room.RoomName}", GUILayout.Width(200));
                GUILayout.Label($"[{room.CurrentPlayers}/{room.MaxPlayers}]", GUILayout.Width(50));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                // 第二行：简介
                if (!string.IsNullOrEmpty(room.Description))
                {
                    GUILayout.Label($"简介: {room.Description}");
                }
                else
                {
                    GUILayout.Label("简介: (无)", GUI.skin.label);
                }

                // 第三行：密码输入框（如果需要）+ 加入按钮
                GUILayout.BeginHorizontal();
                
                if (room.RequirePassword)
                {
                    GUILayout.Label("密码:", GUILayout.Width(40));
                    
                    // 确保字典中有这个房间的条目
                    if (!_roomPasswords.ContainsKey(room.RoomId))
                    {
                        _roomPasswords[room.RoomId] = "";
                    }
                    
                    _roomPasswords[room.RoomId] = GUILayout.PasswordField(_roomPasswords[room.RoomId], '*', GUILayout.Width(120));
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }
                
                GUILayout.FlexibleSpace();
                
                if (GUILayout.Button("加入", GUILayout.Width(80)))
                {
                    string password = room.RequirePassword && _roomPasswords.ContainsKey(room.RoomId) 
                        ? _roomPasswords[room.RoomId] 
                        : "";
                    JoinRoomAsync(room.RoomId, password);
                }

                GUILayout.EndHorizontal();
                
                GUILayout.EndVertical();
                GUILayout.Space(5);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);
        }


        /// <summary>
        /// 绘制创建房间区域
        /// </summary>
        private void DrawCreateRoomSection()
        {
            // 创建房间
            GUILayout.Label("创建房间", GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("房间名称:", GUILayout.Width(80));
            _newRoomName = GUILayout.TextField(_newRoomName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("密码:", GUILayout.Width(80));
            _newRoomPassword = GUILayout.PasswordField(_newRoomPassword, '*');
            GUILayout.Label("(可选)", GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("描述:", GUILayout.Width(80));
            _newRoomDescription = GUILayout.TextField(_newRoomDescription);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("最大人数:", GUILayout.Width(80));
            _newRoomMaxPlayers = (int)GUILayout.HorizontalSlider(_newRoomMaxPlayers, 2, 16);
            GUILayout.Label(_newRoomMaxPlayers.ToString(), GUILayout.Width(30));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("创建房间"))
            {
                CreateRoomAsync();
            }
        }

        private async void RefreshRoomListAsync()
        {
            try
            {
                var rooms = await _roomService.GetRoomListAsync();

                _roomList = new List<RoomInfo>(rooms);
                UnityEngine.Debug.Log($"[LobbyPage] 房间列表已刷新：共 {rooms.Length} 个房间");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LobbyPage] 刷新房间列表失败：{ex.Message}");
            }
        }

        private async void CreateRoomAsync()
        {
            try
            {
                var result = await _roomService.CreateRoomAsync(new CreateRoomRequest
                {
                    RoomName = _newRoomName,
                    Password = _newRoomPassword,
                    Description = _newRoomDescription,
                    MaxPlayers = _newRoomMaxPlayers
                });

                if (result.Success && result.Room != null)
                {
                    UnityEngine.Debug.Log($"[LobbyPage] 房间创建成功：{result.Room.RoomId}");
                    
                    // 🔥 创建房间成功后，立即同步场景信息（现在已经在房间中了）
                    if (GameContext.IsInitialized)
                    {
                        var sceneManager = GameContext.Instance.SceneClientManager;
                        var localPlayer = GameContext.Instance.PlayerManager.LocalPlayer;
                        
                        if (!string.IsNullOrEmpty(sceneManager._scenelDataList.SceneName))
                        {
                            // 🔥 更新本地玩家的场景信息
                            localPlayer.Info.CurrentScenelData = sceneManager._scenelDataList;
                            UnityEngine.Debug.Log($"[LobbyPage] 🔥 创建房间后同步场景信息: {sceneManager._scenelDataList.SceneName}");
                            
                            // 🔥 发送场景进入请求（现在服务器知道你在房间中了，会广播给房间内所有人）
                            var sceneService = new SceneServiceClientProxy(new ClientServerContext(_client));
                            await sceneService.EnterSceneAsync(sceneManager._scenelDataList);
                            UnityEngine.Debug.Log($"[LobbyPage] 🔥 场景同步完成");
                        }
                        else
                        {
                            UnityEngine.Debug.Log($"[LobbyPage] ⚠️ 当前未在场景中，跳过场景同步");
                        }
                    }
                    
                    _mainWindow.RoomPage.SetCurrentRoom(result.Room);
                    _mainWindow.SwitchToPage(MainMenuPage.Room);
                }
                else
                {
                    UnityEngine.Debug.LogError($"[LobbyPage] 创建房间失败：{result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LobbyPage] 创建房间出错：{ex.Message}");
            }
        }

        private async void JoinRoomAsync(string roomId, string password)
        {
            try
            {
                var request = new JoinRoomRequest
                {
                    RoomId = roomId,
                    Password = password
                };

                var result = await _roomService.JoinRoomAsync(request);

                if (result.Success && result.Room != null)
                {
                    UnityEngine.Debug.Log($"[LobbyPage] 加入房间成功：{roomId}");
                    
                    // 🔥 加入房间成功后，立即同步场景信息（现在已经在房间中了）
                    if (GameContext.IsInitialized)
                    {
                        var sceneManager = GameContext.Instance.SceneClientManager;
                        var localPlayer = GameContext.Instance.PlayerManager.LocalPlayer;
                        
                        if (!string.IsNullOrEmpty(sceneManager._scenelDataList.SceneName))
                        {
                            // 🔥 更新本地玩家的场景信息
                            localPlayer.Info.CurrentScenelData = sceneManager._scenelDataList;
                            UnityEngine.Debug.Log($"[LobbyPage] 🔥 加入房间后同步场景信息: {sceneManager._scenelDataList.SceneName}");
                            
                            // 🔥 发送场景进入请求（现在服务器知道你在房间中了，会广播给房间内所有人）
                            var sceneService = new SceneServiceClientProxy(new ClientServerContext(_client));
                            await sceneService.EnterSceneAsync(sceneManager._scenelDataList);
                            UnityEngine.Debug.Log($"[LobbyPage] 🔥 场景同步完成");
                        }
                        else
                        {
                            UnityEngine.Debug.Log($"[LobbyPage] ⚠️ 当前未在场景中，跳过场景同步");
                        }
                    }
                    
                    _mainWindow.RoomPage.SetCurrentRoom(result.Room);
                    _mainWindow.SwitchToPage(MainMenuPage.Room);
                }
                else
                {
                    UnityEngine.Debug.LogError($"[LobbyPage] 加入房间失败：{result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[LobbyPage] 加入房间出错：{ex.Message}");
            }
        }
    }
}

