using System;

namespace Motio.ObjectStoring
{
    /// <summary>
    /// method marked with this will be called instead of the usual
    /// algorithm to handle the saving to json
    /// the method should NOT return actual json but a Dictionary<string, object>
    /// or an array
    /// example:
    /// 
    ///[CustomSaver]
    ///object OnSave()
    ///{
    ///    return properties
    ///        .ToDictionary(
    ///            pair => pair.Key,
    ///           pair => TimelineSaver.SaveObjectToJson(pair.Value));
    ///}
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
