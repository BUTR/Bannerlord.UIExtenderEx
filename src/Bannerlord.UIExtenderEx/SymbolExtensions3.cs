using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Bannerlord.UIExtenderEx
{
    internal static class SymbolExtensions3
    {
        public static PropertyInfo GetPropertyInfo<T>(Expression<Func<T>> expression)
        {
            return GetPropertyInfo((LambdaExpression)expression);
        }
        public static PropertyInfo GetPropertyInfo<T, TResult>(Expression<Func<T, TResult>> expression)
        {
            return GetPropertyInfo((LambdaExpression)expression);
        }
        public static PropertyInfo GetPropertyInfo(LambdaExpression expression)
        {
            if (expression.Body is MemberExpression body && body.Member is PropertyInfo propertyInfo)
                return propertyInfo;

            throw new ArgumentException("Invalid Expression. Expression should consist of a Property return only.");
        }
    }
}