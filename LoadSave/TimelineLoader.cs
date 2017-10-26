//#define LOGGER
//#define PYTHON
#if PYTHON
using PythonRunning;
#endif
#if LOGGER
using Tools;
#endif
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace ObjectStoring
{
    //TODO check if an attribute is marked with [SaveMe] before LOADING the value into it, 
    //this could be a problem is a bad boy wants to set a property to a value 

    public class TimelineLoader
    {
        /// <summary>
        /// load a json object from the given path using all our attributes 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static object Load(string path)
        {
            string raw = File.ReadAllText(path);
            JToken dict = (JToken)JsonConvert.DeserializeObject(raw);
            return LoadObjectFromJson(dict);
        }

        /// <summary>
        /// parent is passed to the created members in CreateLoadInstance
        /// </summary>
        /// <param name="jtoken"></param>
        /// <param name="guessedType"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static object LoadObjectFromJson(JToken jtoken, Type guessedType = null, object parent = null)
        {
            //if it's a jobject try to create an instance from it's Type, if it fails use the guessedType
            //otherwise just use the value or collection
            if(jtoken is JObject jobj)
            {
                object objInstance = CreateObjectInstance(jobj, guessedType, parent);
                if(objInstance == null)
                {
#if LOGGER
                    Logger.WriteLine("couldn't create an instance of " + jobj + " returning");
#endif
                    return null;
                }
                PopulateObjectInstance(ref objInstance, jobj);
                CallOnDoneLoading(ref objInstance);
                return objInstance;
            }
            else if(jtoken is JProperty jprop)
            {
                //if it's a JProperty, take it's value and run the loading process on it
                if (jprop.Value is JArray jarray)
                    return LoadArrayFromJson(jarray, guessedType, parent);
                else if (jprop.Value is JObject)
                    return LoadObjectFromJson(jprop.Value, guessedType, parent);
                else
                    //if it's not an array or a dict, it's probably just a raw value like a string or int
                    return Convert.ChangeType(jprop.Value, guessedType);
            }
            else if(jtoken is JValue jvalue)
            {
                //if all fails, just return the jtoken as a string
                return jvalue.Value;
            }
            else
            {
#if LOGGER
                Logger.WriteLine("couldn't interpret json:" + jtoken);
#endif
                return jtoken.ToString();
            }
        }

        /// <summary>
        /// call the method marked with on done loading on the given object
        /// we use ref that way is objInstance is a struct it's still good
        /// </summary>
        /// <param name="objInstance"></param>
        private static void CallOnDoneLoading(ref object objInstance)
        {
#if PYTHON
            if (objInstance.GetType().ToString().Contains("IronPython"))
            {
                //for python objects, since IronPython doesn't support Attributes the method has to be named "OnDoneLoading"
                if (Python.Engine.Operations.ContainsMember(objInstance, "OnDoneLoading"))
                {
                    dynamic onDoneLoading = Python.Engine.Operations.GetMember(objInstance, "OnDoneLoading");
                    onDoneLoading();
                }
#if LOGGER
                else
                {
                    Logger.WriteLine("no OnDoneLoading on " + objInstance);
                }
#endif
            }
            else
            {
#endif
                MethodInfo method = TimelineSaver.SearchMethodWithAttr<OnDoneLoadingAttribute>(objInstance.GetType(),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if(method != null)
                {
                    method.Invoke(objInstance, null);
                }
#if LOGGER
                else
                {
                    Logger.WriteLine("no OnDoneLoading on " + objInstance);
                }
#endif
#if PYTHON
            }
#endif
        }

        /// <summary>
        /// populate on object instance with the values in the given JObject
        /// </summary>
        /// <param name="objInstance"></param>
        /// <param name="jobj"></param>
        private static void PopulateObjectInstance(ref object objInstance, JObject jobj)
        {
            //Take in accont custom loader
            bool hasCustomLoader = TryCallCustomLoader(ref objInstance, jobj);
            //if no custom loader, go through each property and assign it myself
            if (!hasCustomLoader)
            {
                foreach(JProperty jprop in jobj.Properties())
                {
                    //except the type entry
                    if(!jprop.Name.Equals("type"))
                        SetPropertyValue(ref objInstance, jprop);
                }
            }
        }

        /// <summary>
        /// the the property/field value of the objInstance to the value of the jProperty
        /// </summary>
        /// <param name="objInstance"></param>
        /// <param name="jprop"></param>
        private static void SetPropertyValue(ref object objInstance, JProperty jprop)
        {
            MemberInfo[] members = objInstance.GetType().GetMember(jprop.Name,
                MemberTypes.Field | MemberTypes.Property, 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
#if LOGGER
            if (members.Length > 1)
                Logger.WriteLine("found " + members.Length + " members named " + jprop.Name + ", using first one");
#endif
            if(members.Length == 0)
            {
#if LOGGER
                Logger.WriteLine("no member named " + jprop + "on " + objInstance + " skipping it");
#endif
                return;
            }

            //member is either FieldInfo or PropertyInfo, both have a "SetValue(object, object)" method
            if(members[0] is PropertyInfo propertyInfo)
            {
                object loaded = LoadObjectFromJson(jprop, propertyInfo.PropertyType, objInstance);
                propertyInfo.SetValue(objInstance, loaded);
            }
            else if(members[0] is FieldInfo fieldInfo)
            {
                fieldInfo.SetValue(objInstance, LoadObjectFromJson(jprop, fieldInfo.FieldType, objInstance));
            }
#if LOGGER
            else
            {
                Logger.WriteLine("couldn't set property value for " + jprop.Name + " on " + objInstance);
            }
#endif
        }

        /// <summary>
        /// call the method marked with CustomLoader on the object, if there is one
        /// otherwise return false
        /// </summary>
        /// <param name="objInstance"></param>
        /// <param name="jobj"></param>
        /// <returns></returns>
        private static bool TryCallCustomLoader(ref object objInstance, JObject jobj)
        {
#if PYTHON
            if (objInstance.GetType().ToString().Contains("IronPython"))
            {
                if(Python.Engine.Operations.ContainsMember(objInstance, "CustomLoader"))
                {
                    dynamic customLoader = Python.Engine.Operations.GetMember(objInstance, "CustomLoader");
                    customLoader(jobj);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
#endif
                MethodInfo method = TimelineSaver.SearchMethodWithAttr<CustomLoaderAttribute>(objInstance.GetType(),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if(method != null)
                {
                    method.Invoke(objInstance, new object[] { jobj });
                    return true;
                }
                else
                {
                    return false;
                }
#if PYTHON
            }
#endif
        }

        /// <summary>
        /// create an instance of the object represented by the JObject taking in account 
        /// all the custom attributes. You should also provide a type if you know what the type
        /// could be. 
        /// </summary>
        /// <param name="jobj"></param>
        /// <param name="guessedType"></param>
        /// <param name="parent">the object that will contain the created instance</param>
        /// <returns></returns>
        public static object CreateObjectInstance(JObject jobj, Type guessedType, object parent)
        {
            JProperty typeProperty = jobj.Property("type");
            if(typeProperty != null)
            {
                string typeString = (string)typeProperty.Value;
                object instance = TryCallCreateLoadInstance(typeString, parent);
                if(instance == null)
                {
                    //no create load instance, trying to create with activator and no argument
                    instance = TryCreateInstance(typeString);
#if LOGGER
                    if (instance == null)
                        Logger.WriteLine("couldn't create instance from type string: " + typeString);
#endif
                    return instance;
                }
                else
                {
                    return instance;
                }
            }
            else if(guessedType != null)
            {
                object instance = TryCallCreateLoadInstance(guessedType, parent);
                if(instance == null)
                {
                    instance = TryCreateInstance(guessedType);
#if LOGGER
                    if (instance == null)
                        Logger.WriteLine("couldn't create instance of type " + guessedType);
#endif
                    return instance;
                }
                else
                {
                    return instance;
                }
            }
            else
            {
#if LOGGER
                Logger.WriteLine("no type entry and no guessed type for " + jobj + " returning null instance");
#endif
                return null;
            }
        }

            /// <summary>
            /// try to create an instance of an object with only it's type name
            /// returns null if the creation failed
            /// </summary>
            /// <param name="typeString"></param>
            /// <returns></returns>
            private static object TryCreateInstance(string typeString)
        {
#if PYTHON
            if (typeString.Contains("IronPython"))
            {
                CreatableNode creatableNode = CreatableNode.CreateDynamic(FindFileWithName(typeString));
                if (creatableNode == null)
                {
#if LOGGER
                    Logger.WriteLine("coudln't create CreatableNode python object");
#endif
                    return null;
                }
                return creatableNode.CreateInstance();
            }
            else
            {
#endif
                Type type = Type.GetType(typeString);
                if(type != null)
                {
                    return TryCreateInstance(type);
                }
                else
                {
#if LOGGER
                    Logger.WriteLine("couldn't create type from string:" + typeString);
#endif
                    return null;
                }
#if PYTHON
            }
#endif
        }

        /// <summary>
        /// try to create an instance of the given type by calling the empty constructor
        /// returns null if the creation failed
        /// </summary>
        /// <param name="guessedType"></param>
        /// <returns></returns>
        private static object TryCreateInstance(Type guessedType)
        {
            try
            {
                return Activator.CreateInstance(guessedType, null);
            }
            catch (MissingMethodException)
            {
                return null;
            }
        }

        /// <summary>
        /// try to call the static method marked with <see cref="CreateLoadInstance"/>
        /// return the instance if the methid was there, null otherwise
        /// </summary>
        /// <param name="typeString"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        private static object TryCallCreateLoadInstance(string typeString, object parent)
        {
#if PYTHON
            if (typeString.Contains("IronPython"))
            {
                CreatableNode creatableNode = CreatableNode.CreateDynamic(FindFileWithName(typeString));
                if (creatableNode == null)
                {
#if LOGGER
                    Logger.WriteLine("coudln't create CreatableNode python object");
#endif
                    return null;
                }

                if (Python.Engine.Operations.GetMemberNames(creatableNode.pythonType).Contains("CreateLoadInstance"))
                {
                    //it has a createLoadInstance method
                    dynamic createLoadInstance = Python.Engine.Operations
                        .GetMember(creatableNode.pythonType, "CreateLoadInstance");
                    return createLoadInstance(parent, typeString);
                }
                else
                {
                    //no create load instance method here
                    return null;
                }
            }
            else
            {
#endif
                Type type = Type.GetType(typeString);
                if(type != null)
                {
                    return TryCallCreateLoadInstance(type, parent);
                }
                else
                {
#if LOGGER
                    Logger.WriteLine("couldn't create type from string:" + typeString);
#endif
                    return null;
                }
#if PYTHON
            }
#endif
        }

        /// <summary>
        /// Same as <see cref="TryCallCreateLoadInstance(string, object)"/> except it won't 
        /// take care of IronPython objects
        /// </summary>
        /// <param name="type"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        private static object TryCallCreateLoadInstance(Type type, object parent)
        {
            MethodInfo method = TimelineSaver.SearchMethodWithAttr<CreateLoadInstanceAttribute>(type,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            return method?.Invoke(null, new object[] { parent, type });
        }

        /// <summary>
        /// create an instane of an array of guessed type and create instances for each element
        /// of the JArray and store them in the array
        /// This will call <see cref="LoadObjectFromJson(JToken, Type, object)"/> on each element
        /// of the array
        /// </summary>
        /// <param name="jarray"></param>
        /// <param name="guessedType"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static object LoadArrayFromJson(JArray jarray, Type guessedType, object parent)
        {
            //we have to know what type the array is supposed to be
            if (guessedType == null)
            {
#if LOGGER
                Logger.WriteLine("guessed type was null in JArray, can't create instance");
#endif
                return null;
            }

            //create array instance 
            IList arrayInstance = (IList)Activator.CreateInstance(guessedType);
            Type itemExpectedType = null;
            if (guessedType.IsGenericType)
                itemExpectedType = guessedType.GenericTypeArguments[0];

            //populate it by calling LoadObjectFromJson on each item 
            for (int i = 0; i < jarray.Count; i++)
            {
                object obj = LoadObjectFromJson(jarray[i], parent: parent);
                if (!itemExpectedType.IsAssignableFrom(obj.GetType()))
                    obj = Convert.ChangeType(obj, itemExpectedType);
                arrayInstance.Add(obj);
                if(obj is IHasHost hasHost)
                {
                    hasHost.SetHost(parent);
                }
            }

            return arrayInstance;
        }

#if PYTHON
        /// <summary>
        /// search a fil in a directory
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="name"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        static bool SearchDirForFile(string dir, string name, out string file)
        {
            if (Directory.Exists(dir))
            {
                string[] files = Directory.GetFiles(dir);
                for (int i = 0; i < files.Length; i++)
                {
                    string n = Path.GetFileNameWithoutExtension(files[i]);
                    if (n.Equals(name))
                    {
                        file = files[i];
                        return true;
                    }
                }
            }
            file = "";
            return false;
        }
        /// <summary>
        /// find a python or c# file that has this name and returns it's full path
        /// or an empty string if not found
        /// The name should start with IronPython. which will be removed
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string FindFileWithName(string name)
        {
            //get everything after the first .
            name = name.Split(new char[] { '.' }, 2)[1];

            string[] dirsToSearch =
            {
                Global.PythonGraphicsPath,
                Global.PythonPropertyPath,
                Global.CsharpGraphicsPath,
                Global.CsharpPropertyPath
            };

            for (int i = 0; i < dirsToSearch.Length; i++)
            {
                if (SearchDirForFile(dirsToSearch[i], name, out string file))
                    return file;
            }

            return string.Empty;
        }
#endif
    }
}
