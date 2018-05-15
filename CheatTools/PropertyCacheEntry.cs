using System;
using System.Reflection;

namespace CheatTools
{
    public partial class CheatTools
    {
        class PropertyCacheEntry : ICacheEntry
        {
            public PropertyCacheEntry(object ins, PropertyInfo p)
            {
                if (p == null)
                    throw new ArgumentNullException(nameof(p));

                instance = ins;
                prop = p;
            }

            readonly PropertyInfo prop;

            object instance;

            public object Get()
            {
                if (!prop.CanRead)
                    return "WRITE ONLY";

                if (prop.PropertyType.IsArray)
                    return "IS INDEXED";


                try { return prop.GetValue(instance, null); }
                catch (Exception ex)
                {
                    return "ERROR: " + ex.Message;
                }
            }

            string name;
            string typeName;

            public string Name()
            {
                if (name == null)
                {
                    if (prop != null)
                        name = prop.Name;
                    else 
                        name = "INVALID";
                }
                return name;
            }

            public string TypeName()
            {
                if (typeName == null)
                {
                    if (prop != null)
                        typeName = prop.PropertyType.GetFriendlyName();
                    else
                        typeName = "INVALID";
                }
                return typeName;
            }

            public void Set(object newValue)
            {
                if (prop.CanWrite)
                {
                    prop.SetValue(instance, newValue, null);
                }
            }

            public Type Type()
            {
                return prop.PropertyType;
            }
        }
    }
}
