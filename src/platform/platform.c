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

#include "dnne.h"

// Must define the assembly name
#ifndef DNNE_ASSEMBLY_NAME
    #error Target assembly name must be defined. Set 'DNNE_ASSEMBLY_NAME'.
#endif

// Include the official nethost API and indicate
// consumption should be as a static library.
#define NETHOST_USE_AS_STATIC
#include <nethost.h>

// Needed for dladdr() in non-macOS scenarios
#if !defined(DNNE_WINDOWS) && !defined(DNNE_OSX) && !defined(_GNU_SOURCE)
    #define _GNU_SOURCE
#endif

#include <stddef.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <assert.h>

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

#define DNNE_MAX_PATH 512
#define DNNE_ARRAY_SIZE(_array) (sizeof(_array) / sizeof(*_array))

#define DNNE_TOSTRING2(s) #s
#define DNNE_TOSTRING(s) DNNE_TOSTRING2(s)

typedef volatile long dnne_lock_handle;
#define DNNE_LOCK_OPEN (0)

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

static int get_this_image_path(int32_t buffer_len, char_t* buffer, int32_t* written)
{
    assert(0 < buffer_len && buffer != NULL && written != NULL);

    HMODULE hmod;
    if (FALSE == GetModuleHandleExW(
        GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        (LPWSTR)&get_this_image_path,
        &hmod))
    {
        return (int)GetLastError();
    }

    DWORD len_local = GetModuleFileNameW(hmod, buffer, (DWORD)buffer_len);
    if (len_local == 0)
    {
        return (int)GetLastError();
    }
    else if (len_local == buffer_len)
    {
        return ERROR_INSUFFICIENT_BUFFER;
    }

    *written = len_local;
    return DNNE_SUCCESS;
}

static int get_current_error()
{
    return (int)GetLastError();
}

static void set_current_error(int err)
{
    SetLastError((DWORD)err);
}

static void enter_lock(dnne_lock_handle* lock)
{
    while (InterlockedCompareExchange(lock, -1, DNNE_LOCK_OPEN) != DNNE_LOCK_OPEN)
    {
        Sleep(1 /* milliseconds */);
    }
}

static void exit_lock(dnne_lock_handle* lock)
{
    InterlockedExchange(lock, DNNE_LOCK_OPEN);
}

#else

#include <dlfcn.h>
#include <limits.h>
#include <string.h>
#include <sched.h>
#include <errno.h>

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

static int get_this_image_path(int32_t buffer_len, char_t* buffer, int32_t* written)
{
    assert(0 < buffer_len && buffer != NULL && written != NULL);

    Dl_info info;
    if (dladdr((void *)&get_this_image_path, &info) == 0)
        return -1;

    if (info.dli_fname == NULL)
        return -2;

    size_t len_local = strlen(info.dli_fname);
    if (buffer_len <= len_local)
        return ENOBUFS;

    // Copy over the null as well.
    memcpy(buffer, info.dli_fname, len_local + 1);

    *written = (int32_t)len_local;
    return DNNE_SUCCESS;
}

static int get_current_error()
{
    return errno;
}

static void set_current_error(int err)
{
    errno = err;
}

static void enter_lock(dnne_lock_handle* lock)
{
    while (__sync_val_compare_and_swap(lock, DNNE_LOCK_OPEN, -1) != DNNE_LOCK_OPEN)
    {
        (void)sched_yield(); // Yield instead of sleeping.
    }
}

static void exit_lock(dnne_lock_handle* lock)
{
    __atomic_exchange_n(lock, DNNE_LOCK_OPEN, __ATOMIC_ACQ_REL);
}

#endif // !DNNE_WINDOWS


static failure_fn failure_fptr;

DNNE_EXTERN_C DNNE_API void DNNE_CALLTYPE set_failure_callback(failure_fn cb)
{
    failure_fptr = cb;
}

// Provide mechanism for users to override default behavior of certain functions.
//  - See https://stackoverflow.com/questions/51656838/attribute-weak-and-static-libraries
//  - See https://devblogs.microsoft.com/oldnewthing/20200731-00/?p=104024
//  - Use the MSC macro to enable Windows builds without MSVC.
#ifdef _MSC_VER
    #define DNNE_DEFAULT_IMPL(methodName, ...) default_ ## methodName(__VA_ARGS__)

    // List of overridable APIs
    // Note the default calling convention is cdecl on x86 for DNNE_APIs.
    // This means, on x86, we mangle the alternative name in the linker command.
    #ifdef _M_IX86
        #pragma comment(linker, "/alternatename:_dnne_abort=_default_dnne_abort")
    #else
        #pragma comment(linker, "/alternatename:dnne_abort=default_dnne_abort")
    #endif
