using System;

namespace ObjectStoring
{
    /// <summary>
    /// method marked with this 
    /// will be called instead of using the usual algorithm to load the json into and object
    /// so this method must return an object that will get written to json 
    /// example :
    /// 
    ///[CustomLoader]
    ///void OnLoad(JObject jobj)
    ///{
    ///    //the jobject contains the list of properties
    ///    foreach (JProperty jprop in jobj.Properties())
    ///    {
    ///        object instance = TimelineLoader.LoadObjectFromJson(jprop, null, this);
    ///        properties.Add(jprop.Name, (NodePropertyBase)instance);
    ///    }
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
