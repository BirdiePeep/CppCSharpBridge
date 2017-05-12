# Introduction

The "type" type is base that is used for every other type.  By itself it doesn't represent any kind of concept in any language, however we can use it to bridge many types of things.  Every other type inherits all options that "type" has, so it's good to have a clear understanding of it from the start.

# Example

```
type uint8
{
    cpptype uint8_t
    cpppass uint8_t
    cstype byte
    cspass byte
}
```

With the above defined in our interface file, we can now use "uint8" to pass around this data.

# Options

When you define a type there are many options available that change how it's handled inside of the build tool.  Keep in mind that all options are not case sensitive, so use whatever scheme fits your fancy, in my examples I will be using all lower case.

Below is the current list of available options to the "type" type.

* cppname
* cpptype
* cpppass
* cppoutpass
* cppinpass
* cppoutconvert
* cppinconvert
* cppinclude
* csname
* cstype
* cspass
* csoutpass
* csinpass
* csoutconvert
* csinconvert

I will now explain in more detail what each of these mean.

* cppname
* csname

These options tells the build tool what text is used to refer to this type in either language.  If no option is provided, it uses the global name you specified for the type.

* cpptype
* cstype

Similar to the above, except this is the text used when declaring a variable of that type in a language.  For example a class might have a "cppname" of "Clock" and a "cpptype" of "Clock*".

* cpppass
* cspass

This specifies the variable type used when passing between the languages.  For example an enum might have a "cstype" of "AnimalType" but have a "cspass" of type "int".

* cppoutpass
* cppinpass
* csoutpass
* csinpass

These are related to the above, they allow you to specifiy a different type depending on direction of the variable.  This is mostly used internally and you shouldn't need to touch it.

* cppinclude

This tells the build tool the include text needed to refer to this type inside of C++.  For example.

```
type MyType
{
  cppinclude #include "types.h"
}
```

* cppoutconvert
* cppinconvert
* csoutconvert
* csinconvert

These are specially formatted strings that tell the build tool how we convert to and from the types depending on the direction the type is going.  In most cases you shouldn't need to touch these.

Below is an example of how we use these options to map the string classes between languages.

```
type string
{
    cpptype ::std::string
    cpppass MonoString*
    cppinconvert char* temp_$input = mono_string_to_utf8($input); $result = temp_$input; mono_free(temp_$input);
    cppoutconvert $result = mono_string_new(mono_domain_get(), $input.c_str());
	
    cstype string
    cspass string
}

type charptr
{
    cpptype const char*
    cpppass MonoString*
    cppinconvert ::std::string temp_str_$input; if($input) { char* temp_$input = mono_string_to_utf8($input); temp_str_$input = temp_$input; $result = temp_str_$input.c_str(); mono_free(temp_$input); } else { $result = nullptr;}
    cppoutconvert $result = mono_string_new(mono_domain_get(), $input);
	
    cstype string
    cspass string
}
```

As you can see convert statements are all done on one line, this is currently a restriction based on parsing.  You can also see that special tokens are used such as "$input" and "$result".  These tokens will be replaced with contexual information at time of generation.  Below are all the tokens used in these statements.

* $input - The name of the variable that is being passed in. This is what needs converted.
* $result - The name of the final converted variable.  This is where you will be converting to.
* $langtype - The language type, the same as the "cppname" or "csname"
* $vartype - The variable type, the same as the "cpptype" or "cstype"
* $wraptype - This is the type's un-qualified name as defined in the interface files. I.E. "Timestamp"
* $uniquename - This is a type's name that is guarenteed to be unique from all other types.  Right now we do this by fully qualifying it. I.E. "Game_Clock_TimeStamp"
