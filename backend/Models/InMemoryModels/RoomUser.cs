namespace Models.InMemoryModels
{
    public class RoomUser
    {
        private User? User { get; set; }
        private string? PlayerId { get; set; }
        private bool IsPlayer { get; set; }
    }
}