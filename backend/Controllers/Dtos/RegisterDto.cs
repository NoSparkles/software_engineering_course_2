namespace Controllers.Dtos
{
    public record class RegisterDto
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}