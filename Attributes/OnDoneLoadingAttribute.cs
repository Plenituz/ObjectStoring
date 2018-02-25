using System;

namespace Motio.ObjectStoring
{
    /// <summary>
    /// a method marked with this attribute will be called when the object gets loaded 
    /// from a json file
    /// All properties/fields that were in the json should be loaded and ready to go 
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class OnDoneLoadingAttribute : Attribute
    {
        public OnDoneLoadingAttribute()
        {
        }
    }
}
