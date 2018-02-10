# Introduction

Namespaces are not a "type" like "class" or "enum".  They are instead a way for you to specify how you would like the generated files to be organized as well as how you refer to types inside of the interface files itself.

Namespaces can either be at the top level or nested inside of other namespaces.

# Example

This is a C++ class that we are translating.  Note this isn't inside any namespace.
```
#ifdef CLOCK_H
#define CLOCK_H

class Clock
{
public:
  Clock(void);
};

#endif
```

This is our interface file definition of the clock class.
```
namespace Game
{
  class Clock()
  {
    cppname Clock
    cppinclude #include "Clock.h"
  }
}
```

In this example we are translating the C++ class Clock into C#. In the interface file we defined Clock inside of a namespace, as a result the generated C# files will have the Clock class inside of a Game namespace.  Be default the build tool will assume the C++ class is also in the same namespace.  However this is not the case in our example, so we used the option "cppname" to tell the build tool that in C++ we only want to refer to the type as "Clock" without the namespace.
