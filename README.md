# CppCSharpBridge
Simple tool to bridge C++ and Mono C#.  No external dependencies required.

# Overview

This project aids in the process of bridging C++ and Mono C# code.  The code compiles into a executable that takes a list of interface files
and generates both C++ and C# which bind the two languages using the Mono API.  This tool is designed to be simple and to the point, not
requiring any external libraries or complicated make process.

# Interface Files

These files define what and how you want to bridge between the languages.  They are very C++/C# like as to make translating classes simple.
Below is a simple example of how the bridging of a c++ class might look.

class Clock
{
  //Options
  cppname Clock
  cppinclude #include "Clock.h"

  //Methods
  func void resetTime(void);
  func int getMilliseconds(void);
  func int getSeconds(void);

  //Variables
  var double timestamp;
}

# Passing Files To The Exe

Once the executable is build you just need to pass in your interface files.  This is done by passing in a list of files through command
line arguments.  The sourcecode is very simple, if you don't like it you can easily change the Main method to use any scheme you need.

The current scheme for command line arguments.
1: C++ Files Output Path
2: C# Files Output Path
3-?: Path to an interface file to read.  Repeat for as many files as you need.

Below is an example of how I accomplish this with CMake for our own projects.

file(GLOB CSHARP_WRAPPER_FILES
	${PROJECT_SOURCE_DIR}/Game/CSharp/Interfaces/*.i
)

execute_process(COMMAND
	${CMAKE_SOURCE_DIR}/Tools/CppCSharpBridge.exe
	${PROJECT_SOURCE_DIR}/Game/CSharp/Cpp/
	${PROJECT_SOURCE_DIR}/Game/CSharp/CSharp/
	${CSHARP_WRAPPER_FILES}
	)
  
 # Conclusion
 
 If you have any questions, feel free to e-mail me at chase.grozdina@gmail.com
