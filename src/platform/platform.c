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
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <assert.h>

#include "dnne.h"

// Modified copy of official hostfxr.h
#ifndef __HOSTFXR_H__

#ifdef DNNE_WINDOWS
#define HOSTFXR_CALLTYPE __cdecl
#else
#define HOSTFXR_CALLTYPE
#endif

enum hostfxr_delegate_type
{
    hdt_com_activation,
    hdt_load_in_memory_assembly,
    hdt_winrt_activation,
    hdt_com_register,
    hdt_com_unregister,
    hdt_load_assembly_and_get_function_pointer
};

typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_main_fn)(const int argc, const char_t** argv);
typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_main_startupinfo_fn)(
    const int argc,
    const char_t** argv,
    const char_t* host_path,
    const char_t* dotnet_root,
    const char_t* app_path);

typedef void(HOSTFXR_CALLTYPE* hostfxr_error_writer_fn)(const char_t* message);
typedef hostfxr_error_writer_fn(HOSTFXR_CALLTYPE* hostfxr_set_error_writer_fn)(hostfxr_error_writer_fn error_writer);

typedef void* hostfxr_handle;
typedef struct hostfxr_initialize_parameters
{
    size_t size;
    const char_t* host_path;
    const char_t* dotnet_root;
} hostfxr_initialize_parameters;

typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_initialize_for_dotnet_command_line_fn)(
    int argc,
    const char_t** argv,
    const hostfxr_initialize_parameters* parameters,
    /*out*/ hostfxr_handle* host_context_handle);
typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_initialize_for_runtime_config_fn)(
    const char_t* runtime_config_path,
    const hostfxr_initialize_parameters* parameters,
    /*out*/ hostfxr_handle* host_context_handle);

typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_get_runtime_property_value_fn)(
    const hostfxr_handle host_context_handle,
    const char_t* name,
    /*out*/ const char_t** value);
typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_set_runtime_property_value_fn)(
    const hostfxr_handle host_context_handle,
    const char_t* name,
    const char_t* value);
typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_get_runtime_properties_fn)(
    const hostfxr_handle host_context_handle,
    /*inout*/ size_t* count,
    /*out*/ const char_t** keys,
    /*out*/ const char_t** values);

typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_run_app_fn)(const hostfxr_handle host_context_handle);
typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_get_runtime_delegate_fn)(
    const hostfxr_handle host_context_handle,
    enum hostfxr_delegate_type type,
    /*out*/ void** delegate);

typedef int32_t(HOSTFXR_CALLTYPE* hostfxr_close_fn)(const hostfxr_handle host_context_handle);
#endif // __HOSTFXR_H__

// Modified copy of official coreclr_delegates.h
#ifndef __CORECLR_DELEGATES_H__

#if defined(DNNE_WINDOWS)
#define CORECLR_DELEGATE_CALLTYPE __stdcall
#else
#define CORECLR_DELEGATE_CALLTYPE
#endif

#define UNMANAGEDCALLERSONLY_METHOD ((const char_t*)-1)

// Signature of delegate returned by coreclr_delegate_type::load_assembly_and_get_function_pointer
typedef int (CORECLR_DELEGATE_CALLTYPE *load_assembly_and_get_function_pointer_fn)(
    const char_t *assembly_path      /* Fully qualified path to assembly */,
    const char_t *type_name          /* Assembly qualified type name */,
    const char_t *method_name        /* Public static method name compatible with delegateType */,
    const char_t *delegate_type_name /* Assembly qualified delegate type name or null
                                        or UNMANAGEDCALLERSONLY_METHOD if the method is marked with
                                        the UnmanagedCallersOnlyAttribute. */,
    void         *reserved           /* Extensibility parameter (currently unused and must be 0) */,
    /*out*/ void **delegate          /* Pointer where to store the function pointer result */);

// Signature of delegate returned by load_assembly_and_get_function_pointer_fn when delegate_type_name == null (default)
typedef int (CORECLR_DELEGATE_CALLTYPE* component_entry_point_fn)(void* arg, int32_t arg_size_in_bytes);

#endif // __CORECLR_DELEGATES_H__

//
// Platform definitions
//

#ifndef NDEBUG
#define DNNE_DEBUG
#endif

