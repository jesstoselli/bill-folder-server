using BillFolder.Application.Abstractions.Time;
using Microsoft.Extensions.Options;

namespace BillFolder.Infrastructure.Time;

public sealed class BusinessDateProvider : IBusinessDateProvider
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _businessTimeZone;

    public BusinessDateProvider(
        TimeProvider timeProvider,
        IOptions<BusinessTimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _timeProvider = timeProvider;
        _businessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    public DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _businessTimeZone).Date);
}
