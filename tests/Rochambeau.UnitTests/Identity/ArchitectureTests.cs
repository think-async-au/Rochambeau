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
        Assert.Contains(ReferencedTypesWithNested(typeof(Offender)), IsForbidden);
    }

    [Fact]
    public void The_check_catches_an_entity_used_only_inside_an_async_method_body()
    {
        Assert.Contains(ReferencedTypesWithNested(typeof(AsyncQueryOffender)), IsForbidden);
    }

    [Fact]
    public void The_check_catches_a_static_member_read_only_inside_a_method_body()
    {
        Assert.Contains(ReferencedTypesWithNested(typeof(StaticMemberOffender)), IsForbidden);
    }

    [Fact]
    public void The_check_ignores_a_type_that_uses_only_the_contracts()
    {
        Assert.DoesNotContain(ReferencedTypesWithNested(typeof(ContractsOnlyUser)), IsForbidden);
    }

    private static IEnumerable<Type> ReferencedTypesWithNested(Type type) =>
        ReferencedTypes(type).Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(ReferencedTypesWithNested));

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
            direct.AddRange(BodyReferences(method));
        }

        return direct.OfType<Type>().SelectMany(Expand);
    }

    private static readonly Dictionary<short, System.Reflection.Emit.OpCode> _opCodes =
        typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)!)
            .ToDictionary(o => o.Value);

    /// <summary>
    /// Types a method body touches through its IL tokens — calls, field reads, generic method
    /// arguments, casts, <c>typeof</c> — which signatures and locals alone never show.
    /// </summary>
    private static IEnumerable<Type?> BodyReferences(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        var typeArgs = method.DeclaringType is { IsGenericType: true } d ? d.GetGenericArguments() : null;
        var methodArgs = method is MethodInfo { IsGenericMethod: true } ? method.GetGenericArguments() : null;
        var position = 0;
        while (position < il.Length)
        {
            short value = il[position++];
            if (value == 0xFE)
            {
                value = unchecked((short)(0xFE00 | il[position++]));
            }

            var opCode = _opCodes[value];
            switch (opCode.OperandType)
            {
                case System.Reflection.Emit.OperandType.InlineMethod:
                case System.Reflection.Emit.OperandType.InlineField:
                case System.Reflection.Emit.OperandType.InlineType:
                case System.Reflection.Emit.OperandType.InlineTok:
                    var member = method.Module.ResolveMember(BitConverter.ToInt32(il, position), typeArgs, methodArgs);
                    foreach (var referenced in TypesOf(member))
                    {
                        yield return referenced;
                    }

                    position += 4;
                    break;
                case System.Reflection.Emit.OperandType.InlineSwitch:
                    position += 4 + (4 * BitConverter.ToInt32(il, position));
                    break;
                default:
                    position += OperandSize(opCode.OperandType);
                    break;
            }
        }
    }

    private static IEnumerable<Type?> TypesOf(MemberInfo? member) => member switch
    {
        Type type => [type],
        FieldInfo field => [field.DeclaringType, field.FieldType],
        MethodInfo method => [method.DeclaringType, method.ReturnType, .. method.GetParameters().Select(p => p.ParameterType),
            .. method.IsGenericMethod ? method.GetGenericArguments() : []],
        ConstructorInfo constructor => [constructor.DeclaringType, .. constructor.GetParameters().Select(p => p.ParameterType)],
        _ => [],
    };

    private static int OperandSize(System.Reflection.Emit.OperandType operand) => operand switch
    {
        System.Reflection.Emit.OperandType.InlineNone => 0,
        System.Reflection.Emit.OperandType.ShortInlineBrTarget
            or System.Reflection.Emit.OperandType.ShortInlineI
            or System.Reflection.Emit.OperandType.ShortInlineVar => 1,
        System.Reflection.Emit.OperandType.InlineVar => 2,
        System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR => 8,
        _ => 4,
    };

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

    /// <summary>A Play-like query that touches the Identity entity only inside an async method body.</summary>
    private static class AsyncQueryOffender
    {
        public static async Task<int> CountAccountsAsync(Rochambeau.Api.Common.Persistence.AppDbContext db) =>
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
                db.Set<Rochambeau.Api.Modules.Identity.Infrastructure.Persistence.Account>());
    }

    /// <summary>Reads an Identity-internal static member only inside a method body.</summary>
    private static class StaticMemberOffender
    {
        public static string Code() => Rochambeau.Api.Modules.Identity.Domain.IdentityErrors.NotAuthenticated.Code;
    }

    private static class ContractsOnlyUser
    {
        public static string? Who(ICurrentAccount current) => current.Current?.Username;
    }
}
