using System.Reflection;

namespace Ramstack.Parsing.Internal;

/// <summary>
/// Provides extension methods for the <see cref="Type" /> and <see cref="MemberInfo" /> classes.
/// </summary>
internal static class ReflectionExtensions
{
    /// <summary>
    /// Determines whether the specified type is a <see cref="Nullable{T}" /> type.
    /// </summary>
    /// <param name="type">The <see cref="Type" /> to evaluate.</param>
    /// <returns>
    /// <see langword="true" /> if the type is a <see cref="Nullable{T}" /> type; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsNullable(this Type type) =>
        type.IsGenericType && !type.IsGenericTypeDefinition && type.GetGenericTypeDefinition() == typeof(Nullable<>);

    /// <summary>
    /// Returns the underlying type of the specified <see cref="Nullable{T}" /> type, or the type itself for any other type.
    /// </summary>
    /// <param name="type">The <see cref="Type" /> to evaluate.</param>
    /// <returns>
    /// The underlying type if <paramref name="type" /> is a <see cref="Nullable{T}" /> type;
    /// otherwise, <paramref name="type" />.
    /// </returns>
    public static Type GetNonNullableType(this Type type) =>
        Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>
    /// Returns the <see cref="Nullable{T}" /> type for the specified non-nullable value type.
    /// </summary>
    /// <param name="type">The non-nullable value type.</param>
    /// <returns>
    /// The <see cref="Nullable{T}" /> type whose underlying type is <paramref name="type" />.
    /// </returns>
    public static Type MakeNullableType(this Type type) =>
        typeof(Nullable<>).MakeGenericType(type);

    /// <summary>
    /// Determines whether the specified type is declared as static.
    /// </summary>
    /// <param name="type">The <see cref="Type" /> to evaluate.</param>
    /// <returns>
    /// <see langword="true" /> if the type is declared as static; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsStatic(this Type type) =>
        type.IsClass && type.IsAbstract && type.IsSealed;

    /// <summary>
    /// Returns the types whose members are looked up when a member is accessed through the specified type.
    /// </summary>
    /// <param name="type">The type through which the member is accessed.</param>
    /// <returns>
    /// The specified type and, if it is an interface, its base interfaces and <see cref="object" />.
    /// </returns>
    /// <remarks>
    /// For an interface, <see cref="Type.GetMembers()" /> returns only the members the interface declares,
    /// while in C# member lookup in an interface also finds the members of its base interfaces and <see cref="object" />.
    /// </remarks>
    public static IEnumerable<Type> GetMemberLookupTypes(this Type type)
    {
        if (type.IsInterface)
            return [type, ..type.GetInterfaces(), typeof(object)];

        return [type];
    }

    /// <summary>
    /// Determines whether the specified type is a base type of another type.
    /// </summary>
    /// <param name="type">The type to evaluate.</param>
    /// <param name="other">The type whose base types are checked.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="type" /> is a base type of <paramref name="other" />;
    /// otherwise, <see langword="false" />.
    /// </returns>
    /// <remarks>
    /// As in C#, base types are determined by inheritance: the base types of a class or struct are its base classes,
    /// and the base types of an interface are its base interfaces and <see cref="object" />. An interface implemented
    /// by a class or struct is not its base type, and an instantiation of a variant interface is not a base type
    /// of another instantiation that is assignable to it. A type is not a base type of itself.
    /// </remarks>
    public static bool IsBaseTypeOf(this Type type, Type other)
    {
        if (type.IsInterface)
            return other.IsInterface && other.GetInterfaces().Contains(type);

        return other.IsSubclassOf(type)
            || other.IsInterface && type == typeof(object);
    }

    /// <summary>
    /// Determines whether the specified member is declared as static.
    /// </summary>
    /// <param name="memberInfo">The <see cref="MemberInfo" /> instance representing the member to evaluate.</param>
    /// <returns>
    /// <see langword="true" /> if the member is declared as static; otherwise, <see langword="false" />.
    /// The determination is made as follows:
    /// <list type="bullet">
    ///   <item><description>For constructors, checks <see cref="MethodBase.IsStatic" />.</description></item>
    ///   <item><description>For events, checks whether the event's <c>AddMethod</c> is static.</description></item>
    ///   <item><description>For fields, checks <see cref="FieldInfo.IsStatic" />.</description></item>
    ///   <item><description>For methods, checks <see cref="MethodBase.IsStatic" />.</description></item>
    ///   <item><description>For properties, checks whether the property's <c>GetMethod</c> or <c>SetMethod</c> is static.</description></item>
    ///   <item><description>For types (including nested types), checks whether the type is static using <see cref="IsStatic(Type)" />.</description></item>
    /// </list>
    /// </returns>
    public static bool IsStatic(this MemberInfo memberInfo)
    {
        bool result;

        switch (memberInfo.MemberType)
        {
            case MemberTypes.Constructor:
                result = ((ConstructorInfo)memberInfo).IsStatic;
                break;

            case MemberTypes.Event:
                result = ((EventInfo)memberInfo).AddMethod!.IsStatic;
                break;

            case MemberTypes.Field:
                result = ((FieldInfo)memberInfo).IsStatic;
                break;

            case MemberTypes.Method:
                result = ((MethodBase)memberInfo).IsStatic;
                break;

            case MemberTypes.Property:
                var pi = (PropertyInfo)memberInfo;
                result = (pi.GetMethod ?? pi.SetMethod)!.IsStatic;
                break;

            case MemberTypes.TypeInfo:
            case MemberTypes.NestedType:
                result = ((Type)memberInfo).IsStatic();
                break;

            default:
                throw new ArgumentException(
                    $"The member type '{memberInfo.MemberType}' is not supported.",
                    nameof(memberInfo));
        }

        return result;
    }

    /// <summary>
    /// Determines the type associated with the specified member.
    /// </summary>
    /// <param name="memberInfo">The <see cref="MemberInfo" /> instance representing the member to evaluate.</param>
    /// <returns>
    /// The <see cref="Type" /> associated with the member, determined as follows:
    /// <list type="bullet">
    ///   <item><description>If the member is a field, returns <see cref="FieldInfo.FieldType" />.</description></item>
    ///   <item><description>If the member is a property, returns <see cref="PropertyInfo.PropertyType" />.</description></item>
    ///   <item><description>If the member is an event, returns <see cref="EventInfo.EventHandlerType" />.</description></item>
    ///   <item><description>If the member is a method, returns <see cref="MethodInfo.ReturnType" />.</description></item>
    ///   <item><description>If the member is a constructor, returns <see cref="MemberInfo.DeclaringType" /> of the constructor.</description></item>
    ///   <item><description>If the member is a type (including nested types), returns the <see cref="Type" /> itself.</description></item>
    /// </list>
    /// </returns>
    public static Type GetMemberType(this MemberInfo memberInfo)
    {
        return memberInfo.MemberType switch
        {
            MemberTypes.Field => ((FieldInfo)memberInfo).FieldType,
            MemberTypes.Property => ((PropertyInfo)memberInfo).PropertyType,
            MemberTypes.Event => ((EventInfo)memberInfo).EventHandlerType,
            MemberTypes.Method => ((MethodInfo)memberInfo).ReturnType,
            MemberTypes.Constructor => memberInfo.DeclaringType,
            MemberTypes.TypeInfo or MemberTypes.NestedType => (Type)memberInfo,
            _ => null
        } ?? throw new ArgumentException(
            $"The member type '{memberInfo.MemberType}' is not supported or the type of the member cannot be determined.",
            nameof(memberInfo));
    }
}
