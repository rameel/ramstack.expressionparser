using System.Linq.Expressions;
using System.Reflection;

namespace Ramstack.Parsing;

/// <summary>
/// Provides an abstract base class for binding expressions.
/// </summary>
public abstract class Binder
{
    /// <summary>
    /// Gets the expression context associated with this binder instance.
    /// </summary>
    public virtual Expression? Context => null;

    /// <summary>
    /// Resolves the type associated with the specified name.
    /// </summary>
    /// <param name="name">The <see cref="Identifier"/> representing the name of the requested type.</param>
    /// <returns>
    /// The <see cref="Type"/> instance corresponding to the specified <paramref name="name"/> 
    /// if resolved successfully; otherwise, <see langword="null"/>.
    /// </returns>
    public abstract Type? BindToType(Identifier name);

    /// <summary>
    /// Attempts to bind to a method based on the specified type, method name, and parameter types.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> from which to select the method.</param>
    /// <param name="methodName">The <see cref="Identifier"/> representing the name of the method to bind.</param>
    /// <param name="parameterTypes">An array of <see cref="Type"/> objects representing
    /// the types of arguments passed to the method.</param>
    /// <param name="isStatic">A <see cref="bool"/> indicating whether to match only static methods
    /// (<see langword="true"/>) or instance methods (<see langword="false"/>).</param>
    /// <returns>
    /// A <see cref="MethodInfo"/> representing the matching method if found;  otherwise,
    /// <see langword="null"/> if no suitable method is identified.
    /// </returns>
    public abstract MethodInfo? BindToMethod(Type? type, Identifier methodName, Type[] parameterTypes, bool isStatic);

    /// <summary>
    /// Attempts to bind to a member (field, property, or method) based on the specified type and member name.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> from which to select the member.</param>
    /// <param name="memberName">The <see cref="Identifier"/> representing the name of the member to bind.</param>
    /// <param name="isStatic">A <see cref="bool"/> indicating whether to match only static members
    /// (<see langword="true"/>) or instance members (<see langword="false"/>).</param>
    /// <returns>
    /// A <see cref="MemberInfo"/> representing the matching member if found; otherwise,
    /// <see langword="null"/> if no suitable member is identified.
    /// </returns>
    public abstract MemberInfo? BindToMember(Type? type, Identifier memberName, bool isStatic);
}
