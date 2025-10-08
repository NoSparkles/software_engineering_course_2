namespace Extensions
{
    public static class RoomKeyExtensions
    {
        public static string ToRoomKey(this string gameType, string roomCode) =>
            $"{gameType}:{roomCode.ToUpper()}";
    }
}