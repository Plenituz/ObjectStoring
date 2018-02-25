#define LOGGER
using Motio.Debuging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Motio.ObjectStoring
{
    //TODO check if an attribute is marked with [SaveMe] before LOADING the value into it, 
    //this could be a problem is a bad boy wants to set a property to a value 
    /// <summary>
    /// WARNING  THIS CLASS IS NOT THREAD SAFE, IN FACT IT IS THE EXACT OPPOSITE OF THREAD SAFE, IT WILL
    /// BREAK IF YOU LOAD SEVERAL FILES AT THE SAME TIME
    /// this is due to the choice of making the LoadObjectFromJson object static
    /// </summary>
    public class TimelineLoader
    {
        private static List<KeyValuePair<object, JObject>> createdObjects = new List<KeyValuePair<object, JObject>>();
        private static ICreatableProvider provider;

        /// <summary>
        /// load a json object from the given path using all our attributes 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static object Load(string path, ICreatableProvider provider)
        {
            string raw = File.ReadAllText(path);
            JToken dict = (JToken)JsonConvert.DeserializeObject(raw);
            return Load(dict, provider);
        }

        public static object Load(JToken jtoken, ICreatableProvider provider)
        {
            createdObjects.Clear();
            //not the best design but makes it super duper easy to use
            TimelineLoader.provider = provider;

            object created = LoadObjectFromJson(jtoken);

            //call on timelineDone on all createdObjects
            foreach (var pair in createdObjects)
            {
                object obj = pair.Key;
                CallOnAllLoaded(ref obj, pair.Value);
            }

            TimelineLoader.provider = null;
            createdObjects.Clear();
            return created;
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
                createdObjects.Add(new KeyValuePair<object, JObject>(objInstance, jobj));
                return objInstance;
            }
            else if(jtoken is JProperty jprop)
            {
                return LoadObjectFromJson(jprop.Value, guessedType, parent);
                //if it's a JProperty, take it's value and run the loading process on it
                    
                
                //else if (jprop.Value is JObject)
                //    return LoadObjectFromJson(jprop.Value, guessedType, parent);
                //else
                //    //if it's not an array or a dict, it's probably just a raw value like a string or int
                //    return Convert.ChangeType(jprop.Value, guessedType);
            }
            else if(jtoken is JArray jarray)
            {
                return LoadArrayFromJson(jarray, guessedType, parent);
            }
            else if(jtoken is JValue jvalue)
            {
                //if all fails, try to convert it
                if (guessedType == null)
                {
                    return jvalue.Value;
                }
                else if (guessedType.IsGenericType && guessedType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    Type genericType = guessedType.GetGenericTypeDefinition();
                    genericType = genericType.MakeGenericType(guessedType.GetGenericArguments());
                    ConstructorInfo ctr = genericType.GetConstructors()[0];

                    //if arg is null don't convert it (because you can't convert null)
                    object arg = jvalue.Value;
                    if (arg != null)
                        arg = Convert.ChangeType(arg, guessedType.GetGenericArguments()[0]);

                    object nullable = ctr.Invoke(new object[] { arg });
                    return nullable;
                }
                else if(guessedType.IsEnum)
                {
                    object enumVal = Enum.ToObject(guessedType, jvalue.Value);
                    return enumVal;
                }
                else
                {
                    //fallback try to convert
                    return Convert.ChangeType(jvalue.Value, guessedType);
                }
            }
            else
            {
#if LOGGER
                Logger.WriteLine("couldn't interpret json:" + jtoken);
#endif
                return jtoken.ToString();
            }
        }

        private static void CallOnAllLoaded(ref object objInstance, JObject jobj)
        {
            if(provider?.IsCustomCreated(objInstance) == true)
            {
                provider.CallOnAllLoaded(objInstance, jobj);
            }
            else
            {
                MethodInfo method = TimelineSaver.SearchMethodWithAttr<OnAllLoadedAttribute>(objInstance.GetType(),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(objInstance, new object[] { jobj });
                }
            }
        }

        /// <summary>
        /// call the method marked with on done loading on the given object
        /// we use ref that way is objInstance is a struct it's still good
        /// </summary>
        /// <param name="objInstance"></param>
        public static void CallOnDoneLoading(ref object objInstance)
        { 
            if(provider?.IsCustomCreated(objInstance) == true)
            {
                provider.CallOnDoneLoading(objInstance);
            }
            else
            {
                MethodInfo method = TimelineSaver.SearchMethodWithAttr<OnDoneLoadingAttribute>(objInstance.GetType(),
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if(method != null)
                {
                    method.Invoke(objInstance, null);
                }
            }
        }

        /// <summary>
        /// populate on object instance with the values in the given JObject
        /// </summary>
        /// <param name="objInstance"></param>
        /// <param name="jobj"></param>
        public static void PopulateObjectInstance(ref object objInstance, JObject jobj, bool useCustom = true)
        {
            //Take in accont custom loader
            bool hasCustomLoader = false;
            if (useCustom)
                hasCustomLoader = TryCallCustomLoader(ref objInstance, jobj);
            //if no custom loader, go through each property and assign it myself
            if (!hasCustomLoader)
            {
                foreach(JProperty jprop in jobj.Properties())
                {
                    //except the type entry
                    if(!jprop.Name.Equals(TimelineSaver.TYPE_NAME))
                        SetPropertyValue(ref objInstance, jprop);
                }
            }
        }

        /// <summary>
        /// the the property/field value of the objInstance to the value of the jProperty
        /// </summary>
        /// <param name="objInstance"></param>
        /// <param name="jprop"></param>
        public static void SetPropertyValue(ref object objInstance, JProperty jprop)
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
            if(provider?.IsCustomCreated(objInstance) == true)
            {
                bool hasLoader = provider.HasCustomLoader(objInstance);
                if(hasLoader)
                    provider.CallCustomLoader(objInstance, jobj);
                
                return hasLoader;
            }
            else
            {
                MethodInfo method = TimelineSaver.SearchMethodWithAttr<CustomLoaderAttribute>(objInstance.GetType(),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if(method != null)
                    method.Invoke(objInstance, new object[] { jobj });
                
                return method != null;
            }
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
            JProperty typeProperty = jobj.Property(TimelineSaver.TYPE_NAME);
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
            if (provider?.IsTypeStringValid(typeString) == true)
            {
                ICreatable creatableNode = provider.CreateFromTypeString(typeString);
                //CreatableNode creatableNode = CreatableNode.CreateDynamic(FindFileWithName(typeString));
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
            }
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
            if (provider?.IsTypeStringValid(typeString) == true)
            {
                ICreatable creatableNode = provider.CreateFromTypeString(typeString);
                if (creatableNode == null)
                {
#if LOGGER
                    Logger.WriteLine("coudln't create CreatableNode python object");
#endif
                    return null;
                }

                if(creatableNode.HasCreateLoadInstance())
                    return creatableNode.CreateInstanceWithCreateLoadInstance(parent, typeString);
                else
                    return null;
            }
            else
            {
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
            }
        }

        /// <summary>
        /// Same as <see cref="TryCallCreateLoadInstance(string, object)"/> except it won't 
        /// take care of IronPython objects
        /// </summary>
        /// <param name="type"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static object TryCallCreateLoadInstance(Type type, object parent)
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
            IList arrayInstance;
            arrayInstance = (IList)Activator.CreateInstance(guessedType);
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
                if(obj is ISetParent hasHost)
                {
                    hasHost.SetParent(parent);
                }
            }

            return arrayInstance;
        }
    }
}
