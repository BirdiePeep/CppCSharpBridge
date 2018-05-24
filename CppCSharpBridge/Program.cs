/*
	MIT License

	Copyright (c) 2017 Chase Grozdina

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CppCSharpBridge
{
	class Program
	{
		static void Main(string[] args)
		{
			WrapperGenerator generator = new WrapperGenerator();
			WrapperParser parser = new WrapperParser(generator);

			//Build paths
			if(args.Length >= 1)
			{
				generator.cppOutputPath = args[0];
			}
			if(args.Length >= 2)
			{
				generator.csharpOutputPath = args[1];
			}

			//Parse input files
			for(int i=2; i<args.Length; i++)
			{
				Console.Write("Parsing Input File:" + args[i] + "\n");
				parser.parseFile(args[i]);
			}
			parser.finalize();

			//Generate
			if(!generator.generate())
			{
				System.Console.Write("Error generating API binding");
				return;
			}
		}
	}

	public class WrapType
	{
		public string name;
		public WrapClass wrapClass;

		public string uniqueName	//Name used to identify this class differently then all other classes
		{
			get
			{
				return buildQualifiedName("_");
			}
		}
		public string buildQualifiedName(string delimiter, bool includeSelf=true)
		{
			//Check for parent context
			if(parentContext == null)
				return name;

			//Build qualifier list
			string result = "";
			List<string> namespaces = parentContext.buildQualifiers();
			int size = namespaces.Count;
			for(int i = 0; i < size; i++)
			{
				result += namespaces[i];
				if(i < size - 1 || includeSelf)
					result += delimiter;
			}

			//Append name
			if(includeSelf)
				result += name;

			//Return
			return result;
		}
		public string buildNamespaceString(string delimiter)
		{
			//Build qualifier list
			string result = "";
			List<string> namespaces = parentContext.buildNamespaces();
			int size = namespaces.Count;
			for(int i = 0; i < size; i++)
			{
				result += namespaces[i];
				if(i < size - 1)
					result += delimiter;
			}

			//Return
			return result;
		}
		public string buildClassNestedString(string delimiter)	//Used for specific mono input.  Creates string off all nested non-namespace contexts
		{
			//Build qualifier list
			string result = "";
			WrapContext context = parentContext;
			while(context != null)
			{
				//Check if done
				if(context.sourceType == null || String.IsNullOrEmpty(context.name))
					break;

				//Add
				result += context.name + delimiter;

				//Continue
				context = context.parent;
			}
			result += name;

			//Return
			return result;
		}

		//Context
		public WrapContext parentContext;

		//Affects C++ generation
		public string cppName;
		public string cppType;			//What type is exposed to the C++ interface
		public string cppInclude;   //When this type is used, what header file needs included

		//Cpp transfer
		public string cppInPass;
		public string cppOutPass;
		public string cppInConvert;
		public string cppOutConvert;
		public string cppOutConvertCleanup;

		//Boxed Helpers
		//Used when using thunks to call into C#
		public string cppInPassBoxed
		{
			get
			{
				if(this is WrapStruct)
					return "MonoObject*";
				else
					return cppInPass;
			}
		}
		public string cppOutPassBoxed
		{
			get
			{
				if(this is WrapStruct)
					return "MonoObject*";
				else
					return cppOutPass;
			}
		}
		public string cppInConvertBoxed
		{
			get
			{
				if(this is WrapStruct)
					return "$result = *($langtype*)mono_object_unbox($input);";
				else
					return cppInConvert;
			}
		}
		public string cppOutConvertBoxed
		{
			get
			{
				if(this is WrapStruct)
					return "$result = mono_value_box(CppCSharpBridge::domain, CppCSharpBridge::class_$uniquename, &$input);";
				else
					return cppOutConvert;
			}
		}

		//Affects C# generation
		public string csName;
		public string csType;			//What type is exposed to the C# interface
		public string csInPass;			//What type is passed to C# and C++
		public string csOutPass;        //What type is passed from C# and C++
		public string csInConvert;		//Converts from input type to actual type
		public string csOutConvert;

		//Other
		public List<string> textBlocks = new List<string>();

		//Clone
		public WrapType clone()
		{
			WrapType clone = new WrapType();
			clone.name = name;
			clone.wrapClass = wrapClass;
			clone.parentContext = parentContext;
			clone.cppName = cppName;
			clone.cppType = cppType;
			clone.cppInclude = cppInclude;

			//Marshled transfer
			clone.cppInPass = cppInPass;
			clone.cppOutPass = cppOutPass;
			clone.cppInConvert = cppInConvert;
			clone.cppOutConvert = cppOutConvert;
			clone.cppOutConvertCleanup = cppOutConvertCleanup;

			//Affects C# generation
			clone.csName = csName;
			clone.csType = csType;
			clone.csInPass = csInPass;
			clone.csOutPass = csOutPass;
			clone.csInConvert = csInConvert;
			clone.csOutConvert = csOutConvert;

			//Other
			clone.textBlocks = new List<string>(textBlocks);

			//Return
			return clone;
		}
	}
	public class WrapClass: WrapType
	{
		public enum InterfaceType
		{
			CPP_TO_CSHARP,
			CSHARP_TO_CPP
		}
		public InterfaceType interfaceType = InterfaceType.CPP_TO_CSHARP;

		public enum PassType
		{
			REFERENCE,
			VALUE,
		}
		public PassType passType = PassType.REFERENCE;

		//Data
		public string parentClass;
		public string type;			//"class" vs "struct"

		//public List<string> cppNamespaces = new List<string>();
		//public List<string> csNamespaces = new List<string>();

		public List<WrapMethod> constructors = new List<WrapMethod>();
		public List<WrapMethod> methods = new List<WrapMethod>();
		public List<WrapVariable> variables = new List<WrapVariable>();
		public List<WrapEnum> enums = new List<WrapEnum>();

		//Affects C++ generation
		public string cppConstruct;						//Create a new an C++ instance of this class
		public string cppDeconstruct;                   //Delete a C++ instance of this class
		public string cppWrapperConstruct;				//Create a new mono object to wrap the class

		public bool explicitConstruction = false;		//Can't new normally, only though explicit New and Delete static methods
		public bool explicitDeconstruction = false;     //Can't new normally, only though explicit New and Delete static methods

		public bool cppCanBePassed = true;              //Can this be passed through to C#, or is it there just to

		//Affects C++/C# generation
		public bool isSealed = false;					//Internal flag, used to determine if a C++ wrapper object is created

		//Meta Data
		//public string cppNameQualified;			//Name with fully qualified namespace
		//public string csNameQualified;			//Name with fully qualified namespace
		public List<WrapClass> childClasses;        //Any classes that inherit from us

		//Context
		public WrapContext context;

		public WrapClass() { }

		public bool isAbstract()
		{
			foreach(WrapMethod wrapMethod in methods)
			{
				if(wrapMethod.isAbstract)
					return true;
			}
			return false;
		}
		public bool hasVirtualMethods(WrapperGenerator generator)
		{
			//Do we have virtual methods
			foreach(WrapMethod wrapMethod in methods)
			{
				if(wrapMethod.isVirtual)
					return true;
			}

			//Check our parents
			if(!String.IsNullOrEmpty(parentClass))
			{
				WrapClass parent = generator.findClass(this.parentClass);
				return parent.hasVirtualMethods(generator);
			}
			else
				return false;
		}
		public int getMethodUniqueIndex(WrapMethod wrapMethod)
		{
			int iter = 0;
			foreach(WrapMethod testMethod in methods)
			{
				if(testMethod == wrapMethod)
					return iter;
				if(testMethod.name == wrapMethod.name)
					iter++;
			}
			return 0;
		}

		public WrapMethod findMethod(string name)
		{
			foreach(WrapMethod method in methods)
			{
				if(method.name == name)
					return method;
			}
			return null;
		}
		public bool isMethodOverride(WrapperGenerator generator, WrapMethod method)
		{
			//Find method name
			WrapMethod matchMethod = findMethod(method.name);
			if(matchMethod != null && matchMethod != method)
				return true;

			//Check parent
			if(!String.IsNullOrEmpty(this.parentClass))
			{
				WrapClass parentClass = generator.findClass(this.parentClass);
				if(parentClass != null)
					return parentClass.isMethodOverride(generator, method);
			}

			//Return
			return false;
		}

		public bool isFinalMethod(WrapperGenerator generator, WrapMethod method)
		{
			//Find method name
			WrapMethod matchMethod = findMethod(method.name);
			if(matchMethod != null)
			{
				if(matchMethod == method)
					return true;

				if(matchMethod.name == method.name)
					return false;
			}

			//Check parent
			if(!String.IsNullOrEmpty(this.parentClass))
			{
				WrapClass parentClass = generator.findClass(this.parentClass);
				if(parentClass != null)
					return parentClass.isFinalMethod(generator, method);
			}

			//Return
			return false;
		}
	}
	public class WrapMethod
	{
		//Data
		public string name;
		public string returnType;
		public List<WrapMethodArg> args = new List<WrapMethodArg>();

		//Options
		public bool isStatic = false;
		public bool isVirtual = false;
		public bool isAbstract = false;
		public bool isConst = false;
		public bool isUnsafe = false;

		public WrapMethod() { }

		public WrapMethod Clone()
		{
			WrapMethod result = new WrapMethod();
			result.returnType = returnType;
			result.name = name;
			result.args = new List<WrapMethodArg>(args);
			result.isStatic = isStatic;
			result.isVirtual = isVirtual;
			result.isAbstract = isAbstract;
			result.isConst = isConst;
			result.isUnsafe = isUnsafe;

			return result;
		}

		public string getCppArgDef(WrapperGenerator generator)
		{
			StringBuilder buffer = new StringBuilder();
			for(int i=0; i<args.Count; i++)
			{
				//Comma
				if(i > 0)
					buffer.Append(", ");

				//Arg
				WrapMethodArg wrapArg = args[i];
				buffer.Append(generator.findType(wrapArg.type).cppType);
				buffer.Append(" ");
				buffer.Append(wrapArg.name);
			}
			return buffer.ToString();
		}
		public string getCppArgCall(WrapperGenerator generator)
		{
			StringBuilder buffer = new StringBuilder();
			for(int i = 0; i < args.Count; i++)
			{
				//Comma
				if(i > 0)
					buffer.Append(", ");

				//Arg
				WrapMethodArg wrapArg = args[i];
				WrapType type = generator.findType(wrapArg.type);

				//Check if struct
				if(type is WrapStruct && !wrapArg.moveOut)
					buffer.Append("*");
				else if(wrapArg.cppRef)
					buffer.Append("*");

				if(String.IsNullOrEmpty(type.cppInConvert))
				{
					buffer.Append(wrapArg.name);
				}
				else
				{
					if(wrapArg.moveOut)
						buffer.Append("&");

					buffer.Append("arg");
					buffer.Append(i);
				}
			}
			return buffer.ToString();
		}
	}
	public class WrapMethodReturn
	{
		public string type;
		public string cppModifier;
	}
	public class WrapMethodArg
	{
		public string type;
		public string name;
		public string defaultValue;

		public bool moveIn = true;
		public bool moveOut = false;

		public bool cppRef = false;

		public WrapMethodArg() { }
		public WrapMethodArg(string type, string name)
		{
			this.type = type;
			this.name = name;
		}
	}
	public class WrapVariable
	{
		public string type;
		public string name;
		//public string refType;

		public string getBody = "$result = $obj->$varname;";
		public string setBody = "$obj->$varname = $input;";
	}

	public class WrapStruct : WrapType
	{
		public List<WrapVariable> variables = new List<WrapVariable>();
		public WrapContext context;
	}
	public class WrapEnum : WrapType
	{
		public string enumType;
		public List<WrapEnumVar> vars = new List<WrapEnumVar>();
	}
	public class WrapEnumVar
	{
		public string name;
		public string value;
	}

	public class WrapNamespace
	{
		public string name;
	}

	public class WrapContext
	{
		//Connections
		public WrapContext parent = null;
		public Dictionary<string, WrapContext> children = new Dictionary<string, WrapContext>();

		public WrapContext findContext(string name)
		{
			if(children.ContainsKey(name))
				return children[name];
			else
				return null;
		}

		public WrapType findType(string name)
		{
			string[] names = name.Split(new string[] { ".", "::" }, StringSplitOptions.None);
			return findType(names, 0);
		}
		public WrapType findType(string[] names, int index)
		{
			//Check size
			if(index > names.Count())
				return null;

			//Check for type
			string name = names[index];
			if(index == names.Count()-1)
			{
				if(types.ContainsKey(name))
					return types[name];
			}
			else
			{
				if(children.ContainsKey(name))
					return children[name].findType(names, index+1);
			}

			//Move to parent
			if(index == 0 && parent != null)
				return parent.findType(names, 0);
			else
				return null;
		}

		public List<string> buildNamespaces()
		{
			List<string> list = new List<string>();
			WrapContext context = this;
			while(context != null)
			{
				//Add
				if(context.sourceType == null && !String.IsNullOrEmpty(context.name))
					list.Insert(0, context.name);

				//Continue
				context = context.parent;
			}
			//Return
			return list;
		}
		public List<string> buildQualifiers()
		{
			List<string> list = new List<string>();
			WrapContext context = this;
			while(context != null)
			{
				//Add
				if(!String.IsNullOrEmpty(context.name))
					list.Insert(0, context.name);

				//Continue
				context = context.parent;
			}
			//Return
			return list;
		}
		public List<WrapContext> buildContextList()
		{
			List<WrapContext> list = new List<WrapContext>();
			WrapContext context = this;
			while(context != null)
			{
				//Add
				list.Insert(0, context);

				//Continue
				context = context.parent;
			}
			//Return
			return list;
		}

		//Data
		public string name;
		public WrapType sourceType;
		public Dictionary<string, WrapType> types = new Dictionary<string, WrapType>();
	}
}
