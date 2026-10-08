namespace BillFolder.Application.Abstractions.Time;

public interface IBusinessDateProvider
{
    DateOnly Today { get; }
}
