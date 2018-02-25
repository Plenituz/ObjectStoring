#define LOGGER
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Motio.Debuging;

namespace Motio.ObjectStoring
{
    /// <summary>
    /// Save any object into a JSON file, taking in account any of the save related attributes:
    /// <see cref="CustomSaverAttribute"/>, <see cref="SaveMeAttribute"/>
    /// Note: only the properties/fields marked with [SaveMe] will be saved
    /// </summary>
    public class TimelineSaver
    {
        public const string TYPE_NAME = "__type__";
        private static ISavableManager manager;

        /// <summary>
        /// save the "timeline" object into the given path as a json file
        /// </summary>
        /// <param name="path"></param>
        public static object Save(object timeline, ISavableManager manager)
        {
            TimelineSaver.manager = manager;
            try
            {
                object dct = SaveObjectToJson(timeline);
                return dct;
            }
            finally
            {
                TimelineSaver.manager = null;
            }
        }

        /// <summary>
        /// extract the properties/fields of the given object into an object that can be saved in json
        /// typically <see cref="Dictionary{string, object}"/> but not limited to it
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static object SaveObjectToJson(object obj, bool asBase = false)
        {
            //check if it has custom saver
            //if it does use it
            //if not list savable members and call saveObjecttojson on them

            Func<object> customSaver = GetCustomSaver(obj, asBase);
            if (customSaver != null)
                return customSaver.Invoke();

            //no custom saver, save marked members
            IEnumerable<SavableMember> savableMembers = ListSavableMembers(obj);
            //if no marked members, this is probably a number or a collection 
            if (savableMembers == null)
            {
                //collection get handled separatly 
                //otherwise juste return the object to json
                if (obj.GetType().GetInterfaces().Contains(typeof(ICollection)))
                    return SaveCollectionToJson((ICollection)obj);
                else
                    //here it's either an object that has no [SaveMe] member or a value type. Let JsonConvert deal with it
                    return obj;
            }
            Dictionary<string, object> jsonDict = new Dictionary<string, object>();
            foreach(SavableMember savableMember in savableMembers)
            {
                if(savableMember.value == null)
                {
#if LOGGER
                    Logger.WriteLine("member " + savableMember.name + " is null while saving");
#endif
                    jsonDict.Add(savableMember.name, null);
                    continue;
                }
                jsonDict.Add(savableMember.name, SaveObjectToJson(savableMember.value));
            }
            jsonDict.Add(TimelineSaver.TYPE_NAME, GetJsonTypeString(obj));
            return jsonDict;
            //check if the list is not null if it is, stop the recursion here
        }

        /// <summary>
        /// save a collection into an array object object calling <see cref="SaveObjectToJson(object)"/>
        /// on every elements of the collection to convert them to json friendly format
        /// </summary>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static object[] SaveCollectionToJson(ICollection collection)
        {
            object[] list = new object[collection.Count];
            int i = 0;
            foreach(object obj in collection)
            {
                list[i++] = SaveObjectToJson(obj);
            }
            return list;
        }

        /// <summary>
        /// get the type of the given object to store into a <see cref="TimelineSaver.TYPE_NAME"/> attribute
        /// this takes in accout the possibility of having IronPython objects in the mix
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string GetJsonTypeString(object obj)
        {
            Type objType = obj.GetType();
            string typeString = objType.AssemblyQualifiedName;
            
            if(manager?.ShouldManageObject(obj) == true)
            {
                typeString = manager.GetTypeString(obj);
            }
            return typeString;
        }

        /// <summary>
        /// return Func that should call the method marked with <see cref="CustomSaverAttribute"/>
        /// on the given object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Func<object> GetCustomSaver(object obj, bool asBase = false)
        {
            if(manager?.ShouldManageObject(obj) == true)
            {
                return manager.GetCustomSaver(obj, asBase);
            }
            else
            {
                MethodInfo customSaver = SearchMethodWithAttr<CustomSaverAttribute>(obj.GetType(),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, asBase ? 1 : 0);
                if (customSaver != null)
                    return () => customSaver.Invoke(obj, null);
            }
            return null;
        }

        /// <summary>
        /// list all the members that should be saved, either marked with [SaveMe]
        /// or if it's a python object in the "saveAttrs" list
        /// if there is no savable members returns null
        /// </summary>
        /// <param name="obj"></param>
        /// <returns>list of Func that return the property/field value</returns>
        static IEnumerable<SavableMember> ListSavableMembers(object obj)
        {
            if(manager?.ShouldManageObject(obj) == true)
            {
                return manager.GetSavableMembers(obj);
            }
            else
            {
                return ListSavableMembersCSharp(obj);
            }
        }

        /// <summary>
        /// same as <see cref="ListSavableMembers(object)"/> but only for CSharp classes
        /// in fact <see cref="ListSavableMembers(object)"/> will call this method
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static IEnumerable<SavableMember> ListSavableMembersCSharp(object obj)
        {
            List<SavableMember> list = new List<SavableMember>();

            //get all properties with the attribute  [SaveMe]
            //and extract the GetValue method
            IEnumerable<SavableMember> properties = obj.GetType()
                .GetProperties(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
                .Where(prop => Attribute.GetCustomAttributes(prop, typeof(SaveMeAttribute), true).Length != 0)
                .Select(propInfo => new SavableMember(propInfo.Name, propInfo.GetValue(obj)));

            //same with the fields
            IEnumerable<SavableMember> fields = obj.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => Attribute.GetCustomAttributes(field, typeof(SaveMeAttribute), true).Length != 0)
                .Select(fieldInfo => new SavableMember(fieldInfo.Name, fieldInfo.GetValue(obj)) );

            list.AddRange(properties);
            list.AddRange(fields);

            if (list.Count == 0)
                return null;
            else
                return list;
        }

        /// <summary>
        /// search a method with the given attribute in the given type and it's base types
        /// </summary>
        /// <typeparam name="AttrFilter"></typeparam>
        /// <param name="type"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static MethodInfo SearchMethodWithAttr<AttrFilter>(Type type, BindingFlags flags, int skip = 0)
            where AttrFilter : Attribute
        {
            MethodInfo method = null;
            do
            {
                method = type
                        .GetMethods(flags)
                        .Where(m => Attribute.GetCustomAttributes(m, true)
                        .OfType<AttrFilter>()
                        .Count() != 0)
                    .FirstOrDefault();
                type = type.BaseType;
                //skip a certain number of method, allows for calling "base" methods
                if(method != null && skip > 0)
                {
                    skip--;
                    method = null;
                }
            } while (method == null && type != null);
            return method;
        }
    }
}
