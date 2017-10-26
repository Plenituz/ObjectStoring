//#define PYTHON
#if PYTHON
using Microsoft.Scripting.Hosting;
using PythonRunning;
#endif
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ObjectStoring
{
    /// <summary>
    /// Save any object into a JSON file, taking in account any of the save related attributes:
    /// <see cref="CustomSaverAttribute"/>, <see cref="SaveMeAttribute"/>
    /// Note: only the properties/fields marked with [SaveMe] will be saved
    /// </summary>
    public class TimelineSaver
    {
        /// <summary>
        /// helper class to store a name an a value
        /// </summary>
        class SavableMember
        {
            public string name;
            public object value;

            public SavableMember(string name, object value)
            {
                this.name = name;
                this.value = value;
            }
        }

        object timeline;

        public TimelineSaver(object timeline)
        {
            this.timeline = timeline;
        }

        /// <summary>
        /// save the "timeline" object into the given path as a json file
        /// </summary>
        /// <param name="path"></param>
        public void Save(string path)
        {
            object dct = SaveObjectToJson(timeline);
            try
            {
                string json = JsonConvert.SerializeObject(dct, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                throw new Exception("Error converting to Json:\n" + e +
                    "\n\nDid you make sur to have a def CustomSaver(self)" +
                    " on all the python attributes that are listed in saveAttrs ?\n");
            }
        }

        /// <summary>
        /// extract the properties/fields of the given object into an object that can be saved in json
        /// typically <see cref="Dictionary{string, object}"/> but not limited to it
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static object SaveObjectToJson(object obj)
        {
            //check if it has custom saver
            //if it does use it
            //if not list savable members and call saveObjecttojson on them
            Func<object> customSaver = GetCustomSaver(obj);
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
                    return obj;
            }
            Dictionary<string, object> jsonDict = new Dictionary<string, object>();
            foreach(SavableMember savableMember in savableMembers)
            {
                jsonDict.Add(savableMember.name, SaveObjectToJson(savableMember.value));
            }
            jsonDict.Add("type", GetJsonTypeString(obj));
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
        /// get the type of the given object to store into a "type" attribute
        /// this takes in accout the possibility of having IronPython objects in the mix
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string GetJsonTypeString(object obj)
        {
            Type objType = obj.GetType();
            string typeString = objType.AssemblyQualifiedName;
#if PYTHON
            Type dynamicType =  Type.GetType("NodeSystem.Utils.IDynamicNode, Tools");
            if (dynamicType.IsAssignableFrom(objType))
            {
                typeString = "IronPython." + Python.GetClassName(obj);
            }
            //else if (typeString.Contains("IronPython"))
            //{
            //    typeString = "IronPython."
            //        + GetPythonTypeString(Python.Engine, obj);
            //}
#endif
            return typeString;
        }

        /// <summary>
        /// return Func that should call the method marked with <see cref="CustomSaverAttribute"/>
        /// on the given object
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Func<object> GetCustomSaver(object obj)
        {
#if PYTHON
            if (obj.GetType().ToString().Contains("IronPython"))
            {
                dynamic dyn = obj;
                if(Python.Engine.Operations.ContainsMember(dyn, "CustomSaver"))
                    return () => dyn.CustomSaver();
            }
            else
            {
#endif
                MethodInfo customSaver = SearchMethodWithAttr<CustomSaverAttribute>(obj.GetType(),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (customSaver != null)
                    return () => customSaver.Invoke(obj, null);
#if PYTHON
            }
#endif
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
#if PYTHON
            if (obj.GetType().ToString().Contains("IronPython"))
            {
                //Add python AND csharp members to have inheritance
                var list = new List<SavableMember>();
                var listPython = ListSavableMembersPython(obj);
                var listCSharp = ListSavableMembersCSharp(obj);
                if (listPython != null)
                    list.AddRange(listPython);
                if (listCSharp != null)
                    list.AddRange(listCSharp);
                return list;
            }
            else
            {
#endif
                return ListSavableMembersCSharp(obj);
#if PYTHON
            }
#endif
        }

#if PYTHON
        /// <summary>
        /// same as <see cref="ListSavableMembers(object)"/> but only for IronPython objects
        /// in fact <see cref="ListSavableMembers(object)"/> will call this method
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static IEnumerable<SavableMember> ListSavableMembersPython(object obj)
        {
            //if it's a python class there won't be any attributes
            //so check the "saveAttrs" class variable
            dynamic dyn = obj;
            if (Python.Engine.Operations.ContainsMember(dyn, "saveAttrs"))
            {
                var members = dyn.__dict__;
                IEnumerable attrsToSave = dyn.saveAttrs;
                List<SavableMember> savableMembers = new List<SavableMember>();
                foreach(string str in attrsToSave)
                {
                    savableMembers.Add(new SavableMember(str, members[str]));
                }
                return savableMembers;
            }
            return null;
        }
#endif

        /// <summary>
        /// same as <see cref="ListSavableMembers(object)"/> but only for CSharp classes
        /// in fact <see cref="ListSavableMembers(object)"/> will call this method
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static IEnumerable<SavableMember> ListSavableMembersCSharp(object obj)
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
        public static MethodInfo SearchMethodWithAttr<AttrFilter>(Type type, BindingFlags flags)
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
            } while (method == null && type != null);
            return method;
        }

#if PYTHON
        /// <summary>
        /// get the type of an IronPython object
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string GetPythonTypeString(ScriptEngine engine, dynamic obj)
        {
            var classMember = engine.Operations.GetMember(obj, "__class__");
            var str = engine.Operations.GetMember(classMember, "__str__");
            string typeStringDirty = str(obj);
            //extract the first word
            //typeStringDirty=<classname at 0x1232F>
            return typeStringDirty.Substring(1).Split(new char[] { ' ' }, 2)[0];
        }
#endif

    }
}