#define DNNE_SUCCESS 0
#define DNNE_MAX_PATH 512
#define DNNE_ARRAY_SIZE(_array) (sizeof(_array) / sizeof(*_array))

#define DNNE_TOSTRING2(s) #s
#define DNNE_TOSTRING(s) DNNE_TOSTRING2(s)

#ifdef DNNE_WINDOWS

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <Windows.h>

#define DNNE_NORETURN __declspec(noreturn)
#define DNNE_DIR_SEPARATOR L'\\'

static void* load_library(const char_t* path)
{
    assert(path != NULL);
    HMODULE h = LoadLibraryW(path);
    assert(h != NULL);
    return (void*)h;
}
static void* get_export(void* h, const char* name)
{
    assert(h != NULL && name != NULL);
    void* f = GetProcAddress((HMODULE)h, name);
    assert(f != NULL);
    return f;
}

static int get_this_image_path(int32_t len, char_t* buffer, int32_t* written)
{
    assert(0 < len && buffer != NULL && written != NULL);

    HMODULE hmod;
    if (FALSE == GetModuleHandleExW(
        GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPWSTR)&get_this_image_path,
        &hmod))
    {
        return (int)GetLastError();
    }

    DWORD len_local = GetModuleFileNameW(hmod, buffer, (DWORD)len);
    if (len_local == 0)
    {
        return (int)GetLastError();
    }
    else if (len_local == len)
    {
        return ERROR_INSUFFICIENT_BUFFER;
    }

    *written = len_local;
    return DNNE_SUCCESS;
}

#else

#include <dlfcn.h>
#include <limits.h>
#include <string.h>
#include <sys/errno.h>

#define DNNE_NORETURN __attribute__((__noreturn__))
#define DNNE_DIR_SEPARATOR '/'

static void* load_library(const char_t* path)
{
    assert(path != NULL);
    void* h = dlopen(path, RTLD_LAZY | RTLD_LOCAL);
    assert(h != NULL);
    return h;
}
static void* get_export(void* h, const char* name)
{
    assert(h != NULL && name != NULL);
    void* f = dlsym(h, name);
    assert(f != NULL);
    return f;
}

static int get_this_image_path(int32_t len, char_t* buffer, int32_t* written)
{
    assert(0 < len && buffer != NULL && written != NULL);

    Dl_info info;
    if (dladdr((void *)&get_this_image_path, &info) == 0)
        return -1;

    if (info.dli_fname == NULL)
        return -2;

    size_t len_local = strlen(info.dli_fname);
    if (len <= len_local)
        return ENOBUFS;

    // Copy over the null as well.
    memcpy(buffer, info.dli_fname, len_local + 1);

    *written = (int32_t)len;
    return DNNE_SUCCESS;
}

#endif // !DNNE_WINDOWS

static failure_fn failure_fptr;

DNNE_API void DNNE_CALLTYPE set_failure_callback(failure_fn cb)
{
    failure_fptr = cb;
}

DNNE_NORETURN static void noreturn_runtime_load_failure(int error_code)
{
    if (failure_fptr)
        failure_fptr(failure_load_runtime, error_code);

    // Nothing to do if the runtime failed to load.
    abort();
}
DNNE_NORETURN static void noreturn_export_load_failure(int error_code)
{
    if (failure_fptr)
        failure_fptr(failure_load_export, error_code);

    // Nothing to do if the export didn't exist.
    abort();
}

static bool is_failure(int ec)
{
    return (ec != DNNE_SUCCESS);
}

static const char_t* get_current_dir_filepath(int32_t len, char_t* buffer, int32_t filename_len, const char_t* filename)
{
    int32_t written = 0;
    int ec = get_this_image_path(len, buffer, &written);
    if (is_failure(ec))
        noreturn_runtime_load_failure(ec);

    char_t* filename_dest = buffer + written;

    // Remove this image filename.
    while (filename_dest != buffer)
    {
        if (*filename_dest == DNNE_DIR_SEPARATOR)
        {
            ++filename_dest;
            break;
        }

        --filename_dest;
    }

    int32_t remaining = (int32_t)(filename_dest - buffer);
    if (remaining < filename_len)
        noreturn_runtime_load_failure(-1);

    // Append the new filename.
    (void)memcpy(filename_dest, filename, filename_len * sizeof(char_t));
    return buffer;
}

