using System;

namespace DICS.Unity
{
    public static class ILocatorExt
    {
        public static ILocator With<T1>(this ILocator locator, T1 t1)
        {
            var local = new LocatorImpl(ILocator.Empty, locator.GetName().ExtendName<T1>());
            local.Put(Key.Of<T1>(), t1);
            return locator.InheritedWithLocal(local);
        }
        
        public static ILocator With<T1, T2>(this ILocator locator, T1 t1, T2 t2)
        {
            var local = new LocatorImpl(ILocator.Empty, locator.GetName().ExtendName<T1, T2>());
            local.Put(Key.Of<T1>(), t1);
            local.Put(Key.Of<T2>(), t2);
            return locator.InheritedWithLocal(local);
        }
        
        public static ILocator With<T1, T2, T3>(this ILocator locator, T1 t1, T2 t2, T3 t3)
        {
            var local = new LocatorImpl(ILocator.Empty, locator.GetName().ExtendName<T1, T2, T3>());
            local.Put(Key.Of<T1>(), t1);
            local.Put(Key.Of<T2>(), t2);
            local.Put(Key.Of<T3>(), t3);
            return locator.InheritedWithLocal(local);
        }
        
        public static ILocator With<T1, T2, T3, T4>(this ILocator locator, T1 t1, T2 t2, T3 t3, T4 t4)
        {
            var local = new LocatorImpl(ILocator.Empty, locator.GetName().ExtendName<T1, T2, T3, T4>());
            local.Put(Key.Of<T1>(), t1);
            local.Put(Key.Of<T2>(), t2);
            local.Put(Key.Of<T3>(), t3);
            local.Put(Key.Of<T4>(), t4);
            return locator.InheritedWithLocal(local);
        }
        
        public static ILocator With<T1, T2, T3, T4, T5>(this ILocator locator, T1 t1, T2 t2, T3 t3, T4 t4, T5 t5)
        {
            var local = new LocatorImpl(ILocator.Empty, locator.GetName().ExtendName<T1, T2, T3, T4, T5>());
            local.Put(Key.Of<T1>(), t1);
            local.Put(Key.Of<T2>(), t2);
            local.Put(Key.Of<T3>(), t3);
            local.Put(Key.Of<T4>(), t4);
            local.Put(Key.Of<T5>(), t5);
            return locator.InheritedWithLocal(local);
        }

        public static Func<ILocator, ILocator> With<T1>(T1 t1)
        {
            return loc => loc.With(t1);
        }
        
        public static Func<ILocator, ILocator> With<T1, T2>(T1 t1, T2 t2)
        {
            return loc => loc.With(t1, t2);
        }
        
        public static Func<ILocator, ILocator> With<T1, T2, T3>(T1 t1, T2 t2, T3 t3)
        {
            return loc => loc.With(t1, t2, t3);
        }
        
        public static Func<ILocator, ILocator> With<T1, T2, T3, T4>(T1 t1, T2 t2, T3 t3, T4 t4)
        {
            return loc => loc.With(t1, t2, t3, t4);
        }
        
        public static Func<ILocator, ILocator> With<T1, T2, T3, T4, T5>(T1 t1, T2 t2, T3 t3, T4 t4, T5 t5)
        {
            return loc => loc.With(t1, t2, t3, t4, t5);
        }

        private static string ExtendName<T1>(this string oldName) 
            => $"{oldName} with {typeof(T1).Name};";
        private static string ExtendName<T1, T2>(this string oldName) 
            => $"{oldName} with {typeof(T1).Name}; {typeof(T2).Name}";
        private static string ExtendName<T1, T2, T3>(this string oldName) 
            => $"{oldName} with {typeof(T1).Name}; {typeof(T2).Name}; {typeof(T3).Name}";
        private static string ExtendName<T1, T2, T3, T4>(this string oldName) 
            => $"{oldName} with {typeof(T1).Name}; {typeof(T2).Name}; {typeof(T3).Name}; {typeof(T4).Name}";
        private static string ExtendName<T1, T2, T3, T4, T5>(this string oldName) 
            => $"{oldName} with {typeof(T1).Name}; {typeof(T2).Name}; {typeof(T3).Name}; {typeof(T4).Name}; {typeof(T5).Name}";
    }
}
