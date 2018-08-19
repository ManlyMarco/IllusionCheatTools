using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;

namespace CheatTools
{
    internal class MethodCacheEntry : CacheEntryBase
    {
        public MethodCacheEntry(object ins, MethodInfo m) : base(GetMethodName(ins, m))
        {
            if (m == null)
                throw new ArgumentNullException(nameof(m));

            _instance = ins;
            _methodInfo = m;
        }

        private static string GetMethodName(object ins, MethodBase methodInfo)
        {
            if (methodInfo != null)
            {
                var name = FieldCacheEntry.GetMemberName(ins, methodInfo);

                var genericArguments = methodInfo.GetGenericArguments();
                if (genericArguments.Any())
                {
                    name += "<" + string.Join(", ", genericArguments.Select(x => x.Name).ToArray()) + ">";
                }

                return name;
            }
            return "INVALID";
        }

        private readonly MethodInfo _methodInfo;

        private readonly object _instance;
        private object _valueCache;

        public override object GetValueToCache()
        {
            return (_instance == null ? "Static " : "") + "Method call - enter to evaluate";
        }

        public override object GetValue()
        {
            return _valueCache ?? base.GetValue();
        }

        public override object EnterValue()
        {
            try
            {
                var result = _methodInfo.Invoke(_instance, null);

                // If this is the first user clicked, eval the method and display the result. second time enter as normal
                if (_valueCache == null)
                {
                    _valueCache = result;
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, $"[CheatTools] Failed to evaluate the method {Name()} - {ex.Message}");
                return null;
            }
        }

        public override void SetValue(object newValue)
        {
        }

        public override Type Type()
        {
            return _methodInfo.ReturnType;
        }

        public override bool CanSetValue()
        {
            return false;
        }

        public override bool CanEnterValue()
        {
            return _valueCache == null || base.CanEnterValue();
        }
    }
}