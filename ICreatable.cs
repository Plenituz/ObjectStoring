namespace Motio.ObjectStoring
{
    public interface ICreatable
    {
        object CreateInstance();
        /// <summary>
        /// return true if this creatable has a custom logic for instance creation
        /// </summary>
        /// <returns></returns>
        bool HasCreateLoadInstance();
        object CreateInstanceWithCreateLoadInstance(object parent, string typeString);
    }
}
