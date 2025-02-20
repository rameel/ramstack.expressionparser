using System.Reflection;

namespace Ramstack.Parsing.Internal;

/// <summary>
/// Provides helper methods for throwing exceptions during expression parsing to simplify error handling.
/// </summary>
internal static class Error
{
    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> indicating an unresolved symbol.
    /// </summary>
    /// <param name="identifier">The <see cref="Identifier"/> that could not be resolved.</param>
    [DoesNotReturn]
    public static void CannotResolveSymbol(Identifier identifier) =>
        throw new ParseErrorException($"Cannot resolve symbol '{identifier}'.");

    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> indicating an identifier was expected.
    /// </summary>
    /// <param name="expr">The <see cref="Expr"/> encountered where an identifier was expected.</param>
    [DoesNotReturn]
    public static void IdentifierExpected(Expr expr) =>
        throw new ParseErrorException($"Identifier expected (expression: {expr}).");

    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> indicating a method name was expected.
    /// </summary>
    /// <param name="expr">The <see cref="Expr"/> encountered where a method name was expected.</param>
    [DoesNotReturn]
    public static void MethodNameExpected(Expr expr) =>
        throw new ParseErrorException($"Method name expected (expression: {expr}).");

    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> indicating a non-invocable member was used like a method.
    /// </summary>
    /// <param name="expr">The <see cref="Expr"/> representing the non-invocable member.</param>
    [DoesNotReturn]
    public static void NonInvocableMember(Expr expr) =>
        throw new ParseErrorException($"Non-invocable member '{expr}' cannot be used like a method.");

    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> indicating a unary operator cannot be applied to an operand.
    /// </summary>
    /// <param name="expr">The <see cref="Expr.Unary"/> containing the operator.</param>
    /// <param name="operandType">The <see cref="Type"/> of the operand.</param>
    [DoesNotReturn]
    public static void NonApplicableUnaryOperator(Expr.Unary expr, Type operandType)
    {
        var message = $"Unary operator '{expr.Operator}' cannot be applied to operand of type '{operandType}'.";
        throw new ParseErrorException(message);
    }

    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> indicating a binary operator cannot be applied to its operands.
    /// </summary>
    /// <param name="op">The <see cref="Identifier"/> representing the binary operator.</param>
    /// <param name="left">The <see cref="Type"/> of the left operand.</param>
    /// <param name="right">The <see cref="Type"/> of the right operand.</param>
    [DoesNotReturn]
    public static void NonApplicableBinaryOperator(Identifier op, Type left, Type right)
    {
        var message = $"Operator '{op}' cannot be applied to operands of type '{left}' and '{right}'.";
        throw new ParseErrorException(message);
    }

    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> indicating an implicit type conversion is not possible.
    /// </summary>
    /// <param name="operandType">The source <see cref="Type"/> of the operand.</param>
    /// <param name="conversionType">The target <see cref="Type"/> for the conversion.</param>
    [DoesNotReturn]
    public static void MissingImplicitConversion(Type operandType, Type conversionType)
    {
        var message = $"Cannot implicitly convert type '{operandType}' to '{conversionType}'.";
        throw new ParseErrorException(message);
    }

    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> indicating indexing cannot be applied to a type.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> that does not support indexing.</param>
    [DoesNotReturn]
    public static void NonIndexingType(Type type)
    {
        var message = $"Cannot apply indexing with [] to an expression of type '{type}'.";
        throw new ParseErrorException(message);
    }

    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> indicating an ambiguous match among multiple candidates.
    /// </summary>
    /// <param name="candidates">The collection of <see cref="MemberInfo"/> objects representing ambiguous matches.</param>
    [DoesNotReturn]
    public static void AmbiguousMatch(IEnumerable<MemberInfo> candidates)
    {
        var names = string.Join("\r\n    ", candidates.Select(c => $"{c} (in {c.DeclaringType})"));
        throw new ParseErrorException($"Ambiguous match found:\r\n    {names}");
    }

    /// <summary>
    /// Throws a <see cref="ParseErrorException"/> with a custom error message.
    /// </summary>
    /// <param name="message">The custom error message, or <c>null</c> for a default message.</param>
    /// <exception cref="ParseErrorException">Always thrown with the specified <paramref name="message"/>.</exception>
    [DoesNotReturn]
    public static void Throw(string? message) =>
        throw new ParseErrorException(message);
}
