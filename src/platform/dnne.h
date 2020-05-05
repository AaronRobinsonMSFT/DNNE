// Copyright 2020 Aaron R Robinson
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is furnished
// to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
// PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#ifndef __SRC_PLATFORM_DNNE_H__
#define __SRC_PLATFORM_DNNE_H__

// Check if we are on Windows
#ifdef _WIN32
    #define DNNE_WINDOWS
#endif

// Define some platform macros
#ifdef DNNE_WINDOWS
    #define DNNE_API __declspec(dllexport)
    #define DNNE_CALLTYPE __stdcall
    #define DNNE_CALLTYPE_CDECL __cdecl
    #define DNNE_CALLTYPE_STDCALL __stdcall
    #define DNNE_CALLTYPE_THISCALL __thiscall
    #define DNNE_CALLTYPE_FASTCALL __fastcall
    #define _DNNE_STR(s1) L ## s1
    #define DNNE_STR(s) _DNNE_STR(s)
#else
    #define DNNE_API __attribute__((__visibility__("default")))
    #define DNNE_CALLTYPE 
    #define DNNE_CALLTYPE_CDECL 
    #define DNNE_CALLTYPE_STDCALL __attribute__((stdcall))
    #define DNNE_CALLTYPE_THISCALL __attribute__((thiscall))
    #define DNNE_CALLTYPE_FASTCALL __attribute__((fastcall))
    #define DNNE_STR(s) s
#endif

#ifndef DNNE_CONSUME_EXPORT_API
    // Must define the assembly name
    #ifndef DNNE_ASSEMBLY_NAME
        #error Target assembly name must be defined. Set 'DNNE_ASSEMBLY_NAME'.
    #endif

    // Include the official nethost API and indicate
    // consumption should be as a static library.
    #define NETHOST_USE_AS_STATIC
    #include <nethost.h>
#endif

//
// Public exports
//
enum failure_type
{
    failure_load_runtime = 1,
    failure_load_export,
};
typedef void (DNNE_CALLTYPE* failure_fn)(enum failure_type type, int error_code);

// Provide a callback for any catastrophic failures.
// The provided callback will be the last call prior to a rude-abort of the process.
DNNE_API void DNNE_CALLTYPE set_failure_callback(failure_fn cb);

#endif // __SRC_PLATFORM_DNNE_H__
