using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;

using Ramstack.Parsing.Internal;

namespace Ramstack.Parsing;

/// <summary>
/// Represents a default implementation of the <see cref="Binder"/> class.
/// </summary>
public class DefaultBinder : Binder
{
    private static readonly Dictionary<string, (Type type, bool isStatic)> Empty = new();

    /// <summary>
    /// A predefined dictionary of safe common .NET types.
    /// </summary>
    private static readonly Dictionary<string, (Type type, bool isStatic)> PredefinedType = new(StringComparer.OrdinalIgnoreCase)
    {
        { "bool", (typeof(bool), false) },
        { "boolean", (typeof(bool), false) },
        { "byte", (typeof(byte), false) },
        { "sbyte", (typeof(sbyte), false) },
        { "short", (typeof(short), false) },
        { "ushort", (typeof(ushort), false) },
        { "int", (typeof(int), false) },
        { "uint", (typeof(uint), false) },
        { "long", (typeof(long), false) },
        { "ulong", (typeof(ulong), false) },
        { "int8", (typeof(sbyte), false) },
        { "uint8", (typeof(byte), false) },
        { "int16", (typeof(short), false) },
        { "uint16", (typeof(ushort), false) },
        { "int32", (typeof(int), false) },
        { "uint32", (typeof(uint), false) },
        { "int64", (typeof(long), false) },
        { "uint64", (typeof(ulong), false) },
        #if NET7_0_OR_GREATER
        { "int128", (typeof(Int128), false) },
        { "uint128", (typeof(UInt128), false) },
        #endif
        { "nint", (typeof(nint), false) },
        { "nuint", (typeof(nuint), false) },
        { "half", (typeof(Half), false) },
        { "float", (typeof(float), false) },
        { "single", (typeof(float), false) },
        { "double", (typeof(double), false) },
        { "decimal", (typeof(decimal), false) },
        { "bigint", (typeof(BigInteger), false) },
        { "biginteger", (typeof(BigInteger), false) },
        { "string", (typeof(string), true) },
        { "char", (typeof(char), false) },
        { "object", (typeof(object), true) },
        { nameof(Convert), (typeof(Convert), false) },
        { nameof(CultureInfo), (typeof(CultureInfo), true) },
        { nameof(DateTime), (typeof(DateTime), false) },
        { nameof(DateTimeOffset), (typeof(DateTimeOffset), false) },
        { nameof(DateOnly), (typeof(DateOnly), false) },
        { nameof(TimeOnly), (typeof(TimeOnly), false) },
        { nameof(DateTimeKind), (typeof(DateTimeKind), false) },
        { nameof(DayOfWeek), (typeof(DayOfWeek), false) },
        { nameof(Guid), (typeof(Guid), false) },
        { nameof(Math), (typeof(Math), true) },
        { nameof(MathF), (typeof(MathF), true) },
        { nameof(Regex), (typeof(Regex), false) },
        { nameof(RegexOptions), (typeof(RegexOptions), false) },
        { nameof(StringComparer), (typeof(StringComparer), false) },
        { nameof(StringComparison), (typeof(StringComparison), false) },
        { nameof(StringSplitOptions), (typeof(StringSplitOptions), true) },
        { nameof(TimeSpan), (typeof(TimeSpan), false) },
        { nameof(TimeZoneInfo), (typeof(TimeZoneInfo), false) },
        { nameof(Version), (typeof(Version), true) }
    };

    /// <summary>
    /// A dictionary of user-registered types.
    /// </summary>
    private Dictionary<string, (Type type, bool isStatic)> _types = Empty;

    /// <summary>
    /// Registers a custom type for use in binding operations, allowing it to be resolved by name.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to register for binding.</param>
    /// <param name="importAsStatic">A <see cref="bool"/> indicating whether the type's members
    /// should be imported directly into scope as static members (default is <see langword="false"/>).</param>
    public void RegisterType(Type type, bool importAsStatic = false)
    {
        if (!PredefinedType.ContainsKey(type.Name))
        {
            if (ReferenceEquals(_types, Empty))
                _types = new Dictionary<string, (Type, bool)>(StringComparer.OrdinalIgnoreCase);

            _types[type.Name] = (type, importAsStatic);
        }
    }

    /// <inheritdoc />
    public override Type? BindToType(Identifier typeName)
    {
        if (!PredefinedType.TryGetValue(typeName.Name, out var value))
            _types.TryGetValue(typeName.Name, out value);

        return value.type;
    }

    /// <inheritdoc />
    public override MethodInfo? BindToMethod(Type? type, Identifier methodName, Type[] parameterTypes, bool isStatic)
    {
        var q = type is not null
            ? GetMethods(type, methodName.Name, isStatic)
            : PredefinedType
                .Concat(_types)
                .Where(t => t.Value.isStatic)
                .SelectMany(t => GetMethods(t.Value.type, methodName.Name, isStatic));

        var methods = q.ToArray();
        if (methods.Length == 0)
            return null;

        var method = (MethodInfo?)MethodResolver.ResolveMethod(methods, parameterTypes);
        if (method is null)
            Error.AmbiguousMatch(methods);

        return method;
    }

    /// <inheritdoc />
    public override MemberInfo? BindToMember(Type? type, Identifier memberName, bool isStatic)
    {
        var bindingFlags = isStatic
            ? BindingFlags.Public | BindingFlags.Static
            : BindingFlags.Public | BindingFlags.Instance;

        var q = type?.GetMembers(bindingFlags)
            ?? PredefinedType
                .Concat(_types)
                .Where(t => t.Value.isStatic)
                .SelectMany(t => t.Value.type.GetMembers(bindingFlags));

        var candidates = q
            .Where(m =>
                m.Name.Equals(memberName.Name, StringComparison.OrdinalIgnoreCase)
                && m.MemberType is MemberTypes.Field or MemberTypes.Property)
            .ToArray();

        switch (candidates.Length)
        {
            case 0: return null;
            case 1: return candidates[0];
        }

        Error.AmbiguousMatch(candidates);
        return null;
    }

    private static IEnumerable<MethodBase> GetMethods(Type type, string methodName, bool isStatic)
    {
        var bindingFlags = isStatic
            ? BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Static
            : BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance;

        return type
            .GetMethods(bindingFlags)
            .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
            .Distinct();
    }
}
