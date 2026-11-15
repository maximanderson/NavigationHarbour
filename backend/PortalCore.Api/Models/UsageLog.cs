namespace PortalCore.Api.Models;

public class UsageLog
{
    public int Id { get; set; }

    public string? UserName { get; set; }
    public string? ScreenId { get; set; }
    public string? NodeId { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? ClientIp { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}