using System;
using System.Collections.Generic;

namespace Motio.ObjectStoring
{
    public interface ISavableManager
    {
        bool ShouldManageObject(object obj);
        string GetTypeString(object obj);
        /// <summary>
        /// return the custom saver or null
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="asBase">if this is true we want the custom saver of the base class of the given object</param>
        /// <returns></returns>
        Func<object> GetCustomSaver(object obj, bool asBase = false);
        IEnumerable<SavableMember> GetSavableMembers(object obj);
    }
}
