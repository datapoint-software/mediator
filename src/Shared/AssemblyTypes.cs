using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Datapoint.Mediator;

// The types an assembly scan can use. Linked as source into the packages that scan assemblies.
internal static class AssemblyTypes
{
    // Excluded from coverage: ReflectionTypeLoadException only occurs when an assembly references a dependency that is
    // missing at runtime, which a test can only reproduce with a deliberately broken build.
    [ExcludeFromCodeCoverage(Justification = "Requires an assembly with a missing dependency.")]
    public static IEnumerable<Type> GetLoadable(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // The types that failed to load are null.
            return exception.Types.OfType<Type>();
        }
    }
}
