using System;

namespace ObjectStoring
{
    /// <summary>
    /// a static method mark with this will be called when loading a file
    /// the method should return a new instance of the class it's in
    /// the new instance should not add itself inside any list, the loading system 
    /// will take care of that
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class CreateLoadInstanceAttribute : Attribute
    {
        public CreateLoadInstanceAttribute()
        {
        }
    }
}
