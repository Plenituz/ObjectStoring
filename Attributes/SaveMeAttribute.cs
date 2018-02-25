using System;

namespace Motio.ObjectStoring
{
    /// <summary>
    /// any property or field marked with this attribute will be saved in the json file
    /// note that if this element is not a value type it will save the whole object according
    /// to the [SaveMe] of the object and so on
    /// </summary>
    [System.AttributeUsage(AttributeTargets.Property|AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class SaveMeAttribute : System.Attribute
    {
        public SaveMeAttribute()
        {
        }

    }
}
