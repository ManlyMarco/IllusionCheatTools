using System;

namespace CheatTools
{
    internal abstract class CacheEntryBase : ICacheEntry
    {
        protected CacheEntryBase(string name)
        {
            _name = name;
        }

        public virtual object EnterValue()
        {
            return _valueCache = (GetValueToCache() ?? GetValue());
        }

        public abstract object GetValueToCache();
        private object _valueCache;
        public virtual object GetValue()
        {
            return _valueCache ?? (_valueCache = GetValueToCache());
        }

        public abstract void SetValue(object newValue);
        public abstract Type Type();
        public abstract bool CanSetValue();

        private readonly string _name;
        private string _typeName;

        public string Name() => _name;

        public string TypeName()
        {
            if (_typeName == null)
            {
                var type = Type();
                if (type != null)
                    _typeName = type.GetFriendlyName();
                else
                    _typeName = "INVALID";
            }
            return _typeName;
        }

        private bool? _canEnter;
        public virtual bool CanEnterValue()
        {
            if (_canEnter == null)
                _canEnter = !Type().IsPrimitive;
            return _canEnter.Value;
        }
    }
}