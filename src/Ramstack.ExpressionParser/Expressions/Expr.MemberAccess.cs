namespace Ramstack.Parsing.Expressions;

partial class Expr
{
    /// <summary>
    /// Represents a member access operation in an expression tree,
    /// such as accessing a field or property like <c>obj.Property</c>.
    /// </summary>
    public sealed class MemberAccess(Expr expression, Expr member) : Expr(ExprKind.MemberAccess)
    {
        /// <summary>
        /// Gets the expression whose member is being accessed.
        /// </summary>
        public Expr Expression { get; } = expression;

        /// <summary>
        /// Gets the expression representing the member being accessed.
        /// </summary>
        public Expr Member { get; } = member;

        /// <summary>
        /// Deconstructs the member access expression into its target expression and member.
        /// </summary>
        /// <param name="expression">The <see cref="Expr"/> representing the target object.</param>
        /// <param name="member">The <see cref="Expr"/> representing the accessed member.</param>
        public void Deconstruct(out Expr expression, out Expr member) =>
            (expression, member) = (Expression, Member);

        /// <inheritdoc />
        public override T Accept<T>(ExprVisitor<T> visitor) =>
            visitor.VisitMemberAccess(this);

        /// <inheritdoc />
        public override string ToString() =>
            $"{Expression}.{Member}";
    }
}
