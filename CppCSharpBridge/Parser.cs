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
	class WrapperParser
	{
		//Data
		WrapperGenerator generator;

		//Parse Data
		string[] lines;
		int lineIndex;

		string line;
		int charIndex;

		//Context stack
		WrapContext currentContext;

		//Constructor
		public WrapperParser(WrapperGenerator generator)
		{
			this.generator = generator;
			currentContext = generator.defaultContext;
		}

		//Parse
		public bool parseFile(string filePath)
		{
			//Set context
			currentContext = generator.defaultContext;

			//Read in lines from file
			lines = System.IO.File.ReadAllLines(filePath);
			if(lines.Count() == 0)
				return false;

			//Look for new class/structs
			for(lineIndex = 0; lineIndex < lines.Count(); lineIndex++)
			{
				//Store line
				line = lines[lineIndex];
				charIndex = 0;

				//Parse word command
				string word = parseWord();

				//Check for comment or blank line
				if(String.IsNullOrEmpty(word) || word.StartsWith("//"))
					continue;

				//Check for command
				switch(word.ToLower())
				{
				case "include":
				{
					//Find path
					string includePath = filePath.Substring(0, filePath.LastIndexOf('/'));
					includePath += line;

					//Parse file
					if(!parseFile(filePath))
						return false;

					//Break
					break;
				}
				case "namespace":
				{
					if(!parseNamespace())
						return false;
					break;
				}
				case "type":
				{
					if(!parseType())
						return false;
					break;
				}
				case "class":
				{
					if(!parseClass())
						return false;
					break;
				}
				case "struct":
				{
					if(!parseStruct())
						return false;
					break;
				}
				case "enum":
				{
					if(!parseEnum())
						return false;
					break;
				}
				default:
				{
					//Attempt to parse option
					if(!parseGlobalOption(word))
						return false;
						
					Console.Write("Unknown command for line:" + lineIndex);
					break;
				}
				}
			}

			//Finalize
			foreach(WrapEnum wrapEnum in generator.enums)
				finalizeEnum(wrapEnum);

			//Return
			return true;
		}
		public void finalize()
		{
			foreach(WrapClass wrapClass in generator.classes)
			{
				Console.WriteLine("Finalize:" + wrapClass.name);
				finalizeClass(wrapClass);
			}
		}

		public string parseWord(char[] endChars = null, bool advance = true)
		{
			skipWhitespace();

			//Default
			if(endChars == null)
				endChars = new char[1] { ' ' };

			//Find end
			int endIndex = line.IndexOfAny(endChars, charIndex);
			if(endIndex < 0)
				endIndex = line.Length;
			if(endIndex == charIndex)
				return null;

			string word = line.Substring(charIndex, (endIndex - charIndex));
			if(word == "")
				return null;

			//Return
			if(advance)
				charIndex = endIndex;
			return word;
		}
		public string parseLine()
		{
			//Skip whitespace
			skipWhitespace();

			//Get remainder
			string result = line.Substring(charIndex);
			charIndex = line.Length;

			//Return
			return result;
		}
		public string peekChars(int length, bool advance = true)
		{
			//Parse
			string result = line.Substring(charIndex, length);
			if(advance)
				charIndex += length;

			//Return
			return result;
		}
		public void seekLine(int pos)
		{
			charIndex += pos;
			if(charIndex < 0)
				charIndex = 0;
			if(charIndex > line.Length)
				charIndex = line.Length;
		}
		public bool nextLine()
		{
			lineIndex += 1;
			if(lineIndex >= lines.Count())
				return false;
			line = lines[lineIndex];
			charIndex = 0;
			return true;
		}
		public bool prevLine()
		{
			lineIndex -= 1;
			if(lineIndex < 0)
				return false;
			line = lines[lineIndex];
			charIndex = 0;
			return true;
		}
		public void resetLine()
		{
			charIndex = 0;
		}
		public void skipWhitespace()
		{
			for(; charIndex < line.Length; charIndex++)
			{
				char temp = line[charIndex];
				if(temp == ' ' || temp == '\t')
					continue;
				else
					break;
			}
		}
		public bool skipPast(char value)
		{
			//Find index
			int index = line.IndexOf(value, charIndex);
			if(index < 0)
				return false;

			//Skip past
			charIndex = index + 1;

			//Return
			return true;
		}
		public bool skipPast(char[] values)
		{
			//Skip past all chars
			for(; charIndex < line.Length; charIndex++)
			{
				bool isType = false;
				foreach(char charType in values)
				{
					if(line[charIndex] == charType)
					{
						isType = true;
						break;
					}
				}
				if(!isType)
					break;
			}

			//Return
			return true;
		}

		//Parse Options
		public bool parseGlobalOption(string word)
		{
			switch(word.ToLower())
			{
				case "cppoutputpath":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return false;
					}
					generator.cppOutputPath = temp;
					break;
				}
				case "csoutputpath":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return false;
					}
					generator.csharpOutputPath = temp;
					break;
				}
				case "cppincreaseref":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return false;
					}
					generator.cppIncreaseRef = temp;
					break;
				}
				case "cppdecreaseref":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return false;
					}
					generator.cppDecreaseRef = temp;
					break;
				}
				default:
				{
					Console.WriteLine("Unknown global line type:" + word);
					return false;
				}
			}

			//Return
			return true;
		}

		//Parse Namespace
		public bool parseNamespace()
		{
			//Name
			string name = parseWord();
			if(String.IsNullOrEmpty(name))
			{
				Console.Write("Unable to parse type name at line:" + lineIndex);
				return false;
			}

			//Opening bracket
			if(!nextLine() || !parseWord().StartsWith("{"))
			{
				Console.Write("Expected type opening bracket at line:" + lineIndex);
				return false;
			}

			//Create context
			WrapContext context = currentContext.findContext(name);
			if(context == null)
			{
				context = new WrapContext();
				context.name = name;

				context.parent = currentContext;
				currentContext.children.Add(name, context);
			}
			currentContext = context;

			//Parse lines
			while(true)
			{
				//Read next line
				if(!nextLine())
				{
					Console.Write("Expected closing bracket for namespace at line:" + lineIndex);
					return false;
				}

				//Check for closing bracket
				string firstWord = parseWord(new char[] { ' ' });
				if(String.IsNullOrWhiteSpace(firstWord) || firstWord.StartsWith("//"))
					continue;
				if(firstWord.StartsWith("}"))
				{
					//Type ended
					break;
				}

				//Read data
				switch(firstWord)
				{
				case "namespace":
				{
					if(!parseNamespace())
						return false;
					break;
				}
				case "type":
				{
					if(!parseType())
						return false;
					break;
				}
				case "class":
				{
					if(!parseClass())
						return false;
					break;
				}
				case "struct":
				{
					if(!parseStruct())
						return false;
					break;
				}
				case "enum":
				{
					if(!parseEnum())
						return false;
					break;
				}
				default:
				{
					Console.Write("Unknown data at line:" + lineIndex);
					return false;
				}
				}

			} //End while

			//Pop context
			currentContext = currentContext.parent;

			//Return
			return true;
		}

		//Parse Type
		public bool parseType()
		{
			//Name
			string name = parseWord();
			if(String.IsNullOrEmpty(name))
			{
				Console.Write("Unable to parse type name at line:" + lineIndex);
				return false;
			}

			//Opening bracket
			if(!nextLine() || !parseWord().StartsWith("{"))
			{
				Console.Write("Expected type opening bracket at line:" + lineIndex);
				return false;
			}

			//Create type
			WrapType wrapType = new WrapType();
			wrapType.name = name;
			wrapType.cppType = name;
			wrapType.cppInPass = name;
			wrapType.cppOutPass = name;
			wrapType.csType = name;
			wrapType.csInPass = name;
			wrapType.csOutPass = name;
			wrapType.parentContext = currentContext;

			//Read lines
			while(true)
			{
				//Read next line
				if(!nextLine())
				{
					Console.Write("Expected closing bracket for type at line:" + lineIndex);
					return false;
				}

				//Check for closing bracket
				string firstWord = parseWord(new char[] { ' ' });
				if(String.IsNullOrWhiteSpace(firstWord) || firstWord.StartsWith("//"))
					continue;
				if(firstWord.StartsWith("}"))
				{
					//Type ended
					break;
				}

				//Parse
				ParseResult result = parseTypeLine(wrapType, firstWord);
				if(result == ParseResult.ERROR)
					return false;
				else if(result == ParseResult.FOUND)
					continue;
			}

			//Add
			currentContext.types.Add(wrapType.name, wrapType);

			//Return
			return true;
		}
		public enum ParseResult
		{
			UNKNOWN,
			FOUND,
			ERROR
		}
		public ParseResult parseTypeLine(WrapType wrapType, string word)
		{
			//Check for options
			switch(word.ToLower())
			{
				
				case "csname":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.csName = temp;
					return ParseResult.FOUND;
				}
				case "cstype":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.csType = temp;
					return ParseResult.FOUND;
				}
				case "cspass":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.csInPass = temp;
					wrapType.csOutPass = temp;
					return ParseResult.FOUND;
				}
				case "cppname":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.cppName = temp;
					return ParseResult.FOUND;
				}
				case "cpptype":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.cppType = temp;
					return ParseResult.FOUND;
				}
				case "cpppass":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.cppInPass = temp;
					wrapType.cppOutPass = temp;
					return ParseResult.FOUND;
				}
				case "cppoutpass":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.cppOutPass = temp;
					return ParseResult.FOUND;
				}
				case "cppinpass":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.cppInPass = temp;
					return ParseResult.FOUND;
				}
				case "csoutpass":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.csOutPass = temp;
					return ParseResult.FOUND;
				}
				case "csinpass":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.csInPass = temp;
					return ParseResult.FOUND;
				}
				case "cppinclude":
				{
					string temp = parseLine();
					wrapType.cppInclude = temp;
					return ParseResult.FOUND;
				}
				case "cppinconvert":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.cppInConvert = temp;
					return ParseResult.FOUND;
				}
				case "cppoutconvert":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.cppOutConvert = temp;
					return ParseResult.FOUND;
				}
				case "csinconvert":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.csInConvert = temp;
					return ParseResult.FOUND;
				}
				case "csoutconvert":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return ParseResult.ERROR;
					}
					wrapType.csOutConvert = temp;
					return ParseResult.FOUND;
				}
			} //End case

			//Return
			return ParseResult.UNKNOWN;
		}

		//Parse Class
		public bool parseClass()
		{
			WrapClass wrapClass = new WrapClass();

			//Name
			wrapClass.name = parseWord(new char[] { ' ', ':' });
			if(String.IsNullOrEmpty(wrapClass.name))
			{
				Console.Write("Unable to parse type name at line:" + lineIndex);
				return false;
			}

			//Inheritence
			{
				string word = parseWord(new char[] { ' ' } );
				if(word == ":")
				{
					while(true)
					{
						//Find class name
						word = parseWord(new char[] { ',' });
						if(String.IsNullOrEmpty(word))
						{
							Console.Write("Expected inhertience class name at line" + lineIndex);
							return false;
						}

						//Store
						wrapClass.parentClass = word;

						//Skip
						if(!skipPast(','))
							break;
					}
				}
			}

			//Opening bracket
			if(!nextLine() || !parseWord().StartsWith("{"))
			{
				Console.Write("Expected type opening bracket at line:" + lineIndex);
				return false;
			}

			//Build qualified name
			string qualifiedName = "";
			{
				List<string> names = new List<string>();
				WrapContext tempContext = currentContext;
				while(tempContext != null)
				{
					//Add
					if(!String.IsNullOrEmpty(tempContext.name))
						names.Insert(0, tempContext.name);

					//Continue
					tempContext = tempContext.parent;
				}

				//Build name
				for(int i = 0; i < names.Count(); i++)
					qualifiedName += names[i] + ".";
				qualifiedName += wrapClass.name;
			}
			wrapClass.cppName = qualifiedName.Replace(".", "::");
			wrapClass.csName = qualifiedName;

			//Create context
			WrapContext context = new WrapContext();
			context.name = wrapClass.name;
			context.sourceType = wrapClass;
			context.parent = currentContext;
			context.parent.children.Add(wrapClass.name, context);
			currentContext = context;

			//Link
			wrapClass.parentContext = context.parent;
			wrapClass.context = context;

			//Read lines
			while(true)
			{
				//Read next line
				if(!nextLine())
				{
					Console.Write("Expected closing bracket for type at line:" + lineIndex);
					return false;
				}

				//Check for closing bracket
				string firstWord = parseWord(new char[] { ' ' });
				if(String.IsNullOrWhiteSpace(firstWord) || firstWord.StartsWith("//"))
					continue;
				if(firstWord.StartsWith("}"))
				{
					//Type ended
					break;
				}

				//Parse
				ParseResult result = parseTypeLine(wrapClass, firstWord);
				if(result == ParseResult.ERROR)
					return false;
				else if(result == ParseResult.FOUND)
					continue;

				//Parse
				if(!parseClassLine(wrapClass, firstWord))
					return false;
			}

			//Finalize
			//finalizeClass(wrapClass);

			//Pop Context
			currentContext = currentContext.parent;

			//Add
			generator.classes.Add(wrapClass);
			currentContext.types.Add(wrapClass.name, wrapClass);

			//Return
			return true;
		}
		public bool parseClassLine(WrapClass wrapType, string word)
		{
			//Parse options
			switch(word.ToLower())
			{
				case "interface":
				{
					if(!parseInterface(wrapType))
						return false;
					break;
				}
				case "cppconstruct":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return false;
					}
					wrapType.cppConstruct = temp;
					break;
				}
				case "cppdeconstruct":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return false;
					}
					wrapType.cppDeconstruct = temp;
					break;
				}
				case "cppnorefcount":
				{
					wrapType.cppConstruct = "$result = new $langtype($args);\n";
					wrapType.cppDeconstruct = "$result = ($langtype*)$input; delete $result;\n";
					wrapType.cppWrapperConstruct = "//No ref count\n";
					wrapType.explicitConstruction = true;
					wrapType.explicitDeconstruction = true;
					break;
				}
				case "cppnopass":
				{
					wrapType.cppCanBePassed = false;
					break;
				}
				case "func":
				{
					if(!parseFunc(wrapType))
						return false;
					break;
				}
				case "var":
				{
					WrapVariable variable = parseVar();
					if(variable == null)
						return false;
					wrapType.variables.Add(variable);
					break;
				}
				case "textbegin":
				{
					if(!parseTextBlock(wrapType))
						return false;
					break;
				}
				case "class":
				{
					if(!parseClass())
						return false;
					break;
				}
				case "struct":
				{
					if(!parseStruct())
						return false;
					break;
				}
				case "enum":
				{
					if(!parseEnum())
						return false;
					break;
				}
			} //End case

			//Return
			return true;
		}

		public bool parseFunc(WrapClass wrapClass)
		{
			//Method
			WrapMethod method = new WrapMethod();

			//Find opening of function
			string openFuncWord = parseWord(new char[] { '(' }, false);
			if(String.IsNullOrEmpty(openFuncWord))
			{
				Console.Write("Method malformed at line:" + lineIndex);
				return false;
			}

			//Options
			string word;
			if(openFuncWord.Contains(' '))
			{
				//Parse options
				while(true)
				{
					//Parse word
					word = parseWord(new char[] { ' ', '(' });
					if(String.IsNullOrEmpty(word))
					{
						Console.Write("Expected return type for function at line:" + lineIndex);
						return false;
					}

					//Check for options
					switch(word.ToLower())
					{
						case "static":
						{
							method.isStatic = true;
							continue;
						}
						case "virtual":
						{
							method.isVirtual = true;
							continue;
						}
						case "abstract":
						{
							method.isVirtual = true;
							method.isAbstract = true;
							continue;
						}
						case "unsafe":
						{
							method.isUnsafe = true;
							continue;
						}
					}

					//End
					break;
				}

				//Check for modifier
				/*if(word.IndexOfAny(new char[] {'*', '&'}) == 0)
				{
					method.returnArg.cppModifier = word.Substring(0, 1);
					word = word.Substring(1);
				}*/

				//Return type
				method.returnType = word;
			}
			else
				method.returnType = "void";

			//Check for return type
			/*if(openFuncWord.Contains(' '))
			{
				string returnType = parseWord(new char[] { ' ', '*', '&' });
				if(returnType == "static")
				{
					method.isStatic = true;
					returnType = parseWord(new char[] { ' ', '*', '&' });
					if(String.IsNullOrEmpty(returnType))
					{
						Console.Write("Unable to parse return type of func at line:" + lineIndex);
						return false;
					}
				}

				//Set
				method.returnType = returnType;
			}
			else
				method.returnType = "void";*/

			//Skip
			skipPast(new char[] { ' ', '*', '&' });

			//Method name
			string methodName = parseWord(new char[1] { '(' });
			if(String.IsNullOrEmpty(methodName))
			{
				Console.Write("Unable to parse method name type of func at line:" + lineIndex);
				return false;
			}
			method.name = methodName;

			//Arguments
			if(!parseArgs(method))
				return false;

			//End options
			word = parseWord(new char[] { ' ', ';' });
			while(!String.IsNullOrEmpty(word))
			{
				//Options
				switch(word.ToLower())
				{
					case "const":
					{
						method.isConst = true;
						break;
					}
					case ";":
					{
						//Nothing
						break;
					}
					default:
					{
						Console.Write("Unexpected postfix on end of method declaration at line:" + lineIndex);
						return false;
					}
				}

				//Continue
				word = parseWord(new char[] { ' ', ';' });
			}

			//Add
			if(method.name == wrapClass.name)
			{
				wrapClass.constructors.Add(method);
			}
			else
			{
				if(string.IsNullOrEmpty(method.returnType))
				{
					Console.Write("Method does not have return type at line:" + lineIndex);
					return false;
				}
				wrapClass.methods.Add(method);
			}

			//Return
			return true;
		}
		public bool parseArgs(WrapMethod method)
		{
			//Arguments
			if(!skipPast('('))
				return false;

			//Find closing
			int endIndex = line.IndexOf(')');
			if(endIndex < 0)
			{
				Console.Write("Unable to find closing parenthese at line:" + lineIndex);
				return false;
			}

			//Check if void
			string arguments = line.Substring(charIndex, (endIndex - charIndex));
			if(arguments == "void" || arguments == "")
			{
				charIndex = endIndex + 1;
				return true;
			}

			//Arguments
			while(true)
			{
				WrapMethodArg wrapArg = new WrapMethodArg();

				//Type
				string word = parseWord(new char[] { ' ', '*', '&', });
				if(String.IsNullOrEmpty(word))
				{
					Console.Write("Unable to parse argument type at line:" + lineIndex);
					return false;
				}

				//In/Out Options
				string wordLower = word.ToLower();
				if(wordLower == "in" || wordLower == "out" || wordLower == "inout")
				{
					//Set direction
					if(wordLower == "in")
					{
						wrapArg.moveIn = true;
						wrapArg.moveOut = false;
					}
					else if(wordLower == "out")
					{
						wrapArg.moveIn = false;
						wrapArg.moveOut = true;
					}
					else if(wordLower == "inout")
					{
						wrapArg.moveIn = true;
						wrapArg.moveOut = true;
					}

					//Read name
					word = parseWord(new char[] { ' ', ')', ',', '&', '*' });
					if(String.IsNullOrEmpty(word))
					{
						Console.Write("Unable to parse argument type at line:" + lineIndex);
						return false;
					}
				}
				wrapArg.type = word;

				//Is CPP Reference
				word = parseWord(new char[] { ' ', ',', ')' }, false);
				if(word == "&")
				{
					Console.Write("word:" + word);
					wrapArg.cppRef = true;
				}

				//Argument Name
				skipPast(new char[] { ' ', '*', '&' });
				wrapArg.name = parseWord(new char[] { ' ', ')', ',', '=' });
				if(String.IsNullOrEmpty(wrapArg.name))
				{
					Console.Write("Unable to parse argument name at line:" + lineIndex);
					return false;
				}

				//Default Value
				skipWhitespace();
				word = peekChars(1, false);
				if(word == "=")
				{
					seekLine(1);
					word = parseWord(new char[] { ',', ')' });
					wrapArg.defaultValue = word;
				}

				//Create
				method.args.Add(wrapArg);

				//Skip until next argument
				skipPast(new char[2] { ' ', ',' });

				//Check if complete
				if(charIndex >= line.Length || line[charIndex] == ')')
				{
					charIndex += 1;
					break;
				}
			}

			//Return
			return true;
		}
		public WrapVariable parseVar()
		{
			WrapVariable variable = new WrapVariable();

			//Parse options
			string word;
			while(true)
			{
				//Parse word
				word = parseWord(new char[] { ' ' });
				if(String.IsNullOrEmpty(word))
				{
					Console.Write("Unable to parse var type at line:" + lineIndex);
					return null;
				}

				//Check for option
				switch(word.ToLower())
				{
					case "readonly":
					{
						variable.setBody = null;
						continue;
					}
					case "writeonly":
					{
						variable.getBody = null;
						continue;
					}
				}

				//Otherwise break
				break;
			}

			//Parse ref type
			string varRefType = new string(word[word.Count() - 1], 1);
			if(varRefType == "&")
			{
				word = word.Substring(0, word.Count() - 1);
				//Get
				if(!String.IsNullOrEmpty(variable.getBody))
					variable.getBody = "$result = &($obj->$varname);";
				//Set
				if(!String.IsNullOrEmpty(variable.setBody))
					variable.setBody = "$obj->$varname = *($input);";
			}

			//Parse name
			string varName = parseWord(new char[] { ';' });
			if(String.IsNullOrEmpty(word))
			{
				Console.Write("Unable to parse var name at line:" + lineIndex);
				return null;
			}

			//Create
			variable.type = word;
			//variable.refType = varRefType;
			variable.name = varName;

			//Peek at next line
			if(nextLine() && parseWord(null, false) == "{")
			{
				variable.getBody = null;
				variable.setBody = null;

				//Reset
				while(true)
				{
					//Next line
					if(!nextLine())
					{
						Console.Write("Unexpected end of file, expected closing bracket to variable def at line:" + lineIndex);
						return null;
					}

					//Check word
					word = parseWord();
					if(word == "get")
					{
						variable.getBody = parseLine();
					}
					else if(word == "set")
					{
						variable.setBody = parseLine();
					}
					else if(word == "}")
					{
						//We are completed
						break;
					}
				} //End while
			}
			else
				prevLine();

			//Return
			return variable;
		}
		public bool parseInterface(WrapClass wrapClass)
		{
			string temp = parseWord();
			if(temp == "cpptocs")
			{
				wrapClass.interfaceType = WrapClass.InterfaceType.CPP_TO_CSHARP;
			}
			else if(temp == "cstocpp")
			{
				wrapClass.interfaceType = WrapClass.InterfaceType.CSHARP_TO_CPP;
			}
			else
			{
				Console.Write("Unknown class interface type at line:" + lineIndex);
				return false;
			}

			//Return
			return true;
		}
		public bool parseTextBlock(WrapType wrapType)
		{
			//Parse text block
			StringBuilder buffer = new StringBuilder();
			while(true)
			{
				//Get next line
				if(!nextLine())
				{
					Console.Write("Unexpected end of file when reading text block at line:" + lineIndex);
					return false;
				}

				//Check if end
				if(parseWord(null, false) == "textend")
					break;

				//Append
				buffer.Append(line);
				buffer.Append("\n");
			}

			//Add
			wrapType.textBlocks.Add(buffer.ToString());

			//Return
			return true;
		}
		/*public bool parseNamespace(List<string> namespaces)
		{
			//Get line
			string temp = parseWord();
			if(String.IsNullOrEmpty(temp))
			{
				Console.Write("Error parsing on line:" + lineIndex);
				return false;
			}

			//Separate
			string[] stringSeparators = new string[] { "::" };
			string[] split = temp.Split(stringSeparators, StringSplitOptions.None);

			//Add
			namespaces.Clear();
			for(int i = 0; i < split.Count(); i++)
				namespaces.Add(split[i]);

			//Return
			return true;
		}*/
		public void finalizeClass(WrapClass wrapClass)
		{
			//Check for class
			if(wrapClass == null)
				return;
			WrapType wrapType = wrapClass;

			if(wrapClass.interfaceType == WrapClass.InterfaceType.CPP_TO_CSHARP)
			{
				//cppType
				if(String.IsNullOrEmpty(wrapType.cppType))
					wrapType.cppType = wrapClass.cppName + "*";

				//cppInPass
				if(String.IsNullOrEmpty(wrapType.cppInPass))
					wrapType.cppInPass = wrapClass.cppName + "*";

				//cppOutPass
				if(String.IsNullOrEmpty(wrapType.cppOutPass))
					wrapType.cppOutPass = "MonoObject*";

				//csType
				if(String.IsNullOrEmpty(wrapType.csType))
					wrapType.csType = wrapClass.csName;

				//csInPass
				if(String.IsNullOrEmpty(wrapType.csInPass))
					wrapType.csInPass = wrapType.csName;

				//csOutPass
				if(String.IsNullOrEmpty(wrapType.csOutPass))
					wrapType.csOutPass = "IntPtr";

				//Construct
				if(String.IsNullOrEmpty(wrapClass.cppConstruct))
					wrapClass.cppConstruct = "$result = new $langtype($args);\n" + generator.cppIncreaseRef + "\n";
				if(String.IsNullOrEmpty(wrapClass.cppDeconstruct))
					wrapClass.cppDeconstruct = "$result = ($langtype*)$input;\n if($result) { " + generator.cppDecreaseRef + " }\n";
				if(String.IsNullOrEmpty(wrapClass.cppWrapperConstruct))
					wrapClass.cppWrapperConstruct = generator.cppIncreaseRef + "\n";

				//Convert
				wrapType.cppOutConvert = "$result = CppCSharpBridge::ConvertObjectOut(($langtype*)$input);";
				wrapType.csOutConvert = "$result = $input != null ? $input.getCPtr() : IntPtr.Zero;";

				//Type
				if(generator.findIfPolymorphic(wrapClass.uniqueName) || (wrapClass.constructors.Count > 0 && !wrapClass.explicitConstruction))
				{
					wrapClass.type = "class";
				}
				else
				{
					//Remove wrapper construct
					wrapClass.cppWrapperConstruct = "//No ref count";

					//Pass by IntPtr
					wrapType.cppOutPass = wrapClass.cppName + "*";
					wrapType.csInPass = "IntPtr";

					//Conversion
					wrapType.cppOutConvert = "";
					wrapType.csInConvert = "$result = new $langtype($input);";
				}
			}
			else
			{
				//Type
				wrapClass.type = "class";

				//cppInclude
				if(String.IsNullOrEmpty(wrapType.cppInclude))
					wrapType.cppInclude = "#include \"" + wrapType.name + "\"";

				//cppType
				if(String.IsNullOrEmpty(wrapType.cppType))
					wrapType.cppType = wrapClass.cppName + "*";

				//cppInPass
				if(String.IsNullOrEmpty(wrapType.cppInPass))
					wrapType.cppInPass = "MonoObject*";
				if(String.IsNullOrEmpty(wrapType.cppOutPass))
					wrapType.cppOutPass = "MonoObject*";

				//csType
				if(String.IsNullOrEmpty(wrapType.csType))
					wrapType.csType = wrapClass.csName;

				//csInPass
				if(String.IsNullOrEmpty(wrapType.csInPass))
					wrapType.csInPass = wrapClass.csName;
				if(String.IsNullOrEmpty(wrapType.csOutPass))
					wrapType.csOutPass = wrapClass.csName;

				//Conversion
				wrapType.cppInConvert = "$result = ($input == nullptr) ? nullptr : new $langtype($input);\n";
				//wrapType.cppOutConvert = "$result = $input->getCPtr();";
			}

			//Remove
			wrapClass = null;
			wrapType = null;
		}

		//Parse Enum
		public bool parseEnum()
		{
			//Create type
			WrapEnum wrapEnum = new WrapEnum();

			//Name
			wrapEnum.name = parseWord(new char[] { ' ', ':' });
			if(String.IsNullOrEmpty(wrapEnum.name))
			{
				Console.Write("Unable to parse type name at line:" + lineIndex);
				return false;
			}

			//Inheritence
			{
				string word = parseWord(new char[] { ' ' });
				if(word == ":")
				{
					while(true)
					{
						//Find class name
						word = parseWord(new char[] { ',' });
						if(String.IsNullOrEmpty(word))
						{
							Console.Write("Expected inhertience class name at line" + lineIndex);
							return false;
						}

						//Store
						wrapEnum.enumType = word;

						//Skip
						if(!skipPast(','))
							break;
					}
				}
			}

			//Opening bracket
			if(!nextLine() || !parseWord().StartsWith("{"))
			{
				Console.Write("Expected type opening bracket at line:" + lineIndex);
				return false;
			}

			//Build qualified name
			/*string qualifiedName = "";
			{
				List<string> names = new List<string>();
				WrapContext tempContext = currentContext;
				while(tempContext != null)
				{
					//Add
					if(!String.IsNullOrEmpty(tempContext.name))
						names.Insert(0, tempContext.name);

					//Continue
					tempContext = tempContext.parent;
				}

				//Build name
				for(int i = 0; i < names.Count(); i++)
					qualifiedName += names[i] + ".";
				qualifiedName += wrapEnum.name;
			}
			wrapEnum.cppName = qualifiedName.Replace(".", "::");
			wrapEnum.csName = qualifiedName;

			wrapEnum.cppType = wrapEnum.cppName;*/
			wrapEnum.cppInPass = "int";
			wrapEnum.cppOutPass = "int";
			wrapEnum.csInPass = "int";
			wrapEnum.csOutPass = "int";
			wrapEnum.cppInConvert = "$result = ($langtype)$input;";
			wrapEnum.cppOutConvert = "$result = (int)$input;";
			wrapEnum.csInConvert = "$result = ($langtype)$input;";
			wrapEnum.csOutConvert = "$result = (int)$input;";

			//Set context
			wrapEnum.parentContext = currentContext;

			//Read lines
			while(true)
			{
				//Read next line
				if(!nextLine())
				{
					Console.Write("Expected closing bracket for type at line:" + lineIndex);
					return false;
				}

				//Check for closing bracket
				string firstWord = parseWord(new char[] { ' ' });
				if(String.IsNullOrWhiteSpace(firstWord) || firstWord.StartsWith("//"))
					continue;
				if(firstWord.StartsWith("}"))
				{
					//Type ended
					break;
				}

				//Parse
				ParseResult result = parseTypeLine(wrapEnum, firstWord);
				if(result == ParseResult.ERROR)
					return false;
				else if(result == ParseResult.FOUND)
					continue;

				//Parse
				{
					//Reset
					resetLine();
					if(!parseEnumVar(wrapEnum))
						return false;
				}
			}

			//Add
			generator.enums.Add(wrapEnum);
			currentContext.types.Add(wrapEnum.name, wrapEnum);

			//Return
			return true;
		}
		public bool parseEnumVar(WrapEnum wrapEnum)
		{
			string word = parseWord(new char[] { ',', ' ', '=', '\t' });

			//Var
			WrapEnumVar var = new WrapEnumVar();
			var.name = word;

			//Check next word
			word = parseWord(new char[] { ' ', '\t' });
			if(word == "=")
			{
				//Value
				word = parseWord(new char[] { ',' });
				var.value = word;
			}

			//Add
			wrapEnum.vars.Add(var);

			//Return
			return true;
		}
		public void finalizeEnum(WrapEnum wrapEnum)
		{
			//Names
			wrapEnum.csName = wrapEnum.parentContext.sourceType.csName + "." + wrapEnum.name;
			wrapEnum.csType = wrapEnum.csName;
			wrapEnum.cppName = wrapEnum.parentContext.sourceType.cppName + "::" + wrapEnum.name;
			wrapEnum.cppType = wrapEnum.cppName;
		}

		//Parse Struct
		public bool parseStruct()
		{
			//Name
			string name = parseWord();
			if(String.IsNullOrEmpty(name))
			{
				Console.Write("Unable to parse type name at line:" + lineIndex);
				return false;
			}

			//Opening bracket
			if(!nextLine() || !parseWord().StartsWith("{"))
			{
				Console.Write("Expected type opening bracket at line:" + lineIndex);
				return false;
			}

			//Create type
			WrapStruct wrapType = new WrapStruct();
			wrapType.name = name;

			wrapType.cppType = "$langtype";
			wrapType.cppInPass = "$langtype";
			wrapType.cppOutPass = "$langtype";

			wrapType.csType = "$langtype";
			wrapType.csInPass = "$langtype";
			wrapType.csOutPass = "$langtype";

			//Create context
			WrapContext context = new WrapContext();
			context.name = wrapType.name;
			context.sourceType = wrapType;
			context.parent = currentContext;
			context.parent.children.Add(wrapType.name, context);
			currentContext = context;

			//Link
			wrapType.parentContext = context.parent;
			wrapType.context = context;

			//Build qualified name
			string qualifiedName = "";
			{
				List<string> names = new List<string>();
				WrapContext tempContext = currentContext.parent;
				while(tempContext != null)
				{
					//Add
					if(!String.IsNullOrEmpty(tempContext.name))
						names.Insert(0, tempContext.name);

					//Continue
					tempContext = tempContext.parent;
				}

				//Build name
				for(int i = 0; i < names.Count(); i++)
					qualifiedName += names[i] + ".";
				qualifiedName += wrapType.name;
			}
			wrapType.cppName = qualifiedName.Replace(".", "::");
			wrapType.csName = qualifiedName;

			//Read lines
			while(true)
			{
				//Read next line
				if(!nextLine())
				{
					Console.Write("Expected closing bracket for type at line:" + lineIndex);
					return false;
				}

				//Check for closing bracket
				string firstWord = parseWord(new char[] { ' ' });
				if(String.IsNullOrWhiteSpace(firstWord) || firstWord.StartsWith("//"))
					continue;
				if(firstWord.StartsWith("}"))
				{
					//Type ended
					break;
				}

				//Parse
				ParseResult result = parseTypeLine(wrapType, firstWord);
				if(result == ParseResult.ERROR)
					return false;
				else if(result == ParseResult.FOUND)
					continue;

				//Parse
				if(!parseStructLine(wrapType, firstWord))
					return false;
			}

			//Finalize
			finalizeStruct(wrapType);

			//Pop Context
			currentContext = currentContext.parent;

			//Add
			generator.structs.Add(wrapType);
			currentContext.types.Add(wrapType.name, wrapType);

			//Return
			return true;
		}
		public bool parseStructLine(WrapStruct wrapType, string word)
		{
			//Data
			switch(word.ToLower())
			{
				case "cppoutstruct":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return false;
					}
					wrapType.cppName = temp;
					break;
				}
				case "cppinstruct":
				{
					string temp = parseLine();
					if(String.IsNullOrEmpty(temp))
					{
						Console.Write("Error parsing on line:" + lineIndex);
						return false;
					}
					wrapType.cppName = temp;
					break;
				}
				case "var":
				{
					WrapVariable variable = parseVar();
					if(variable == null)
						return false;
					wrapType.variables.Add(variable);
					break;
				}
				case "textbegin":
				{
					if(!parseTextBlock(wrapType))
						return false;
					break;
				}
				case "enum":
				{
					if(!parseEnum())
						return false;
					break;
				}
			} //End While

			//Return
			return true;
		}
		public void finalizeStruct(WrapStruct wrapType)
		{
			//C++
			finalizeTypeVar(ref wrapType.cppType, wrapType, wrapType.cppName);
			finalizeTypeVar(ref wrapType.cppInPass, wrapType, wrapType.cppName);
			finalizeTypeVar(ref wrapType.cppOutPass, wrapType, wrapType.cppName);

			//C#
			finalizeTypeVar(ref wrapType.csType, wrapType, wrapType.csName);
			finalizeTypeVar(ref wrapType.csInPass, wrapType, wrapType.csName);
			finalizeTypeVar(ref wrapType.csOutPass, wrapType, wrapType.csName);
		}
		public void finalizeTypeVar(ref string input, WrapType type, string langtype)
		{
			//Check if null
			if(String.IsNullOrEmpty(input))
				return;

			//Replace
			input = input.Replace("$langtype", langtype);
		}

	} //End class
}
