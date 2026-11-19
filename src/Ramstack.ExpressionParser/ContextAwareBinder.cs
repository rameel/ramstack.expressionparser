using System.Linq.Expressions;
using System.Reflection;

namespace Ramstack.Parsing;

/// <summary>
/// Represents a binder that also resolves unqualified names to the members of a context expression.
/// </summary>
public class ContextAwareBinder : DefaultBinder
{
    private readonly Expression _context;

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "ConvertToAutoProperty")]
    public override Expression Context => _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextAwareBinder"/> class.
    /// </summary>
    /// <param name="context">The expression whose members can be referenced without qualification.</param>
    public ContextAwareBinder(Expression context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        if (context.Type != typeof(object))
            RegisterType(context.Type, true);
    }

    /// <inheritdoc />
    /// <remarks>
    /// For an unqualified member name, the instance members of <see cref="Context"/> take precedence
    /// over the static members of the types imported into scope.
    /// </remarks>
    public override MemberInfo? BindToMember(Type? type, Identifier memberName, bool isStatic)
    {
        if (type is null && isStatic)
        {
            var member = base.BindToMember(_context.Type, memberName, false);
            if (member is not null)
                return member;
        }

        return base.BindToMember(type, memberName, isStatic);
    }

    /// <inheritdoc />
    /// <remarks>
    /// For an unqualified method name, the instance methods of <see cref="Context"/> take precedence
    /// over the static methods of the types imported into scope.
    /// </remarks>
    public override MethodInfo? BindToMethod(Type? type, Identifier methodName, Type[] parameterTypes, bool isStatic)
    {
        if (type is null && isStatic)
        {
            var method = base.BindToMethod(_context.Type, methodName, parameterTypes, false);
            if (method is not null)
                return method;
        }

        return base.BindToMethod(type, methodName, parameterTypes, isStatic);
    }
}
