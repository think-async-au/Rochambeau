using System.Reflection;
using Rochambeau.Api.Modules.Identity.Contracts;

namespace Rochambeau.UnitTests.Identity;

/// <summary>QG-4: outside <c>Modules/Identity/</c>, only the Identity <c>Contracts/</c> may be used (accounts ADR-0004).</summary>
public class ArchitectureTests
{
    private const string _identityModule = "Rochambeau.Api.Modules.Identity";

    private static readonly string[] _forbidden =
    [
        _identityModule + ".Domain",
        _identityModule + ".Application",
        _identityModule + ".Infrastructure",
        _identityModule + ".Endpoints",
        _identityModule + ".Commands",
        "Microsoft.AspNetCore.Identity",
    ];

    [Fact]
    public void Types_outside_the_identity_module_reference_only_its_contracts()
    {
        var offenders = typeof(ICurrentAccount).Assembly.GetTypes()
            .Where(t => !InNamespace(t, _identityModule))
            .SelectMany(t => ReferencedTypes(t).Where(IsForbidden).Select(r => $"{t.FullName} → {r.FullName}"))
            .Distinct()
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_check_catches_an_offender()
    {
        Assert.Contains(ReferencedTypes(typeof(Offender)), IsForbidden);
    }

    private static bool IsForbidden(Type type) => _forbidden.Any(ns => InNamespace(type, ns));

    private static bool InNamespace(Type type, string ns) =>
        type.Namespace is { } n && (n == ns || n.StartsWith(ns + ".", StringComparison.Ordinal));

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        var direct = new List<Type?> { type.BaseType };
        direct.AddRange(type.GetInterfaces());
        direct.AddRange(type.GetFields(all).Select(f => f.FieldType));
        direct.AddRange(type.GetProperties(all).Select(p => p.PropertyType));
        direct.AddRange(type.GetEvents(all).Select(e => e.EventHandlerType));
        foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
        {
            direct.AddRange(method.GetParameters().Select(p => p.ParameterType));
            if (method is MethodInfo info)
            {
                direct.Add(info.ReturnType);
            }

            direct.AddRange(method.GetMethodBody()?.LocalVariables.Select(l => l.LocalType) ?? []);
        }

        return direct.OfType<Type>().SelectMany(Expand);
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.HasElementType)
        {
            return Expand(type.GetElementType()!);
        }

        return type.IsGenericType
            ? type.GetGenericArguments().SelectMany(Expand).Append(type.GetGenericTypeDefinition())
            : [type];
    }

    private sealed class Offender
    {
        public Microsoft.AspNetCore.Identity.IdentityOptions? Options { get; init; }
    }
}
