#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ExportGlobals.CodeExecution;

/// <summary>
/// Exception thrown when script compilation fails.
/// </summary>
public class CompilationErrorException : Exception
{
    public IEnumerable<Diagnostic> Diagnostics { get; }

    public CompilationErrorException(string message, IEnumerable<Diagnostic> diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics;
    }
}
