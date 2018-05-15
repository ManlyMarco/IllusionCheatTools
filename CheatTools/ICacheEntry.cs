using System;

namespace CheatTools
{
    interface ICacheEntry
    {
        object Get();
        string Name();
        string TypeName();

        void Set(object newValue);
        Type Type();
    }
}