#else
    #define DNNE_DEFAULT_IMPL(methodName, ...) __attribute__((weak)) methodName(__VA_ARGS__)
#endif

DNNE_EXTERN_C DNNE_API void DNNE_DEFAULT_IMPL(dnne_abort, enum failure_type type, int error_code)
{
    abort();
}

DNNE_NORETURN static void noreturn_failure(enum failure_type type, int error_code)
{
    if (failure_fptr)
        failure_fptr(type, error_code);

    // Nothing to do if the runtime failed to load.
    dnne_abort(type, error_code);

    // Don't trust anything the user can override.
    abort();
}

static bool is_failure(int rc)
{
    // The CLR hosting API uses the Win32 HRESULT scheme. This means
    // the high order bit indicates an error and S_FALSE (1) can be returned
    // and is _not_ a failure.
    return (rc < DNNE_SUCCESS);
}

static int get_current_dir_filepath(int32_t buffer_len, char_t* buffer, int32_t filename_len, const char_t* filename, const char_t** result)
{
    assert(buffer != NULL && filename != NULL && result != NULL);

    int32_t written = 0;
    int rc = get_this_image_path(buffer_len, buffer, &written);
    if (is_failure(rc))
        return rc;

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

    int32_t remaining = buffer_len - (int32_t)(filename_dest - buffer);
    if (remaining < filename_len)
        return (-1);

    // Append the new filename.
    (void)memcpy(filename_dest, filename, filename_len * sizeof(char_t));

    *result = buffer;
    return DNNE_SUCCESS;
}

// Globals to hold hostfxr exports

static hostfxr_initialize_for_dotnet_command_line_fn init_self_contained_fptr;
static hostfxr_initialize_for_runtime_config_fn init_fptr;
static hostfxr_get_runtime_delegate_fn get_delegate_fptr;
static hostfxr_close_fn close_fptr;

static int load_hostfxr(const char_t* assembly_path)
{
    // Discover the path to hostfxr.
    char_t buffer[DNNE_MAX_PATH];
    size_t buffer_size = DNNE_ARRAY_SIZE(buffer);
    struct get_hostfxr_parameters params;
    params.size = sizeof(params);
    params.assembly_path = assembly_path;
    params.dotnet_root = NULL;
    int rc = get_hostfxr_path(buffer, &buffer_size, &params);
    if (is_failure(rc))
        return rc;

    // Load hostfxr and get desired exports.
    void* lib = load_library(buffer);
    init_self_contained_fptr = (hostfxr_initialize_for_dotnet_command_line_fn)get_export(lib, "hostfxr_initialize_for_dotnet_command_line");
    init_fptr = (hostfxr_initialize_for_runtime_config_fn)get_export(lib, "hostfxr_initialize_for_runtime_config");
    get_delegate_fptr = (hostfxr_get_runtime_delegate_fn)get_export(lib, "hostfxr_get_runtime_delegate");
    close_fptr = (hostfxr_close_fn)get_export(lib, "hostfxr_close");

    assert(init_self_contained_fptr && init_fptr && get_delegate_fptr && close_fptr);
    return DNNE_SUCCESS;
}

// Globals to hold runtime exports
static load_assembly_and_get_function_pointer_fn volatile get_managed_export_fptr;

static int init_dotnet(const char_t* assembly_path)
{
    const char_t* config_path = NULL;
    int rc;

#ifdef DNNE_SELF_CONTAINED_RUNTIME
    // Self-contained scenario support is experimental and relies upon the application scenario
    // entry-point. The logic here is to trick the hosting API into initializing as an application
    // but call the "load assembly and get delegate" instead of "run main". This has impact
    // on the TPA make-up and hence assembly loading in general since the TPA populates the default ALC.
    config_path = assembly_path;
#else
    char_t buffer[DNNE_MAX_PATH];
    const char_t config_filename[] = DNNE_STR(DNNE_TOSTRING(DNNE_ASSEMBLY_NAME)) DNNE_STR(".runtimeconfig.json");
    rc = get_current_dir_filepath(DNNE_ARRAY_SIZE(buffer), buffer, DNNE_ARRAY_SIZE(config_filename), config_filename, &config_path);
    if (is_failure(rc))
        return rc;
#endif

    // Load .NET runtime
    void* load_assembly_and_get_function_pointer = NULL;
    hostfxr_handle cxt = NULL;
#ifdef DNNE_SELF_CONTAINED_RUNTIME
    rc = init_self_contained_fptr(1, &config_path, NULL, &cxt);
#else
    rc = init_fptr(config_path, NULL, &cxt);
#endif
    if (is_failure(rc))
    {
        close_fptr(cxt);
        return rc;
    }

    // Get the load assembly function pointer
    rc = get_delegate_fptr(
        cxt,
        hdt_load_assembly_and_get_function_pointer,
        &load_assembly_and_get_function_pointer);
    if (is_failure(rc))
    {
        close_fptr(cxt);
        return rc;
    }

    get_managed_export_fptr = (load_assembly_and_get_function_pointer_fn)load_assembly_and_get_function_pointer;
    return DNNE_SUCCESS;
}

