#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace ExportGlobals.CodeExecution;

/// <summary>
/// Custom AssemblyLoadContext for script execution that resolves assemblies
/// from the host context to avoid type duplication.
/// </summary>
internal sealed class ScriptAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyLoadContext? _hostContext;

    public ScriptAssemblyLoadContext(AssemblyLoadContext? hostContext)
        : base($"Script_{Guid.NewGuid():N}", isCollectible: true)
    {
        _hostContext = hostContext;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var simple = assemblyName.Name;
        if (string.IsNullOrEmpty(simple))
            return null;

        // Prefer whatever is already in Default
        var alreadyDefault = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(a => !a.IsDynamic &&
                                 string.Equals(a.GetName().Name, simple, StringComparison.OrdinalIgnoreCase));
        if (alreadyDefault != null)
            return alreadyDefault;

        // If we have a host context, redirect to it
        if (_hostContext != null && _hostContext != AssemblyLoadContext.Default)
        {
            var fromHost = _hostContext.Assemblies.FirstOrDefault(a => !a.IsDynamic &&
                string.Equals(a.GetName().Name, simple, StringComparison.OrdinalIgnoreCase));
            if (fromHost != null)
                return fromHost;
        }

        return null;
    }
}
