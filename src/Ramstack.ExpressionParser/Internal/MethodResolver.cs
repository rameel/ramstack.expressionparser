//
// This file is based on the System.DefaultBinder class from the .NET Foundation.
// The original implementation is licensed under the MIT license.
//
// Source: https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/DefaultBinder.cs
//

using System.Reflection;

namespace Ramstack.Parsing.Internal;

/// <summary>
/// Represents a helper class that provides functionality to resolve a method
/// from a set of candidate methods based on specified parameter types.
/// </summary>
internal static class MethodResolver
{
    /// <summary>
    /// Resolves the most appropriate method from the provided array of methods
    /// that matches the specified parameter types.
    /// </summary>
    /// <param name="match">An array of methods representing the candidates for matching.</param>
    /// <param name="argumentTypes">The argument types used to locate a matching method.</param>
    /// <returns>
    /// The resolved <see cref="MethodInfo"/> if a matching method is found; otherwise, <c>null</c>.
    /// </returns>
    public static MethodBase? ResolveMethod(IReadOnlyList<MethodBase> match, Type[] argumentTypes)
    {
        if (match.Count == 0)
            throw new ArgumentException("Value cannot be an empty collection.");

        var candidates = match.ToArray();
        var parameterArrayTypes = new Type?[candidates.Length];

        // Find the method that matches...
        var currentIndex = 0;

        // Filter methods by parameter count and type
        for (var i = 0; i < candidates.Length; i++)
        {
            var paramArrayType = default(Type?);

            // Validate the parameters.
            var parameters = candidates[i].GetParameters();

            // Match method by parameter count
            if (parameters.Length == 0)
            {
                // No formal parameters
                if (argumentTypes.Length != 0)
                    if ((candidates[i].CallingConvention & CallingConventions.VarArgs) == 0)
                        continue;

                // This is a valid routine so we move it up the candidates list.
                candidates[currentIndex++] = candidates[i];
                continue;
            }

            int j;
            if (parameters.Length > argumentTypes.Length)
            {
                // Shortage of provided parameters
                //
                // If the number of parameters is greater than the number of argumentTypes then
                // we are in the situation were we may be using default values.
                for (j = argumentTypes.Length; j < parameters.Length - 1; j++)
                {
                    if (parameters[j].DefaultValue == DBNull.Value)
                        break;
                }

                if (j != parameters.Length - 1)
                    continue;

                if (parameters[j].DefaultValue == DBNull.Value)
                {
                    if (!parameters[j].ParameterType.IsArray)
                        continue;

                    if (!parameters[j].IsDefined(typeof(ParamArrayAttribute), true))
                        continue;

                    paramArrayType = parameters[j].ParameterType.GetElementType();
                }
            }
            else if (parameters.Length < argumentTypes.Length)
            {
                // Excess provided parameters

                // Test for the ParamArray case
                var lastParameterIndex = parameters.Length - 1;

                if (!parameters[lastParameterIndex].ParameterType.IsArray)
                    continue;

                if (!parameters[lastParameterIndex].IsDefined(typeof(ParamArrayAttribute), true))
                    continue;

                paramArrayType = parameters[lastParameterIndex].ParameterType.GetElementType();
            }
            else
            {
                // Test for paramArray, save paramArray type
                var lastParameterIndex = parameters.Length - 1;
                var lastParameterType = parameters[lastParameterIndex].ParameterType;

                if (lastParameterType.IsArray)
                    if (parameters[lastParameterIndex].IsDefined(typeof(ParamArrayAttribute), true))
                        if (!lastParameterType.IsAssignableFrom(argumentTypes[lastParameterIndex]))
                            paramArrayType = lastParameterType.GetElementType();
            }

            var argumentsToCheck = paramArrayType is null ? argumentTypes.Length : parameters.Length - 1;

            // Match method by parameter type
            for (j = 0; j < argumentsToCheck && j < argumentTypes.Length; j++)
            {
                // Classic argument coercion checks

                // get the formal type
                var parameterType = parameters[j].ParameterType;

                if (parameterType.IsByRef)
                {
                    Debug.Assert(parameterType.GetElementType() is not null);
                    parameterType = parameterType.GetElementType()!;
                }

                // the type is the same
                if (parameterType == argumentTypes[j])
                    continue;

                // the type is Object, so it will match everything
                if (parameterType == typeof(object))
                    continue;

                // now do a "classic" type check
                if (parameterType.IsPrimitive)
                {
                    if (!TypeUtils.CanConvertPrimitive(argumentTypes[j], parameterType))
                        break;
                }
                else
                {
                    //if (argumentTypes[j] == typeof(object))
                    //    continue;

                    if (!parameterType.IsAssignableFrom(argumentTypes[j]))
                    {
                        if (argumentTypes[j].IsCOMObject)
                            // ReSharper disable once PossibleMistakenSystemTypeArgument
                            if (parameterType.IsInstanceOfType(argumentTypes[j]))
                                continue;

                        break;
                    }
                }
            }

            if (paramArrayType is not null && j == parameters.Length - 1)
            {
                // Check that excess arguments can be placed in the param array
                for (; j < argumentTypes.Length; j++)
                {
                    if (paramArrayType.IsPrimitive)
                    {
                        if (!TypeUtils.CanConvertPrimitive(argumentTypes[j], paramArrayType))
                            break;
                    }
                    else
                    {
                        //if (argumentTypes[j] == typeof(object))
                        //    continue;

                        if (!paramArrayType.IsAssignableFrom(argumentTypes[j]))
                        {
                            if (argumentTypes[j].IsCOMObject)
                                // ReSharper disable once PossibleMistakenSystemTypeArgument
                                if (paramArrayType.IsInstanceOfType(argumentTypes[j]))
                                    continue;

                            break;
                        }
                    }
                }
            }

            if (j == argumentTypes.Length)
            {
                // This is a valid routine so we move it up the candidates list
                parameterArrayTypes[currentIndex] = paramArrayType;
                candidates[currentIndex++] = candidates[i];
            }
        }

        // If we didn't find a method
        if (currentIndex == 0)
            return null;

        if (currentIndex == 1)
            return candidates[0];

        var min = 0;
        for (var i = 1; i < currentIndex; i++)
        {
            // Walk all of the methods looking the most specific method to invoke
            var newMin = FindMostSpecificMethod(
                candidates[min],
                parameterArrayTypes[min],
                candidates[i],
                parameterArrayTypes[i],
                argumentTypes);

            // Ambiguous match found
            if (newMin == 0)
                return null;

            if (newMin == 2)
                min = i;
        }

        return candidates[min];
    }

