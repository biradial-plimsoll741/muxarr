using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace Muxarr.Core.Extensions;

[AttributeUsage(AttributeTargets.Property)]
public sealed class CompareIgnoreAttribute : Attribute
{
}

public static class EntityCompare
{
    public static bool Equal<T>(T? a, T? b) where T : class
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return Cache<T>.Comparer.Equal(a, b);
    }

    // Creates a new TTarget and copies matching scalar properties from source.
    // Honors the same [CompareIgnore] attribute - any property tagged there is
    // skipped for both equality and copy.
    public static TTarget CopyTo<TTarget>(object source) where TTarget : class, new()
    {
        var target = new TTarget();
        Cache<TTarget>.Comparer.CopyFrom(source, target);
        return target;
    }

    private static class Cache<T> where T : class
    {
        public static readonly Comparer Comparer = new(typeof(T));
    }

    private sealed class Comparer
    {
        private readonly Member[] _members;

        public Comparer(Type t)
        {
            var members = new List<Member>();
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (prop.GetCustomAttribute<CompareIgnoreAttribute>() is not null)
                {
                    continue;
                }

                if (IsScalar(prop.PropertyType))
                {
                    members.Add(new Member(prop, null));
                }
                else if (TryGetListElementType(prop.PropertyType, out var elementType))
                {
                    members.Add(new Member(prop, elementType));
                }
            }

            _members = members.ToArray();
        }

        public bool Equal(object a, object b)
        {
            foreach (var m in _members)
            {
                var va = m.Info.GetValue(a);
                var vb = m.Info.GetValue(b);

                if (m.ElementType is null)
                {
                    if (!Equals(va, vb))
                    {
                        return false;
                    }
                }
                else if (!ListEqual((IList?)va, (IList?)vb, m.ElementType))
                {
                    return false;
                }
            }

            return true;
        }

        public void CopyFrom(object source, object target)
        {
            var sourceProps = SourceProps.GetOrAdd(source.GetType(), BuildSourceProps);
            foreach (var m in _members)
            {
                if (!m.Info.CanWrite || m.ElementType is not null)
                {
                    continue;
                }

                if (!sourceProps.TryGetValue(m.Info.Name, out var sp))
                {
                    continue;
                }

                if (!IsCopyCompatible(m.Info.PropertyType, sp.PropertyType))
                {
                    continue;
                }

                m.Info.SetValue(target, sp.GetValue(source));
            }
        }

        private static bool IsCopyCompatible(Type target, Type source)
        {
            if (target.IsAssignableFrom(source))
            {
                return true;
            }

            // Allow T -> Nullable<T> (reflection boxing handles the wrap).
            return Nullable.GetUnderlyingType(target) is { } underlying
                   && underlying.IsAssignableFrom(source);
        }

        private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> SourceProps = new();

        private static Dictionary<string, PropertyInfo> BuildSourceProps(Type t) =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToDictionary(p => p.Name);

        private static bool ListEqual(IList? a, IList? b, Type elementType)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null || a.Count != b.Count)
            {
                return false;
            }

            var elementEqual = ElementEqualFor(elementType);
            for (var i = 0; i < a.Count; i++)
            {
                if (!elementEqual(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static Func<object?, object?, bool> ElementEqualFor(Type type) =>
            ElementComparers.GetOrAdd(type, BuildElementComparer);

        private static readonly ConcurrentDictionary<Type, Func<object?, object?, bool>> ElementComparers = new();

        private static Func<object?, object?, bool> BuildElementComparer(Type type)
        {
            if (IsScalar(type))
            {
                return Equals;
            }

            var method = typeof(EntityCompare)
                .GetMethod(nameof(Equal), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(type);
            return (a, b) => (bool)method.Invoke(null, new[] { a, b })!;
        }
    }

    private sealed record Member(PropertyInfo Info, Type? ElementType);

    private static bool IsScalar(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
               || underlying.IsEnum
               || underlying == typeof(string)
               || underlying == typeof(DateTime)
               || underlying == typeof(DateTimeOffset)
               || underlying == typeof(TimeSpan)
               || underlying == typeof(Guid)
               || underlying == typeof(decimal);
    }

    private static bool TryGetListElementType(Type type, out Type elementType)
    {
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(IReadOnlyList<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null!;
        return false;
    }
}
