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

#include <stddef.h>
#include <stdlib.h>
#include <stdio.h>

#include <dnne.h>

#ifdef _WIN32
#include <Windows.h>

static void* load_library(const char* path)
{
    HMODULE h = LoadLibraryA(path);
    return (void*)h;
}
static void* get_export(void* h, const char* name)
{
    void* f = GetProcAddress((HMODULE)h, name);
    return f;
}

#else
#include <dlfcn.h>
#include <limits.h>

static void* load_library(const char* path)
{
    void* h = dlopen(path, RTLD_LAZY | RTLD_LOCAL);
    return h;
}
static void* get_export(void* h, const char* name)
{
    void* f = dlsym(h, name);
    return f;
}

#endif

#define RETURN_FAIL_IF_FALSE(exp, msg) { if (!(exp)) { printf(msg); return EXIT_FAILURE; } }

typedef int(DNNE_CALLTYPE* IntIntInt_t)(int,int);

struct T { int a; int b; int c; };
typedef int (DNNE_CALLTYPE* ReturnDataCMember_t)(struct T);
typedef int (DNNE_CALLTYPE* ReturnRefDataCMember_t)(struct T*);

typedef void (DNNE_CALLTYPE* set_failure_callback_t)(failure_fn cb);
typedef void (DNNE_CALLTYPE* preload_runtime_t)(void);
typedef int (DNNE_CALLTYPE* try_preload_runtime_t)(void);

static void DNNE_CALLTYPE on_failure(enum failure_type type, int error_code)
{
    printf("FAILURE: Type: %d, Error code: %08x\n", type, error_code);
}

int main(int ac, char** av)
{
    void* mod = load_library(av[1]);
    RETURN_FAIL_IF_FALSE(mod, "Failed to load library\n");

    set_failure_callback_t set_cb = (set_failure_callback_t)get_export(mod, "set_failure_callback");
    RETURN_FAIL_IF_FALSE(set_cb, "Failed to get set_failure_callback export\n");
    set_cb(on_failure);

    {
        try_preload_runtime_t try_preload = (try_preload_runtime_t)get_export(mod, "try_preload_runtime");
        RETURN_FAIL_IF_FALSE(try_preload, "Failed to get try_preload_runtime export\n");
        int ret = try_preload();
        RETURN_FAIL_IF_FALSE(ret == DNNE_SUCCESS, "try_preload_runtime failed\n");
    }

    {
        preload_runtime_t preload = (preload_runtime_t)get_export(mod, "preload_runtime");
        RETURN_FAIL_IF_FALSE(preload, "Failed to get preload_runtime export\n");
        preload();
    }

    {
        IntIntInt_t fptr = NULL;
        int a = 3;
        int b = 5;
        int c = -1;

        fptr = (IntIntInt_t)get_export(mod, "IntIntInt");
        RETURN_FAIL_IF_FALSE(fptr, "Failed to get IntIntInt export\n");

        c = fptr(a, b);
        printf("IntIntInt(%d, %d) = %d\n", a, b, c);

        fptr = (IntIntInt_t)get_export(mod, "UnmanagedIntIntInt");
        RETURN_FAIL_IF_FALSE(fptr, "Failed to get UnmanagedIntIntInt export\n");

        c = fptr(a, b);
        printf("UnmanagedIntIntInt(%d, %d) = %d\n", a, b, c);
    }

    int expected = 12345;
    struct T t;
    {
        t.a = -1;
        t.b = -1;
        t.c = expected;
        ReturnDataCMember_t fptr = (ReturnDataCMember_t)get_export(mod, "ReturnDataCMember");
        RETURN_FAIL_IF_FALSE(fptr, "Failed to get ReturnDataCMember export\n");

        int c = fptr(t);
        printf("ReturnDataCMember(struct T{ %d }) = %d\n", expected, c);
    }

    {
        t.a = -1;
        t.b = -1;
        t.c = expected;
        ReturnRefDataCMember_t fptr = (ReturnRefDataCMember_t)get_export(mod, "ReturnRefDataCMember");
        RETURN_FAIL_IF_FALSE(fptr, "Failed to get ReturnRefDataCMember export\n");

        int c = fptr(&t);
        printf("ReturnRefDataCMember(struct T*{ %d }) = %d\n", expected, c);
    }

    return EXIT_SUCCESS;
}
