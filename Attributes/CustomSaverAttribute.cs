using System;

namespace ObjectStoring
{
    /// <summary>
    /// method marked with this will be called instead of the usual
    /// algorithm to handle the saving to json
    /// the method should NOT return actual json but a Dictionary<string, object>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class CustomSaverAttribute : System.Attribute
    {
        // This is a positional argument
        public CustomSaverAttribute()
        {
        }
    }
}
