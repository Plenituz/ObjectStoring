using System;

namespace Motio.ObjectStoring
{
    /// <summary>
    /// this is called once the entire json has been loaded 
    /// the method should take a JObject that will be this object's properties (the same that is given in CustomLoader)
    /// this attribute most likely won't work for structs 
    ///[OnAllLoaded]
    ///void AllLoaded(JObject jobj)
    ///{
    /// //code that depends on other object that are sure to be loaded now
    ///}
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class OnAllLoadedAttribute : Attribute
    {
        public OnAllLoadedAttribute()
        {
        }
    }
}
