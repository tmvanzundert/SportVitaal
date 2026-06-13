using System.Reflection;

namespace SportVitaal.Shared
{
    /// <summary>
    /// Lets a head (e.g. the Web app) register extra assemblies whose routable pages the
    /// shared <c>Routes</c> component should discover. Read as a static rather than passed as a
    /// component parameter, because <c>Routes</c> is rendered with an interactive render mode and
    /// its parameters would otherwise have to be JSON-serializable (assemblies are not).
    /// The MAUI head leaves this null, so it only scans the Shared assembly.
    /// </summary>
    public static class RouteAssemblyRegistry
    {
        public static IReadOnlyList<Assembly>? Additional { get; set; }
    }
}
