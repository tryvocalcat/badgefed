using System.Reflection;

namespace BadgeFed.Services;

/// <summary>
/// Register implementations of this interface from a private plugin (via IHostingStartup)
/// to expose Blazor component assemblies to the main Router in Routes.razor.
/// The OSS app registers no providers by default — all components are found in the main assembly.
/// </summary>
public interface IPluginAssemblyProvider
{
    Assembly GetAssembly();
}
