using System;

namespace ObjectStoring
{
    /// <summary>
    /// method marked with this 
    /// will be called instead of using the usual algorithm to load the json into and object
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class CustomLoaderAttribute : Attribute
    {
        public CustomLoaderAttribute()
        {
        }
    }
}
