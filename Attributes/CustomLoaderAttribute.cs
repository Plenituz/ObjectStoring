using System;

namespace ObjectStoring
{
    /// <summary>
    /// method marked with this 
    /// will be called instead of using the usual algorithm to load the json into and object
    /// so this method must return an object that will get written to json 
    /// example :
    /// 
    ///[CustomSaver]
    ///object OnSave()
    ///{
    ///    return properties
    ///        .ToDictionary(
    ///           pair => pair.Key,
    ///           pair => TimelineSaver.SaveObjectToJson(pair.Value));
    ///}
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class CustomLoaderAttribute : Attribute
    {
        public CustomLoaderAttribute()
        {
        }
    }
}