    private static int FindMostSpecific(
        ParameterInfo[] p1, Type? paramArrayType1,
        ParameterInfo[] p2, Type? paramArrayType2,
        Type[] types)
    {
        // A method using params is always less specific than one not using params
        if (paramArrayType1 is not null && paramArrayType2 is null)
            return 2;

        if (paramArrayType1 is null && paramArrayType2 is not null)
            return 1;

        // now either p1 and p2 both use params or neither does.

        var p1Less = false;
        var p2Less = false;

        for (var i = 0; i < types.Length; i++)
        {
            // If a param array is present, then either
            //     the user re-ordered the parameters in which case
            //         the argument to the param array is either an array
            //             in which case the params is conceptually ignored and so paramArrayType1 is null
            //         or the argument to the param array is a single element
            //             in which case paramOrder[i] == p1.Length - 1 for that element
            //     or the user did not re-order the parameters in which case
            //         the paramOrder array could contain indexes larger than p.Length - 1 (see VSW 577286)
            //         so any index >= p.Length - 1 is being put in the param array

            var c1 = paramArrayType1 is not null && i >= p1.Length - 1
                ? paramArrayType1
                : p1[i].ParameterType;

            var c2 = paramArrayType2 is not null && i >= p2.Length - 1
                ? paramArrayType2
                : p2[i].ParameterType;

            if (c1 == c2)
                continue;

            switch (FindMostSpecificType(c1, c2, types[i]))
            {
                case 0: return 0;
                case 1: p1Less = true; break;
                case 2: p2Less = true; break;
            }
        }

        // Two way p1Less and p2Less can be equal. All the arguments are the
        // same they both equal false, otherwise there were things that both
        // were the most specific type on....
        if (p1Less == p2Less)
        {
            // if we cannot tell which is a better match based on parameter types (p1Less == p2Less),
            // let's see which one has the most matches without using the params array (the longer one wins).
            if (!p1Less)
            {
                if (p1.Length > p2.Length)
                    return 1;

                if (p2.Length > p1.Length)
                    return 2;
            }

            return 0;
        }

        return p1Less ? 1 : 2;
    }

