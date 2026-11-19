using Ramstack.Parsing.Parsers;

using static Ramstack.Parsing.Parser;

namespace Ramstack.Parsing;

partial class ExpressionParser
{
    /// <summary>
    /// The maximum depth of an expression tree produced by the parser.
    /// </summary>
    /// <remarks>
    /// The expression builder, the <c>ToString</c> methods of the expression nodes, and other visitors
    /// process the tree recursively.
    /// On a 1 MB stack, the expression builder runs out of stack at a depth of about 1,000 in a Debug build,
    /// so the limit leaves room for the caller's own stack frames.
    /// </remarks>
    private const int MaxDepth = 256;

    /// <summary>
    /// Gets the expression parser that does not require the expression to extend to the end of the input.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Parser"/>, this parser leaves the input that follows the expression unconsumed,
    /// so it can be used as part of a larger grammar.
    /// Parsing stops with a fatal error if the expression tree is deeper than 256 levels.
    /// Each operator, member access, call, indexer, and pair of parentheses adds a level,
    /// so a chain such as <c>a + b + c</c> gets one level deeper with each operator.
    /// Parsing also stops with a fatal error if the thread's stack runs short. On a small stack, this can happen
    /// before the limit is reached for deeply nested parentheses, arguments, indexers, and conditional expressions.
    /// </remarks>
    public static Parser<Expr> Subparser { get; } = CreateParser();

