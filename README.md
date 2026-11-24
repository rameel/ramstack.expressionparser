# Ramstack.ExpressionParser
[![NuGet](https://img.shields.io/nuget/v/Ramstack.ExpressionParser.svg)](https://nuget.org/packages/Ramstack.ExpressionParser)
[![MIT](https://img.shields.io/github/license/rameel/ramstack.expressionparser)](https://github.com/rameel/ramstack.expressionparser/blob/main/LICENSE)

**Ramstack.ExpressionParser** is a .NET library that parses expressions written in a C#-like syntax
into `System.Linq.Expressions` trees, which you can compile and evaluate,
optionally against a context object.

## Getting Started

To add the `Ramstack.ExpressionParser` [NuGet package](https://www.nuget.org/packages/Ramstack.ExpressionParser) to your project, run the following command:

```shell
dotnet add package Ramstack.ExpressionParser
```

## Usage

```csharp
using System.Linq.Expressions;
using Ramstack.Parsing;

var result = ExpressionParser.Parse("math.min(2 + 3, 2 * 3)");
if (result.Success)
{
    var lambda = Expression.Lambda<Func<int>>(result.Value!);
    var fn = lambda.Compile();

    Console.WriteLine(fn());
}
```

If the expression is invalid, `Success` is `false`, and `ErrorMessage` describes the error.

Identifiers are resolved in a case-insensitive manner, so `math.min`, `Math.Min`, and `MATH.MIN`
refer to the same method.

`true`, `false`, and `null` are keywords. As in C#, prefix a name with `@` to use it as an identifier,
for example `item.@null` for a member named `null`.

### Evaluating expressions

`Evaluate` parses, compiles, and runs an expression in a single call:

```csharp
var sum = ExpressionParser.Evaluate<int>("2 + 3 * 4");
var area = ExpressionParser.Evaluate<Circle, double>("Math.PI * radius * radius", circle);
```

The second call evaluates the expression against `circle`, so the expression can use its members
without qualification. If the expression is invalid, `Evaluate` throws an `ArgumentException`
that describes the error.

`Evaluate` parses and compiles the expression on every call. To evaluate the same expression repeatedly,
use `Parse` and compile the resulting expression once.

### Available types

By default, expressions can use the following types:

- the built-in types `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `nint`, `nuint`,
  `float`, `double`, `decimal`, `char`, `string`, and `object`, also by their .NET names, such as `Int32`
  and `Boolean`;
- `Half` and `BigInteger`;
- `Convert`, `CultureInfo`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `DateTimeKind`, `DayOfWeek`,
  `Guid`, `Math`, `MathF`, `Regex`, `RegexOptions`, `StringComparer`, `StringComparison`, `StringSplitOptions`,
  `TimeSpan`, `TimeZoneInfo`, and `Version`.

Other types are registered with `RegisterType` under their simple name, without the namespace:

```csharp
var binder = new DefaultBinder();
binder.RegisterType(typeof(Path));

var result = ExpressionParser.Parse("Path.GetFileName(\"logs/app.txt\")", binder);
```

With `importAsStatic: true`, the static members of the type can also be used without the type name,
as with a `using static` directive, for example `GetFileName("logs/app.txt")`.

### Using `ContextAwareBinder`

`ContextAwareBinder` allows binding expressions to a specific context,
making it possible to reference its properties, fields, and methods directly within expressions.
The provided context acts as an implicit **this**, meaning you can access its members without qualification.

```csharp
var parameter = Expression.Parameter(typeof(LogEvent), "logEvent");
var binder = new ContextAwareBinder(parameter);
binder.RegisterType(typeof(LogLevel));

var result = ExpressionParser.Parse("level == LogLevel.Error && string.IsNullOrEmpty(source)", binder);
_predicate = Expression
    .Lambda<Predicate<LogEvent>>(result.Value!, parameter)
    .Compile();
```

Types that are not available by default, such as `LogLevel` here, are registered with `RegisterType`.

`IsEnabled` then evaluates the compiled predicate against a `LogEvent` instance:
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

In earlier versions, conversions were written as `x:int`. That syntax conflicted
with the conditional operator, so expressions such as `flag ? 0 : count` failed to parse.
It is no longer supported; replace `x:int` with `x :> int`.

### Limits

An expression tree can be at most 256 levels deep. Each operator, member access, call, indexer,
and pair of parentheses adds a level, so a chain such as `a + b + c` gets one level deeper
with each operator.
Parsing fails with an error rather than crashing the process when an expression is deeper than that,
or when it is nested too deeply to fit on the stack of the current thread.

Parsing an expression takes time and memory that grow with its length,
so limit the length of expressions that come from untrusted sources.

## Supported Versions

|      | Version     |
|------|-------------|
| .NET | 6 and later |

## Contributions

Bug reports and contributions are welcome.
See [CONTRIBUTING.md](https://github.com/rameel/ramstack.expressionparser/blob/main/CONTRIBUTING.md) for details.

## License

This package is released under the **MIT License**.
See the [LICENSE](https://github.com/rameel/ramstack.expressionparser/blob/main/LICENSE) file for details.
