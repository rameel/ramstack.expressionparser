using System.Linq.Expressions;
using System.Reflection;

namespace Ramstack.Parsing;

/// <summary>
/// Represents a binder that is aware of a specific context expression,
/// allows resolution of members and methods based on the provided context.
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
    /// <param name="context">The context expression used for binding operations.</param>
    public ContextAwareBinder(Expression context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

        if (context.Type != typeof(object))
            RegisterType(context.Type, true);
    }

    /// <inheritdoc />
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
