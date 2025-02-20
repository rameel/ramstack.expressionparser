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

## Supported Versions

|      | Version    |
|------|------------|
| .NET | 6, 7, 8, 9 |

## Contributions

Bug reports and contributions are welcome.

## License

This package is released under the **MIT License**.
See the [LICENSE](https://github.com/rameel/ramstack.expressionparser/blob/main/LICENSE) file for details.
