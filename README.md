# CppCSharpBridge
Simple tool to bridge C++ and Mono C#.  No external dependencies required.

# Overview

This project bridges C++ and C# code through the Mono embedding API.  The code compiles into a executable that reads in interface files and generates binding C++ and C# files. Once generated those files are all that is needed to bridge the languages.  This tool is designed to be simple and to the point, not requiring any external libraries or make process.

# Features

* Namespaces
* Class Inheritence
* Virtual Methods
* Reference Counting
* Out/Ref Method Arguments
* Struct Passing

# Interface Files

Interface files define what and how you want to bridge between the languages.  They are C++/C# like but have some special syntax. Below is an example interface for a C++ class.

```
class Clock
{
  //Options
  cppname MyClockClass
  cppinclude #include "MyClockClass.h"

  //Methods
  func void resetTime(void);
  func int getMilliseconds(void);
  func int getSeconds(void);

  //Variables
  var double timestamp;
}
```

# Passing Files To The Exe

Once the executable is build you just need to pass in your interface files.  This is done by passing in a list of files through command
line arguments.  The sourcecode is very simple, if you don't like it you can easily change the Main method to use any scheme you need.

The current scheme for command line arguments.
* C++ Files Output Path
* C# Files Output Path
* Path to an interface file to read.  Repeat for as many files as you need.

Below is an example of how I accomplish this with CMake for our own projects.

```
file(GLOB CSHARP_WRAPPER_FILES
	${PROJECT_SOURCE_DIR}/Game/CSharp/Interfaces/*.i
)

execute_process(COMMAND
	${CMAKE_SOURCE_DIR}/Tools/CppCSharpBridge.exe
	${PROJECT_SOURCE_DIR}/Game/CSharp/Cpp/
	${PROJECT_SOURCE_DIR}/Game/CSharp/CSharp/
	${CSHARP_WRAPPER_FILES}
	)
```

# Initialization

Once all the generated files are created, you simply add them to their respective projects.  The only other step is to call the CppCSharpBridge::Init() method inside of C++.

Below is an more complex example of how I init mono and call the bridge init method.

```
void _monoLog(const char *log_domain, const char *log_level, const char *message, mono_bool fatal, void *user_data)
{
	if(log_domain)
		cout << log_domain << endl;
	if(log_level)
		cout << log_level << endl;
	if(message)
		cout << message << endl;
}
void _monoPrint(const char *string, mono_bool is_stdout)
{
	cout << string << endl;
}
void _monoPrintErr(const char *string, mono_bool is_stdout)
{
	cout << string << endl;
}
bool InitMono(void)
{
	//Init
	mono_config_parse(nullptr);

	//Debugging
	#ifdef ENABLE_MONO_SOFT_DEBUG
	const char* options[] =
	{
		"--soft-breakpoints",
		"--debugger-agent=transport=dt_socket,address=127.0.0.1:10000"
	};
	mono_jit_parse_options(2, (char**)options);
	mono_debug_init(MONO_DEBUG_FORMAT_MONO);
	#endif

	//Domain
	domain = mono_jit_init("Game");
	mono_thread_attach(domain);

	//Init debugging
	#ifdef ENABLE_MONO_SOFT_DEBUG
	mono_debug_domain_create(domain);
	#endif

	//Assembly
	assembly = mono_domain_assembly_open(domain, "GameCSharp.dll");
	if(!assembly)
		return false;

	//Configure
	if(!CppCSharpBridge::Init(domain, assembly))
	{
		cout << "Unable to init CppCSharpBridge" << endl;
		assert(false);
		return false;
	}

	//Logging
	mono_trace_set_log_handler(_monoLog, nullptr);
	mono_trace_set_print_handler(_monoPrint);
	mono_trace_set_printerr_handler(_monoPrintErr);

	//Return
	return true;
}
```
  
 # Conclusion
 
 In the next coming days I will include examples, documentation and interface files for common complex types like strings.  The project works as in, but it will be bug are found or more complex features need added for my own needs.
 
 If you have any questions, feel free to e-mail me at chase.grozdina@gmail.com
