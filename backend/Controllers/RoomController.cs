using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RoomController : ControllerBase
    {
        private readonly RoomService _roomService;

        public RoomController(RoomService roomService)
        {
            _roomService = roomService;
        }

        [HttpGet("exists/{gameType}/{code}")]
        public async Task<ActionResult> RoomExists(string gameType, string code)
        {
            var res = _roomService.RoomExists(gameType, code);
            if (!res)
            {
                return NotFound(res);
            }
            return Ok(res);
        }
    }
}
