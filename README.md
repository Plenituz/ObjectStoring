# ObjectStoring
Store and load objects as a json file according to attributes you put on the members of the class
I use this in one of my projects which uses IronPython so this library takes in account IronPython objects, but as some effort is neccessary (see below) 
to implement creation of objects with IronPython, this functionnality is disabled by default. If you want to enable it go into TimelineLoader and TimelineSaver
and uncomment the `#define PYTHON`
Other than that this same library should work out of the box provided your objects have the right attributes (especially CreateLoadIntance which is neccessary 
if your object can't be created with an empty constructor that is public)

See each attributes in the Attributes folder to know what you can do with this system.

Note: 
This won't compile as provided, you will need to change some things since it depends on other classes not in this repo
(Logger for example) but if you remove any reference to them it should still work

Note 2: 
Any element that will be part of an array and have a reference to the object storing the array can implement the IHasHost interface. 
For example if you have :

```c#
class Holder{
   List<Item> items;
}

class Item{
  Holder parent;
}
```

the Item class can implement IHasHost and the SetHost method will be called uppon loading the "items" list with the reference to the instance of Holder

Note 3:
If you want to use this with IronPython objects notice I handled the creation of python instances in another class called "CreatableNode". 
You will need to create your own way of creating a python instance (and a PythonType) from the path to a .py file.
You should also provide a list of directories to search for python files in FindFileWithName(string name).dirsToSearch


