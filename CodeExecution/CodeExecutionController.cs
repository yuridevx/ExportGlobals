using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExportGlobals.CodeExecution.Results;

namespace ExportGlobals.CodeExecution;

public class CodeExecutionController
{
    private readonly ScriptExecutor _executor = new();

    public async Task<ExecuteToolResult> ExecuteAsync(string code, TimeSpan timeout)
    {
        if (string.IsNullOrEmpty(code))
            return ExecuteToolResult.Failure("Missing 'code' argument");

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var result = await _executor.ExecuteAsync(code, cts.Token);
            return ExecuteToolResult.Completed(result);
        }
        catch (OperationCanceledException)
        {
            return ExecuteToolResult.Failure($"Execution timed out after {timeout.TotalSeconds:F0}s");
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join("\n", ex.Diagnostics.Select(d =>
                $"  Line {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}"));
            return ExecuteToolResult.CompilationFailed(errors);
        }
        catch (Exception ex)
        {
            return ExecuteToolResult.Failure($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
