using Newtonsoft.Json.Linq;

namespace Motio.ObjectStoring
{
    public interface ICreatableProvider
    {
        /// <summary>
        /// return true if the given string represent a type you should handle
        /// </summary>
        /// <param name="typeString"></param>
        /// <returns></returns>
        bool IsTypeStringValid(string typeString);
        /// <summary>
        /// return a ICreatable from the given string representing a type, or null
        /// </summary>
        /// <param name="pathToFile"></param>
        /// <returns></returns>
        ICreatable CreateFromTypeString(string typeString);
        /// <summary>
        /// return true if the given object has been created by you 
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        bool IsCustomCreated(object instance);
        /// <summary>
        /// return true if filling this object should be done with a custom logic
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        bool HasCustomLoader(object instance);

        /// <summary>
        /// should call OnAllLoaded, if it exist. you have to make sure it exists yourself
        /// </summary>
        /// <param name="instance"></param>
        void CallOnAllLoaded(object instance, JObject jobj);
        /// <summary>
        /// should call OnDoneLoading, if it exist. you have to make sure it exists yourself
        /// </summary>
        /// <param name="instance"></param>
        void CallOnDoneLoading(object instance);
        void CallCustomLoader(object instance, JObject jobj);
    }
}
