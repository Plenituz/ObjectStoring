using System;

namespace ObjectStoring
{

    [System.AttributeUsage(AttributeTargets.Property|AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class SaveMeAttribute : System.Attribute
    {
        public SaveMeAttribute()
        {
        }

    }
}