    private static int FindMostSpecificType(Type c1, Type c2, Type t)
    {
        // If the two types are exact move on...
        if (c1 == c2)
            return 0;

        if (c1 == t)
            return 1;

        if (c2 == t)
            return 2;

        if (c1.IsByRef || c2.IsByRef)
        {
            if (c1.IsByRef && c2.IsByRef)
            {
                c1 = c1.GetElementType()!;
                c2 = c2.GetElementType()!;
            }
            else if (c1.IsByRef)
            {
                if (c1.GetElementType() == c2)
                    return 2;

                c1 = c1.GetElementType()!;
            }
            else
            {
                if (c2.GetElementType() == c1)
                    return 1;

                c2 = c2.GetElementType()!;
            }
        }

        bool c1FromC2;
        bool c2FromC1;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        Debug.Assert(c1 is not null);

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        Debug.Assert(c2 is not null);

        if (c1.IsPrimitive && c2.IsPrimitive)
        {
            c1FromC2 = TypeUtils.CanConvertPrimitive(c2, c1);
            c2FromC1 = TypeUtils.CanConvertPrimitive(c1, c2);
        }
        else
        {
            c1FromC2 = c1.IsAssignableFrom(c2);
            c2FromC1 = c2.IsAssignableFrom(c1);
        }

        if (c1FromC2 == c2FromC1)
            return 0;

        if (c1FromC2)
            return 2;

        return 1;
    }

    private static int FindMostSpecificMethod(
        MethodBase m1, Type? paramArrayType1,
        MethodBase m2, Type? paramArrayType2,
        Type[] types)
    {
        // Find the most specific method based on the parameters.
        var res = FindMostSpecific(
            m1.GetParameters(), paramArrayType1,
            m2.GetParameters(), paramArrayType2, types);

        // If the match was not ambiguous then return the result.
        if (res != 0)
            return res;

        // Check to see if the methods have the exact same name and signature.
        if (CompareSignature(m1, m2))
        {
            Debug.Assert(m1.DeclaringType is not null);
            Debug.Assert(m2.DeclaringType is not null);

            // Determine the depth of the declaring types for both methods.
            var hierarchyDepth1 = GetHierarchyDepth(m1.DeclaringType);
            var hierarchyDepth2 = GetHierarchyDepth(m2.DeclaringType);

            // The most derived method is the most specific one.
            if (hierarchyDepth1 == hierarchyDepth2)
                return 0;

            if (hierarchyDepth1 < hierarchyDepth2)
                return 2;

            return 1;
        }

        // The match is ambiguous.
        return 0;
    }

    private static bool CompareSignature(MethodBase m1, MethodBase m2)
    {
        var params1 = m1.GetParameters();
        var params2 = m2.GetParameters();

        if (params1.Length != params2.Length)
            return false;

        for (var i = 0; i < params1.Length; i++)
            if (params1[i].ParameterType != params2[i].ParameterType)
                return false;

        return true;
    }

    private static int GetHierarchyDepth(Type? type)
    {
        var depth = 0;
        do
        {
            depth++;
            type = type!.BaseType;
        }
        while (type is not null);

        return depth;
    }
}
