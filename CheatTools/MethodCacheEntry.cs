using System;
using System.Linq;
using System.Reflection;

namespace CheatTools
{
    internal class MethodCacheEntry : CacheEntryBase
    {
        public MethodCacheEntry(object ins, MethodInfo m) : base(m?.Name)
        {
            if (m == null)
                throw new ArgumentNullException(nameof(m));

            _instance = ins;
            _methodInfo = m;
        }

        private readonly MethodInfo _methodInfo;

        private readonly object _instance;

        public override object GetValueToCache()
        {
            try { return _methodInfo.Invoke(_instance, null); }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
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
    }
}