using System;
using System.Linq;
using System.Reflection;

namespace CheatTools
{
    class MethodCacheEntry : ICacheEntry
    {
        public MethodCacheEntry(object ins, MethodInfo m)
        {
            if (m == null)
                throw new ArgumentNullException(nameof(m));

            instance = ins;
            methodInfo = m;
        }

        readonly MethodInfo methodInfo;

        object instance;

        public object Get()
        {
            try { return methodInfo.Invoke(instance, null); }
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
                if (methodInfo != null)
                {
                    name = methodInfo.Name;

                    var genericArguments = methodInfo.GetGenericArguments();
                    if (genericArguments.Any())
                    {
                        name += "<" + string.Join(", ", genericArguments.Select(x => x.Name).ToArray()) + ">";
                    }
                }
                else
                    name = "INVALID";
            }
            return name;
        }

        public string TypeName()
        {
            if (typeName == null)
            {
                if (methodInfo?.ReturnType != null)
                    typeName = methodInfo.ReturnType.GetFriendlyName();
                else
                    typeName = "INVALID";
            }
            return typeName;
        }

        public void Set(object newValue)
        {
        }

        public Type Type()
        {
            return methodInfo.ReturnType;
        }

        public bool CanSet()
        {
            return false;
        }
    }
}