    /// <summary>
    /// Gets the main expression parser, which ensures that the entire input is consumed.
    /// </summary>
    public static Parser<Expr> Parser { get; } = Subparser.ThenIgnore(Eof);

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static Parser<Expr> CreateParser()
    {
        var identifier_start =
            Choice(
                Character.Letter,
                L('_')).Void();

        var identifier_body =
            Choice(
                Character.LetterOrDigit,
                L('_')).Void();

        var identifier =
            Seq(
                identifier_start,
                identifier_body.Many()
                ).Map(m => new Identifier(m.ToString())).As("identifier");

        var keyword =
            Seq(
                OneOf("true", "false", "null"),
                Not(identifier_body)
            ).Void();

        var number_literal =
            ConstantNumberParser
                .NumericLiteral
                .Do(Expr (v) => new Expr.Literal(v));

        var string_literal =
            Literal
                .DoubleQuotedString
                .Do(Expr (v) => new Expr.Literal(v));

        var char_literal =
            Literal
                .QuotedCharacter
                .Do(Expr (v) => new Expr.Literal(v));

        var boolean_literal =
            OneOf("true", "false")
                .ThenIgnore(Not(identifier_body))
                .Do(Expr (v) => new Expr.Literal(v == "true"));

        var null_literal =
            L("null")
                .ThenIgnore(Not(identifier_body))
                .Do(Expr (_) => new Expr.Literal(null));

        var prefix_operator =
            OneOf("+-!~")
                .ThenIgnore(S)
                .Do(v => new Identifier(v.ToString()));

        var expression =
            Deferred<Expr>();

        var literal_expression =
            Choice(
                number_literal,
                string_literal,
                char_literal,
                boolean_literal,
                null_literal
            ).ThenIgnore(S);

        var identifier_expression =
            identifier
                .Between(Not(keyword), S)
                .Do(Expr (v) => new Expr.Reference(v));

        var parenthesis_expression =
            expression
                .Between(
                    Seq(L('('), S),
                    Seq(L(')'), S))
                .Do(Expr (e) => new Expr.Parenthesized(e));

        var primary_expression =
            Choice(
                literal_expression,
                identifier_expression,
                parenthesis_expression);

        var expression_list =
            expression
                .Separated(
                    Seq(L(','), S)).As("expression_list");

        var call_expression =
            expression_list
                .Between(
                    Seq(L('('), S),
                    Seq(L(')'), S)).Do(list => (list, indexer: false));

        var indexer_expression =
            expression_list
                .Between(
                    Seq(L('['), S),
                    Seq(L(']'), S)).Do(list => (list, indexer: true));

        var call_or_indexer_expression =
            Seq(
                primary_expression,
                Choice(
                    call_expression,
                    indexer_expression
                ).Many()
            ).Do((expr, list) =>
            {
                foreach (var (parameters, indexer) in list)
                    expr = indexer
                        ? new Expr.Indexer(expr, parameters)
                        : new Expr.Call(expr, parameters);

                //
                // HijackMemberExpression processes this chain recursively during parsing,
                // so check the depth before the chain reaches it.
                //
                return EnsureDepth(expr);
            });

        var member_expression =
            call_or_indexer_expression.Fold(Seq(L('.'), S, Not(keyword)), (lhs, rhs, _) =>
            {
                return HijackMemberExpression(lhs, rhs);

                static Expr HijackMemberExpression(Expr result, Expr value)
                {
                    return value switch
                    {
                        Expr.Call call => new Expr.Call(HijackMemberExpression(result, call.Expression), call.Parameters),
                        Expr.Indexer indexer => new Expr.Indexer(HijackMemberExpression(result, indexer.Expression), indexer.Parameters),
                        _ => new Expr.MemberAccess(result, value)
                    };
                }
            });

        //
        // The conversion operator is ':>' rather than ':', which would conflict with the conditional
        // operator: in 'flag ? 0 : count', ': count' would be taken for a conversion to the type 'count'.
        //
        var cast_expression =
            Seq(
                member_expression,
                identifier.Between(
                    Seq(L(":>"), S),
                    S).Many()
            ).Do((expr, ids) =>
            {
                foreach (var id in ids)
                    expr = new Expr.Unary(id, UnaryType.Convert, expr);

                return expr;
            });

        var prefix_expression =
            Seq(
                prefix_operator.Many(),
                cast_expression
            ).Do((operators, expr) =>
            {
                for (var i = operators.Count - 1; i >= 0; i--)
                {
                    var type = operators[i].Name[0] switch
                    {
                        '+' => UnaryType.UnaryPlus,
                        '!' => UnaryType.Not,
                        '~' => UnaryType.OnesComplement,
                         _  => UnaryType.Negate
                    };

                    expr = new Expr.Unary(operators[i], type, expr);
                }

                return expr;
            });

        var mul_expression =
            prefix_expression.Fold(
                OneOf("*/%").ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var add_expression =
            mul_expression.Fold(
                OneOf("+-").ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var shift_expression =
            add_expression.Fold(
                OneOf(["<<", ">>", ">>>"]).ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var relational_expression =
            shift_expression.Fold(
                OneOf(["<", ">", "<=", ">="]).ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var equality_expression =
            relational_expression.Fold(
                OneOf("==", "!=").ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var bitwise_and_expression =
            equality_expression.Fold(
                L('&').ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var bitwise_xor_expression =
            bitwise_and_expression.Fold(
                L('^').ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var bitwise_or_expression =
            bitwise_xor_expression.Fold(
                L('|').ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var logical_and_expression =
            bitwise_or_expression.Fold(
                L("&&").ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var logical_or_expression =
            logical_and_expression.Fold(
                L("||").ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var null_coalesce_expression =
            logical_or_expression.FoldR(
                L("??").ThenIgnore(S).Do(CreateIdentifier),
                (lhs, rhs, op) => new Expr.Binary(op, lhs, rhs));

        var conditional_expression =
            Seq(
                null_coalesce_expression,
                Seq(
                    L('?'), S,
                    expression,
                    L(':').Or(Fail<char>("Expected ':'")), S,
                    expression.Or(Fail<Expr>("Expected expression"))
                ).Optional()
            ).Do(Expr (Expr condition, OptionalValue<(char, Unit, Expr @true, char, Unit, Expr @false)> optional) =>
                optional.HasValue
                    ? new Expr.Conditional(condition, optional.Value.@true, optional.Value.@false)
                    : condition);

        //
        // Every recursion of the grammar (parentheses, arguments, indexers, and both branches
        // of a conditional expression) re-enters it through this parser, so a single check
        // protects the parser from a stack overflow on deeply nested input.
        //
        expression.Parser =
            new StackGuardParser<Expr>(conditional_expression);

        return S.Then(expression).Do(EnsureDepth);
    }

    /// <summary>
    /// Stops parsing with a fatal error if the specified expression tree is deeper than <see cref="MaxDepth"/>.
    /// </summary>
    /// <param name="expr">The expression tree to check.</param>
    /// <returns>
    /// The specified expression tree.
    /// </returns>
    private static Expr EnsureDepth(Expr expr)
    {
        if (expr.Depth > MaxDepth)
            FatalError($"The expression exceeds the maximum depth of {MaxDepth}.");

        return expr;
    }

    private static Identifier CreateIdentifier(string v) =>
        new Identifier(v);

    private static Identifier CreateIdentifier(char v) =>
        new Identifier(v.ToString());
}
