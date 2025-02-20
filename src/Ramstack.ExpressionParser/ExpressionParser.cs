using System.Linq.Expressions;

namespace Ramstack.Parsing;

/// <summary>
/// Provides methods for parsing and evaluating expressions.
/// </summary>
public static partial class ExpressionParser
{
    /// <summary>
    /// Parses the specified expression into an <see cref="Expression"/>.
    /// </summary>
    /// <param name="source">The expression string to parse.</param>
    /// <returns>
    /// The result of the parsing operation.
    /// </returns>
    public static ParseResult<Expression> Parse(ReadOnlySpan<char> source) =>
        Parse(source, new ExpressionBuilder(new DefaultBinder()));

    /// <summary>
    /// Parses the specified expression into an <see cref="Expression"/> using the specified <see cref="Binder"/>.
    /// </summary>
    /// <param name="source">The expression string to parse.</param>
    /// <param name="binder">The binder instance used for resolving members and methods.</param>
    /// <returns>
    /// The result of the parsing operation.
    /// </returns>
    public static ParseResult<Expression> Parse(ReadOnlySpan<char> source, Binder binder) =>
        Parse(source, new ExpressionBuilder(binder));

    /// <summary>
    /// Parses the specified expression into an <see cref="Expression"/> using the specified <see cref="ExpressionBuilder"/>.
    /// </summary>
    /// <param name="source">The expression string to parse.</param>
    /// <param name="builder">The expression builder responsible for constructing the expression tree.</param>
    /// <returns>
    /// The result of the parsing operation.
    /// </returns>
    public static ParseResult<Expression> Parse(ReadOnlySpan<char> source, ExpressionBuilder builder)
    {
        try
        {
            var result = Parser.Parse(source);
            if (!result.Success)
            {
                return new ParseResult<Expression>
                {
                    Length = result.Length,
                    ErrorMessage = result.ErrorMessage
                };
            }

            var expression = builder.Visit(result.Value!);
            return new ParseResult<Expression>
            {
                Length = result.Length,
                Value = expression
            };
        }
        catch (ParseErrorException e)
        {
            return new ParseResult<Expression>
            {
                Length = 0,
                ErrorMessage = e.Message,
                Exception = e.InnerException
            };
        }
        catch (Exception e)
        {
            return new ParseResult<Expression>
            {
                Length = 0,
                ErrorMessage = e.Message,
                Exception = e
            };
        }
    }

    /// <summary>
    /// Evaluates the specified expression and returns the result as an object.
    /// </summary>
    /// <param name="source">The expression string to evaluate.</param>
    /// <returns>
    /// The evaluated result.
    /// </returns>
    public static object? Evaluate(ReadOnlySpan<char> source) =>
        Evaluate<object>(source);

    /// <summary>
    /// Evaluates the specified expression within the provided context.
    /// </summary>
    /// <param name="source">The expression string to evaluate.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>
    /// The evaluated result.
    /// </returns>
    public static object? Evaluate(ReadOnlySpan<char> source, object context)
    {
        var p = Expression.Parameter(typeof(object), "p");
        var c = Expression.Variable(context.GetType(), "context");
        var r = Parse(source, new ContextAwareBinder(c));

        if (!r.Success)
            throw new ArgumentException(r.ErrorMessage);

        if (r.Value is null)
            return null;

        var block = Expression.Block(
            variables: [c],
            expressions:
            [
                Expression.Assign(c, Expression.Convert(p, context.GetType())),
                r.Value.Type.IsClass ? r.Value : Expression.Convert(r.Value, typeof(object))
            ]);

        return Expression
            .Lambda<Func<object, object?>>(block, p)
            .Compile()(context);
    }

    /// <summary>
    /// Evaluates the specified expression.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="source">The expression string to evaluate.</param>
    /// <returns>
    /// The evaluated result of type <typeparamref name="TResult"/>.
    /// </returns>
    public static TResult? Evaluate<TResult>(ReadOnlySpan<char> source)
    {
        var r = Parse(source, new DefaultBinder());
        if (!r.Success)
            throw new ArgumentException(r.ErrorMessage);

        if (r.Value is null)
            return default;

        var e = r.Value;
        if (e.Type != typeof(TResult) && (!e.Type.IsClass || !typeof(TResult).IsAssignableFrom(e.Type)))
            e = Expression.Convert(e, typeof(TResult));

        return Expression
            .Lambda<Func<TResult?>>(e)
            .Compile()();
    }

    /// <summary>
    /// Evaluates the specified expression within the given context and returns the result as the specified type.
    /// </summary>
    /// <typeparam name="TContext">The type of the evaluation context.</typeparam>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="source">The expression string to evaluate.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>
    /// The evaluated result of type <typeparamref name="TResult"/>.
    /// </returns>
    public static TResult? Evaluate<TContext, TResult>(ReadOnlySpan<char> source, TContext context) where TContext : notnull
    {
        var c = Expression.Parameter(context.GetType(), "context");
        var r = Parse(source, new ContextAwareBinder(c));

        if (!r.Success)
            throw new ArgumentException(r.ErrorMessage);

        if (r.Value is null)
            return default;

        var e = r.Value;
        if (e.Type != typeof(TResult) && (!e.Type.IsClass || !typeof(TResult).IsAssignableFrom(e.Type)))
            e = Expression.Convert(e, typeof(TResult));

        return Expression
            .Lambda<Func<TContext, TResult?>>(e, c)
            .Compile()(context);
    }
}
