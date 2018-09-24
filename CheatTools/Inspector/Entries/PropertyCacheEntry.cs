using System;
using System.Reflection;

namespace CheatTools
{
    internal class PropertyCacheEntry : CacheEntryBase
    {
        public PropertyCacheEntry(object ins, PropertyInfo p) : base(FieldCacheEntry.GetMemberName(ins, p))
        {
            if (p == null)
                throw new ArgumentNullException(nameof(p));

            _instance = ins;
            _prop = p;
        }

        private readonly PropertyInfo _prop;

        private readonly object _instance;

        public override object GetValueToCache()
        {
            if (!_prop.CanRead)
                return "WRITE ONLY";

            if (_prop.PropertyType.IsArray)
                return "IS INDEXED";

            try { return _prop.GetValue(_instance, null); }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        public override void SetValue(object newValue)
        {
            if (_prop.CanWrite)
            {
                _prop.SetValue(_instance, newValue, null);
            }
        }

        public override Type Type()
        {
            return _prop.PropertyType;
        }

        public override bool CanSetValue()
        {
            return _prop.CanWrite;
        }
    }
}
