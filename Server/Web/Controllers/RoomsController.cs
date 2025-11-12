using Microsoft.AspNetCore.Mvc;
using DuckyNet.Server.Managers;
using System.Linq;

namespace DuckyNet.Server.Web.Controllers
{
    /// <summary>
    /// 后台管理 - 房间管理
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RoomsController : ControllerBase
    {
        private readonly RoomManager _roomManager;
        private readonly PlayerManager _playerManager;

        public RoomsController(RoomManager roomManager, PlayerManager playerManager)
        {
            _roomManager = roomManager;
            _playerManager = playerManager;
        }

        /// <summary>
        /// 获取所有房间列表
        /// </summary>
        [HttpGet]
        public IActionResult GetAllRooms()
        {
            var rooms = _roomManager.GetAllRooms();
            var roomList = rooms.Select(room => new
            {
                roomId = room.RoomId,
                roomName = room.RoomName,
                description = room.Description,
                hostSteamId = room.HostSteamId,
                currentPlayers = room.CurrentPlayers,
                maxPlayers = room.MaxPlayers,
                requirePassword = room.RequirePassword,
                createTime = room.CreateTime,
                isFull = room.IsFull
            }).ToList();

            return Ok(roomList);
        }

        /// <summary>
        /// 获取指定房间的详细信息
        /// </summary>
        [HttpGet("{roomId}")]
        public IActionResult GetRoomDetail(string roomId)
        {
            var room = _roomManager.GetRoom(roomId);
            if (room == null)
            {
                return NotFound(new { message = "房间不存在" });
            }

            var players = _roomManager.GetRoomPlayers(roomId);
            var playerList = players.Select(p => new
            {
                steamId = p.SteamId,
                steamName = p.SteamName,
                sceneName = p.CurrentScenelData?.SceneName ?? "",
                subSceneName = p.CurrentScenelData?.SubSceneName ?? ""
            }).ToList();

            return Ok(new
            {
                roomInfo = new
                {
                    roomId = room.RoomId,
                    roomName = room.RoomName,
                    description = room.Description,
                    hostSteamId = room.HostSteamId,
                    currentPlayers = room.CurrentPlayers,
                    maxPlayers = room.MaxPlayers,
                    requirePassword = room.RequirePassword,
                    createTime = room.CreateTime
                },
                players = playerList
            });
        }
    }
}

