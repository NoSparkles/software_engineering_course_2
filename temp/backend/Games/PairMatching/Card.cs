namespace games
{
    public class Card
    {
        public int Value { get; set; }
        public CardState state { get; set; }

        public Card(int value)
        {
            Value = value;
            state = CardState.FaceDown;
        }
    }
}