namespace Ramstack.Parsing.Expressions;

/// <summary>
/// Defines the possible types of nodes in an expression tree used by the expression parser.
/// </summary>
public enum ExprKind
{
    /// <summary>
    /// Represents a reference to a variable, parameter, or member in the expression.
    /// </summary>
    /// <example>
    /// In the expression <c>x</c>, this would indicate a reference to the variable "x".
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
    /// Represents a read operation on a field or property of an object.
    /// </summary>
    /// <example>
    /// In the expression <c>obj.Property</c>, this denotes accessing the "Property" member.
    /// </example>
    MemberAccess,

    /// <summary>
    /// Represents a unary operation, such as negation or logical NOT, involving a single operand.
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
