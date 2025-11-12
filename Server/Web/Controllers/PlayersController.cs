using Microsoft.AspNetCore.Mvc;
using DuckyNet.Server.Managers;
using System.Linq;

namespace DuckyNet.Server.Web.Controllers
{
    /// <summary>
    /// 后台管理 - 玩家管理
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PlayersController : ControllerBase
    {
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly SceneManager _sceneManager;

        public PlayersController(
            PlayerManager playerManager,
            RoomManager roomManager,
            SceneManager sceneManager)
        {
            _playerManager = playerManager;
            _roomManager = roomManager;
            _sceneManager = sceneManager;
        }

        /// <summary>
        /// 获取所有在线玩家列表
        /// </summary>
        [HttpGet]
        public IActionResult GetAllPlayers()
        {
            var players = _playerManager.GetAllOnlinePlayers();
            var playerList = players.Select(p =>
            {
                var room = _roomManager.GetPlayerRoom(p);
                var position = _sceneManager.GetPlayerPosition(p.SteamId);

                return new
                {
                    steamId = p.SteamId,
                    steamName = p.SteamName,
                    sceneName = p.CurrentScenelData?.SceneName ?? "",
                    subSceneName = p.CurrentScenelData?.SubSceneName ?? "",
                    roomId = room?.RoomId ?? "",
                    roomName = room?.RoomName ?? "",
                    position = position != null ? new
                    {
                        x = position.Value.X,
                        y = position.Value.Y,
                        z = position.Value.Z
                    } : null
                };
            }).ToList();

            return Ok(playerList);
        }

        /// <summary>
        /// 获取指定玩家的详细信息
        /// </summary>
        [HttpGet("{steamId}")]
        public IActionResult GetPlayerDetail(string steamId)
        {
            var player = _playerManager.GetPlayerBySteamId(steamId);
            if (player == null)
            {
                return NotFound(new { message = "玩家不存在" });
            }

            var room = _roomManager.GetPlayerRoom(player);
            var position = _sceneManager.GetPlayerPosition(steamId);

            return Ok(new
            {
                steamId = player.SteamId,
                steamName = player.SteamName,
                sceneName = player.CurrentScenelData?.SceneName ?? "",
                subSceneName = player.CurrentScenelData?.SubSceneName ?? "",
                roomId = room?.RoomId ?? "",
                roomName = room?.RoomName ?? "",
                position = position != null ? new
                {
                    x = position.Value.X,
                    y = position.Value.Y,
                    z = position.Value.Z
                } : null
            });
        }
    }
}

