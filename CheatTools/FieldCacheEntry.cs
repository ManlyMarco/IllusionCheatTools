using System;
using System.Reflection;

namespace CheatTools
{
    internal class FieldCacheEntry : CacheEntryBase
    {
        public FieldCacheEntry(object ins, FieldInfo f) : base(GetMemberName(ins, f))
        {
            if (f == null)
                throw new ArgumentNullException(nameof(f));

            _instance = ins;
            _field = f;
        }

        internal static string GetMemberName(object ins, MemberInfo f)
        {
            if (ins != null) return f?.Name;
            return "S/" + f?.Name;
        }

        private readonly FieldInfo _field;
        private readonly object _instance;

        public override object GetValueToCache()
        {
            return _field.GetValue(_instance);
        }
        
        public override void SetValue(object newValue)
        {
            if (!_field.IsInitOnly)
            {
                _field.SetValue(_instance, newValue);
            }
        }

        public override Type Type()
        {
            return _field.FieldType;
        }

        public override bool CanSetValue()
        {
            return true;
        }
    }
}
