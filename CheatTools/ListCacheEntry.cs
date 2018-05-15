using System;

namespace CheatTools
{
    public partial class CheatTools
    {
        class ListCacheEntry : ICacheEntry
        {
            private object target;
            Type type;

            public ListCacheEntry(object o, int index)
            {
                target = o;
                type = o.GetType();
                typeName = type.GetFriendlyName();
                name = "ID: " + index;
            }

            string typeName, name;

            public object Get()
            {
                return target;
            }

            public string Name()
            {
                return name;
            }

            public string TypeName()
            {
                return typeName;
            }

            public void Set(object newValue)
            {

            }

            public Type Type()
            {
                return type;
            }
        }
    }
}
