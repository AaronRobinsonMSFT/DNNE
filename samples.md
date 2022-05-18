# Native Exports for .NET Samples

## String Samples

### Calculate ANSI String Length

The following C example calls an exported .Net function to get the ANSI (LPStr) string length of the provided string

```C
typedef LONG(WINAPI* LPStrLengthProc)(LPSTR);

int main()
{
	HMODULE dnne = LoadLibraryA("<Path to export library>");

	LPStrLengthProc fLPStrLengthProc = (LPStrLengthProc)GetProcAddress(dnne, "LPStrLength");

	LONG sLPNum = fLPStrLengthProc("DNNE - .Net Native Exports");

	return 0;
}
```

The following C# example is been called by a native C function with an ANSI (LPStr) string as parameter, returning its length. Make sure to compile it by enabling unsafe code.

```CSharp
public unsafe static class Exports
{
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    public static int LPStrLength(sbyte* value)
    {
        return new string(value).Length;
    }
}
```

### Calculate UNICODE String Length

The following C example calls an exported .Net function to get the UNICODE (LPWStr) string length of the provided string

```C
typedef LONG(WINAPI* LPWStrLengthProc)(LPWSTR);

int main()
{
	HMODULE dnne = LoadLibraryW(L"<Path to export library>");

	LPWStrLengthProc fLPWStrLengthProc = (LPWStrLengthProc)GetProcAddress(dnne, "LPWStrLength");

	LONG sLPNum = fLPWStrLengthProc(L"DNNE - .Net Native Exports");

	return 0;
}
```

The following C# example is been called by a native C function with an UNICODE (LPWStr) string as parameter, returning its length. Make sure to compile it by enabling unsafe code.

```CSharp
public unsafe static class Exports
{
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    public static int LPWStrLength(char* value)
    {
        return new string(value).Length;
    }
}
```

### Print ANSI String

The following C example calls an exported .Net function to print the provided ANSI (LPStr) string

```C
typedef VOID(WINAPI* LPStrPrintProc)(LPSTR);

int main()
{
	HMODULE dnne = LoadLibraryA("<Path to export library>");

	LPStrPrintProc fLPStrPrintProc = (LPStrPrintProc)GetProcAddress(dnne, "LPStrPrint");

    char message[] = "DNNE - .Net Native Exports";

	fLPStrPrintProc(message);

	return 0;
}
```

The following C# example is been called by a native C function with an ANSI (LPStr) string printing it to the console

```CSharp
public unsafe static class Exports
{
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    public static void LPStrPrint(sbyte* value)
    {
        string arg = new string(value);

        Console.WriteLine("Printing " + arg);
    }
}
```

### Print UNICODE String

The following C example calls an exported .Net function to print the provided UNICODE (LPWStr) string

```C
typedef VOID(WINAPI* LPWStrPrintProc)(LPWSTR);

int main()
{
	HMODULE dnne = LoadLibraryW(L"<Path to export library>");

	LPWStrPrintProc fLPWStrPrintProc = (LPWStrPrintProc)GetProcAddress(dnne, "LPWStrPrint");

    wchar_t message[] = L"DNNE - .Net Native Exports";

	fLPWStrPrintProc(message);

	return 0;
}
```

The following C# example is been called by a native C function with an UNICODE (LPWStr) string printing it to the console

```CSharp
public unsafe static class Exports
{
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    public static void LPWStrPrint(char* value)
    {
        string arg = new string(value);

        Console.WriteLine("Printing " + arg);
    }
}
```