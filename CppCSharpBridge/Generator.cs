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
	public class WrapperGenerator
	{
		//Data -------------------------------------------------------------

		//Public options
		public string cppOutputPath = "./Cpp/";
		public string csharpOutputPath = "./CSharp/";

		public string cppIncreaseRef = "$result->increaseRef();";
		public string cppDecreaseRef = "$result->decreaseRef();";

		public WrapContext defaultContext = new WrapContext();
		public WrapContext currentContext;

		//public List<WrapClass> classes = new List<WrapClass>();
		//public Dictionary<string, WrapClass> classDictionary = new Dictionary<string, WrapClass>();
		//public Dictionary<string, WrapEnum> enumDictionary = new Dictionary<string, WrapEnum>();

		public List<WrapClass> classes = new List<WrapClass>();
		public List<WrapEnum> enums = new List<WrapEnum>();
		public List<WrapStruct> structs = new List<WrapStruct>();

		//Meta
		List<WrapClass> cppToCsClasses = new List<WrapClass>();
		List<WrapClass> csToCppClasses = new List<WrapClass>();

		//Methods -------------------------------------------------------------
		public WrapperGenerator()
		{
		}
		public WrapType findType(string name)
		{
			WrapType type;

			//Check if meta type
			if(name.IndexOfAny(new char[] { '*', '&' }) == 0)
			{
				//Get actual type
				string mod = name.Substring(0, 1);
				name = name.Substring(1);

				//Find in current context
				type = currentContext.findType(name);
				if(type == null)
				{
					//Return sentry object
					type = new WrapType();
					type.name = name;
					type.cppType = name;
					type.cppInPass = name;
					type.cppOutPass = name;
					type.csType = name;
					type.csInPass = name;
					type.csOutPass = name;
					type.cppName = name;
					type.csName = name;
				}
				else
					type = type.clone();

				//Update conversion
				if(mod == "*")
				{
					type.cppOutConvert = "$result = *$input;";
					type.cppInConvert = "*$result = $input;";
				}
				else if(mod == "&")
				{
					type.cppOutConvert = "$result = &$input;";
					type.cppInConvert = "$result = *$input;";
				}

				//Return
				return type;
			}

			//Find in current context
			type = currentContext.findType(name);
			if(type == null)
			{
				//Return sentry object
				type = new WrapType();
				type.name = name;
				type.cppType = name;
				type.cppInPass = name;
				type.cppOutPass = name;
				type.csType = name;
				type.csInPass = name;
				type.csOutPass = name;
				type.cppName = name;
				type.csName = name;
			}

			//Return
			return type;
		}
		public WrapClass findClass(string name)
		{
			if(String.IsNullOrEmpty(name))
				return null;
			return currentContext.findType(name) as WrapClass;
		}
		public WrapClass findClass(string name, WrapContext context)
		{
			if(String.IsNullOrEmpty(name))
				return null;
			return context.findType(name) as WrapClass;
		}
		public bool findIfParentClass(string name)
		{
			foreach(WrapClass wrapClass in classes)
			{
				if(wrapClass.parentClass == name)
					return true;
			}
			return false;
		}
		public bool findIfPolymorphic(string uniqueName)
		{
			//Do we have a parent class
			WrapClass wrapClass = null;
			foreach(WrapClass wrapClass2 in classes)
			{
				//Console.WriteLine("findIfPolymorphic - " + wrapClass2.name);
				if(wrapClass2.uniqueName == uniqueName)
				{
					wrapClass = wrapClass2;
					break;
				}
			}
			if(!String.IsNullOrEmpty(wrapClass.parentClass))
				return true;

			//Check if we are a parent class
			foreach(WrapClass wrapClass2 in classes)
			{
				if(!String.IsNullOrEmpty(wrapClass2.parentClass))
				{
					WrapClass parentClass = findClass(wrapClass2.parentClass, wrapClass2.parentContext);
					if(parentClass != null && parentClass.uniqueName == uniqueName)
						return true;
				}
			}

			//Return
			return false;
		}

		public bool classHasDeconstructor(WrapClass wrapClass)
		{
			bool isValid = true;
			WrapClass parentClass = findClass(wrapClass.parentClass);
			while(parentClass != null)
			{
				if(parentClass.constructors.Count > 0)
				{
					isValid = false;
					break;
				}
				parentClass = findClass(parentClass.parentClass);
			}
			return isValid;
		}

		public bool generate()
		{
			//Ouput
			Console.Write("CSharp Wrapper Generator\n");
			Console.Write("Cpp Output Path:" + cppOutputPath + "\n");
			Console.Write("CSharp Output Path:" + csharpOutputPath + "\n");

			//Sort classes by type
			cppToCsClasses.Clear();
			csToCppClasses.Clear();
			foreach(WrapClass wrapClass in classes)
			{
				if(wrapClass.interfaceType == WrapClass.InterfaceType.CPP_TO_CSHARP)
					cppToCsClasses.Add(wrapClass);
				else if(wrapClass.interfaceType == WrapClass.InterfaceType.CSHARP_TO_CPP)
					csToCppClasses.Add(wrapClass);
			}
			if(!sortClasses(cppToCsClasses))
				return false;
			if(!sortClasses(csToCppClasses))
				return false;

			//Write CS Files ----------------------------
			if(!writeWrapperCsFile())
				return false;

			//Class files
			foreach(WrapClass wrapClass in cppToCsClasses)
			{
				//Check if root class
				if(wrapClass.parentContext.sourceType != null && wrapClass.parentContext.sourceType.GetType() == typeof(WrapClass))
					continue;

				//Write file
				writeCppWrap_CSharpClassFile(wrapClass);
			}

			//Struct files
			foreach(WrapStruct wrapType in structs)
			{
				//Check if root class
				if(wrapType.parentContext.sourceType != null && wrapType.parentContext.sourceType.GetType() == typeof(WrapClass))
					continue;

				//Check if has data
				if(wrapType.textBlocks.Count() == 0 && wrapType.variables.Count() == 0)
					continue;

				//Write file
				writeCppWrap_CSharpStructFile(wrapType);
			}

			//Write C++ files ---------------------------
			if(!writeWrapperHeaderFile())
				return false;
			if(!writeWrapperInternalHeaderFile())
				return false;
			if(!writeWrapperSourceFile())
				return false;

			//Class files
			if(csToCppClasses.Count() > 0)
			{
				foreach(WrapClass wrapClass in csToCppClasses)
				{
					writeCsWrap_CppClassHeaderFile(wrapClass);
					writeCsWrap_CppClassSourceFile(wrapClass);
				}
			}

			//Return
			return true;
		}
		public bool sortClasses(List<WrapClass> classList)
		{
			HashSet<WrapClass> addedMap = new HashSet<WrapClass>();
			List<WrapClass> newList = new List<WrapClass>();

			while(true)
			{
				bool classAdded = false;
				int size = classList.Count;
				for(int i=0; i<size; i++)
				{
					var wrapClass = classList[i];

					//Check if parent class has been defined yet
					var parentClass = findClass(wrapClass.parentClass, wrapClass.context);
					if(parentClass != null)
					{
						if(!addedMap.Contains(parentClass))
							continue;
					}

					//Add to new list
					newList.Add(wrapClass);
					addedMap.Add(wrapClass);
					classAdded = true;

					//Remove from old list
					classList.RemoveAt(i);
					size--;
					i--;
				}

				//End
				if(classList.Count == 0)
					break;
				else
				{
					if(!classAdded)
					{
						Console.Write("Generator::sortClasses - Unable to sort classes");
						return false;
					}
				}
			}

			//Return true
			classList.AddRange(newList);
			return true;
		}

		//C++ Files
		public bool writeWrapperHeaderFile()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append("#ifndef __CPP_CSHARP_BRIDGE_H\n");
			buffer.Append("#define __CPP_CSHARP_BRIDGE_H\n");
			buffer.Append("\n");
			buffer.Append("#include \"mono/jit/jit.h\"\n");
			buffer.Append("\n");

			buffer.Append("namespace CppCSharpBridge\n");
			buffer.Append("{\n");
			buffer.Append("bool Init(MonoDomain* domain, MonoAssembly* monoAssembly);\n");
			buffer.Append("}\n");
			buffer.Append("\n");

			//End
			buffer.Append("#endif");

			//Write file
			string path = cppOutputPath + "CppCSharpBridge.h";
			System.IO.File.WriteAllText(path, buffer.ToString());

			//Return
			return true;
		}
		public bool writeWrapperInternalHeaderFile()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append("#ifndef __CSHARP_WRAPPER_INTERNAL_H\n");
			buffer.Append("#define __CSHARP_WRAPPER_INTERNAL_H\n");
			buffer.Append("\n");

			//Includes
			buffer.Append("#include \"mono/jit/jit.h\"\n");
			buffer.Append("#include <typeinfo>\n");
			buffer.Append("#include <assert.h>\n");
			buffer.Append("\n");

			//Class includes
			buffer.Append("//Type Includes\n");
			foreach(WrapClass wrapClass in cppToCsClasses)
			{
				if(!String.IsNullOrEmpty(wrapClass.cppInclude))
				{
					buffer.Append(wrapClass.cppInclude);
					buffer.Append("\n");
				}
			}
			buffer.Append("\n");

			//Generated class includes
			buffer.Append("//Generated Class Includes\n");
			foreach(WrapClass wrapClass in csToCppClasses)
			{
				buffer.Append("#include \"");
				buffer.Append(wrapClass.name);
				buffer.Append(".h\"\n");
			}
			buffer.Append("\n");

			//Begin namespace
			buffer.Append("namespace CppCSharpBridge\n");
			buffer.Append("{\n");
			buffer.Append("\n");

			//Base wrapper class
			{
				//Begin class
				buffer.Append("class InheritedClass\n");
				buffer.Append("{\n");
				buffer.Append("public:\n");

				//Constructor
				buffer.Append("InheritedClass(MonoObject* objPtr)\n");
				buffer.Append("{\n");
				buffer.Append("monoObjPtr = objPtr;\n");
				buffer.Append("gcHandle = mono_gchandle_new(monoObjPtr, false);\n");
				buffer.Append("}\n");

				//Deconstructor
				buffer.Append("virtual ~InheritedClass(void)\n");
				buffer.Append("{\n");
				buffer.Append("mono_gchandle_free(gcHandle);\n");
				buffer.Append("}\n");
				buffer.Append("\n");

				//Method
				buffer.Append("bool findImplementsMethod(MonoClass* definedClass, MonoClass* monoClass, const char* methodName, int argCount)\n");
				buffer.Append("{\n");
				buffer.Append("while(monoClass && monoClass != definedClass)\n");
				buffer.Append("{\n");
				buffer.Append("if(mono_class_get_method_from_name(monoClass, methodName, argCount)) { return true; }\n");
				buffer.Append("monoClass = mono_class_get_parent(monoClass);\n");
				buffer.Append("}\n");
				buffer.Append("return false;\n");
				buffer.Append("}\n");

				//Variables
				buffer.Append("MonoObject* monoObjPtr;\n");
				buffer.Append("int32_t gcHandle;\n");

				//End class
				buffer.Append("}; //End Wrapper Class\n");
				buffer.Append("\n");
			}

			//Extern Data
			buffer.Append("//External data\n");
			buffer.Append("extern MonoDomain* domain;\n");
			buffer.Append("extern MonoImage* image;\n");
			buffer.Append("extern MonoAssembly* assembly;\n");
			buffer.Append("\n");

			//Known mono classes
			buffer.Append("//Exposed Mono Classes\n");
			foreach(WrapClass wrapClass in cppToCsClasses)
			{
				buffer.Append("extern MonoClass* class_");
				buffer.Append(wrapClass.uniqueName);
				buffer.Append(";\n");
			}
			foreach(WrapStruct wrapStruct in structs)
			{
				buffer.Append("extern MonoClass* class_");
				buffer.Append(wrapStruct.uniqueName);
				buffer.Append(";\n");
			}
			buffer.Append("\n");

			//Object conversion out
			buffer.Append("//Object Out Conversions\n");

			//Object conversions
			foreach(WrapClass wrapClass in classes)
			{
				//Validate
				if(!wrapClass.cppCanBePassed)
					continue;

				//Write
				buffer.Append("MonoObject* ConvertObjectOut(");
				buffer.Append(wrapClass.cppType);
				buffer.Append(" obj);\n");
			}

			//Define generic method
			buffer.Append("MonoObject* ConvertObjectOut(const type_info& type, void* obj);\n");

			/*buffer.Append("template<class TYPE>\n");
			buffer.Append("MonoObject* ConvertObjectOut(const TYPE* obj)\n");
			/*buffer.Append("{\n");

			
			//Check if null
			buffer.Append("if(!obj) { return nullptr; }\n");
			//Check if wrapper
			buffer.Append("const InheritedClass* wrapper = dynamic_cast<const InheritedClass*>(obj);\n");
			buffer.Append("if(wrapper) { return wrapper->monoObjPtr; }\n");
			//Create wrapper object
			buffer.Append("MonoObject* result = ConvertObjectOut(typeid(*(TYPE*)obj), (void*)obj);\n");
			buffer.Append("if(!result) { result = ConvertObjectOut(typeid(TYPE), (void*)obj); assert(result); }\n");
			buffer.Append("return result;");
			buffer.Append("}\n");
			buffer.Append("\n");

			//Simple object
			buffer.Append("template<class TYPE>\n");
			buffer.Append("MonoObject* ConvertSimpleObjectOut(const TYPE* obj)\n");
			buffer.Append("{\n");
			//Check if null
			buffer.Append("if(!obj) { return nullptr; }\n");
			//Create wrapper object
			buffer.Append("MonoObject* result = ConvertObjectOut(typeid(*(TYPE*)obj), (void*)obj);\n");
			buffer.Append("if(!result) { result = ConvertObjectOut(typeid(TYPE), (void*)obj); assert(result); }\n");
			buffer.Append("return result;");
			buffer.Append("}\n");
			buffer.Append("\n");*/

			//End namespace
			buffer.Append("} //End Namespace\n");

			//End
			buffer.Append("#endif");

			//Write file
			string path = cppOutputPath + "CppCSharpBridge_Internal.h";
			System.IO.File.WriteAllText(path, buffer.ToString());

			//Return
			return true;
		}
		public bool writeWrapperSourceFile()
		{
			//Write all cpp wrappers into same buffer
			StringBuilder buffer = new StringBuilder();

			//Internal Includes
			buffer.Append("#include \"CppCSharpBridge_Internal.h\"\n");
			buffer.Append("\n");
			buffer.Append("#include \"mono/jit/jit.h\"\n");
			buffer.Append("#include \"mono/metadata/assembly.h\"\n");
			buffer.Append("#include \"mono/metadata/loader.h\"\n");
			buffer.Append("\n");
			buffer.Append("#include <typeindex>\n");
			buffer.Append("#include <unordered_map>\n");
			buffer.Append("\n");

			//Begin Namespace
			buffer.Append("namespace CppCSharpBridge\n");
			buffer.Append("{\n");
			buffer.Append("\n");

			//Extern Data
			buffer.Append("//Global Data\n");
			buffer.Append("MonoDomain* domain = nullptr;\n");
			buffer.Append("MonoImage* image = nullptr;\n");
			buffer.Append("MonoAssembly* assembly = nullptr;\n");
			buffer.Append("\n");

			//Known mono classes
			buffer.Append("//Mono Classes\n");
			foreach(WrapClass wrapClass in cppToCsClasses)
			{
				buffer.Append("MonoClass* class_");
				buffer.Append(wrapClass.uniqueName);
				buffer.Append(" = nullptr;\n");
			}
			foreach(WrapStruct wrapStruct in structs)
			{
				buffer.Append("MonoClass* class_");
				buffer.Append(wrapStruct.uniqueName);
				buffer.Append(" = nullptr;\n");
			}
			buffer.Append("\n");

			//Class map
			buffer.Append("//Class Map\n");
			buffer.Append("class ClassPair\n");
			buffer.Append("{\n");
			buffer.Append("public:\n");
			buffer.Append("ClassPair() { klass = nullptr; field = nullptr; }\n");
			buffer.Append("ClassPair(MonoClass* klass)\n");
			buffer.Append("{\n");
			buffer.Append("this->klass = klass;\n");
			buffer.Append("this->field = mono_class_get_field_from_name(klass, \"objCPtr\");\n");
			buffer.Append("}\n");
			buffer.Append("MonoClass* klass;\n");
			buffer.Append("MonoClassField* field;\n");
			buffer.Append("};\n");
			buffer.Append("::std::unordered_map<::std::type_index, ClassPair> classMap;\n");
			buffer.Append("\n");

			//Write all cpp wrappers into same buffer
			foreach(WrapClass wrapClass in cppToCsClasses)
			{
				writeCppWrap_CppClass(wrapClass, buffer);
			}
			buffer.Append("\n");

			//Write object conversion method
			{
				//Complex object
				buffer.Append("//Function to convert C++ objects into C# objects\n");
				buffer.Append("MonoObject* ConvertObjectOut(const type_info& type, void* obj)\n");
				buffer.Append("{\n");

				//Find class
				buffer.Append("if(!obj) return nullptr;\n");
				buffer.Append("auto iter = CppCSharpBridge::classMap.find(type);\n");

				//Check if wrapper
				buffer.Append("if(iter != CppCSharpBridge::classMap.end() && iter->second.klass)\n");
				buffer.Append("{\n");

				//Create object instance
				buffer.Append("MonoObject* monoObject = mono_object_new(CppCSharpBridge::domain, iter->second.klass);\n");
				buffer.Append("mono_field_set_value(monoObject, iter->second.field, &obj);\n");
				buffer.Append("return monoObject;\n");

				buffer.Append("}\n");
				buffer.Append("else\n");
				buffer.Append("{\n");
				buffer.Append("return nullptr;\n");
				buffer.Append("}\n");

				buffer.Append("} //End convert object out method\n");
			}

			//Init Method
			buffer.Append("bool Init(MonoDomain* domain, MonoAssembly* assembly)\n");
			buffer.Append("{\n");

			//Store defaults
			buffer.Append("//Store mono data\n");
			buffer.Append("CppCSharpBridge::domain = domain;\n");
			buffer.Append("CppCSharpBridge::assembly = assembly;\n");
			buffer.Append("CppCSharpBridge::image = mono_assembly_get_image(assembly);\n");
			buffer.Append("\n");

			//Find all classes ----------------------------
			buffer.Append("//Find all mono classes\n");
			foreach(WrapType wrapType in cppToCsClasses)
			{
				buffer.Append("class_");
				buffer.Append(wrapType.uniqueName);
				buffer.Append(" = mono_class_from_name(image, ");

				//Namespaces
				buffer.Append("\"");
				buffer.Append(wrapType.buildNamespaceString("."));

				//Class name
				buffer.Append("\", \"");
				buffer.Append(wrapType.buildClassNestedString("/"));
				buffer.Append("\");");

				//Assert
				buffer.Append(" if(!class_");
				buffer.Append(wrapType.uniqueName);
				buffer.Append(") {");
				buffer.Append(" std::cout << \"CppCSharpBridge Error: Unable to find class \'");
				buffer.Append(wrapType.uniqueName);
				buffer.Append("\'\" << std::endl;");
				buffer.Append(" return false; }\n");
			}
			foreach(WrapType wrapType in structs)
			{
				buffer.Append("class_");
				buffer.Append(wrapType.uniqueName);
				buffer.Append(" = mono_class_from_name(image, ");

				//Namespaces
				buffer.Append("\"");
				buffer.Append(wrapType.buildNamespaceString("."));

				//Class name
				buffer.Append("\", \"");
				buffer.Append(wrapType.buildClassNestedString("/"));
				buffer.Append("\");");

				//Assert
				buffer.Append(" if(!class_");
				buffer.Append(wrapType.uniqueName);
				buffer.Append(") {");
				buffer.Append(" std::cout << \"CppCSharpBridge Error: Unable to find struct \'");
				buffer.Append(wrapType.uniqueName);
				buffer.Append("\'\" << std::endl;");
				buffer.Append(" return false; }\n");
			}
			buffer.Append("\n");

			//Find all classes for class map
			buffer.Append("//Initialize class map with type_index to MonoClass* data\n");
			foreach(WrapClass wrapClass in cppToCsClasses)
			{
				buffer.Append("classMap[typeid(");
				buffer.Append(wrapClass.cppName);
				buffer.Append(")] = ClassPair(class_");
				buffer.Append(wrapClass.uniqueName);
				buffer.Append(");\n");

				//Wrap classes
				if(wrapClass.constructors.Count() > 0)
				{
					buffer.Append("classMap[typeid(");
					buffer.Append(wrapClass.uniqueName);
					buffer.Append("_CSharpWrapper");
					buffer.Append(")] = ClassPair();\n");
				}
			}
			buffer.Append("\n");

			//Call init for each class
			buffer.Append("//Init C# class bindings\n");
			foreach(WrapClass wrapClass in cppToCsClasses)
			{
				buffer.Append("Init_");
				buffer.Append(wrapClass.uniqueName);
				buffer.Append("();\n");
			}
			buffer.Append("\n");

			//Write init for all C# to C++ classes
			buffer.Append("MonoClass* monoClass = mono_class_from_name(image, \"\", \"CppCSharpBridge\");\n");
			buffer.Append("MonoMethod* monoMethod;\n");
			buffer.Append("\n");
			foreach(WrapClass wrapClass in csToCppClasses)
				writeCsWrap_CppInitClass(wrapClass, buffer);

			//Write init for all C++ virtual methods
			foreach(WrapClass wrapClass in cppToCsClasses)
			{
				//Push context
				currentContext = wrapClass.context;

				//Each method
				foreach(WrapMethod wrapMethod in wrapClass.methods)
				{
					//Virtual only
					if(!wrapMethod.isVirtual)
						continue;

					//Find method
					buffer.Append("monoMethod = mono_class_get_method_from_name(monoClass, \"");
					buffer.Append(wrapClass.uniqueName);
					buffer.Append("_");
					buffer.Append(wrapMethod.name);
					buffer.Append("\", ");
					buffer.Append(wrapMethod.args.Count() + 1);
					buffer.Append(");\n");

					//Store Thunk
					buffer.Append(wrapClass.uniqueName);
					buffer.Append("_CSharpWrapper");
					buffer.Append("::");
					buffer.Append("CSharp_");
					buffer.Append(wrapClass.name);
					buffer.Append("_");
					buffer.Append(wrapMethod.name);
					buffer.Append(" = (");

					//Typecase to func ptr
					WrapMethod tempMethod = wrapMethod.Clone();
					tempMethod.args.Insert(0, new WrapMethodArg("MonoObject*", "monoObject"));
					writeCsWrap_CppMethodPtr(wrapClass, tempMethod, "", buffer);

					//End thunk
					buffer.Append(")mono_method_get_unmanaged_thunk(monoMethod);\n");
				}
			}
			buffer.Append("\n");

			//End init method
			buffer.Append("return true;\n");
			buffer.Append("} //End init()\n");

			//End namespace
			buffer.Append("} //End Namespace\n");

			//Write file
			string path = cppOutputPath + "CppCSharpBridge.cpp";
			System.IO.File.WriteAllText(path, buffer.ToString());

			//Return
			return true;
		}

		//C# Files
		public bool writeWrapperCsFile()
		{
			StringBuilder buffer = new StringBuilder();

			//Includes
			buffer.Append("using System;\n");
			buffer.Append("using System.Runtime.CompilerServices;\n");
			buffer.Append("\n");

			//Declare class
			buffer.Append("public class CppCSharpBridge\n");
			buffer.Append("{\n");

			//Build directors
			foreach(WrapClass wrapClass in csToCppClasses)
			{
				writeCsWrap_CSharpClass(wrapClass, buffer);
			}

			//Virtual methods
			foreach(WrapClass wrapClass in cppToCsClasses)
			{
				//Push context
				currentContext = wrapClass.context;

				//Methods
				foreach(WrapMethod wrapMethod in wrapClass.methods)
				{
					if(wrapMethod.isVirtual)
						writeCsWrap_CSharpMethod(wrapClass, wrapMethod, buffer);
				}
			}

			//End class
			buffer.Append("}\n");

			//Write file
			string path = csharpOutputPath + "CppCSharpBridge.cs";
			System.IO.File.WriteAllText(path, buffer.ToString());

			//Return
			return true;
		}

		//C++ to C# Wrapper
		public void writeCppWrap_CSharpClassFile(WrapClass wrapClass)
		{
			Console.WriteLine("C# Class File:" + wrapClass.name + ".cs");

			System.Text.StringBuilder buffer = new StringBuilder();

			//Standard Namespaces
			buffer.Append("using System;\n");
			buffer.Append("using System.Runtime.CompilerServices;\n");
			buffer.Append("using System.Runtime.InteropServices;\n");
			buffer.Append("\n");

			//Namespaces
			List<string> namespaces = wrapClass.parentContext.buildNamespaces();
			foreach(string wrapNamespace in namespaces)
			{
				buffer.Append("namespace ");
				buffer.Append(wrapNamespace);
				buffer.Append("\n");
				buffer.Append("{\n");
			}

			//Write class
			writeCppWrap_CSharpClass(wrapClass, buffer);

			//Close namespaces
			foreach(string wrapNamespace in namespaces)
			{
				buffer.Append("} //End namespace - ");
				buffer.Append(wrapNamespace);
				buffer.Append("\n");
			}

			//Write to file
			string filePath = csharpOutputPath + wrapClass.uniqueName + ".cs";
			System.IO.File.WriteAllText(filePath, buffer.ToString());
		}
		public void writeCppWrap_CSharpClass(WrapClass wrapClass, StringBuilder buffer)
		{
			//Push context
			currentContext = wrapClass.context;

			//Find class type
			string classType;
			if((wrapClass.constructors.Count == 0 || wrapClass.explicitConstruction) && !findIfPolymorphic(wrapClass.uniqueName))
				classType = "struct";
			else
				classType = "class";

			//Class Declaration
			buffer.Append("public ");
			buffer.Append(classType);
			buffer.Append(" ");
			buffer.Append(wrapClass.name);

			//Parent class
			buffer.Append(":");
			if(!String.IsNullOrEmpty(wrapClass.parentClass))
			{
				buffer.Append(" ");
				buffer.Append(findClass(wrapClass.parentClass).buildQualifiedName("."));
				buffer.Append(",");
			}
			buffer.Append(" IEquatable<");
			buffer.Append(wrapClass.name);
			buffer.Append(">");

			buffer.Append("\n");
			buffer.Append("{\n");

			//Comment
			buffer.Append("//Internal Methods --------------------\n");

			//Variables
			if(String.IsNullOrEmpty(wrapClass.parentClass))
			{
				//Ptr Variable
				if(classType == "struct")
					buffer.Append("private");
				else
					buffer.Append("protected");
				buffer.Append(" IntPtr objCPtr;\n");

				//Ptr Accessor
				buffer.Append("public IntPtr getCPtr()\n");
				buffer.Append("{\n");
				buffer.Append("return objCPtr;\n");
				buffer.Append("}\n");

				//Release
				/*{
					buffer.Append("public void releaseCPtr()\n");
					buffer.Append("{\n");
					buffer.Append(wrapClass.name);
					buffer.Append("_DeconstructorDefault(objCPtr);\n");
					buffer.Append("objCPtr = IntPtr.Zero;\n");
					buffer.Append("}\n");
				}*/
			}

			//Pass Constructor
			buffer.Append("public ");
			buffer.Append(wrapClass.name);
			buffer.Append("(IntPtr cPtr)");
			if(!String.IsNullOrEmpty(wrapClass.parentClass))
				buffer.Append(" : base(IntPtr.Zero)");
			buffer.Append("\n");
			buffer.Append("{\n");
			buffer.Append("objCPtr = cPtr;\n");
			buffer.Append("}\n");
			buffer.Append("\n");

			//Constructors
			if(wrapClass.constructors.Count() > 0)
			{
				buffer.Append("//Class Constructors --------------------\n");
				writeCppWrap_CSharpClassConstructors(wrapClass, buffer);
				writeCppWrap_CSharpExplicitConstructors(wrapClass, buffer);
			}

			//Methods
			if(wrapClass.methods.Count() > 0)
			{
				buffer.Append("//Class Methods --------------------\n");
				foreach(WrapMethod method in wrapClass.methods)
				{
					writeCppWrap_CSharpMethod(wrapClass, method, buffer);
				}
			}

			//Variables
			if(wrapClass.variables.Count() > 0)
			{
				buffer.Append("//Variables --------------------\n");
				foreach(WrapVariable wrapVariable in wrapClass.variables)
				{
					writeCppWrap_CSharpVariable(wrapClass, wrapVariable, buffer);
				}
			}

			//Operators
			//if(classType == "struct")
			{
				//Equals
				string conversion = @"
public static bool operator ==($wraptype a, object b)
{
	if((object)a == null)
	{
		if(b == null)
			return true;
		else if(b is $wraptype)
			return (($wraptype)b).objCPtr == IntPtr.Zero;
		else
			return false;
	}
	else
	{
		if(b == null)
			return a.objCPtr == IntPtr.Zero;
		else if(b is $wraptype)
			return a.objCPtr == (($wraptype)b).objCPtr;
		else
			return false;
	}
}";
				conversion = conversion.Replace("$wraptype", wrapClass.csName);
				buffer.Append(conversion);
				buffer.Append("\n");

				//Doesn't Equals
				conversion = @"
public static bool operator !=($wraptype a, object b)
{
	if((object)a == null)
	{
		if(b == null)
			return false;
		else if(b is $wraptype)
			return (($wraptype)b).objCPtr != IntPtr.Zero;
		else
			return true;
	}
	else
	{
		if(b == null)
			return a.objCPtr != IntPtr.Zero;
		else if(b is $wraptype)
			return a.objCPtr != (($wraptype)b).objCPtr;
		else
			return true;
	}
}";
				conversion = conversion.Replace("$wraptype", wrapClass.csName);
				buffer.Append(conversion);
				buffer.Append("\n");

				//IEquatable<>
				buffer.Append("public bool Equals(");
				buffer.Append(wrapClass.name);
				buffer.Append(" source)\n");
				buffer.Append("{\n");
				buffer.Append("return (source == null) ? (objCPtr == null) : (objCPtr == source.objCPtr);\n");
				buffer.Append("}\n");
			}

			//Enums
			List<WrapEnum> enumList = new List<WrapEnum>();
			foreach(WrapEnum wrapEnum in enums)
			{
				if(wrapEnum.parentContext == wrapClass.context)
					enumList.Add(wrapEnum);
			}
			if(enumList.Count() > 0)
			{
				buffer.Append("//Enums --------------------\n");
				foreach(WrapEnum wrapEnum in enumList)
					writeCppWrap_CSharpEnum(wrapEnum, buffer);
			}

			//Structs
			List<WrapStruct> structList = new List<WrapStruct>();
			foreach(WrapStruct childStruct in structs)
			{
				if(childStruct.parentContext == wrapClass.context)
					structList.Add(childStruct);
			}
			if(structList.Count() > 0)
			{
				buffer.Append("//Structs --------------------\n");
				foreach(WrapStruct childStruct in structList)
					writeCppWrap_CSharpStruct(childStruct, buffer);
			}

			//Classes
			List<WrapClass> classList = new List<WrapClass>();
			foreach(WrapClass childClass in classes)
			{
				if(childClass.parentContext == wrapClass.context)
					classList.Add(childClass);
			}
			if(classList.Count() > 0)
			{
				buffer.Append("//Classes --------------------\n");
				foreach(WrapClass childClass in classList)
					writeCppWrap_CSharpClass(childClass, buffer);
			}

			//Text Blocks
			foreach(string text in wrapClass.textBlocks)
			{
				buffer.Append(text);
			}

			//Close class declaration
			buffer.Append("}\n");
		}
		public void writeCppWrap_CSharpClassConstructors(WrapClass wrapClass, StringBuilder buffer)
		{
			//Constructors
			int constructorSize = wrapClass.constructors.Count();
			if(constructorSize > 0)
			{
				//Constructors
				for(int constructorIter = 0; constructorIter < constructorSize; constructorIter++)
				{
					WrapMethod wrapMethod = wrapClass.constructors[constructorIter];

					//Constructor -------------------------------------
					if(!wrapClass.explicitConstruction)
					{
						buffer.Append("public ");
						buffer.Append(wrapClass.name);
						buffer.Append("(");

						//Arguments
						{
							int argCount = 0;
							foreach(WrapMethodArg wrapArg in wrapMethod.args)
							{
								//Comma
								if(argCount > 0)
									buffer.Append(", ");
								argCount += 1;

								//Arg
								buffer.Append(findType(wrapArg.type).csType);
								buffer.Append(" ");
								buffer.Append(wrapArg.name);
							}
						}

						buffer.Append(")");
						if(!String.IsNullOrEmpty(wrapClass.parentClass))
							buffer.Append(" : base(IntPtr.Zero)");
						buffer.Append("\n");
						buffer.Append("{\n");

						//Convert arguments
						foreach(WrapMethodArg arg in wrapMethod.args)
						{
							WrapType wrapType = findType(arg.type);
							if(String.IsNullOrEmpty(wrapType.csOutConvert))
								continue;

							string conversion = wrapType.csOutConvert;

							//Replace keywords
							string variableDecl = wrapType.csOutPass + " arg" + wrapMethod.args.IndexOf(arg);
							conversion = conversion.Replace("$result", variableDecl);
							conversion = conversion.Replace("$langtype", wrapType.csName);
							conversion = conversion.Replace("$vartype", wrapType.csType);
							conversion = conversion.Replace("$wraptype", wrapType.name);
							conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
							conversion = conversion.Replace("$input", arg.name);

							//Append
							buffer.Append(conversion);
							buffer.Append("\n");
						}

						//Default constructor
						if(!wrapClass.isAbstract())
						{
							buffer.Append("if(this.GetType() == typeof(");
							buffer.Append(wrapClass.name);
							buffer.Append("))\n");
							buffer.Append("objCPtr = ");

							buffer.Append(wrapClass.name);
							buffer.Append("_ConstructorDefault");
							buffer.Append(constructorIter);
							buffer.Append("(");
							{
								int argCount = 0;
								foreach(WrapMethodArg wrapArg in wrapMethod.args)
								{
									//Comma
									if(argCount > 0)
										buffer.Append(", ");
									argCount += 1;

									//Arg
									buffer.Append(wrapArg.name);
								}
							}
							buffer.Append(");\n");

							//Wrapper constructor
							buffer.Append("else\n");
							buffer.Append("objCPtr = ");
							buffer.Append(wrapClass.name);
							buffer.Append("_ConstructorWrapper");
							buffer.Append(constructorIter);
							buffer.Append("(this");
							{
								foreach(WrapMethodArg wrapArg in wrapMethod.args)
								{
									buffer.Append(", ");
									buffer.Append(wrapArg.name);
								}
							}
							buffer.Append(");\n");
						}
						else
						{
							//Just wrapper method
							buffer.Append("objCPtr = ");
							buffer.Append(wrapClass.name);
							buffer.Append("_ConstructorWrapper");
							buffer.Append(constructorIter);
							buffer.Append("(this");
							{
								foreach(WrapMethodArg wrapArg in wrapMethod.args)
								{
									buffer.Append(", ");
									buffer.Append(wrapArg.name);
								}
							}
							buffer.Append(");\n");
						}

						buffer.Append("}\n");
					} //End public constructor

					//Static - Constructor Default
					if(!wrapClass.isAbstract())
					{
						buffer.Append("[MethodImpl(MethodImplOptions.InternalCall)]\n");
						buffer.Append("extern static IntPtr ");
						buffer.Append(wrapClass.name);
						buffer.Append("_ConstructorDefault");
						buffer.Append(constructorIter);
						buffer.Append("(");
						{
							int argCount = 0;
							foreach(WrapMethodArg wrapArg in wrapMethod.args)
							{
								//Comma
								if(argCount > 0)
									buffer.Append(", ");
								argCount += 1;

								//Arg
								buffer.Append(findType(wrapArg.type).csOutPass);
								buffer.Append(" ");
								buffer.Append(wrapArg.name);
							}
						}
						buffer.Append(");\n");
					}

					//Static - Constructor Wrapper
					buffer.Append("[MethodImpl(MethodImplOptions.InternalCall)]\n");
					buffer.Append("extern static IntPtr ");
					buffer.Append(wrapClass.name);
					buffer.Append("_ConstructorWrapper");
					buffer.Append(constructorIter);
					buffer.Append("(Object obj");
					{
						foreach(WrapMethodArg wrapArg in wrapMethod.args)
						{
							//Comma
							buffer.Append(", ");

							//Arg
							buffer.Append(findType(wrapArg.type).csOutPass);
							buffer.Append(" ");
							buffer.Append(wrapArg.name);
						}
					}
					buffer.Append(");\n");
					buffer.Append("\n");
				}

				//Deconstructor
				if(classHasDeconstructor(wrapClass))
				{
					if(!wrapClass.explicitDeconstruction)
					{
						buffer.Append("~");
						buffer.Append(wrapClass.name);
						buffer.Append("()\n");
						buffer.Append("{\n");
						buffer.Append(wrapClass.name);
						buffer.Append("_DeconstructorDefault(objCPtr);\n");
						buffer.Append("}\n");
					}

					//Static - Deconstructor Default
					buffer.Append("[MethodImpl(MethodImplOptions.InternalCall)]\n");
					buffer.Append("extern static void ");
					buffer.Append(wrapClass.name);
					buffer.Append("_DeconstructorDefault(IntPtr objCPtr);\n");
					buffer.Append("\n");
				}
			}
		}
		public void writeCppWrap_CSharpExplicitConstructors(WrapClass wrapClass, StringBuilder buffer)
		{
			//Validate options
			if(wrapClass.explicitConstruction)
			{
				//Constructor
				int constructorIter = 0;
				foreach(WrapMethod wrapMethod in wrapClass.constructors)
				{
					buffer.Append("public static ");
					buffer.Append(wrapClass.name);
					buffer.Append(" New(");

					//Arguments
					{
						int argCount = 0;
						foreach(WrapMethodArg wrapArg in wrapMethod.args)
						{
							//Comma
							if(argCount > 0)
								buffer.Append(", ");
							argCount += 1;

							//Arg
							buffer.Append(findType(wrapArg.type).csType);
							buffer.Append(" ");
							buffer.Append(wrapArg.name);
						}
					}

					buffer.Append(")\n");
					buffer.Append("{\n");

					//Convert arguments
					foreach(WrapMethodArg arg in wrapMethod.args)
					{
						WrapType wrapType = findType(arg.type);
						if(String.IsNullOrEmpty(wrapType.csOutConvert))
							continue;

						string conversion = wrapType.csOutConvert;

						//Replace keywords
						string variableDecl = wrapType.csOutPass + " arg" + wrapMethod.args.IndexOf(arg);
						conversion = conversion.Replace("$result", variableDecl);
						conversion = conversion.Replace("$langtype", wrapType.csName);
						conversion = conversion.Replace("$vartype", wrapType.csType);
						conversion = conversion.Replace("$wraptype", wrapType.name);
						conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
						conversion = conversion.Replace("$input", arg.name);

						//Append
						buffer.Append(conversion);
						buffer.Append("\n");
					}

					//Call constructor
					buffer.Append("IntPtr objCPtr = ");
					buffer.Append(wrapClass.name);
					buffer.Append("_ConstructorDefault");
					buffer.Append(constructorIter);
					buffer.Append("(");

					//Arguments
					{
						int argCount = 0;
						foreach(WrapMethodArg arg in wrapMethod.args)
						{
							//Spacer
							if(argCount > 0)
								buffer.Append(", ");
							argCount += 1;

							//Check if struct
							WrapType argType = findType(arg.type);
							if(arg.moveOut || argType is WrapStruct)
							{
								//IN/OUT
								if(arg.moveIn)
									buffer.Append("ref ");
								else
									buffer.Append("out ");
							}

							//Arg name
							if(!String.IsNullOrEmpty(argType.csOutConvert))
							{
								//Pass converted argument
								buffer.Append("arg");
								buffer.Append(wrapMethod.args.IndexOf(arg));
							}
							else
							{
								//Pass without conversion
								buffer.Append(arg.name);
							}
						}
					}
					buffer.Append(");\n");

					buffer.Append("return new ");
					buffer.Append(wrapClass.name);
					buffer.Append("(objCPtr);\n");

					buffer.Append("}\n");

					//Increment
					constructorIter += 1;
				}
			}

			//Deconstructor
			if(wrapClass.explicitDeconstruction && classHasDeconstructor(wrapClass))
			{
				buffer.Append("public static void Delete(");
				buffer.Append(wrapClass.name);
				buffer.Append(" obj)\n");
				buffer.Append("{\n");

				buffer.Append(wrapClass.name);
				buffer.Append("_DeconstructorDefault(obj.objCPtr);\n");
				buffer.Append("obj.objCPtr = IntPtr.Zero;\n");

				buffer.Append("}\n");
			}
		}
		public void writeCppWrap_CSharpMethod(WrapClass wrapClass, WrapMethod wrapMethod, StringBuilder buffer)
		{
			//Static binding method ------------------------------

			string staticMethodName = wrapClass.name + "_" + wrapMethod.name + "_" + wrapClass.getMethodUniqueIndex(wrapMethod);

			//Declaration
			buffer.Append("[MethodImpl(MethodImplOptions.InternalCall)]\n");
			if(wrapMethod.isUnsafe)
				buffer.Append("unsafe ");
			buffer.Append("extern static void ");
			buffer.Append(staticMethodName);
			buffer.Append("(");

			//Initial argument
			int argCount = 0;
			if(!wrapMethod.isStatic)
			{
				buffer.Append("IntPtr cPtr");
				argCount += 1;
			}

			//Arguments
			foreach(WrapMethodArg arg in wrapMethod.args)
			{
				//Spacer
				if(argCount > 0)
					buffer.Append(", ");
				argCount += 1;

				//Check if struct
				WrapType argType = findType(arg.type);
				if(arg.moveOut || argType is WrapStruct)
				{
					if(arg.moveIn)
						buffer.Append("ref ");
					else
						buffer.Append("out ");
				}

				//Argument
				buffer.Append(argType.csOutPass);
				buffer.Append(" ");
				buffer.Append(arg.name);
			}

			//Return
			if(wrapMethod.returnType != "void")
			{
				if(argCount > 0)
					buffer.Append(", ");
				buffer.Append("out ");
				buffer.Append(findType(wrapMethod.returnType).csInPass);
				buffer.Append(" resultOut");
			}

			buffer.Append(");\n");

			//Wrapper method --------------------------------------
			buffer.Append("public ");
			if(wrapMethod.isVirtual)
			{
				if(wrapClass.isMethodOverride(this, wrapMethod))
					buffer.Append("override ");
				else
					buffer.Append("virtual ");
			}
			if(wrapMethod.isUnsafe)
				buffer.Append("unsafe ");
			if(wrapMethod.isStatic)
				buffer.Append("static ");
			buffer.Append(findType(wrapMethod.returnType).csType);
			buffer.Append(" ");
			buffer.Append(wrapMethod.name);
			buffer.Append("(");

			foreach(WrapMethodArg arg in wrapMethod.args)
			{
				//Spacer
				if(arg != wrapMethod.args[0])
					buffer.Append(", ");

				//In/Out
				WrapType argType = findType(arg.type);
				if(arg.moveOut)
				{
					if(arg.moveIn)
						buffer.Append("ref ");
					else
						buffer.Append("out ");
				}

				//Argument
				buffer.Append(findType(arg.type).csType);
				buffer.Append(" ");
				buffer.Append(arg.name);

				//Default
				if(!String.IsNullOrEmpty(arg.defaultValue))
				{
					buffer.Append(" = ");
					buffer.Append(arg.defaultValue);
				}
			}

			//Open method
			buffer.Append(")\n");
			buffer.Append("{\n");

			//Convert arguments [C#] to [C# Pass Out]
			foreach(WrapMethodArg arg in wrapMethod.args)
			{
				WrapType wrapType = findType(arg.type);
				if(String.IsNullOrEmpty(wrapType.csOutConvert))
					continue;

				string conversion = wrapType.csOutConvert;

				//Replace keywords
				string variableDecl = wrapType.csOutPass + " arg" + wrapMethod.args.IndexOf(arg);
				conversion = conversion.Replace("$result", variableDecl);
				conversion = conversion.Replace("$langtype", wrapType.csName);
				conversion = conversion.Replace("$vartype", wrapType.csType);
				conversion = conversion.Replace("$wraptype", wrapType.name);
				conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
				conversion = conversion.Replace("$input", arg.name);

				//Append
				buffer.Append(conversion);
				buffer.Append("\n");
			}

			//Create static method call
			string wrapMethodCall = "";
			{
				StringBuilder tempBuffer = new StringBuilder();

				//Static wrap call
				argCount = 0;
				tempBuffer.Append(staticMethodName);
				tempBuffer.Append("(");
				if(!wrapMethod.isStatic)
				{
					tempBuffer.Append("objCPtr");
					argCount += 1;
				}

				//Arguments
				foreach(WrapMethodArg arg in wrapMethod.args)
				{
					//Spacer
					if(argCount > 0)
						tempBuffer.Append(", ");
					argCount += 1;

					//Check if struct
					WrapType argType = findType(arg.type);
					if(arg.moveOut || argType is WrapStruct)
					{
						//IN/OUT
						if(arg.moveIn)
							tempBuffer.Append("ref ");
						else
							tempBuffer.Append("out ");
					}

					//Arg name
					if(!String.IsNullOrEmpty(argType.csOutConvert))
					{
						//Pass converted argument
						tempBuffer.Append("arg");
						tempBuffer.Append(wrapMethod.args.IndexOf(arg));
					}
					else
					{
						//Pass without conversion
						tempBuffer.Append(arg.name);
					}
				}

				//Return
				if(wrapMethod.returnType != "void")
				{
					if(argCount > 0)
						tempBuffer.Append(", ");
					tempBuffer.Append("out result");
				}

				//Close call
				tempBuffer.Append(");\n");

				//Store
				wrapMethodCall = tempBuffer.ToString();
			}

			//Return
			if(wrapMethod.returnType != "void")
			{
				//Type Conversion
				WrapType wrapType = findType(wrapMethod.returnType);
				if(!String.IsNullOrEmpty(wrapType.csInConvert))
				{
					string conversion = wrapType.csInConvert;

					//Declare return
					buffer.Append(wrapType.csInPass);
					buffer.Append(" result;\n");

					//Wrap method call
					buffer.Append(wrapMethodCall);

					//Declare variable
					buffer.Append(wrapType.csType);
					buffer.Append(" resultConvert;\n");

					//Return
					conversion = conversion.Replace("$result", "resultConvert");
					conversion = conversion.Replace("$langtype", wrapType.csName);
					conversion = conversion.Replace("$vartype", wrapType.csType);
					conversion = conversion.Replace("$wraptype", wrapType.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", "result");
					buffer.Append(conversion);
					buffer.Append("\n");
					buffer.Append("return resultConvert;\n");
				}
				else
				{
					//Declare return
					buffer.Append(wrapType.csInPass);
					buffer.Append(" result;\n");

					//Wrap method call
					buffer.Append(wrapMethodCall);

					//Return
					buffer.Append("return result;\n");
				}
			}
			else //Normal, call method
			{
				//Wrap method call
				buffer.Append(wrapMethodCall);
			}

			//Out Variables
			foreach(WrapMethodArg arg in wrapMethod.args)
			{
				//Only out/ref variables
				if(!arg.moveOut)
					continue;

				//Check for conversion
				WrapType wrapType = findType(arg.type);
				if(String.IsNullOrEmpty(wrapType.csInConvert))
					continue;

				//Input name
				string inputName;
				if(String.IsNullOrEmpty(wrapType.csOutConvert))
					inputName = arg.name;
				else
					inputName = "arg" + wrapMethod.args.IndexOf(arg);

				//Replace keywords
				string conversion = wrapType.csInConvert;
				conversion = conversion.Replace("$result", arg.name);
				conversion = conversion.Replace("$langtype", wrapType.csName);
				conversion = conversion.Replace("$vartype", wrapType.csType);
				conversion = conversion.Replace("$wraptype", wrapType.name);
				conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
				conversion = conversion.Replace("$input", inputName);

				//Append
				buffer.Append(conversion);
				buffer.Append("\n");
			}

			//Close method
			buffer.Append("}\n");
		}
		public void writeCppWrap_CSharpVariable(WrapClass wrapClass, WrapVariable wrapVariable, StringBuilder buffer)
		{
			WrapType variableType = findType(wrapVariable.type);

			//Begin definition
			buffer.Append("public ");
			buffer.Append(variableType.csType);
			buffer.Append(" ");
			buffer.Append(wrapVariable.name);
			buffer.Append("\n");
			buffer.Append("{\n");

			string staticMethodName_Get = wrapClass.name + "_" + wrapVariable.name + "_Get";
			string staticMethodName_Set = wrapClass.name + "_" + wrapVariable.name + "_Set";

			//Begin get ------------------------------------------------
			if(!String.IsNullOrEmpty(wrapVariable.getBody))
			{
				buffer.Append("get\n");
				buffer.Append("{\n");

				//Method call
				if(!String.IsNullOrEmpty(variableType.csInConvert))
				{
					//Convert argument
					string conversion = variableType.csInConvert;

					//Get result
					buffer.Append(variableType.csInPass);
					buffer.Append(" resultIn;\n");
					buffer.Append(staticMethodName_Get);
					buffer.Append("(objCPtr, out resultIn);\n");

					//Replace keywords
					conversion = conversion.Replace("$result", "resultOut");
					conversion = conversion.Replace("$langtype", variableType.csName);
					conversion = conversion.Replace("$vartype", variableType.csType);
					conversion = conversion.Replace("$wraptype", variableType.name);
					conversion = conversion.Replace("$uniquename", variableType.uniqueName);
					conversion = conversion.Replace("$input", "resultIn");

					//Append
					buffer.Append(variableType.csType);
					buffer.Append(" resultOut;\n");
					buffer.Append(conversion);
					buffer.Append("\n");
					buffer.Append("return resultOut;\n");
				}
				else
				{
					//Call method
					buffer.Append(variableType.csType);
					buffer.Append(" result;\n");
					buffer.Append(staticMethodName_Get);
					buffer.Append("(objCPtr, out result);\n");
					buffer.Append("return result;\n");
				}

				//End get
				buffer.Append("}\n");
			}

			//Begin set ------------------------------------------------
			if(!String.IsNullOrEmpty(wrapVariable.setBody))
			{
				buffer.Append("set\n");
				buffer.Append("{\n");

				//Method class
				if(!String.IsNullOrEmpty(variableType.csOutConvert))
				{
					//Convert argument
					string conversion = variableType.csOutConvert;

					//Replace keywords
					conversion = conversion.Replace("$result", "result");
					conversion = conversion.Replace("$langtype", wrapClass.csName);
					conversion = conversion.Replace("$vartype", wrapClass.csType);
					conversion = conversion.Replace("$wraptype", wrapClass.name);
					conversion = conversion.Replace("$uniquename", wrapClass.uniqueName);
					conversion = conversion.Replace("$input", "value");

					//Append
					buffer.Append(variableType.csOutPass);
					buffer.Append(" result;\n");
					buffer.Append(conversion);
					buffer.Append("\n");
					buffer.Append(staticMethodName_Set);
					buffer.Append("(objCPtr, result);\n");
				}
				else
				{
					//Call method
					buffer.Append(staticMethodName_Set);
					buffer.Append("(objCPtr, ");

					//Check if struct
					if(variableType is WrapStruct)
						buffer.Append("ref ");

					//Final
					buffer.Append("value);\n");
				}

				//End set
				buffer.Append("}\n");
			}

			//End var definition
			buffer.Append("}\n");

			//Get - Static binding method ------------------------------
			if(!String.IsNullOrEmpty(wrapVariable.getBody))
			{
				//Declaration
				buffer.Append("[MethodImpl(MethodImplOptions.InternalCall)]\n");
				buffer.Append("extern static void ");
				buffer.Append(staticMethodName_Get);
				buffer.Append("(IntPtr cPtr, out ");
				buffer.Append(variableType.csInPass);
				buffer.Append(" resultOut);\n");
			}

			//Set - Static binding method ------------------------------
			if(!String.IsNullOrEmpty(wrapVariable.setBody))
			{
				//Declaration
				buffer.Append("[MethodImpl(MethodImplOptions.InternalCall)]\n");
				buffer.Append("extern static void ");
				buffer.Append(staticMethodName_Set);
				buffer.Append("(IntPtr cPtr, ");

				//Check if struct
				if(variableType is WrapStruct)
					buffer.Append("ref ");

				buffer.Append(variableType.csOutPass);
				buffer.Append(" value);\n");
			}
		}
		public void writeCppWrap_CSharpEnum(WrapEnum wrapEnum, StringBuilder buffer)
		{
			//Begin enum
			buffer.Append("public enum ");
			buffer.Append(wrapEnum.name);
			if(!String.IsNullOrEmpty(wrapEnum.enumType))
			{
				WrapType type = findType(wrapEnum.enumType);
				buffer.Append(" : ");
				buffer.Append(type.csType);
			}
			buffer.Append("\n");
			buffer.Append("{\n");

			//Variables
			foreach(WrapEnumVar wrapVar in wrapEnum.vars)
			{
				//Name
				buffer.Append(wrapVar.name);

				//Value
				if(!String.IsNullOrEmpty(wrapVar.value))
				{
					buffer.Append(" = ");
					buffer.Append(wrapVar.value);
				}

				//End
				if(wrapVar != wrapEnum.vars[wrapEnum.vars.Count() - 1])
					buffer.Append(",\n");
				else
					buffer.Append("\n");
			}

			//End enum
			buffer.Append("}\n");
		}
		public void writeCppWrap_CSharpStructFile(WrapStruct wrapStruct)
		{
			Console.WriteLine("C# Struct File:" + wrapStruct.name + ".cs");

			System.Text.StringBuilder buffer = new StringBuilder();

			//Standard Namespaces
			buffer.Append("using System;\n");
			buffer.Append("using System.Runtime.InteropServices;\n");
			buffer.Append("\n");

			//Namespaces
			List<string> namespaces = wrapStruct.parentContext.buildNamespaces();
			foreach(string wrapNamespace in namespaces)
			{
				buffer.Append("namespace ");
				buffer.Append(wrapNamespace);
				buffer.Append("\n");
				buffer.Append("{\n");
			}

			//Write class
			writeCppWrap_CSharpStruct(wrapStruct, buffer);

			//Close namespaces
			foreach(string wrapNamespace in namespaces)
			{
				buffer.Append("} //End namespace - ");
				buffer.Append(wrapNamespace);
				buffer.Append("\n");
			}

			//Write to file
			string path = csharpOutputPath + wrapStruct.uniqueName + ".cs";
			System.IO.File.WriteAllText(path, buffer.ToString());
		}
		public void writeCppWrap_CSharpStruct(WrapStruct wrapStruct, StringBuilder buffer)
		{
			//Begin struct
			buffer.Append("[StructLayout(LayoutKind.Sequential)]\n");
			buffer.Append("public struct ");
			buffer.Append(wrapStruct.name);
			buffer.Append("\n");
			buffer.Append("{\n");

			//Variables
			if(wrapStruct.variables.Count() > 0)
			{
				buffer.Append("//Variables --------------------\n");
				foreach(WrapVariable wrapVariable in wrapStruct.variables)
				{
					WrapType varType = findType(wrapVariable.type);

					buffer.Append("public ");
					buffer.Append(varType.csType);
					buffer.Append(" ");
					buffer.Append(wrapVariable.name);
					buffer.Append(";\n");
				}
			}

			//Enums
			List<WrapEnum> enumList = new List<WrapEnum>();
			foreach(WrapEnum wrapEnum in enums)
			{
				if(wrapEnum.parentContext == wrapStruct.context)
					enumList.Add(wrapEnum);
			}
			if(enumList.Count() > 0)
			{
				buffer.Append("//Enums --------------------\n");
				foreach(WrapEnum wrapEnum in enumList)
					writeCppWrap_CSharpEnum(wrapEnum, buffer);
			}

			//Text blocks
			foreach(string text in wrapStruct.textBlocks)
			{
				buffer.Append(text);
				buffer.Append("\n");
			}

			//End struct
			buffer.Append("}\n");
		}

		public void writeCppWrap_CppClass(WrapClass wrapClass, StringBuilder buffer)
		{
			//Push context
			currentContext = wrapClass.context;

			//Writer C++ Wrapper class --------------------------------------------------
			if(wrapClass.constructors.Count() > 0)
				writeCppWrap_CppWrapperClass(wrapClass, buffer);

			//Wrapper Constructors --------------------------------------------------
			buffer.Append("//Constructors -----------------------------\n");
			writeCppWrap_CppConstructors(wrapClass, buffer);

			//Wrapper methods -----------------------------------------------
			buffer.Append("//Methods -----------------------------\n");
			foreach(WrapMethod wrapMethod in wrapClass.methods)
			{
				writeCppWrap_CppMethod(wrapClass, wrapMethod, buffer);
			}

			//Wrapper variables -----------------------------------------------
			buffer.Append("//Variables -----------------------------\n");
			foreach(WrapVariable wrapVariable in wrapClass.variables)
			{
				writeCppWrap_CppVariable(wrapClass, wrapVariable, buffer);
			}

			//Initialization method -----------------------------------------------
			buffer.Append("void Init_");
			buffer.Append(wrapClass.uniqueName);
			buffer.Append("()\n");
			buffer.Append("{\n");

			//Build qualifier
			string classQualifier = "";
			{
				List<WrapContext> contextList = wrapClass.context.buildContextList();
				foreach(WrapContext context in contextList)
				{
					if(String.IsNullOrEmpty(context.name))
						continue;
					if(context.sourceType == null)
						classQualifier += context.name + ".";
					else
						classQualifier += context.name + "/";
				}
				classQualifier = classQualifier.Remove(classQualifier.Length - 1); //Remove the last
			}

			//Constructors
			if(wrapClass.constructors.Count > 0)
			{
				//Constructors
				int constructorCount = 0;
				foreach(WrapMethod wrapMethod in wrapClass.constructors)
				{
					//Constructor Default ----------------------------
					if(!wrapClass.isAbstract())
					{
						buffer.Append("mono_add_internal_call(\"");

						//Arg 1
						buffer.Append(classQualifier);
						buffer.Append("::");
						buffer.Append(wrapClass.name);
						buffer.Append("_ConstructorDefault");
						buffer.Append(constructorCount);
						buffer.Append("\", ");

						//Arg 2
						buffer.Append(wrapClass.uniqueName);
						buffer.Append("_ConstructorDefault");
						buffer.Append(constructorCount);
						buffer.Append(");\n");
					}

					//Constructor Wrapper ----------------------------
					buffer.Append("mono_add_internal_call(\"");

					//Arg 1
					buffer.Append(classQualifier);
					buffer.Append("::");
					buffer.Append(wrapClass.name);
					buffer.Append("_ConstructorWrapper");
					buffer.Append(constructorCount);
					buffer.Append("\", ");

					//Arg 2
					buffer.Append(wrapClass.uniqueName);
					buffer.Append("_ConstructorWrapper");
					buffer.Append(constructorCount);
					buffer.Append(");\n");

					//Iterate
					constructorCount += 1;
				}

				//Deconstructor
				if(classHasDeconstructor(wrapClass))
				{
					//Constructor Default ----------------------------
					buffer.Append("mono_add_internal_call(\"");

					//Arg 1
					buffer.Append(classQualifier);
					buffer.Append("::");
					buffer.Append(wrapClass.name);
					buffer.Append("_DeconstructorDefault\", ");

					//Arg 2
					buffer.Append(wrapClass.uniqueName);
					buffer.Append("_DeconstructorDefault);\n");
				}

				//Aquire wrap object
				/*buffer.Append("mono_add_internal_call(\"");

				//Namespaces					
				foreach(string name in namespaces)
				{
					buffer.Append(name);
					buffer.Append(".");
				}

				buffer.Append(wrapClass.name);
				buffer.Append("::");
				buffer.Append(wrapClass.name);
				buffer.Append("_AquireWrapObject\", ");
				buffer.Append(wrapClass.name);
				buffer.Append("_AquireWrapObject);\n");*/
			}

			//Methods
			foreach(WrapMethod wrapMethod in wrapClass.methods)
			{
				buffer.Append("mono_add_internal_call(\"");

				//CSharp Method Name -------------------------

				//Arg 1
				buffer.Append(classQualifier);
				buffer.Append("::");
				buffer.Append(wrapClass.name);
				buffer.Append("_");
				buffer.Append(wrapMethod.name);
				buffer.Append("_");
				buffer.Append(wrapClass.getMethodUniqueIndex(wrapMethod));

				//End
				buffer.Append("\", ");

				//C++ Method Name -------------------------
				buffer.Append("static_cast<void(*)(");

				//Args
				int argCount = 0;
				if(!wrapMethod.isStatic)
				{
					buffer.Append("void*");
					argCount += 1;
				}
				foreach(WrapMethodArg wrapArg in wrapMethod.args)
				{
					//Spacer
					if(argCount > 0)
						buffer.Append(", ");
					argCount += 1;

					//Arg
					WrapType argType = findType(wrapArg.type);
					buffer.Append(argType.cppInPass);
					if(wrapArg.moveOut || argType is WrapStruct)
						buffer.Append("*");
				}

				//Return
				if(wrapMethod.returnType != "void")
				{
					if(argCount > 0)
						buffer.Append(", ");
					buffer.Append(findType(wrapMethod.returnType).cppOutPass);
					buffer.Append("*");
				}

				buffer.Append(")>(&");

				//Method
				buffer.Append(wrapClass.uniqueName);
				buffer.Append("_");
				buffer.Append(wrapMethod.name);

				//End
				buffer.Append("));\n");
			}

			//Variables
			foreach(WrapVariable wrapVariable in wrapClass.variables)
			{
				//Get -------------------------------------
				if(!String.IsNullOrEmpty(wrapVariable.getBody))
				{
					buffer.Append("mono_add_internal_call(\"");

					//Arg 1
					buffer.Append(classQualifier);
					buffer.Append("::");
					buffer.Append(wrapClass.name);
					buffer.Append("_");
					buffer.Append(wrapVariable.name);
					buffer.Append("_Get\", ");

					//Arg 2
					buffer.Append(wrapClass.uniqueName);
					buffer.Append("_");
					buffer.Append(wrapVariable.name);
					buffer.Append("_Get);\n");
				}

				//Set -------------------------------------
				if(!String.IsNullOrEmpty(wrapVariable.setBody))
				{
					buffer.Append("mono_add_internal_call(\"");

					//Arg 1
					buffer.Append(classQualifier);
					buffer.Append("::");
					buffer.Append(wrapClass.name);
					buffer.Append("_");
					buffer.Append(wrapVariable.name);
					buffer.Append("_Set\", ");

					//Arg 2
					buffer.Append(wrapClass.uniqueName);
					buffer.Append("_");
					buffer.Append(wrapVariable.name);
					buffer.Append("_Set);\n");
				}
			}

			buffer.Append("}\n");
		}
		public void writeCppWrap_CppConstructors(WrapClass wrapClass, StringBuilder buffer)
		{
			WrapType wrapType = findType(wrapClass.name);
			int constructorSize = wrapClass.constructors.Count();
			if(constructorSize > 0)
			{
				//Constructors
				for(int constructorIter = 0; constructorIter < constructorSize; constructorIter++)
				{
					WrapMethod wrapMethod = wrapClass.constructors[constructorIter];

					//Constructor Default
					if(!wrapClass.isAbstract())
					{
						//Method begin
						buffer.Append("static void* ");
						buffer.Append(wrapClass.uniqueName);
						buffer.Append("_ConstructorDefault");
						buffer.Append(constructorIter);
						buffer.Append("(");
						{
							int argumentCount = 0;
							foreach(WrapMethodArg wrapArg in wrapMethod.args)
							{
								//Comma
								if(argumentCount > 0)
									buffer.Append(", ");

								//Argument
								buffer.Append(findType(wrapArg.type).cppInPass);
								buffer.Append(" ");
								buffer.Append(wrapArg.name);

								//Increment
								argumentCount += 1;
							}
						}
						buffer.Append(")\n");
						buffer.Append("{\n");

						//Argument conversion in
						writeCppWrap_MethodArgConvertIn(wrapMethod, buffer);

						//Arg Line
						string argsLine;
						{
							StringBuilder argBuffer = new StringBuilder();

							int argumentCount = 0;
							foreach(WrapMethodArg wrapArg in wrapMethod.args)
							{
								//Comma
								if(argumentCount > 0)
									argBuffer.Append(", ");

								//Argument
								if(!String.IsNullOrEmpty(findType(wrapArg.type).cppInConvert))
								{
									argBuffer.Append("arg");
									argBuffer.Append(argumentCount);
								}
								else
									argBuffer.Append(wrapArg.name);

								//Increment
								argumentCount += 1;
							}
							argsLine = argBuffer.ToString();
						}

						//Construct Instance
						{
							//Build string
							string conversion = wrapClass.cppConstruct;
							conversion = conversion.Replace("$result", "obj");
							conversion = conversion.Replace("$langtype", wrapClass.cppName);
							conversion = conversion.Replace("$vartype", wrapClass.cppType);
							conversion = conversion.Replace("$wraptype", wrapClass.name);
							conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
							conversion = conversion.Replace("$args", argsLine);

							//Append
							buffer.Append(wrapType.cppType);
							buffer.Append(" obj;\n");
							buffer.Append(conversion);
						}

						//Method end
						buffer.Append("return obj;\n");
						buffer.Append("}\n\n");
					}

					//Constructor Wrapper
					{
						//Method begin
						buffer.Append("static void* ");
						buffer.Append(wrapClass.uniqueName);
						buffer.Append("_ConstructorWrapper");
						buffer.Append(constructorIter);
						buffer.Append("(MonoObject* monoObjPtr");
						{
							int argumentCount = 0;
							foreach(WrapMethodArg wrapArg in wrapMethod.args)
							{
								//Comma
								buffer.Append(", ");

								//Argument
								buffer.Append(findType(wrapArg.type).cppInPass);
								buffer.Append(" arg");
								buffer.Append(argumentCount);

								//Increment
								argumentCount += 1;
							}
						}
						buffer.Append(")\n");
						buffer.Append("{\n");

						//Argument conversion in
						writeCppWrap_MethodArgConvertIn(wrapMethod, buffer);

						//Arg line
						string argsLine;
						{
							StringBuilder argBuffer = new StringBuilder();
							argBuffer.Append("monoObjPtr");

							int argumentCount = 0;
							foreach(WrapMethodArg wrapArg in wrapMethod.args)
							{
								//Comma
								argBuffer.Append(", ");

								//Argument
								argBuffer.Append("arg");
								argBuffer.Append(argumentCount);

								//Increment
								argumentCount += 1;
							}
							argsLine = argBuffer.ToString();
						}

						//Replace keywords
						string conversion = wrapClass.cppConstruct;
						conversion = conversion.Replace("$result", "obj");
						conversion = conversion.Replace("$langtype", wrapClass.uniqueName + "_CSharpWrapper");
						conversion = conversion.Replace("$vartype", wrapClass.cppType);
						conversion = conversion.Replace("$wraptype", wrapClass.name);
						conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
						conversion = conversion.Replace("$args", argsLine);

						//Create
						buffer.Append(wrapType.cppType);
						buffer.Append(" obj;\n");
						buffer.Append(conversion);
						buffer.Append("return obj;\n");

						//Method end
						buffer.Append("}\n\n");
					}
				} //End constructor wrappers

				//Deconstructor
				if(classHasDeconstructor(wrapClass))
				{
					string conversion = wrapClass.cppDeconstruct;

					//Replace keywords
					conversion = conversion.Replace("$result", "obj");
					conversion = conversion.Replace("$langtype", wrapClass.cppName);
					conversion = conversion.Replace("$vartype", wrapClass.cppType);
					conversion = conversion.Replace("$wraptype", wrapClass.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", "objCPtr");

					//Method begin
					buffer.Append("static void ");
					buffer.Append(wrapClass.uniqueName);
					buffer.Append("_DeconstructorDefault(void* objCPtr)\n");
					buffer.Append("{\n");

					//Create
					buffer.Append(wrapType.cppType);
					buffer.Append(" obj;\n");
					buffer.Append(conversion);
					buffer.Append("\n");

					//Method end
					buffer.Append("}\n\n");
				}
			} //End constructors > 0

			//Construct MonoObject Out
			if(wrapClass.cppCanBePassed)
			{
				//Method declaration
				buffer.Append("MonoObject* ConvertObjectOut(");
				buffer.Append(wrapType.cppType);
				buffer.Append(" obj)\n");
				buffer.Append("{\n");

				//Check if null
				buffer.Append("if(!obj) return nullptr;\n");

				//Polymorphic
				if(findIfPolymorphic(wrapClass.uniqueName))
				{
					buffer.Append("const InheritedClass* wrapper = dynamic_cast<const InheritedClass*>(obj);\n");
					buffer.Append("if(wrapper) { return wrapper->monoObjPtr; }\n");
				}

				//User Code
				string conversion = wrapClass.cppWrapperConstruct;
				conversion = conversion.Replace("$langtype", wrapClass.cppName);
				conversion = conversion.Replace("$vartype", wrapClass.cppType);
				conversion = conversion.Replace("$wraptype", wrapClass.name);
				conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
				conversion = conversion.Replace("$result", "obj");

				buffer.Append(conversion);
				buffer.Append("\n");

				//Convert
				buffer.Append("MonoObject* result = ConvertObjectOut(typeid(*obj), (void*)obj);\n");
				buffer.Append("if(!result) { result = ConvertObjectOut(typeid(");
				buffer.Append(wrapClass.cppName);
				buffer.Append("), (void*)obj); assert(result); }\n");
				buffer.Append("return result;\n");

				//End
				buffer.Append("}\n");
			}
		}
		public void writeCppWrap_CppWrapperClass(WrapClass wrapClass, StringBuilder buffer)
		{
			int constructorSize = wrapClass.constructors.Count();
			bool hasVirtualMethods = wrapClass.hasVirtualMethods(this);

			//Wrapper Interface ----------------------------------------------------
			/*buffer.Append("class ");
			buffer.Append(wrapClass.name);
			buffer.Append("_CSharpInterface\n");
			buffer.Append("{\n");
			buffer.Append("public:\n");

			//Base methods
			if(hasVirtualMethods)
			{
				//Each class
				WrapClass tempWrapClass = wrapClass;
				while(tempWrapClass != null)
				{
					//Each method
					foreach(WrapMethod wrapMethod in tempWrapClass.methods)
					{
						//Virtual only
						if(!wrapMethod.isVirtual || wrapMethod.isAbstract)
							continue;

						buffer.Append("virtual ");
						buffer.Append(findType(wrapMethod.returnType).cppType);
						buffer.Append(" ");
						buffer.Append(wrapMethod.name);
						buffer.Append("_BaseCall(");
						buffer.Append(wrapMethod.getCppArgDef(this));
						buffer.Append(") = 0;\n");
					}

					//Continue
					if(String.IsNullOrEmpty(tempWrapClass.parentClass))
						break;
					tempWrapClass = findClass(tempWrapClass.parentClass);
				}
			}

			//End class
			buffer.Append("};\n");*/

			//Wrapper Class --------------------------------------------------------
			buffer.Append("class ");
			buffer.Append(wrapClass.uniqueName);
			buffer.Append("_CSharpWrapper: public InheritedClass, public ");
			buffer.Append(wrapClass.cppName);
			buffer.Append("\n");
			buffer.Append("{\n");
			buffer.Append("public:\n");

			//Constructors
			for(int constructorIter = 0; constructorIter < constructorSize; constructorIter++)
			{
				WrapMethod wrapMethod = wrapClass.constructors[constructorIter];

				buffer.Append(wrapClass.uniqueName);
				buffer.Append("_CSharpWrapper(MonoObject * objPtr");
				{
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Comma
						buffer.Append(", ");

						//Argument
						buffer.Append(findType(wrapArg.type).cppType);
						buffer.Append(" ");
						buffer.Append(wrapArg.name);
					}
				}
				buffer.Append(")");

				//Call base wrapper constructor
				buffer.Append(": InheritedClass(objPtr), ");

				//Call base class constructor
				buffer.Append(wrapClass.cppName);
				buffer.Append("(");
				{
					int argCount = 0;
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Comma
						if(argCount > 0)
							buffer.Append(", ");

						//Argument
						buffer.Append(wrapArg.name);
                        argCount++;
					}
				}
				buffer.Append(")");

				//Body
				if(hasVirtualMethods)
					buffer.Append(" { initVirtualMethods(); }\n");
				else
					buffer.Append(" {}\n");
			}

			//Init virtual methods (Entire class tree)
			if(hasVirtualMethods)
			{
				//Begin method
				buffer.Append("void initVirtualMethods(void)\n");
				buffer.Append("{\n");

				//Setup
				buffer.Append("MonoClass* actualClass = mono_object_get_class(monoObjPtr);\n");
				buffer.Append("\n");

				//Methods
				WrapClass tempWrapClass = wrapClass;
				while(tempWrapClass != null)
				{
					//Comment
					buffer.Append("//Class: ");
					buffer.Append(tempWrapClass.name);
					buffer.Append("\n");

					//Virtual methods
					foreach(WrapMethod wrapMethod in tempWrapClass.methods)
					{
						//Virtual only
						if(!wrapMethod.isVirtual)
							continue;

						//Ask for method
						buffer.Append(wrapMethod.name);
						buffer.Append("_HasVirtualMethod = findImplementsMethod(class_" + tempWrapClass.uniqueName + ", actualClass, \"");
						buffer.Append(wrapMethod.name);
						buffer.Append("\", ");
						buffer.Append(wrapMethod.args.Count);
						buffer.Append(");\n");
					}
					buffer.Append("\n");

					//Continue to parent class
					if(String.IsNullOrEmpty(tempWrapClass.parentClass))
						break;
					tempWrapClass = findClass(tempWrapClass.parentClass);
				}

				//Abstract methods
				tempWrapClass = wrapClass;
				while(tempWrapClass != null)
				{
					//Abstract methods
					foreach(WrapMethod wrapMethod in tempWrapClass.methods)
					{
						//Virtual only
						if(!wrapMethod.isAbstract)
							continue;

						//Ask for method
						buffer.Append("assert(");
						buffer.Append(wrapMethod.name);
						buffer.Append("_HasVirtualMethod);\n");
					}

					//Continue to parent class
					if(String.IsNullOrEmpty(tempWrapClass.parentClass))
						break;
					tempWrapClass = findClass(tempWrapClass.parentClass);
				}

				//End method
				buffer.Append("}\n");
			}

			//Virtual methods
			{
				WrapClass tempWrapClass = wrapClass;

				//Virtual method functions
				tempWrapClass = wrapClass;
				while(tempWrapClass != null)
				{
					//Methods
					foreach(WrapMethod wrapMethod in tempWrapClass.methods)
					{
						//Is virtual
						if(!wrapMethod.isVirtual)
							continue;

						//Check if already defined
						if(!wrapClass.isFinalMethod(this, wrapMethod))
							continue;

						//Set context
						this.currentContext = tempWrapClass.context;

						//------------------------------------------------------
						//Begin method definition
						buffer.Append(findType(wrapMethod.returnType).cppType);
						buffer.Append(" ");
						buffer.Append(wrapMethod.name);
						buffer.Append("(");
						buffer.Append(wrapMethod.getCppArgDef(this));
						buffer.Append(")");
						if(wrapMethod.isConst)
							buffer.Append(" const");
						buffer.Append("\n");
						buffer.Append("{\n");

						//Call virtual method
						buffer.Append("if(");
						buffer.Append(wrapMethod.name);
						buffer.Append("_HasVirtualMethod)\n");
						buffer.Append("{\n");
						writeCsWrap_CppMethodBody(tempWrapClass, wrapMethod, tempWrapClass.uniqueName + "_CSharpWrapper::CSharp_" + tempWrapClass.name + "_", buffer);
						buffer.Append("}\n");

						//Call normal method
						if(!wrapMethod.isAbstract)
						{
							buffer.Append("else\n");
							buffer.Append("{\n");

							//Call normal method
							if(wrapMethod.returnType != "void")
								buffer.Append("return ");
							buffer.Append(wrapClass.cppName);
							buffer.Append("::");
							buffer.Append(wrapMethod.name);
							buffer.Append("(");
							for(int i = 0; i < wrapMethod.args.Count; i++)
							{
								if(i > 0)
									buffer.Append(", ");
								buffer.Append(wrapMethod.args[i].name);
							}
							buffer.Append(");\n");

							buffer.Append("}\n");
						}
						else
						{
							buffer.Append("else\n");
							buffer.Append("{\n");
							buffer.Append("assert(false); //Abstract method not defined in C#\n");
							buffer.Append("}\n");
						}

						//End method
						buffer.Append("}\n");

						//------------------------------------------------------
						//Base method function
						/*if(!wrapMethod.isAbstract)
						{
							buffer.Append(findType(wrapMethod.returnType).cppType);
							buffer.Append(" ");
							buffer.Append(wrapMethod.name);
							buffer.Append("_BaseCall");
							buffer.Append("(");
							buffer.Append(wrapMethod.getCppArgDef(this));
							buffer.Append(")");
							if(wrapMethod.isConst)
								buffer.Append(" const");
							buffer.Append("\n");
							buffer.Append("{\n");

							if(wrapMethod.returnType != "void")
								buffer.Append("return ");
							buffer.Append(wrapClass.cppName);
							buffer.Append("::");
							buffer.Append(wrapMethod.name);
							buffer.Append("(");

							//Arguments
							foreach(WrapMethodArg arg in wrapMethod.args)
							{
								//Comma
								if(wrapMethod.args.IndexOf(arg) > 0)
									buffer.Append(", ");

								//Arg
								buffer.Append(arg.name);
							}
							buffer.Append(");\n");

							//End method
							buffer.Append("}\n");
						}*/

						//------------------------------------------------------
						//Declare variable
						if(tempWrapClass == wrapClass)
						{
							buffer.Append("static ");
							WrapMethod tempMethod = wrapMethod.Clone();
							tempMethod.args.Insert(0, new WrapMethodArg("MonoObject*", "monoObj"));
							writeCsWrap_CppMethodPtr(wrapClass, tempMethod, "CSharp_" + tempWrapClass.name + "_" + wrapMethod.name, buffer);
							buffer.Append(";\n");
						}

						buffer.Append("\n");
					}

					//Continue
					if(String.IsNullOrEmpty(tempWrapClass.parentClass))
						break;
					tempWrapClass = findClass(tempWrapClass.parentClass);
				} //End while
				this.currentContext = wrapClass.context;

				//Virtual method variables
				tempWrapClass = wrapClass;
				while(tempWrapClass != null)
				{
					//Methods
					foreach(WrapMethod wrapMethod in tempWrapClass.methods)
					{
						//Is virtual
						if(!wrapMethod.isVirtual)
							continue;

						//Check if already defined
						if(!wrapClass.isFinalMethod(this, wrapMethod))
							continue;

						//Variable
						buffer.Append("bool ");
						buffer.Append(wrapMethod.name);
						buffer.Append("_HasVirtualMethod = false;\n");
					}

					//Continue
					if(String.IsNullOrEmpty(tempWrapClass.parentClass))
						break;
					tempWrapClass = findClass(tempWrapClass.parentClass);
				}
			}

			//End wrapper class
			buffer.Append("}; //End Wrapper Class\n");
			buffer.Append("\n");

			//Wrap object virtual methods --------------------------------------------------------
			buffer.Append("//Virtual Method Variables-----------------------------\n");
			foreach(WrapMethod wrapMethod in wrapClass.methods)
			{
				//Virtual only
				if(!wrapMethod.isVirtual)
					continue;

				//Declare method ptr
				WrapMethod tempMethod = wrapMethod.Clone();
				tempMethod.args.Insert(0, new WrapMethodArg("MonoObject", "obj"));
				writeCsWrap_CppMethodPtr(wrapClass, tempMethod, wrapClass.uniqueName + "_CSharpWrapper::CSharp_" + wrapClass.name + "_" + wrapMethod.name, buffer);
				buffer.Append(" = nullptr;\n");
			}
			buffer.Append("\n");

			//Wrap object static methods --------------------------------------------------------
			/*buffer.Append("static MonoObject* ");
			buffer.Append(wrapClass.name);
			buffer.Append("_AquireWrapObject(void* cppObjPtr)\n");
			buffer.Append("{\n");
			buffer.Append("InheritedClass* wrapper = dynamic_cast<InheritedClass*>((");
			buffer.Append(wrapClass.cppName);
			buffer.Append("*)cppObjPtr);\n");
			buffer.Append("return wrapper ? wrapper->monoObjPtr : nullptr;\n");
			buffer.Append("}\n");*/
		}
		public void writeCppWrap_CppMethod(WrapClass wrapClass, WrapMethod wrapMethod, StringBuilder buffer)
		{
			//Begin method
			buffer.Append("static void ");
			buffer.Append(wrapClass.uniqueName);
			buffer.Append("_");
			buffer.Append(wrapMethod.name);
			buffer.Append("(");

			//Initial argument
			int argCount = 0;
			if(!wrapMethod.isStatic)
			{
				buffer.Append("void* cppObject");
				argCount += 1;
			}

			//Arguments
			foreach(WrapMethodArg arg in wrapMethod.args)
			{
				//Spacer
				if(argCount > 0)
					buffer.Append(", ");
				argCount += 1;

				//Argument
				WrapType argType = findType(arg.type);
				buffer.Append(findType(arg.type).cppInPass);
				if(arg.moveOut || argType is WrapStruct)
					buffer.Append("*");
				buffer.Append(" ");
				buffer.Append(arg.name);
			}

			//Return
			if(wrapMethod.returnType != "void")
			{
				if(argCount > 0)
					buffer.Append(", ");
				buffer.Append(findType(wrapMethod.returnType).cppOutPass);
				buffer.Append("* ");
				buffer.Append("resultOut");
			}

			buffer.Append(")\n");
			buffer.Append("{\n");

			//Argument conversion - IN
			writeCppWrap_MethodArgConvertIn(wrapMethod, buffer);

			//Method call
			string methodCall;
			{
				StringBuilder tempBuffer = new StringBuilder();

				//Begin wrap
				tempBuffer.Append("(");

				//If virtual
				if(wrapMethod.isVirtual && !wrapMethod.isAbstract)
				{
					//Variable
					buffer.Append("auto wrapper = ");
					buffer.Append("dynamic_cast<InheritedClass*>((");
					buffer.Append(wrapClass.cppName);
					buffer.Append("*)cppObject);\n");

					tempBuffer.Append("wrapper ? ");

					//Method call
					tempBuffer.Append("((");
					tempBuffer.Append(wrapClass.cppName);
					tempBuffer.Append("*)cppObject)->");
					tempBuffer.Append(wrapClass.cppName);
					tempBuffer.Append("::");
					tempBuffer.Append(wrapMethod.name);
					tempBuffer.Append("(");
					tempBuffer.Append(wrapMethod.getCppArgCall(this));
					tempBuffer.Append(")");

					//Option
					tempBuffer.Append(" : ");
				}

				//Method call
				if(wrapMethod.isStatic)
				{
					tempBuffer.Append(wrapClass.cppName);
					tempBuffer.Append("::");
				}
				else
				{
					tempBuffer.Append("((");
					tempBuffer.Append(wrapClass.cppName);
					tempBuffer.Append("*)cppObject)->");
				}
				tempBuffer.Append(wrapMethod.name);
				tempBuffer.Append("(");
				tempBuffer.Append(wrapMethod.getCppArgCall(this));
				tempBuffer.Append(")");

				//End wrap
				tempBuffer.Append(")");

				//Store
				methodCall = tempBuffer.ToString();
			}

			//Return
			if(wrapMethod.returnType != "void")
			{
				WrapType wrapType = findType(wrapMethod.returnType);

				if(String.IsNullOrEmpty(wrapType.cppOutConvert))
				{
					buffer.Append("*resultOut = ");
					buffer.Append(methodCall);
					buffer.Append(";\n");
				}
				else
				{
					//Build conversion string
					string conversion = wrapType.cppOutConvert;
					conversion = conversion.Replace("$result", "(*resultOut)");
					conversion = conversion.Replace("$langtype", wrapType.cppName);
					conversion = conversion.Replace("$vartype", wrapType.cppType);
					conversion = conversion.Replace("$wraptype", wrapType.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", methodCall);

					//Add
					buffer.Append(conversion);
					buffer.Append("\n");
				}
			}
			else
			{
				//Just call method
				buffer.Append(methodCall);
				buffer.Append(";\n");
			}

			//Out Arguments
			foreach(WrapMethodArg wrapArg in wrapMethod.args)
			{
				//Check if out
				if(!wrapArg.moveOut)
					continue;

				//Check if out conversion
				WrapType wrapType = findType(wrapArg.type);
				if(String.IsNullOrEmpty(wrapType.cppOutConvert))
					continue;

				//Build conversion string
				string conversion = wrapType.cppOutConvert;
				conversion = conversion.Replace("$result", "*" + wrapArg.name);
				conversion = conversion.Replace("$langtype", wrapType.cppName);
				conversion = conversion.Replace("$vartype", wrapType.cppType);
				conversion = conversion.Replace("$wraptype", wrapType.name);
				conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
				conversion = conversion.Replace("$input", "arg" + wrapMethod.args.IndexOf(wrapArg));

				//Add
				buffer.Append(conversion);
				buffer.Append("\n");
			}

			//Close function
			buffer.Append("}\n");
		}
		public void writeCppWrap_CppVariable(WrapClass wrapClass, WrapVariable wrapVariable, StringBuilder buffer)
		{
			WrapType wrapType = findType(wrapVariable.type);

			//Begin get --------------------------------------------
			if(!String.IsNullOrEmpty(wrapVariable.getBody))
			{
				buffer.Append("static void ");
				buffer.Append(wrapClass.uniqueName);
				buffer.Append("_");
				buffer.Append(wrapVariable.name);
				buffer.Append("_Get(void* obj, ");
				buffer.Append(wrapType.cppOutPass);
				buffer.Append("* resultOut)\n");
				buffer.Append("{\n");

				//Input
				buffer.Append(findType(wrapVariable.type).cppType);
				buffer.Append(" input;\n");

				string input = wrapVariable.getBody;
				input = input.Replace("$result", "input");
				input = input.Replace("$obj", "((" + wrapClass.cppName + " *)obj)");
				input = input.Replace("$varname", wrapVariable.name);
				buffer.Append(input);
				buffer.Append("\n");

				//Argument conversion out
				if(!String.IsNullOrEmpty(wrapType.cppOutConvert))
				{
					//Replace keywords
					string conversion = wrapType.cppOutConvert;
					conversion = conversion.Replace("$result",  "(*resultOut)");
					conversion = conversion.Replace("$langtype", wrapType.cppName);
					conversion = conversion.Replace("$vartype", wrapType.cppType);
					conversion = conversion.Replace("$wraptype", wrapType.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", "input");

					//Append
					buffer.Append(conversion);
					buffer.Append("\n");
				}
				else
				{
					//Return
					buffer.Append("*resultOut = input;\n");
				}

				//End get
				buffer.Append("}\n");
			}

			//Begin set --------------------------------------------
			if(!String.IsNullOrEmpty(wrapVariable.setBody))
			{
				buffer.Append("static void ");
				buffer.Append(wrapClass.uniqueName);
				buffer.Append("_");
				buffer.Append(wrapVariable.name);
				buffer.Append("_Set(void* obj, ");
				buffer.Append(wrapType.cppInPass);
				if(wrapType is WrapStruct)
					buffer.Append("*");
				buffer.Append(" value)\n");
				buffer.Append("{\n");

				//Argument conversion in
				string input;
				if(wrapType is WrapStruct)
					input = "*value";
				else
					input = "value";
				if(!String.IsNullOrEmpty(wrapType.cppInConvert))
				{
					//Replace keywords
					string conversion = wrapType.cppInConvert;
					conversion = conversion.Replace("$result", "result");
					conversion = conversion.Replace("$langtype", wrapType.cppName);
					conversion = conversion.Replace("$vartype", wrapType.cppType);
					conversion = conversion.Replace("$wraptype", wrapType.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", input);

					//Append
					buffer.Append(wrapType.cppType);
					buffer.Append(" result;\n");
					buffer.Append(conversion);
					buffer.Append("\n");

					//Set
					input = "result";
				}

				//Set Body
				string setBody = wrapVariable.setBody;
				setBody = setBody.Replace("$obj", "((" + wrapClass.cppName + " *)obj)");
				setBody = setBody.Replace("$varname", wrapVariable.name);
				setBody = setBody.Replace("$input", input);
				buffer.Append(setBody);
				buffer.Append("\n");

				//End set
				buffer.Append("}\n");
			}
		}

		public void writeCppWrap_MethodArgConvertIn(WrapMethod wrapMethod, StringBuilder buffer)
		{
			//Argument conversion - IN
			foreach(WrapMethodArg wrapArg in wrapMethod.args)
			{
				WrapType wrapType = findType(wrapArg.type);

				//Check type
				if(wrapArg.moveIn)
				{
					//Check for conversion string
					if(String.IsNullOrEmpty(wrapType.cppInConvert))
						continue;

					string conversion = wrapType.cppInConvert;

					//Replace keywords
					conversion = conversion.Replace("$result", "arg" + wrapMethod.args.IndexOf(wrapArg));
					conversion = conversion.Replace("$langtype", wrapType.cppName);
					conversion = conversion.Replace("$vartype", wrapType.cppType);
					conversion = conversion.Replace("$wraptype", wrapType.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", wrapArg.name);

					//Append
					buffer.Append(wrapType.cppType);
					buffer.Append(" arg");
					buffer.Append(wrapMethod.args.IndexOf(wrapArg));
					buffer.Append(";\n");
					buffer.Append(conversion);
					buffer.Append("\n");
				}
				else if(wrapArg.moveOut)
				{
					buffer.Append(wrapType.cppType);
					buffer.Append(" arg");
					buffer.Append(wrapMethod.args.IndexOf(wrapArg));
					buffer.Append(";\n");
				}
			}
		}
		public void writeCppWrap_MethodArgConvertOut(WrapMethod wrapMethod, StringBuilder buffer)
		{
			//Argument conversion - OUT
			foreach(WrapMethodArg wrapArg in wrapMethod.args)
			{
				WrapType wrapType = findType(wrapArg.type);

				//Check for conversion string
				if(String.IsNullOrEmpty(wrapType.cppOutConvertBoxed))
					continue;

				//Replace keywords
				string conversion = wrapType.cppOutConvertBoxed;
				conversion = conversion.Replace("$result", "arg" + wrapMethod.args.IndexOf(wrapArg));
				conversion = conversion.Replace("$langtype", wrapType.cppName);
				conversion = conversion.Replace("$vartype", wrapType.cppType);
				conversion = conversion.Replace("$wraptype", wrapType.name);
				conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
				conversion = conversion.Replace("$input", wrapArg.name);

				//Declare arg variable
				buffer.Append(wrapType.cppOutPassBoxed);
				buffer.Append(" arg");
				buffer.Append(wrapMethod.args.IndexOf(wrapArg));
				buffer.Append(";\n");

				//Conversion
				buffer.Append(conversion);
				buffer.Append("\n");
			}
		}

		//C# to C++ Wrapper
		public void writeCsWrap_CSharpClass(WrapClass wrapClass, StringBuilder buffer)
		{
			//Push context
			currentContext = wrapClass.context;

			//Constructors
			if(!wrapClass.isAbstract())
			{
				int constructorCount = 0;
				foreach(WrapMethod wrapMethod in wrapClass.constructors)
				{
					//Begin method
					buffer.Append("public static ");
					buffer.Append(wrapClass.csType);
					buffer.Append(" ");
					buffer.Append(wrapClass.name);
					buffer.Append("_ConstructorDefault");
					buffer.Append(constructorCount);
					buffer.Append("(");

					//Begin Args
					int argCount = 0;
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Comma
						if(argCount > 0)
							buffer.Append(", ");

						//Argument
						buffer.Append(findType(wrapArg.type).csType);
						buffer.Append(" ");
						buffer.Append(wrapArg.name);
					}

					//End Args
					buffer.Append(")\n");
					buffer.Append("{\n");

					//Argument conversion - IN
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Check for conversion string
						WrapType wrapType = findType(wrapArg.type);
						if(String.IsNullOrEmpty(wrapType.csInConvert))
							continue;

						string conversion = wrapType.csInConvert;

						//Replace keywords
						string argName = "arg" + wrapMethod.args.IndexOf(wrapArg);
						conversion = conversion.Replace("$result", argName);
						conversion = conversion.Replace("$langtype", wrapType.csName);
						conversion = conversion.Replace("$vartype", wrapType.csType);
						conversion = conversion.Replace("$wraptype", wrapType.name);
						conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
						conversion = conversion.Replace("$input", wrapArg.name);

						//Append
						buffer.Append(wrapType.csType);
						buffer.Append(" ");
						buffer.Append(argName);
						buffer.Append(";\n");

						buffer.Append(conversion);
						buffer.Append("\n");
					}

					//Method Call
					buffer.Append("return new ");
					buffer.Append(wrapClass.csName);
					buffer.Append("(");

					//Args
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Spacer
						if(wrapArg != wrapMethod.args[0])
							buffer.Append(", ");

						//Argument
						if(String.IsNullOrEmpty(findType(wrapArg.type).csInConvert))
							buffer.Append(wrapArg.name);
						else
						{
							buffer.Append("arg");
							buffer.Append(wrapMethod.args.IndexOf(wrapArg));
						}
					}
					buffer.Append(");\n");

					//End method
					buffer.Append("}\n");

					//Increment
					constructorCount += 1;
				}
			}
			

			//Methods
			foreach(WrapMethod wrapMethod in wrapClass.methods)
			{
				writeCsWrap_CSharpMethod(wrapClass, wrapMethod, buffer);
			}
		}
		public void writeCsWrap_CSharpMethod(WrapClass wrapClass, WrapMethod wrapMethod, StringBuilder buffer)
		{
			//Begin method
			if(wrapMethod.isUnsafe)
				buffer.Append("unsafe ");
			buffer.Append("public static ");
			buffer.Append(findType(wrapMethod.returnType).csOutPass);
			buffer.Append(" ");
			buffer.Append(wrapClass.uniqueName);
			buffer.Append("_");
			buffer.Append(wrapMethod.name);
			buffer.Append("(");

			//Arguments
			buffer.Append(findType(wrapClass.name).csType);
			buffer.Append(" obj");
			foreach(WrapMethodArg wrapArg in wrapMethod.args)
			{
				//Spacer
				buffer.Append(", ");

				//Arg
				buffer.Append(findType(wrapArg.type).csInPass);
				buffer.Append(" ");
				buffer.Append(wrapArg.name);
			}
			buffer.Append(")\n");

			//Body
			buffer.Append("{\n");

			//Argument conversion - IN
			foreach(WrapMethodArg wrapArg in wrapMethod.args)
			{
				//Check for conversion string
				WrapType wrapType = findType(wrapArg.type);
				if(String.IsNullOrEmpty(wrapType.csInConvert))
					continue;

				string conversion = wrapType.csInConvert;

				//Replace keywords
				string argName = "arg" + wrapMethod.args.IndexOf(wrapArg);
				conversion = conversion.Replace("$result", argName);
				conversion = conversion.Replace("$langtype", wrapType.csName);
				conversion = conversion.Replace("$vartype", wrapType.csType);
				conversion = conversion.Replace("$wraptype", wrapType.name);
				conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
				conversion = conversion.Replace("$input", wrapArg.name);

				//Append
				buffer.Append(wrapType.csType);
				buffer.Append(" ");
				buffer.Append(argName);
				buffer.Append(";\n");

				buffer.Append(conversion);
				buffer.Append("\n");
			}

			//Build method call
			string methodCall;
			{
				StringBuilder tempBuilder = new StringBuilder();

				//Method call
				if(wrapMethod.isStatic)
				{
					tempBuilder.Append(findType(wrapClass.name).csType);
					tempBuilder.Append(".");
				}
				else
					tempBuilder.Append("obj.");
				tempBuilder.Append(wrapMethod.name);
				tempBuilder.Append("(");

				//Argumnets
				foreach(WrapMethodArg wrapArg in wrapMethod.args)
				{
					//Spacer
					if(wrapArg != wrapMethod.args[0])
						tempBuilder.Append(", ");

					//Argument
					if(String.IsNullOrEmpty(findType(wrapArg.type).csInConvert))
						tempBuilder.Append(wrapArg.name);
					else
					{
						tempBuilder.Append("arg");
						tempBuilder.Append(wrapMethod.args.IndexOf(wrapArg));
					}
				}

				//End call
				tempBuilder.Append(")");

				//Store
				methodCall = tempBuilder.ToString();
			}

			//Return
			if(wrapMethod.returnType != "void")
			{
				//Return Variable Conversion - OUT
				WrapType wrapType = findType(wrapMethod.returnType);
				if(!String.IsNullOrEmpty(wrapType.csOutConvert))
				{
					string conversion = wrapType.csOutConvert;

					//Replace keywords
					conversion = conversion.Replace("$result", "result");
					conversion = conversion.Replace("$langtype", wrapType.csName);
					conversion = conversion.Replace("$vartype", wrapType.csType);
					conversion = conversion.Replace("$wraptype", wrapType.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", "input");

					//Append
					buffer.Append(wrapType.csType);
					buffer.Append(" input = ");
					buffer.Append(methodCall);
					buffer.Append(";\n");
					buffer.Append(wrapType.csOutPass);
					buffer.Append(" result;\n");
					buffer.Append(conversion);
					buffer.Append("\n");
					buffer.Append("return result;\n");
				}
				else
				{
					//Just call and return
					buffer.Append("return ");
					buffer.Append(methodCall);
					buffer.Append(";\n");
				}
			}
			else
			{
				//Just call
				buffer.Append(methodCall);
				buffer.Append(";\n");
			}

			//End method
			buffer.Append("}\n");
		}
		public void writeCsWrap_CppClassHeaderFile(WrapClass wrapClass)
		{
			StringBuilder buffer = new StringBuilder();

			//Push context
			currentContext = wrapClass.context;

			//Include protector
			buffer.Append("#ifndef __");
			buffer.Append(wrapClass.cppName.Replace("::", "_").ToUpper());
			buffer.Append("_H\n");
			buffer.Append("#define __");
			buffer.Append(wrapClass.cppName.Replace("::", "_").ToUpper());
			buffer.Append("_H\n");
			buffer.Append("\n");

			//Includes
			buffer.Append("#include \"mono/jit/jit.h\"\n");
			buffer.Append("\n");

			//Include all types that are used
			{

				//Find all include types
				WrapType type;
				Dictionary<string, string> typeIncludes = new Dictionary<string, string>();
				foreach(WrapMethod wrapMethod in wrapClass.methods)
				{
					//Return
					type = findType(wrapMethod.returnType);
					if(!String.IsNullOrEmpty(type.cppInclude) && !typeIncludes.ContainsKey(type.name))
						typeIncludes.Add(type.name, type.cppInclude);

					//Args
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						type = findType(wrapArg.type);
						if(!String.IsNullOrEmpty(type.cppInclude) && !typeIncludes.ContainsKey(type.name))
							typeIncludes.Add(type.name, type.cppInclude);
					}
				}

				//Add includes
				foreach(KeyValuePair<string, string> pair in typeIncludes)
				{
					buffer.Append(pair.Value);
					buffer.Append("\n");
				}
				buffer.Append("\n");
			}

			//Namespace
			List<string> namespaceList = wrapClass.parentContext.buildNamespaces();
			if(namespaceList.Count > 0)
			{
				foreach(string name in namespaceList)
				{
					buffer.Append("namespace ");
					buffer.Append(name);
					buffer.Append("\n");
					buffer.Append("{");
					buffer.Append("\n");
				}
				buffer.Append("\n");
			}

			//Class Declaration
			buffer.Append("class ");
			buffer.Append(wrapClass.name);
			buffer.Append("\n");
			buffer.Append("{\n");

			//Constructor/Deconstructor
			buffer.Append("public:\n");
			buffer.Append(wrapClass.name);
			buffer.Append("(MonoObject* monoObjPtr);\n");
			buffer.Append("~");
			buffer.Append(wrapClass.name);
			buffer.Append("(void);\n");
			buffer.Append("\n");

			//Constructors
			buffer.Append("//Constructors-----------------------------------\n");
			if(wrapClass.constructors.Count > 0)
			{
				int constructorSize = 0;
				foreach(WrapMethod wrapMethod in wrapClass.constructors)
				{
					//Method begin
					buffer.Append(wrapClass.name);
					buffer.Append("(");

					//Arguments
					int argCount = 0;
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Comma
						if(argCount > 0)
							buffer.Append(", ");
						argCount += 1;

						//Argument
						buffer.Append(findType(wrapArg.type).cppType);
						buffer.Append(" ");
						buffer.Append(wrapArg.name);
					}

					//Method end
					buffer.Append(");\n");

					//Increment
					constructorSize += 1;
				}
				buffer.Append("\n");
			}
			buffer.Append("MonoObject* getMonoObject(void) const;\n");
			buffer.Append("\n");

			//Methods
			buffer.Append("//Methods-----------------------------------\n");
			foreach(WrapMethod wrapMethod in wrapClass.methods)
			{
				//Static
				if(wrapMethod.isUnsafe)
					buffer.Append("unsafe ");
				if(wrapMethod.isStatic)
					buffer.Append("static ");

				//Begin call
				buffer.Append(findType(wrapMethod.returnType).cppType);
				buffer.Append(" ");
				buffer.Append(wrapMethod.name);
				buffer.Append("(");

				//Arguments
				if(wrapMethod.args.Count() > 0)
				{
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Spacer
						if(wrapArg != wrapMethod.args[0])
							buffer.Append(", ");

						//Argument
						buffer.Append(findType(wrapArg.type).cppType);
						buffer.Append(" ");
						buffer.Append(wrapArg.name);
					}
				}
				else
				{
					//No arguments
					buffer.Append("void");
				}

				//End call
				buffer.Append(");\n");
			}
			buffer.Append("\n");

			//Static wrapper methods

			//Constructors
			if(!wrapClass.isAbstract())
			{
				int methodCount = 0;
				foreach(WrapMethod wrapMethod in wrapClass.constructors)
				{
					//Name
					string methodName = wrapClass.name + "_ConstructorDefault" + methodCount;

					//Write
					buffer.Append("static ");
					WrapMethod tempMethod = wrapMethod.Clone();
					tempMethod.returnType = "MonoObject*";
					writeCsWrap_CppMethodPtr(wrapClass, tempMethod, methodName, buffer);
					buffer.Append(";\n");

					//Increment
					methodCount += 1;
				}
			}

			//Methods
			foreach(WrapMethod wrapMethod in wrapClass.methods)
			{
				//Name
				string methodName = wrapClass.name + "_" + wrapMethod.name;

				//Write
				buffer.Append("static ");
				WrapMethod tempMethod = wrapMethod.Clone();
				tempMethod.args.Insert(0, new WrapMethodArg("MonoObject*", "monoObject"));
				writeCsWrap_CppMethodPtr(wrapClass, tempMethod, methodName, buffer);
				buffer.Append(";\n");
			}
			buffer.Append("\n");

			//Private variables
			buffer.Append("private:\n");
			buffer.Append("MonoObject* monoObjPtr;\n");
			buffer.Append("int32_t gcHandle;\n");

			//End class
			buffer.Append("};\n");

			//End namespace
			if(namespaceList.Count > 0)
			{
				foreach(string name in namespaceList)
				{
					buffer.Append("\n} //End Namespace");
					buffer.Append(name);
					buffer.Append("\n");
				}
			}

			//End protector
			buffer.Append("\n");
			buffer.Append("#endif\n");

			//Write file
			string path = cppOutputPath + wrapClass.name + ".h";
			System.IO.File.WriteAllText(path, buffer.ToString());
		}
		public void writeCsWrap_CppClassSourceFile(WrapClass wrapClass)
		{
			StringBuilder buffer = new StringBuilder();

			//Push context
			currentContext = wrapClass.context;

			//Includes
			buffer.Append("#include \"");
			buffer.Append(wrapClass.name);
			buffer.Append(".h\"\n");
			buffer.Append("#include \"CppCSharpBridge.h\"\n");
			buffer.Append("#include \"CppCSharpBridge_Internal.h\"\n");
			buffer.Append("\n");

			//Namespace
			List<string> namespaceList = wrapClass.parentContext.buildNamespaces();
			if(namespaceList.Count > 0)
			{
				foreach(string name in namespaceList)
				{
					buffer.Append("namespace ");
					buffer.Append(name);
					buffer.Append("\n");
					buffer.Append("{");
					buffer.Append("\n");
				}
				buffer.Append("\n");
			}

			//Static Variables - Methods
			if(!wrapClass.isAbstract())
			{
				int methodCount = 0;
				foreach(WrapMethod wrapMethod in wrapClass.constructors)
				{
					//Name
					string methodName = wrapClass.name + "::" + wrapClass.name + "_ConstructorDefault" + methodCount;

					//Method ptr
					WrapMethod tempMethod = wrapMethod.Clone();
					tempMethod.returnType = "MonoObject*";
					writeCsWrap_CppMethodPtr(wrapClass, tempMethod, methodName, buffer);
					buffer.Append(" = nullptr;\n");

					//Increment
					methodCount += 1;
				}
			}
			foreach(WrapMethod wrapMethod in wrapClass.methods)
			{
				//Name
				string methodName = wrapClass.name + "::" + wrapClass.name + "_" + wrapMethod.name;

				//Method ptr
				WrapMethod tempMethod = wrapMethod.Clone();
				tempMethod.args.Insert(0, new WrapMethodArg("MonoObject*", "monoObject"));
				writeCsWrap_CppMethodPtr(wrapClass, tempMethod, methodName, buffer);
				buffer.Append(" = nullptr;\n");
			}
			buffer.Append("\n");

			//Constructor
			buffer.Append(wrapClass.name);
			buffer.Append("::");
			buffer.Append(wrapClass.name);
			buffer.Append("(MonoObject* monoObjPtr)\n");
			buffer.Append("{\n");
			buffer.Append("this->monoObjPtr = monoObjPtr;\n");
			buffer.Append("gcHandle = mono_gchandle_new(monoObjPtr, false);\n");
			buffer.Append("}\n");
			buffer.Append("\n");

			//Deconstructor
			buffer.Append(wrapClass.name);
			buffer.Append("::~");
			buffer.Append(wrapClass.name);
			buffer.Append("(void)\n");
			buffer.Append("{\n");
			buffer.Append("mono_gchandle_free(gcHandle);\n");
			buffer.Append("}\n");
			buffer.Append("\n");

			//Constructors
			if(wrapClass.constructors.Count > 0)
			{
				int constructorCount = 0;
				foreach(WrapMethod wrapMethod in wrapClass.constructors)
				{
					//Begin method name
					buffer.Append(wrapClass.name);
					buffer.Append("::");
					buffer.Append(wrapClass.name);
					buffer.Append("(");

					//Arguments
					int argCount = 0;
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Comma
						if(argCount > 0)
							buffer.Append(", ");

						//Argument
						buffer.Append(findType(wrapArg.type).cppType);
						buffer.Append(" ");
						buffer.Append(wrapArg.name);
					}

					//End method name
					buffer.Append(")\n");
					buffer.Append("{\n");

					//Argument conversion - IN
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Check type
						if(!wrapArg.moveIn)
							continue;

						//Check for conversion string
						WrapType wrapType = findType(wrapArg.type);
						if(String.IsNullOrEmpty(wrapType.cppInConvertBoxed))
							continue;

						string conversion = wrapType.cppOutConvertBoxed;

						//Replace keywords
						conversion = conversion.Replace("$result", wrapType.cppOutPassBoxed + " arg" + wrapMethod.args.IndexOf(wrapArg));
						conversion = conversion.Replace("$langtype", wrapType.cppName);
						conversion = conversion.Replace("$vartype", wrapType.cppType);
						conversion = conversion.Replace("$wraptype", wrapType.name);
						conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
						conversion = conversion.Replace("$input", wrapArg.name);

						//Append
						buffer.Append(conversion);
						buffer.Append("\n");
					}

					//Begin method call
					buffer.Append("MonoException* exception = nullptr;\n");
					buffer.Append("monoObjPtr = ");
					buffer.Append(wrapClass.name);
					buffer.Append("_ConstructorDefault");
					buffer.Append(constructorCount);
					buffer.Append("(");

					//Arguments
					foreach(WrapMethodArg wrapArg in wrapMethod.args)
					{
						//Comma
						if(wrapArg != wrapMethod.args[0])
							buffer.Append(", ");

						//Arg
						if(String.IsNullOrEmpty(findType(wrapArg.type).cppOutConvertBoxed))
							buffer.Append(wrapArg.name);
						else
						{
							buffer.Append("arg");
							buffer.Append(wrapMethod.args.IndexOf(wrapArg));
						}
					}

					//End method call
					if(wrapMethod.args.Count > 0)
						buffer.Append(", ");
					buffer.Append("&exception);\n");
					buffer.Append("if(exception) { mono_print_unhandled_exception((MonoObject*)exception); assert(false); }\n");
					buffer.Append("gcHandle = mono_gchandle_new(monoObjPtr, false);\n");
					buffer.Append("}\n");

					//Iterate
					constructorCount += 1;
				}
			}

			//Convenience Methods
			buffer.Append("MonoObject* ");
			buffer.Append(wrapClass.name);
			buffer.Append("::getMonoObject(void) const { return monoObjPtr; }\n");

			//Methods
			foreach(WrapMethod wrapMethod in wrapClass.methods)
			{
				writeCsWrap_CppMethod(wrapClass, wrapMethod, buffer);
			}

			//Wrapper Out Method
			if(wrapClass.cppCanBePassed)
			{
				//Method declaration
				buffer.Append("MonoObject* ConvertObjectOut(");
				buffer.Append(wrapClass.cppType);
				buffer.Append(" obj)\n");
				buffer.Append("{\n");

				//Check if null
				buffer.Append("if(!obj) return nullptr;\n");

				//Return
				buffer.Append("return obj->getMonoObject();\n");

				//End
				buffer.Append("}\n");
			}

			//End namespace
			if(namespaceList.Count > 0)
			{
				foreach(string name in namespaceList)
				{
					buffer.Append("\n} //End Namespace");
					buffer.Append(name);
					buffer.Append("\n");
				}
			}

			//Write file
			string path = cppOutputPath + wrapClass.name + ".cpp";
			System.IO.File.WriteAllText(path, buffer.ToString());
		}
		public void writeCsWrap_CppMethod(WrapClass wrapClass, WrapMethod wrapMethod, StringBuilder buffer)
		{
			//Begin method declaration
			buffer.Append(findType(wrapMethod.returnType).cppType);
			buffer.Append(" ");
			buffer.Append(wrapClass.name);
			buffer.Append("::");
			buffer.Append(wrapMethod.name);
			buffer.Append("(");

			//Args
			if(wrapMethod.args.Count() > 0)
			{
				foreach(WrapMethodArg wrapArg in wrapMethod.args)
				{
					//Spacer
					if(wrapArg != wrapMethod.args[0])
						buffer.Append(", ");

					//Arg
					buffer.Append(findType(wrapArg.type).cppType);
					buffer.Append(" ");
					buffer.Append(wrapArg.name);
				}
			}
			else
			{
				//No arguments
				buffer.Append("void");
			}

			//End method declaration
			buffer.Append(")\n");

			//Begin method body
			buffer.Append("{\n");
			writeCsWrap_CppMethodBody(wrapClass, wrapMethod, wrapClass.name + "_", buffer);
			buffer.Append("}\n");
			buffer.Append("\n");
		}
		public void writeCsWrap_CppMethodBody(WrapClass wrapClass, WrapMethod wrapMethod, string methodNamePrefix, StringBuilder buffer)
		{
			//Argument conversion - OUT
			foreach(WrapMethodArg wrapArg in wrapMethod.args)
			{
				//Check type
				if(!wrapArg.moveIn)
					continue;

				//Check for conversion string
				WrapType wrapType = findType(wrapArg.type);
				if(!String.IsNullOrEmpty(wrapType.cppOutConvertBoxed))
				{
					//Replace keywords
					string conversion = wrapType.cppOutConvertBoxed;
					conversion = conversion.Replace("$result", "arg" + wrapMethod.args.IndexOf(wrapArg));
					conversion = conversion.Replace("$langtype", wrapType.cppName);
					conversion = conversion.Replace("$vartype", wrapType.cppType);
					conversion = conversion.Replace("$wraptype", wrapType.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", wrapArg.name);

					//Append
					buffer.Append(wrapType.cppOutPassBoxed);
					buffer.Append(" arg");
					buffer.Append(wrapMethod.args.IndexOf(wrapArg));
					buffer.Append(";\n");
					buffer.Append(conversion);
					buffer.Append("\n");
				}				
			}

			buffer.Append("MonoException* exception = nullptr;\n");

			//Create static method call
			string wrapMethodCall = "";
			{
				StringBuilder temp = new StringBuilder();

				//Begin wrapper call
				temp.Append(methodNamePrefix);
				temp.Append(wrapMethod.name);
				temp.Append("(");

				//Args
				if(!wrapMethod.isStatic)
					temp.Append("monoObjPtr");
				else
					temp.Append("nullptr");

				foreach(WrapMethodArg wrapArg in wrapMethod.args)
				{
					//Spacer
					temp.Append(", ");

					//Arg
					if(String.IsNullOrEmpty(findType(wrapArg.type).cppOutConvertBoxed))
						temp.Append(wrapArg.name);
					else
					{
						temp.Append("arg");
						temp.Append(wrapMethod.args.IndexOf(wrapArg));
					}
				}

				//End wrapper call
				temp.Append(", &exception);\n");

				//Store
				wrapMethodCall = temp.ToString();
			}

			//Return arg
			/*if(wrapMethod.returnType != "void")
			{
				WrapType wrapType = findType(wrapMethod.returnType);
				string conversion = wrapType.cppInConvert;
				if(!String.IsNullOrEmpty(conversion))
				{
					//Input
					buffer.Append(wrapType.cppInPass);
					buffer.Append(" input = ");
					buffer.Append(wrapMethodCall);

					//Result variable
					buffer.Append(wrapType.cppType);
					buffer.Append(" result;\n");

					//Replace keywords
					conversion = conversion.Replace("$result", "result");
					conversion = conversion.Replace("$langtype", wrapType.cppName);
					conversion = conversion.Replace("$vartype", wrapType.cppType);
					conversion = conversion.Replace("$wraptype", wrapType.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", "input");
					buffer.Append(conversion);
					buffer.Append("\n");

					//Return
					buffer.Append("return result;\n");
				}
				else
				{
					//Method call with return
					buffer.Append("return ");
					buffer.Append(wrapMethodCall);
				}
			}
			else
			{
				//Nothing method call
				buffer.Append(wrapMethodCall);
			}

			buffer.Append("if(exception) { mono_print_unhandled_exception((MonoObject*)exception); assert(false); }\n");*/

			//Call method
			if(wrapMethod.returnType != "void")
			{
				WrapType wrapType = findType(wrapMethod.returnType);
				if(!String.IsNullOrEmpty(wrapType.cppInConvertBoxed))
				{
					//Result
					buffer.Append(wrapType.cppType);
					buffer.Append(" result;\n");

					//Input
					buffer.Append(wrapType.cppInPassBoxed);
					buffer.Append(" input = ");
					buffer.Append(wrapMethodCall);

					//Replace keywords
					string conversion = wrapType.cppInConvertBoxed;
					conversion = conversion.Replace("$result", "result");
					conversion = conversion.Replace("$langtype", wrapType.cppName);
					conversion = conversion.Replace("$vartype", wrapType.cppType);
					conversion = conversion.Replace("$wraptype", wrapType.name);
					conversion = conversion.Replace("$uniquename", wrapType.uniqueName);
					conversion = conversion.Replace("$input", "input");
					buffer.Append(conversion);
					buffer.Append("\n");
				}
				else
				{
					//Result
					buffer.Append(wrapType.cppType);
					buffer.Append(" result;\n");

					//Method call
					buffer.Append("result = ");
					buffer.Append(wrapMethodCall);
				}

				//Check exception
				buffer.Append("if(exception) { mono_print_unhandled_exception((MonoObject*)exception); assert(false); }\n");

				//Return
				buffer.Append("return result;\n");
			}
			else
			{
				//Method call
				buffer.Append(wrapMethodCall);

				//Check exception
				buffer.Append("if(exception) { mono_print_unhandled_exception((MonoObject*)exception); assert(false); }\n");
			}
			
		}
		public void writeCsWrap_CppInitClass(WrapClass wrapClass, StringBuilder buffer)
		{
			//Comment
			buffer.Append("//");
			buffer.Append(wrapClass.name);
			buffer.Append("\n");

			//Constructors
			foreach(WrapMethod wrapMethod in wrapClass.constructors)
			{
				//Find method
				buffer.Append("monoMethod = mono_class_get_method_from_name(monoClass, \"");
				buffer.Append(wrapClass.name);
				buffer.Append("_ConstructorDefault");
				buffer.Append(wrapClass.constructors.IndexOf(wrapMethod));
				buffer.Append("\", ");
				buffer.Append(wrapMethod.args.Count());
				buffer.Append(");\n");

				//Store Thunk
				buffer.Append(wrapClass.cppName);
				buffer.Append("::");
				buffer.Append(wrapClass.name);
				buffer.Append("_ConstructorDefault");
				buffer.Append(wrapClass.constructors.IndexOf(wrapMethod));
				buffer.Append(" = (");

				//Typecase to func ptr
				WrapMethod tempMethod = wrapMethod.Clone();
				tempMethod.returnType = "MonoObject*";
				writeCsWrap_CppMethodPtr(wrapClass, tempMethod, "", buffer);

				//End thunk
				buffer.Append(")mono_method_get_unmanaged_thunk(monoMethod);\n");
			}

			//Methods
			foreach(WrapMethod wrapMethod in wrapClass.methods)
			{
				//Find method
				buffer.Append("monoMethod = mono_class_get_method_from_name(monoClass, \"");
				buffer.Append(wrapClass.uniqueName);
				buffer.Append("_");
				buffer.Append(wrapMethod.name);
				buffer.Append("\", ");
				buffer.Append(wrapMethod.args.Count()+1);
				buffer.Append(");\n");

				//Store Thunk
				buffer.Append(wrapClass.cppName);
				buffer.Append("::");
				buffer.Append(wrapClass.name);
				buffer.Append("_");
				buffer.Append(wrapMethod.name);
				buffer.Append(" = (");

				//Typecase to func ptr
				WrapMethod tempMethod = wrapMethod.Clone();
				tempMethod.args.Insert(0, new WrapMethodArg("MonoObject*", "monoObject"));
				writeCsWrap_CppMethodPtr(wrapClass, tempMethod, "", buffer);

				//End thunk
				buffer.Append(")mono_method_get_unmanaged_thunk(monoMethod);\n");
			}
			buffer.Append("\n");
		}

		public void writeCsWrap_CppMethodPtr(WrapClass wrapClass, WrapMethod wrapMethod, string name, StringBuilder buffer)
		{
			//Begin variable
			buffer.Append(findType(wrapMethod.returnType).cppInPassBoxed);
			buffer.Append(" (_stdcall *");
			buffer.Append(name);
			buffer.Append(")(");

			//Args
			foreach(WrapMethodArg wrapArg in wrapMethod.args)
			{
				//Spacer
				if(wrapArg != wrapMethod.args[0])
					buffer.Append(", ");

				//Arg
				buffer.Append(findType(wrapArg.type).cppOutPassBoxed);
			}

			//End variable
			if(wrapMethod.args.Count > 0)
				buffer.Append(", ");
			buffer.Append("MonoException**)");
		}
	}
}