// Globals to hold hostfxr exports
static hostfxr_initialize_for_runtime_config_fn init_fptr;
static hostfxr_get_runtime_delegate_fn get_delegate_fptr;
static hostfxr_close_fn close_fptr;

static void load_hostfxr()
{
    // Discover the path to hostfxr.
    char_t buffer[DNNE_MAX_PATH];
    size_t buffer_size = DNNE_ARRAY_SIZE(buffer);
    int rc = get_hostfxr_path(buffer, &buffer_size, NULL);
    if (is_failure(rc))
        noreturn_runtime_load_failure(rc);

    // Load hostfxr and get desired exports.
    void* lib = load_library(buffer);
    init_fptr = (hostfxr_initialize_for_runtime_config_fn)get_export(lib, "hostfxr_initialize_for_runtime_config");
    get_delegate_fptr = (hostfxr_get_runtime_delegate_fn)get_export(lib, "hostfxr_get_runtime_delegate");
    close_fptr = (hostfxr_close_fn)get_export(lib, "hostfxr_close");

    assert(init_fptr && get_delegate_fptr && close_fptr);
}

// Globals to hold runtime exports
static load_assembly_and_get_function_pointer_fn get_managed_export_fptr;

static void init_dotnet(const char_t* config_path)
{
    // Load .NET runtime
    void* load_assembly_and_get_function_pointer = NULL;
    hostfxr_handle cxt = NULL;
    int rc = init_fptr(config_path, NULL, &cxt);
    if (is_failure(rc) || cxt == NULL)
    {
        close_fptr(cxt);
        noreturn_runtime_load_failure(rc);
    }

    // Get the load assembly function pointer
    rc = get_delegate_fptr(
        cxt,
        hdt_load_assembly_and_get_function_pointer,
        &load_assembly_and_get_function_pointer);
    if (is_failure(rc) || load_assembly_and_get_function_pointer == NULL)
    {
        close_fptr(cxt);
        noreturn_runtime_load_failure(rc);
    }

    get_managed_export_fptr = (load_assembly_and_get_function_pointer_fn)load_assembly_and_get_function_pointer;
}

static void prepare_runtime()
{
    // Load HostFxr and get exported hosting functions.
    load_hostfxr();

    // Initialize and start the runtime.
    char_t buffer[DNNE_MAX_PATH];
    const char_t config_filename[] = DNNE_STR(DNNE_TOSTRING(DNNE_ASSEMBLY_NAME)) DNNE_STR(".runtimeconfig.json");
    const char_t* config_path = get_current_dir_filepath(DNNE_ARRAY_SIZE(buffer), buffer, DNNE_ARRAY_SIZE(config_filename), config_filename);
    init_dotnet(config_path);
}

void* get_callable_managed_function(
    const char_t* dotnet_type,
    const char_t* dotnet_type_method,
    const char_t* dotnet_delegate_type)
{
    assert(dotnet_type && dotnet_type_method);

    // Check if the runtime has already been prepared.
    if (!get_managed_export_fptr)
    {
        prepare_runtime();
        assert(get_managed_export_fptr != NULL);
    }

    char_t buffer[DNNE_MAX_PATH];
    const char_t assembly_filename[] = DNNE_STR(DNNE_TOSTRING(DNNE_ASSEMBLY_NAME)) DNNE_STR(".dll");
    const char_t* assembly_path = get_current_dir_filepath(DNNE_ARRAY_SIZE(buffer), buffer, DNNE_ARRAY_SIZE(assembly_filename), assembly_filename);

    // Function pointer to managed function
    void* func = NULL;
    int rc = get_managed_export_fptr(
        assembly_path,
        dotnet_type,
        dotnet_type_method,
        dotnet_delegate_type,
        NULL,
        &func);

    if (is_failure(rc))
        noreturn_export_load_failure(rc);

    return func;
}

void* get_fast_callable_managed_function(
    const char_t* dotnet_type,
    const char_t* dotnet_type_method)
{
    return get_callable_managed_function(dotnet_type, dotnet_type_method, UNMANAGEDCALLERSONLY_METHOD);
}
