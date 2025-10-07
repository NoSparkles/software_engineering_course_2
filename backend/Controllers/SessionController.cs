using Microsoft.AspNetCore.Mvc;
using Services;
using Models.InMemoryModels;
using Models; // PATCH: Add this if RoomUser is defined in Models namespace
using System.Linq;

namespace Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : ControllerBase
    {
        private readonly RoomService RoomService;

        public SessionController(RoomService roomService)
        {
            RoomService = roomService;
        }

        [HttpGet("active-session")]
        public IActionResult GetActiveSession([FromQuery] string? playerId, [FromQuery] string? username)
        {
            Console.WriteLine($"GetActiveSession called with playerId: {playerId}, username: {username}");
            
            Room? foundRoom = null;
            string? foundCloseTime = null;

            // Check all rooms with active timers (room close time set)
            var rooms = RoomService.Rooms.Values
                .Where(room => room.RoomCloseTime != null && room.RoomCloseTime > DateTime.UtcNow)
                .ToList();

            Console.WriteLine($"Found {rooms.Count} rooms with active timers");

            // Try by username first
            if (!string.IsNullOrEmpty(username))
            {
                foundRoom = rooms.FirstOrDefault(room =>
                {
                    var hasUsername = room.RoomPlayers.Any(p => p.Username == username) || 
                                    room.DisconnectedPlayers.Values.Any(p => p.Username == username);
                    Console.WriteLine($"Room {room.Code}: hasUsername={hasUsername}, RoomPlayers={room.RoomPlayers.Count}, DisconnectedPlayers={room.DisconnectedPlayers.Count}");
                    return hasUsername;
                });
                
                if (foundRoom != null)
                {
                    foundCloseTime = foundRoom.RoomCloseTime?.ToString("o");
                    Console.WriteLine($"Found room by username: {foundRoom.Code}, closeTime: {foundCloseTime}");
                }
            }

            // Then by playerId
            if (foundRoom == null && !string.IsNullOrEmpty(playerId))
            {
                foundRoom = rooms.FirstOrDefault(room =>
                {
                    var hasPlayerId = room.RoomPlayers.Any(p => p.PlayerId == playerId) || 
                                    room.DisconnectedPlayers.ContainsKey(playerId);
                    Console.WriteLine($"Room {room.Code}: hasPlayerId={hasPlayerId}");
                    return hasPlayerId;
                });
                
                if (foundRoom != null)
                {
                    foundCloseTime = foundRoom.RoomCloseTime?.ToString("o");
                    Console.WriteLine($"Found room by playerId: {foundRoom.Code}, closeTime: {foundCloseTime}");
                }
            }

            // Return active session if found
            if (foundRoom != null && foundCloseTime != null)
            {
                var gameType = foundRoom.Game.GetType().Name.ToLower()
                    .Replace("game", "")
                    .Replace("instance", "")
                    .Replace("rockpaperscissors", "rock-paper-scissors")
                    .Replace("fourinarow", "four-in-a-row")
                    .Replace("pairmatching", "pair-matching");

                var result = new
                {
                    activeGame = new
                    {
                        gameType = gameType,
                        code = foundRoom.Code,
                        isMatchmaking = foundRoom.IsMatchMaking
                    },
                    roomCloseTime = foundCloseTime
                };

                Console.WriteLine($"Returning active session: {System.Text.Json.JsonSerializer.Serialize(result)}");
                return Ok(result);
            }
            
            Console.WriteLine("No active session found");
            return Ok(new { activeGame = (object?)null, roomCloseTime = (string?)null });
        }
    }
}