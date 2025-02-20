using System.Reflection;

namespace Ramstack.Parsing.Internal;

/// <summary>
/// Provides reflection based extension methods.
/// </summary>
internal static class ReflectionExtensions
{
    /// <summary>
    /// Determines whether the specified type is a <see cref="Nullable{T}"/> type.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the type is a <see cref="Nullable{T}"/> type; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNullable(this Type type) =>
        type.IsGenericType && !type.IsGenericTypeDefinition && type.GetGenericTypeDefinition() == typeof(Nullable<>);

    /// <summary>
    /// Determines whether the specified type is declared as static.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the type is declared as static; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsStatic(this Type type) =>
        type.IsClass && type.IsAbstract && type.IsSealed;

    /// <summary>
    /// Determines whether the specified member is declared as static.
    /// </summary>
    /// <param name="memberInfo">The <see cref="MemberInfo"/> instance representing the member to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the member is declared as static; otherwise, <c>false</c>.
    /// The determination is made as follows:
    /// <list type="bullet">
    ///   <item><description>For constructors, checks <see cref="MethodBase.IsStatic"/>.</description></item>
    ///   <item><description>For events, checks whether the event's <c>AddMethod</c> is static.</description></item>
    ///   <item><description>For fields, checks <see cref="FieldInfo.IsStatic"/>.</description></item>
    ///   <item><description>For methods, checks <see cref="MethodBase.IsStatic"/>.</description></item>
    ///   <item><description>For properties, checks whether the property's <c>GetMethod</c> or <c>SetMethod</c> is static.</description></item>
    ///   <item><description>For types (including nested types), checks whether the type is static using <see cref="IsStatic(Type)"/>.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the member type is unsupported or cannot be evaluated for static declaration.
    /// </exception>
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
    /// Determines the runtime type associated with the specified member.
    /// </summary>
    /// <param name="memberInfo">The <see cref="MemberInfo"/> instance representing the member to evaluate.</param>
    /// <returns>
    /// The <see cref="Type"/> associated with the member, determined as follows:
    /// <list type="bullet">
    ///   <item><description>If the member is a field, returns <see cref="FieldInfo.FieldType"/>.</description></item>
    ///   <item><description>If the member is a property, returns <see cref="PropertyInfo.PropertyType"/>.</description></item>
    ///   <item><description>If the member is an event, returns <see cref="EventInfo.EventHandlerType"/>.</description></item>
    ///   <item><description>If the member is a method, returns <see cref="MethodInfo.ReturnType"/>.</description></item>
    ///   <item><description>If the member is a constructor, returns <see cref="MemberInfo.DeclaringType"/> of the constructor.</description></item>
    ///   <item><description>If the member is a type, returns the <see cref="Type"/> itself.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the member type is unsupported or the runtime type cannot be determined.
    /// </exception>
    public static Type GetMemberType(this MemberInfo memberInfo)
    {
        return memberInfo.MemberType switch
        {
            MemberTypes.Field => ((FieldInfo)memberInfo).FieldType,
            MemberTypes.Property => ((PropertyInfo)memberInfo).PropertyType,
            MemberTypes.Event => ((EventInfo)memberInfo).EventHandlerType,
            MemberTypes.Method => ((MethodInfo)memberInfo).ReturnType,
            MemberTypes.Constructor => memberInfo.DeclaringType,
            MemberTypes.TypeInfo => (Type)memberInfo,
            _ => null
        } ?? throw new ArgumentException(
            $"The member type '{memberInfo.MemberType}' is not supported or its runtime type cannot be determined.",
            nameof(memberInfo));
    }
}
