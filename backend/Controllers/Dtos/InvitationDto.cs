namespace Controllers.Dtos
{
    public record class InvitationDto
{
    public required string Username { get; init; }
    public required string GameType { get; init; }
    public required string Code { get; init; }
}
}