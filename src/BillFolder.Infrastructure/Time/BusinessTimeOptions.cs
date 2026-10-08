namespace BillFolder.Infrastructure.Time;

public sealed class BusinessTimeOptions
{
    public const string SectionName = "BusinessTime";
    public const string DefaultTimeZoneId = "America/Sao_Paulo";

    public string TimeZoneId { get; init; } = DefaultTimeZoneId;
}
