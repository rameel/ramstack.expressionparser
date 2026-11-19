using System.Linq.Expressions;

namespace Ramstack.Parsing;

/// <summary>
/// Provides methods for parsing and evaluating expressions.
/// </summary>
public static partial class ExpressionParser
{
    /// <summary>
    /// Parses the specified expression into an <see cref="Expression" />.
    /// </summary>
    /// <param name="source">The expression string to parse.</param>
    /// <returns>
    /// The result of the parsing operation, which contains either the expression tree or a description of the error.
    /// </returns>
    public static ParseResult<Expression> Parse(ReadOnlySpan<char> source) =>
        Parse(source, new ExpressionBuilder(new DefaultBinder()));

    /// <summary>
    /// Parses the specified expression into an <see cref="Expression" /> using the specified <see cref="Binder" />.
    /// </summary>
    /// <param name="source">The expression string to parse.</param>
    /// <param name="binder">The binder that resolves the types, members, and methods referenced in the expression.</param>
    /// <returns>
    /// The result of the parsing operation, which contains either the expression tree or a description of the error.
    /// </returns>
    public static ParseResult<Expression> Parse(ReadOnlySpan<char> source, Binder binder) =>
        Parse(source, new ExpressionBuilder(binder));

    /// <summary>
    /// Parses the specified expression into an <see cref="Expression" /> using the specified <see cref="ExpressionBuilder" />.
    /// </summary>
    /// <param name="source">The expression string to parse.</param>
    /// <param name="builder">The expression builder responsible for constructing the expression tree.</param>
    /// <returns>
    /// The result of the parsing operation, which contains either the expression tree or a description of the error.
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
    /// <remarks>
    /// If the expression is invalid or the type of its result cannot be converted to <see cref="object" />,
    /// an <see cref="ArgumentException" /> that describes the error is thrown.
    /// </remarks>
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
    /// <remarks>
    /// The expression can access the members of the runtime type of <paramref name="context" />.
    /// If the expression is invalid or the type of its result cannot be converted to <see cref="object" />,
    /// an <see cref="ArgumentException" /> that describes the error is thrown.
    /// </remarks>
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
                ConvertResult(r.Value, typeof(object))
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
    /// The evaluated result of type <typeparamref name="TResult" />.
    /// </returns>
    /// <remarks>
    /// If the expression is invalid or the type of its result cannot be converted to <typeparamref name="TResult" />,
    /// an <see cref="ArgumentException" /> that describes the error is thrown.
    /// </remarks>
    public static TResult? Evaluate<TResult>(ReadOnlySpan<char> source)
    {
        var r = Parse(source, new DefaultBinder());
        if (!r.Success)
            throw new ArgumentException(r.ErrorMessage);

        if (r.Value is null)
            return default;

        var e = ConvertResult(r.Value, typeof(TResult));

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
    /// The evaluated result of type <typeparamref name="TResult" />.
    /// </returns>
    /// <remarks>
    /// The expression can access the members of <typeparamref name="TContext" />, not of the runtime type
    /// of <paramref name="context" />. Members declared only in a derived or implementing type are not available.
    /// If the expression is invalid or the type of its result cannot be converted to <typeparamref name="TResult" />,
    /// an <see cref="ArgumentException" /> that describes the error is thrown.
    /// </remarks>
    public static TResult? Evaluate<TContext, TResult>(ReadOnlySpan<char> source, TContext context) where TContext : notnull
    {
        var c = Expression.Parameter(typeof(TContext), "context");
        var r = Parse(source, new ContextAwareBinder(c));

        if (!r.Success)
            throw new ArgumentException(r.ErrorMessage);

        if (r.Value is null)
            return default;

        var e = ConvertResult(r.Value, typeof(TResult));

        return Expression
            .Lambda<Func<TContext, TResult?>>(e, c)
            .Compile()(context);
    }

    /// <summary>
    /// Converts the result of an expression to the specified type.
    /// </summary>
    /// <param name="expression">The expression whose result to convert.</param>
    /// <param name="type">The type to convert the result to.</param>
    /// <returns>
    /// The <paramref name="expression" /> itself if its result needs no conversion;
    /// otherwise, an expression that converts the result to <paramref name="type" />.
    /// </returns>
    /// <remarks>
    /// The result is converted explicitly. If there is no such conversion, as for an expression
    /// of type <see langword="void" />, an <see cref="ArgumentException" /> is thrown.
    /// </remarks>
    private static Expression ConvertResult(Expression expression, Type type)
    {
        if (expression.Type == type || expression.Type.IsClass && type.IsAssignableFrom(expression.Type))
            return expression;

        try
        {
            return Expression.Convert(expression, type);
        }
        catch (InvalidOperationException)
        {
            throw new ArgumentException($"Cannot convert type '{expression.Type}' to '{type}'.");
        }
    }
}
