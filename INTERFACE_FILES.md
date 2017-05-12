# Introduction

Interface files are text file you write to tell the build tool what and how you want to bridge C++ and C#.  They are structured similarly to C++/C# but have some special syntax.

# Types - Overview
Types are the core building block for the interface file and define everything you can bridge between the two languages.

* Class
```
class Clock
{
  func Clock();
  func int getTime();
}
```

* Struct
```
struct Vector3
{
  var float x;
  var float y;
  var float z;
}
```

* Namespaces
```
namespace Game
{
  class Clock
  {
    func Clock();
    func int getTime();
  }
}
```

* Enums
```
enum SeekPos
{
  BEGIN,
  CURRENT,
  END
}
enum Numbers : int
{
  ZERO = 0,
  ONE = 1,
  TWO = 2
}
```

* Type
```
type uint8
{
    cpptype uint8_t
    cpppass uint8_t
    cstype byte
    cspass byte
}
```

# Types - Advanced

Each interface file can contain any number of types.  Once a type is defined, it can be refered to in any other interface file.  Ordering of the type definitions do not matter and can exist in any file as long as they are all fed to the build tool at the same time.

Types can be nested in namespaces or classes.  Because of this, when you refer to a type you either have to give a fully qualified name or qualified enough for your current context.  This works like most object oriented C languages.  You can use either the . or :: dilimiter, in my examples I will be using the dot.

```
//Defines a class inside of a namespace
namespace Game
{
  class Clock
  {
  }
}

//Refers to that type outside of the namespace
class Application
{
  func Game.Clock getClock();
}

//Refers to that type inside of that namespace
namespace Game
{
  class Application2
  {
    func Clock getClock();
  }
}
```

If a type has not been defined or can't be found, we will use the type text verbatim.  This is how simple types like "float", "int", ect are handled.

# Commenting

The parser does support commenting, but only a particular style.  A comment must be on its own line using the // notation.
More robust commenting is a feature to be worked on at another time.

Example of acceptable commenting
```
//A clock class
//Used for clock things
class Clock
{
  //Methods
  func int getTime();
}
```

# Continued Reading

You can read more details about each type by reading its own INTERFACE-"TYPE".md file

I would recommend starting with the TYPE type, as all other types are extentsion of this and inherit the same options.
