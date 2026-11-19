using System.Linq.Expressions;
using System.Reflection;

namespace Ramstack.Parsing;

/// <summary>
/// Represents the base class for binders that resolve the types, members, and methods referenced in an expression.
/// </summary>
public abstract class Binder
{
    /// <summary>
    /// Gets the expression whose instance members can be referenced in an expression without qualification.
    /// </summary>
    /// <value>
    /// The context expression, or <see langword="null" /> if the binder has no context.
    /// </value>
    /// <remarks>
    /// When <see cref="BindToMember" /> or <see cref="BindToMethod" /> returns an instance member
    /// for an unqualified name, the member is accessed through this expression.
    /// </remarks>
    public virtual Expression? Context => null;

    /// <summary>
    /// Resolves the type associated with the specified name.
    /// </summary>
    /// <param name="name">The <see cref="Identifier" /> representing the name of the requested type.</param>
    /// <returns>
    /// The <see cref="Type" /> instance corresponding to the specified <paramref name="name" />
    /// if resolved successfully; otherwise, <see langword="null" />.
    /// </returns>
    public abstract Type? BindToType(Identifier name);

    /// <summary>
    /// Attempts to bind to a method based on the specified type, method name, and parameter types.
    /// </summary>
    /// <param name="type">The <see cref="Type" /> from which to select the method,
    /// or <see langword="null" /> for an unqualified method name.</param>
    /// <param name="methodName">The <see cref="Identifier" /> representing the name of the method to bind.</param>
    /// <param name="parameterTypes">An array of <see cref="Type" /> objects representing
    /// the types of arguments passed to the method.</param>
    /// <param name="isStatic">A <see cref="bool" /> indicating whether to match only static methods
    /// (<see langword="true" />) or instance methods (<see langword="false" />).</param>
    /// <returns>
    /// A <see cref="MethodInfo" /> representing the matching method if found; otherwise, <see langword="null" />.
    /// </returns>
    /// <remarks>
    /// For an unqualified method name, <paramref name="type" /> is <see langword="null" /> and <paramref name="isStatic" />
    /// is <see langword="true" />. The binder can then return a static method of a type whose members are available
    /// without qualification, or an instance method of the type of <see cref="Context" />.
    /// </remarks>
    public abstract MethodInfo? BindToMethod(Type? type, Identifier methodName, Type[] parameterTypes, bool isStatic);

    /// <summary>
    /// Attempts to bind to a member (field or property) based on the specified type and member name.
    /// </summary>
    /// <param name="type">The <see cref="Type" /> from which to select the member,
    /// or <see langword="null" /> for an unqualified member name.</param>
    /// <param name="memberName">The <see cref="Identifier" /> representing the name of the member to bind.</param>
    /// <param name="isStatic">A <see cref="bool" /> indicating whether to match only static members
    /// (<see langword="true" />) or instance members (<see langword="false" />).</param>
    /// <returns>
    /// A <see cref="MemberInfo" /> representing the matching member if found; otherwise, <see langword="null" />.
    /// </returns>
    /// <remarks>
    /// For an unqualified member name, <paramref name="type" /> is <see langword="null" /> and <paramref name="isStatic" />
    /// is <see langword="true" />. The binder can then return a static member of a type whose members are available
    /// without qualification, or an instance member of the type of <see cref="Context" />.
    /// </remarks>
    public abstract MemberInfo? BindToMember(Type? type, Identifier memberName, bool isStatic);
}
