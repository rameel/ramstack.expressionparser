# Ramstack.ExpressionParser
[![NuGet](https://img.shields.io/nuget/v/Ramstack.ExpressionParser.svg)](https://nuget.org/packages/Ramstack.ExpressionParser)
[![MIT](https://img.shields.io/github/license/rameel/ramstack.expressionparser)](https://github.com/rameel/ramstack.expressionparser/blob/main/LICENSE)

**Ramstack.ExpressionParser** is a flexible expression parser library for .NET,
allowing dynamic evaluation and binding of expressions with context-aware support.

## Getting Started

To install the `Ramstack.ExpressionParser` [NuGet package](https://www.nuget.org/packages/Ramstack.ExpressionParser) to your project, run the following command:

```shell
dotnet add package Ramstack.ExpressionParser
```

## Usage

```csharp
var result = ExpressionParser.Parse("math.min(2 + 3, 2 * 3)");
if (result.Success)
{
    var lambda = Expression.Lambda<Func<int>>(result.Value);
    var fn = lambda.Compile();

    Console.WriteLine(fn());
}
```

### Using `ContextAwareBinder`

`ContextAwareBinder` allows binding expressions to a specific context,
making it possible to reference its properties, fields, and methods directly within expressions.

- The provided context acts as an implicit **this**, meaning you can access its members without prefixes.
- Case-insensitive binding: identifiers in expressions are resolved in case-insensitive manner (e.g., level, Level, and LEVEL are treated the same).

```csharp
var parameter = Expression.Parameter(typeof(LogEvent), "logEvent");
var binder = new ContextAwareBinder(parameter);

var result = ExpressionParser.Parse("level == LogLevel.Error && string.IsNullOrEmpty(source)", binder);
var predicate = Expression
    .Lambda<Predicate<LogEvent>>(result.Value, parameter)
    .Compile();
```

Here, `IsEnabled` evaluates the parsed expression against a `LogEvent` instance:
```csharp
public bool IsEnabled(LogEvent logEvent)
{
    return _predicate(logEvent);
}
```

This makes it easy to create dynamic, human-readable expressions for filtering or evaluating objects at runtime.

### Type conversions

Use the `:>` operator to convert a value to a type, for example `count :> long` or `Math.PI :> float`.
A conversion binds more tightly than prefix and binary operators, so `-x :> int` means `-(x :> int)`,
and `x :> long + 1` means `(x :> long) + 1`.

Earlier versions wrote conversions as `x:int`. That syntax conflicted with the conditional operator,
so expressions such as `flag ? 0 : count` failed to parse. It is no longer supported;
replace `x:int` with `x :> int`.

### Limits

An expression tree can be at most 256 levels deep. Each operator, member access, call, indexer,
and pair of parentheses adds a level, so `a + b + c` counts as deeply as `((a + b) + c)`.
Parsing fails with an error rather than crashing the process when an expression is deeper than that,
or when it is nested too deeply to fit on the stack of the current thread.

Parsing an expression takes time and memory that grow with its length,
so limit the length of expressions that come from untrusted sources.

## Supported Versions

|      | Version    |
|------|------------|
| .NET | 6, 7, 8, 9 |

## Contributions

Bug reports and contributions are welcome.

## License

This package is released under the **MIT License**.
See the [LICENSE](https://github.com/rameel/ramstack.expressionparser/blob/main/LICENSE) file for details.
