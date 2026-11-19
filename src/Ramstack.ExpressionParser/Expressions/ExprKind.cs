namespace Ramstack.Parsing.Expressions;

/// <summary>
/// Defines the possible types of nodes in an expression tree used by the expression parser.
/// </summary>
public enum ExprKind
{
    /// <summary>
    /// Represents a reference to a type or member by its name.
    /// </summary>
    /// <example>
    /// In the expression <c>Math.PI</c>, <c>Math</c> is a reference to a type, and <c>PI</c> is a reference to its member.
    /// </example>
    Reference,

    /// <summary>
    /// Represents a constant value, such as a number or string literal.
    /// </summary>
    /// <example>
    /// In the expression <c>42</c> or <c>"hello"</c>, this denotes a literal value.
    /// </example>
    Literal,

    /// <summary>
    /// Represents a binary operation, such as addition or subtraction, involving two operands.
    /// </summary>
    /// <example>
    /// In the expression <c>a + b</c>, this indicates the addition operation.
    /// </example>
    Binary,

    /// <summary>
    /// Represents a method invocation within the expression.
    /// </summary>
    /// <example>
    /// In the expression <c>Math.Abs(-5)</c>, this denotes the method call to "Abs".
    /// </example>
    Call,

    /// <summary>
    /// Represents an indexing operation or access to a parameterized property.
    /// </summary>
    /// <example>
    /// In the expression <c>array[0]</c>, this indicates an index operation.
    /// </example>
    Index,

    /// <summary>
    /// Represents access to a member of an object or a type.
    /// </summary>
    /// <example>
    /// In the expression <c>obj.Property</c>, this denotes accessing the "Property" member.
    /// In the expression <c>Math.Abs(x)</c>, <c>Math.Abs</c> is a member access as well.
    /// </example>
    MemberAccess,

    /// <summary>
    /// Represents a unary operation on a single operand, such as negation, logical NOT,
    /// or a conversion with the <c>:></c> operator.
    /// </summary>
    /// <example>
    /// In the expression <c>-x</c>, this indicates the negation operation.
    /// </example>
    Unary,

    /// <summary>
    /// Represents a conditional (ternary) operation with a condition, true branch, and false branch.
    /// </summary>
    /// <example>
    /// In the expression <c>a > b ? a : b</c>, this denotes a conditional operation.
    /// </example>
    Conditional,

    /// <summary>
    /// Represents an expression enclosed in parentheses to enforce precedence or grouping.
    /// </summary>
    /// <example>
    /// In the expression <c>(a + b)</c>, this indicates a parenthesized sub-expression.
    /// </example>
    Parenthesized
}
