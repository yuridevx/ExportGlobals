using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExportGlobals.CodeExecution;

public class ExecutionSession
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task<object> _task;

    public ExecutionSession(Task<object> task)
    {
        _task = task;
        _ = MonitorTaskAsync();
    }

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public ExecutionSessionStatus Status { get; private set; } = ExecutionSessionStatus.Running;

    public Task CompletionTask => _task;

    public bool IsCompleted => Status is ExecutionSessionStatus.Completed or ExecutionSessionStatus.Faulted
        or ExecutionSessionStatus.Cancelled;

    public bool IsSuccess => Status == ExecutionSessionStatus.Completed;
    public object Result { get; private set; }
    public string Error { get; private set; }

    public CancellationToken CancellationToken => _cts.Token;

    private async Task MonitorTaskAsync()
    {
        try
        {
            Result = await _task;
            Status = ExecutionSessionStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            Status = ExecutionSessionStatus.Cancelled;
        }
        catch (Exception ex)
        {
            Error = FormatException(ex);
            Status = ExecutionSessionStatus.Faulted;
        }
    }

    private static string FormatException(Exception ex)
    {
        return $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
    }

    public void Cancel()
    {
        _cts.Cancel();
        Status = ExecutionSessionStatus.Cancelled;
    }
}
