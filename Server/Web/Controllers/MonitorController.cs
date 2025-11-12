using Microsoft.AspNetCore.Mvc;
using DuckyNet.Server.Managers;
using System;
using System.Linq;

namespace DuckyNet.Server.Web.Controllers
{
    /// <summary>
    /// 后台管理 - 实时监控
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MonitorController : ControllerBase
    {
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly SceneManager _sceneManager;
        private readonly PlayerNpcManager _npcManager;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public MonitorController(
            PlayerManager playerManager,
            RoomManager roomManager,
            SceneManager sceneManager,
            PlayerNpcManager npcManager)
        {
            _playerManager = playerManager;
            _roomManager = roomManager;
            _sceneManager = sceneManager;
            _npcManager = npcManager;
        }

        /// <summary>
        /// 获取服务器性能统计
        /// </summary>
        [HttpGet("performance")]
        public IActionResult GetPerformance()
        {
            var uptime = DateTime.UtcNow - _startTime;
            var players = _playerManager.GetAllOnlinePlayers();
            var rooms = _roomManager.GetAllRooms();
            var npcStats = _npcManager.GetStats();

            return Ok(new
            {
                server = new
                {
                    startTime = _startTime,
                    uptime = new
                    {
                        days = uptime.Days,
                        hours = uptime.Hours,
                        minutes = uptime.Minutes,
                        seconds = uptime.Seconds,
                        totalSeconds = (long)uptime.TotalSeconds
                    },
                    uptimeString = $"{uptime.Days}天 {uptime.Hours}小时 {uptime.Minutes}分钟"
                },
                connections = new
                {
                    totalPlayers = players.Length,
                    pendingLogins = _playerManager.GetStatistics().PendingLogins,
                    refreshMethod = "HTTP Polling (3s)"
                },
                resources = new
                {
                    totalRooms = rooms.Length,
                    totalNpcs = npcStats.TotalNpcs,
                    playersWithNpcs = npcStats.TotalPlayers
                },
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// 获取玩家分布统计
        /// </summary>
        [HttpGet("player-distribution")]
        public IActionResult GetPlayerDistribution()
        {
            var players = _playerManager.GetAllOnlinePlayers();
            var rooms = _roomManager.GetAllRooms();

            // 按房间分组
            var roomDistribution = rooms.Select(room => new
            {
                roomId = room.RoomId,
                roomName = room.RoomName,
                playerCount = room.CurrentPlayers,
                maxPlayers = room.MaxPlayers,
                utilization = room.MaxPlayers > 0 
                    ? (double)room.CurrentPlayers / room.MaxPlayers * 100 
                    : 0
            }).ToList();

            // 按场景分组
            var sceneDistribution = players
                .Where(p => !string.IsNullOrEmpty(p.CurrentScenelData?.SceneName))
                .GroupBy(p => new 
                { 
                    SceneName = p.CurrentScenelData!.SceneName,
                    SubSceneName = p.CurrentScenelData.SubSceneName 
                })
                .Select(g => new
                {
                    sceneName = g.Key.SceneName,
                    subSceneName = g.Key.SubSceneName,
                    playerCount = g.Count(),
                    players = g.Select(p => p.SteamName).ToList()
                })
                .OrderByDescending(x => x.playerCount)
                .ToList();

            return Ok(new
            {
                totalPlayers = players.Length,
                playersInRooms = rooms.Sum(r => r.CurrentPlayers),
                playersInScenes = sceneDistribution.Sum(s => s.playerCount),
                roomDistribution,
                sceneDistribution
            });
        }

        /// <summary>
        /// 获取NPC统计详情
        /// </summary>
        [HttpGet("npc-stats")]
        public IActionResult GetNpcStats()
        {
            var npcStats = _npcManager.GetStats();
            var players = _playerManager.GetAllOnlinePlayers();

            // 获取每个玩家的NPC数量
            var playerNpcCounts = players.Select(p => new
            {
                steamId = p.SteamId,
                steamName = p.SteamName,
                npcCount = _npcManager.GetPlayerNpcs(p.SteamId).Count
            })
            .Where(x => x.npcCount > 0)
            .OrderByDescending(x => x.npcCount)
            .ToList();

            // 按场景统计NPC
            var sceneNpcCounts = players
                .Where(p => !string.IsNullOrEmpty(p.CurrentScenelData?.SceneName))
                .Select(p => new
                {
                    sceneName = p.CurrentScenelData!.SceneName,
                    subSceneName = p.CurrentScenelData.SubSceneName,
                    npcCount = _npcManager.GetSceneNpcs(
                        p.CurrentScenelData.SceneName, 
                        p.CurrentScenelData.SubSceneName).Count
                })
                .GroupBy(x => new { x.sceneName, x.subSceneName })
                .Select(g => new
                {
                    sceneName = g.Key.sceneName,
                    subSceneName = g.Key.subSceneName,
                    npcCount = g.First().npcCount
                })
                .OrderByDescending(x => x.npcCount)
                .ToList();

            return Ok(new
            {
                overview = new
                {
                    totalNpcs = npcStats.TotalNpcs,
                    playersWithNpcs = npcStats.TotalPlayers,
                    averageNpcsPerPlayer = npcStats.TotalPlayers > 0 
                        ? (double)npcStats.TotalNpcs / npcStats.TotalPlayers 
                        : 0
                },
                playerNpcCounts,
                sceneNpcCounts
            });
        }

        /// <summary>
        /// 获取热门场景排行
        /// </summary>
        [HttpGet("hot-scenes")]
        public IActionResult GetHotScenes()
        {
            var players = _playerManager.GetAllOnlinePlayers();

            var hotScenes = players
                .Where(p => !string.IsNullOrEmpty(p.CurrentScenelData?.SceneName))
                .GroupBy(p => new 
                { 
                    SceneName = p.CurrentScenelData!.SceneName,
                    SubSceneName = p.CurrentScenelData.SubSceneName 
                })
                .Select(g => new
                {
                    sceneName = g.Key.SceneName,
                    subSceneName = g.Key.SubSceneName,
                    playerCount = g.Count(),
                    npcCount = _npcManager.GetSceneNpcs(g.Key.SceneName, g.Key.SubSceneName).Count,
                    players = g.Select(p => new
                    {
                        steamId = p.SteamId,
                        steamName = p.SteamName
                    }).ToList()
                })
                .OrderByDescending(x => x.playerCount)
                .Take(10)
                .ToList();

            return Ok(hotScenes);
        }

        /// <summary>
        /// 获取系统健康状态
        /// </summary>
        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            var uptime = DateTime.UtcNow - _startTime;
            var players = _playerManager.GetAllOnlinePlayers();
            var rooms = _roomManager.GetAllRooms();

            var isHealthy = true;
            var warnings = new System.Collections.Generic.List<string>();

            // 检查是否有玩家但没有房间
            if (players.Length > 0 && rooms.Length == 0)
            {
                warnings.Add("有玩家在线但没有活跃房间");
            }

            // 检查是否有房间满员
            var fullRooms = rooms.Count(r => r.IsFull);
            if (fullRooms > 0)
            {
                warnings.Add($"有 {fullRooms} 个房间已满员");
            }

            return Ok(new
            {
                status = isHealthy ? "healthy" : "warning",
                uptime = $"{uptime.Days}天 {uptime.Hours}小时 {uptime.Minutes}分钟",
                metrics = new
                {
                    onlinePlayers = players.Length,
                    activeRooms = rooms.Length
                },
                warnings
            });
        }
    }
}

