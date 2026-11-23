namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a literal value in an expression tree, such as a number, a string, a character,
    /// a Boolean value, or <see langword="null" />.
    /// </summary>
    /// <param name="value">The constant value of the literal, which may be <see langword="null" />.</param>
    public sealed class Literal(object? value) : Expr(ExprKind.Literal)
    {
        /// <summary>
        /// Gets the constant value of the literal expression, or <see langword="null" /> for the <c>null</c> literal.
        /// </summary>
        public object? Value { get; } = value;

        /// <summary>
        /// Gets a value indicating whether the literal is an integer literal with the <c>U</c> or <c>UL</c> suffix.
        /// </summary>
        internal bool HasUnsignedSuffix { get; init; }

        /// <summary>
        /// Deconstructs the literal expression into its value.
        /// </summary>
        /// <param name="value">The constant value of the literal, which may be <see langword="null" />.</param>
        public void Deconstruct(out object? value) =>
            value = Value;

        /// <inheritdoc />
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitLiteral(this);

        /// <inheritdoc />
        public override string ToString() => Value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            _ => Value.ToString()!
        };
    }
}
