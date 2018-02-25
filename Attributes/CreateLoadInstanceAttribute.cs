using System;

namespace Motio.ObjectStoring
{
    /// <summary>
    /// a static method marked with this will be called when loading a file
    /// the method should return a new instance of the class it's in
    /// the new instance should not add itself inside any list, the loading system 
    /// will take care of that
    /// The method can be private of public an must accept an object and a type as parameters
    /// the object will be the parent in the json hirarchy and the type will be the type of the created object
    /// the type is given because this method id "inherited", if you define it on a parent class, 
    /// the derived classes will not need to implement the method (but still can)
    /// example :
    /// 
    /// [CreateLoadInstance]
    /// private static object CreateLoadInstance(object parent, Type type)
    /// {
    ///    return new AnimationTimeline();
    /// }
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class CreateLoadInstanceAttribute : Attribute
    {
        public CreateLoadInstanceAttribute()
        {
        }
    }
}
