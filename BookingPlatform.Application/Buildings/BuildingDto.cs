namespace BookingPlatform.Application.Buildings;

public sealed class BuildingDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

