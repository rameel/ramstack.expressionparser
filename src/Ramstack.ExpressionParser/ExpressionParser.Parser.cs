using Ramstack.Parsing.Parsers;

using static Ramstack.Parsing.Parser;

namespace Ramstack.Parsing;

partial class ExpressionParser
{
    /// <summary>
    /// Gets the sub-expression parser, which serves as the base parser for expressions.
    /// </summary>
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
                return expr;
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

        var cast_expression =
            Seq(
                member_expression,
                identifier.Between(
                    Seq(L(':'), S),
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

        var conditional_expression = Deferred<Expr>();
        conditional_expression.Parser =
            Seq(
                null_coalesce_expression,
                Seq(
                    L('?'), S,
                    expression,
                    L(':').Or(Fail<char>("Expected ':'")), S,
                    conditional_expression.Or(Fail<Expr>("Expected expression"))
                ).Optional()
            ).Do(Expr (Expr condition, OptionalValue<(char, Unit, Expr @true, char, Unit, Expr @false)> optional) =>
                optional.HasValue
                    ? new Expr.Conditional(condition, optional.Value.@true, optional.Value.@false)
                    : condition);

        expression.Parser =
            conditional_expression;

        return S.Then(expression);
    }

    private static Identifier CreateIdentifier(string v) =>
        new Identifier(v);

    private static Identifier CreateIdentifier(char v) =>
        new Identifier(v.ToString());
}