#define IF_FAILURE_RETURN_OR_ABORT(ret_maybe, type, rc, lock) \
{ \
    if (is_failure(rc)) \
    { \
        exit_lock(lock); \
        if (ret_maybe) \
        { \
            *ret_maybe = rc; \
            return; \
        } \
        noreturn_failure(type, rc); \
    } \
}

dnne_lock_handle _prepare_lock = DNNE_LOCK_OPEN;

static void prepare_runtime(int* ret)
{
    // Lock and check if the needed export was already acquired.
    enter_lock(&_prepare_lock);
    if (!get_managed_export_fptr)
    {
        char_t buffer[DNNE_MAX_PATH];
        const char_t assembly_filename[] = DNNE_STR(DNNE_TOSTRING(DNNE_ASSEMBLY_NAME)) DNNE_STR(".dll");
        const char_t* assembly_path = NULL;
        int rc = get_current_dir_filepath(DNNE_ARRAY_SIZE(buffer), buffer, DNNE_ARRAY_SIZE(assembly_filename), assembly_filename, &assembly_path);
        IF_FAILURE_RETURN_OR_ABORT(ret, failure_load_runtime, rc, &_prepare_lock);

        // Load HostFxr and get exported hosting functions.
        rc = load_hostfxr(assembly_path);
        IF_FAILURE_RETURN_OR_ABORT(ret, failure_load_runtime, rc, &_prepare_lock);

        // Initialize and start the runtime.
        rc = init_dotnet(assembly_path);
        IF_FAILURE_RETURN_OR_ABORT(ret, failure_load_runtime, rc, &_prepare_lock);

        assert(get_managed_export_fptr != NULL);
    }
    exit_lock(&_prepare_lock);
}

DNNE_EXTERN_C DNNE_API void DNNE_CALLTYPE preload_runtime(void)
{
    prepare_runtime(NULL);
}

DNNE_EXTERN_C DNNE_API int DNNE_CALLTYPE try_preload_runtime(void)
{
    int ret = DNNE_SUCCESS;
    prepare_runtime(&ret);
    return ret;
}

void* get_callable_managed_function(
    const char_t* dotnet_type,
    const char_t* dotnet_type_method,
    const char_t* dotnet_delegate_type)
{
    assert(dotnet_type && dotnet_type_method);

    // Store the current error state to reset it when
    // we exit this function. This being done because this
    // API is an implementation detail of the export but
    // can result in side-effects during export resolution.
    int curr_error = get_current_error();

    // Check if the runtime has already been prepared.
    if (!get_managed_export_fptr)
    {
        prepare_runtime(NULL);
        assert(get_managed_export_fptr != NULL);
    }

    char_t buffer[DNNE_MAX_PATH];
    const char_t assembly_filename[] = DNNE_STR(DNNE_TOSTRING(DNNE_ASSEMBLY_NAME)) DNNE_STR(".dll");
    const char_t* assembly_path = NULL;
    int rc = get_current_dir_filepath(DNNE_ARRAY_SIZE(buffer), buffer, DNNE_ARRAY_SIZE(assembly_filename), assembly_filename, &assembly_path);
    if (is_failure(rc))
        noreturn_failure(failure_load_export, rc);

    // Function pointer to managed function
    void* func = NULL;
    rc = get_managed_export_fptr(
        assembly_path,
        dotnet_type,
        dotnet_type_method,
        dotnet_delegate_type,
        NULL,
        &func);

    if (is_failure(rc))
        noreturn_failure(failure_load_export, rc);

    // Now that the export has been resolved, reset
    // the error state to hide this implementation detail.
    set_current_error(curr_error);
    return func;
}

void* get_fast_callable_managed_function(
    const char_t* dotnet_type,
    const char_t* dotnet_type_method)
{
    return get_callable_managed_function(dotnet_type, dotnet_type_method, UNMANAGEDCALLERSONLY_METHOD);
}
