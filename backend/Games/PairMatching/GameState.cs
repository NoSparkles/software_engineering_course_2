namespace Games
{
    public class GameState
    {
        public List<CardInfo> Board { get; set; } = new();
        public string CurrentPlayer { get; set; } = "";
        public List<int> Flipped { get; set; } = new();
        public Dictionary<string, int> Scores { get; set; } = new();
        public string Winner { get; set; } = "";
    }
}