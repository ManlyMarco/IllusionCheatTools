using System;

namespace CheatTools
{
    interface ICacheEntry
    {
        string Name();
        string TypeName();

        /// <summary>
        /// Get object that is entered when variable name is clicked in inspector
        /// </summary>
        /// <returns></returns>
        object EnterValue();
        object GetValue();
        void SetValue(object newValue);
        Type Type();
        bool CanSetValue();
        bool CanEnterValue();
    }
}