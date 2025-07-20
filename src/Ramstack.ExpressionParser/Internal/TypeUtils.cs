using System.Reflection;

namespace Ramstack.Parsing.Internal;

/// <summary>
/// Provides type utility methods.
/// </summary>
public static class TypeUtils
{
    private static ReadOnlySpan<Primitives> PrimitiveConversions =>
    [
        /* Empty    */  0,
        /* Object   */  0,
        /* DBNull   */  0,
        /* Boolean  */  Primitives.Boolean,
        /* Char     */  Primitives.Char    | Primitives.UInt16 | Primitives.UInt32 | Primitives.Int32  | Primitives.UInt64 | Primitives.Int64  | Primitives.Single |  Primitives.Double,
        /* SByte    */  Primitives.SByte   | Primitives.Int16  | Primitives.Int32  | Primitives.Int64  | Primitives.Single | Primitives.Double,
        /* Byte     */  Primitives.Byte    | Primitives.Char   | Primitives.UInt16 | Primitives.Int16  | Primitives.UInt32 | Primitives.Int32  | Primitives.UInt64 |  Primitives.Int64 |  Primitives.Single |  Primitives.Double,
        /* Int16    */  Primitives.Int16   | Primitives.Int32  | Primitives.Int64  | Primitives.Single | Primitives.Double,
        /* UInt16   */  Primitives.UInt16  | Primitives.UInt32 | Primitives.Int32  | Primitives.UInt64 | Primitives.Int64  | Primitives.Single | Primitives.Double,
        /* Int32    */  Primitives.Int32   | Primitives.Int64  | Primitives.Single | Primitives.Double,
        /* UInt32   */  Primitives.UInt32  | Primitives.UInt64 | Primitives.Int64  | Primitives.Single | Primitives.Double,
        /* Int64    */  Primitives.Int64   | Primitives.Single | Primitives.Double,
        /* UInt64   */  Primitives.UInt64  | Primitives.Single | Primitives.Double,
        /* Single   */  Primitives.Single  | Primitives.Double,
        /* Double   */  Primitives.Double,
        /* Decimal  */  Primitives.Decimal,
        /* DateTime */  Primitives.DateTime,
        /* Unknown  */  0,
        /* String   */  Primitives.String
    ];

    /// <summary>
    /// Determines whether the <paramref name="type"/> is a numeric type.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to test.</param>
    /// <returns>
    /// A value indicating whether the <paramref name="type"/> is a numeric type.
    /// </returns>
    public static bool IsNumeric(Type type)
    {
        if (!type.IsEnum)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Char:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Double:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Decimal:
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the specified type represents an integer type.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to evaluate.</param>
    /// <returns>
    /// <c>true</c> if the type is an integer type (signed or unsigned); otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method checks for the following integer types:
    /// <list type="bullet">
    ///   <item><description><see cref="TypeCode.SByte"/></description></item>
    ///   <item><description><see cref="TypeCode.Byte"/></description></item>
    ///   <item><description><see cref="TypeCode.Int16"/></description></item>
    ///   <item><description><see cref="TypeCode.UInt16"/></description></item>
    ///   <item><description><see cref="TypeCode.Int32"/></description></item>
    ///   <item><description><see cref="TypeCode.UInt32"/></description></item>
    ///   <item><description><see cref="TypeCode.Int64"/></description></item>
    ///   <item><description><see cref="TypeCode.UInt64"/></description></item>
    /// </list>
    /// </remarks>
    public static bool IsInteger(Type type)
    {
        if (!type.IsEnum)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return true;
            }
        }

        return false;
    }


    /// <summary>
    /// Determines whether the <paramref name="source"/> can convert to the <paramref name="target"/>.
    /// </summary>
    /// <param name="source">The type to convert from.</param>
    /// <param name="target">The type to convert to.</param>
    /// <returns>
    /// A value indicating whether the conversion is possible.
    /// </returns>
    public static bool CanConvertPrimitive(Type source, Type target)
    {
        if (source.IsEnum)
            return false;

        if ((source == typeof(IntPtr) && target == typeof(IntPtr))
            || (source == typeof(UIntPtr) && target == typeof(UIntPtr)))
            return true;

        var s = PrimitiveConversions[(int)Type.GetTypeCode(source)];
        var t = (Primitives)(1 << (int)Type.GetTypeCode(target));

        return (s & t) != 0;
    }

    /// <summary>
    /// Attempts to find an implicit conversion operator.
    /// </summary>
    /// <param name="source">The type to convert from.</param>
    /// <param name="target">The type to convert to.</param>
    /// <returns>
    /// The conversion operator.
    /// </returns>
    public static MethodInfo? FindConversionOperator(Type source, Type target)
    {
        var methods = source.GetMethods(
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Static);

        foreach (var mi in methods)
        {
            if (mi.Name != "op_Implicit" || mi.ReturnType != target)
                continue;

            if (mi.GetParameters()[0].ParameterType == source)
                return mi;
        }

        return null;
    }

    #region Inner type: Primitives

    [Flags]
    private enum Primitives
    {
        Boolean = 1 << TypeCode.Boolean,
        Char = 1 << TypeCode.Char,
        SByte = 1 << TypeCode.SByte,
        Byte = 1 << TypeCode.Byte,
        Int16 = 1 << TypeCode.Int16,
        UInt16 = 1 << TypeCode.UInt16,
        Int32 = 1 << TypeCode.Int32,
        UInt32 = 1 << TypeCode.UInt32,
        Int64 = 1 << TypeCode.Int64,
        UInt64 = 1 << TypeCode.UInt64,
        Single = 1 << TypeCode.Single,
        Double = 1 << TypeCode.Double,
        Decimal = 1 << TypeCode.Decimal,
        DateTime = 1 << TypeCode.DateTime,
        String = 1 << TypeCode.String
    }

    #endregion
}
