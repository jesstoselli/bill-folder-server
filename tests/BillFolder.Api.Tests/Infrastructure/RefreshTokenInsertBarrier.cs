using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BillFolder.Api.Tests.Infrastructure;

public sealed class RefreshTokenInsertBarrier : DbCommandInterceptor
{
    private readonly Lock _lock = new();
    private TaskCompletionSource? _insertEntered;
    private TaskCompletionSource? _contenderEntered;
    private TaskCompletionSource? _releaseInsert;

    public void Enable()
    {
        lock (_lock)
        {
            _insertEntered = NewSignal();
            _contenderEntered = NewSignal();
            _releaseInsert = NewSignal();
        }
    }

    public Task WaitUntilInsertEnteredAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return (_insertEntered?.Task ?? Task.CompletedTask).WaitAsync(cancellationToken);
        }
    }

    public Task WaitUntilContenderEnteredAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return (_contenderEntered?.Task ?? Task.CompletedTask).WaitAsync(cancellationToken);
        }
    }

    public void ReleaseInsert()
    {
        lock (_lock)
        {
            _releaseInsert?.TrySetResult();
        }
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("INSERT INTO refresh_tokens", StringComparison.Ordinal))
        {
            Task waitTask;
            lock (_lock)
            {
                _insertEntered?.TrySetResult();
                waitTask = _releaseInsert?.Task ?? Task.CompletedTask;
            }

            await waitTask.WaitAsync(cancellationToken);
        }
        else if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal))
        {
            SignalContender();
        }

        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)
            || (command.CommandText.Contains("UPDATE refresh_tokens", StringComparison.Ordinal)
                && command.CommandText.Contains("family_id", StringComparison.Ordinal)))
        {
            SignalContender();
        }

        return ValueTask.FromResult(result);
    }

    private void SignalContender()
    {
        lock (_lock)
        {
            _contenderEntered?.TrySetResult();
        }
    }

    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
