using System;
using System.Reflection;

namespace CheatTools
{
    public partial class CheatTools
    {
        class FieldCacheEntry : ICacheEntry
        {
            public FieldCacheEntry(object ins, FieldInfo f)
            {
                if (f == null)
                    throw new ArgumentNullException(nameof(f));

                instance = ins;
                field = f;
            }

            readonly FieldInfo field;

            object instance;

            public object Get()
            {
                return field.GetValue(instance);
            }

            string name;
            string typeName;

            public string Name()
            {
                if (name == null)
                {
                    if (field != null)
                        name = field.Name;
                    else
                        name = "INVALID";
                }
                return name;
            }

            public string TypeName()
            {
                if (typeName == null)
                {
                    if (field != null)
                        typeName = field.FieldType.GetFriendlyName();
                    else
                        typeName = "INVALID";
                }
                return typeName;
            }

            public void Set(object newValue)
            {
                if (!field.IsInitOnly)
                {
                    field.SetValue(instance, newValue);
                }
            }

            public Type Type()
            {
                return field.FieldType;
            }
        }
    }
}
