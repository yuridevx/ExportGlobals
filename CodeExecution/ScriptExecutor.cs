#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ExportGlobals.CodeExecution;

public class ScriptExecutor
{
    private static MetadataReference[] BuildReferences(AssemblyLoadContext? baseAlc, AssemblyLoadContext? currentAlc)
    {
        var refs = new List<MetadataReference>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddAssemblyReference(Assembly assembly)
        {
            if (assembly.IsDynamic)
                return;

            if (string.IsNullOrEmpty(assembly.Location))
                return;

            if (!File.Exists(assembly.Location))
                return;

            if (addedPaths.Contains(assembly.Location))
                return;

            try
            {
                refs.Add(MetadataReference.CreateFromFile(assembly.Location));
                addedPaths.Add(assembly.Location);
            }
            catch
            {
                // Skip assemblies that can't be referenced
            }
        }

        if (baseAlc != null)
        {
            foreach (var assembly in baseAlc.Assemblies)
            {
                AddAssemblyReference(assembly);
            }
        }

        if (currentAlc != null && currentAlc != baseAlc)
        {
            foreach (var assembly in currentAlc.Assemblies)
            {
                AddAssemblyReference(assembly);
            }
        }

        return refs.ToArray();
    }

    public async Task<object> ExecuteAsync(string code, CancellationToken ct)
    {
        return await RunScriptAsync(code, ct);
    }

    private async Task<object> RunScriptAsync(string code, CancellationToken ct = default)
    {
        var hostContext = AssemblyLoadContext.GetLoadContext(typeof(ScriptExecutor).Assembly);
        var baseAlc = AssemblyLoadContext.Default;
        var scriptAlc = new ScriptAssemblyLoadContext(hostContext);

        try
        {
            var references = BuildReferences(baseAlc, hostContext);

            var compilation = CSharpCompilation.Create(
                $"Script_{Guid.NewGuid():N}",
                new[] { CSharpSyntaxTree.ParseText(code, cancellationToken: ct) },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms, cancellationToken: ct);
            if (!emitResult.Success)
            {
                var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
                throw new CompilationErrorException(
                    $"Compilation failed:\n{string.Join("\n", errors.Select(e => e.GetMessage()))}",
                    errors
                );
            }

            ms.Position = 0;
            var assembly = scriptAlc.LoadFromStream(ms);

            // Find the first public class in the assembly
            var scriptType = assembly.GetTypes()
                .FirstOrDefault(t => t.IsClass && t.IsPublic && !t.IsAbstract && !t.IsGenericTypeDefinition)
                ?? throw new InvalidOperationException("No public class found in compiled code");

            // Find Execute method
            var executeMethod = scriptType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
                null, new[] { typeof(CancellationToken) }, null)
                ?? throw new InvalidOperationException("Could not find Execute method. Expected: public async Task<object> Execute(CancellationToken ct) or public static async Task<object> Execute(CancellationToken ct)");

            // Create instance only if method is not static
            object? instance = null;
            if (!executeMethod.IsStatic)
            {
                instance = Activator.CreateInstance(scriptType);
            }

            var result = await (Task<object>)executeMethod.Invoke(instance, new object[] { ct })!;
            return result;
        }
        finally
        {
            scriptAlc.Unload();
        }
    }
}
