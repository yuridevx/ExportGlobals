using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExportGlobals.CodeExecution.Results;

namespace ExportGlobals.CodeExecution;

public class CodeExecutionController
{
    private readonly ScriptExecutor _executor = new();
    private ExecutionSession _activeSession;
    private ExecutionSession _lastSession;

    public async Task<ExecuteToolResult> ExecuteAsync(string code, TimeSpan timeout)
    {
        if (string.IsNullOrEmpty(code))
            return ExecuteToolResult.Failure("Missing 'code' argument");

        if (_activeSession is { IsCompleted: false })
            return ExecuteToolResult.Failure("Another execution is in progress");

        try
        {
            var cts = new CancellationTokenSource(timeout);
            var task = _executor.ExecuteAsync(code, cts.Token);
            _activeSession = new ExecutionSession(task);

            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                var sessionId = _activeSession.Id;
                _lastSession = _activeSession;
                _activeSession = null;
                return ExecuteToolResult.Failure($"Execution timed out after {timeout.TotalSeconds:F0}s. Session {sessionId} was cancelled.");
            }
            catch (Exception ex)
            {
                _lastSession = _activeSession;
                _activeSession = null;
                return ExecuteToolResult.Failure($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }

            var result = _activeSession.IsSuccess
                ? ExecuteToolResult.Completed(_activeSession.Result)
                : ExecuteToolResult.Failure(_activeSession.Error);

            _lastSession = _activeSession;
            _activeSession = null;
            return result;
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join("\n", ex.Diagnostics.Select(d =>
                $"  Line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}"));

            return ExecuteToolResult.CompilationFailed(errors);
        }
    }

    public CancelToolResult Cancel(string sessionId)
    {
        if (_activeSession?.Id != sessionId)
            return CancelToolResult.NotFound();

        _activeSession.Cancel();
        _lastSession = _activeSession;
        _activeSession = null;

        return CancelToolResult.Cancelled(sessionId);
    }

    public string GetActiveSessionId() => _activeSession?.Id;
}
