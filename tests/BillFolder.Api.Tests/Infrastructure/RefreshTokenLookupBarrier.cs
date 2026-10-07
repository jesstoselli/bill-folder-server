using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BillFolder.Api.Tests.Infrastructure;

public sealed class RefreshTokenLookupBarrier : DbCommandInterceptor
{
    private readonly Lock _lock = new();
    private TaskCompletionSource? _release;
    private int _remaining;

    public void Enable(int participants)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(participants, 2);

        lock (_lock)
        {
            _remaining = participants;
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        var waitTask = SignalIfRefreshTokenLookup(command.CommandText);
        await waitTask.WaitAsync(cancellationToken);

        return result;
    }

    private Task SignalIfRefreshTokenLookup(string commandText)
    {
        if (!commandText.Contains("FROM refresh_tokens", StringComparison.Ordinal)
            || !commandText.Contains("revoked_at", StringComparison.Ordinal)
            || !commandText.Contains("token_hash", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            if (_release is null || _remaining == 0)
            {
                return Task.CompletedTask;
            }

            _remaining--;
            if (_remaining == 0)
            {
                _release.TrySetResult();
            }

            return _release.Task;
        }
    }
}
