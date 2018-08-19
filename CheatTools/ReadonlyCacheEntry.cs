using System;

namespace CheatTools
{
    class ReadonlyCacheEntry : ICacheEntry
    {
        public readonly string EntryName;
        public readonly object Object;
        private readonly Type _type;
        private string _tostringCashe;

        public ReadonlyCacheEntry(string name, object obj)
        {
            Object = obj;
            EntryName = name;
            _type = obj.GetType();
        }

        public object Get()
        {
            return Object;
        }

        public string Name()
        {
            return EntryName;
        }

        public string TypeName()
        {
            return _type.Name;
        }

        public void Set(object newValue)
        {
        }

        public Type Type()
        {
            return _type;
        }

        public bool CanSet()
        {
            return false;
        }

        public override string ToString()
        {
            return _tostringCashe ?? (_tostringCashe = Name() + " | " + Object);
        }
    }
}