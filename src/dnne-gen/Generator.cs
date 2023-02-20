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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace DNNE
{
    class GeneratorException : Exception
    {
        public string AssemblyPath { get; private set; }

        public GeneratorException(string assemblyPath, string message)
            : base(message)
        {
            this.AssemblyPath = assemblyPath;
        }
    }

    class Generator : IDisposable
    {
        private bool isDisposed = false;

        private readonly ICustomAttributeTypeProvider<KnownType> typeResolver = new TypeResolver();
        private readonly string assemblyPath;
        private readonly PEReader peReader;
        private readonly MetadataReader mdReader;
        private readonly Scope assemblyScope;
        private readonly Scope moduleScope;
        private readonly IDictionary<TypeDefinitionHandle, Scope> typePlatformScenarios = new Dictionary<TypeDefinitionHandle, Scope>();
        private readonly Dictionary<string, string> loadedXmlDocumentation;

        public Generator(string validAssemblyPath, string xmlDocFile)
        {
            this.assemblyPath = validAssemblyPath;
            this.peReader = new PEReader(File.OpenRead(this.assemblyPath));
            this.mdReader = this.peReader.GetMetadataReader(MetadataReaderOptions.None);
            this.loadedXmlDocumentation = Generator.LoadXmlDocumentation(xmlDocFile);

            // Check for platform scenario attributes
            AssemblyDefinition asmDef = this.mdReader.GetAssemblyDefinition();
            this.assemblyScope = this.GetOSPlatformScope(asmDef.GetCustomAttributes());

            ModuleDefinition modDef = this.mdReader.GetModuleDefinition();
            this.moduleScope = this.GetOSPlatformScope(modDef.GetCustomAttributes());
        }

        public void Emit(string outputFile)
        {
            var generatedCode = new StringWriter();
            Emit(generatedCode);

            // Check if the file exists
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            // Write the generated code to the output file.
            using (var outputFileStream = new StreamWriter(File.OpenWrite(outputFile)))
            {
                outputFileStream.Write(generatedCode.ToString());
            }
        }

        public void Emit(TextWriter outputStream)
        {
            var additionalCodeStatements = new List<string>();
            var exportedMethods = new List<ExportedMethod>();
            foreach (var methodDefHandle in this.mdReader.MethodDefinitions)
            {
                MethodDefinition methodDef = this.mdReader.GetMethodDefinition(methodDefHandle);

                // Only check public static functions
                if (!methodDef.Attributes.HasFlag(MethodAttributes.Public | MethodAttributes.Static))
                {
                    continue;
                }

                var supported = new List<OSPlatform>();
                var unsupported = new List<OSPlatform>();
                var callConv = SignatureCallingConvention.Unmanaged;
                var exportAttrType = ExportType.None;
                string managedMethodName = this.mdReader.GetString(methodDef.Name);
                string exportName = managedMethodName;
                // Check for target attribute
                foreach (var customAttrHandle in methodDef.GetCustomAttributes())
                {
                    CustomAttribute customAttr = this.mdReader.GetCustomAttribute(customAttrHandle);
                    var currAttrType = this.GetExportAttributeType(customAttr);
                    if (currAttrType == ExportType.None)
                    {
                        // Check if method has other supported attributes.
                        if (this.TryGetC99DeclCodeAttributeValue(customAttr, out string c99Decl))
                        {
                            additionalCodeStatements.Add(c99Decl);
                        }
                        else if (this.TryGetOSPlatformAttributeValue(customAttr, out bool isSupported, out OSPlatform scen))
                        {
                            if (isSupported)
                            {
                                supported.Add(scen);
                            }
                            else
                            {
                                unsupported.Add(scen);
                            }
                        }

                        continue;
                    }

                    exportAttrType = currAttrType;
                    if (exportAttrType == ExportType.Export)
                    {
                        CustomAttributeValue<KnownType> data = customAttr.DecodeValue(this.typeResolver);
                        if (data.NamedArguments.Length == 1)
                        {
                            exportName = (string)data.NamedArguments[0].Value;
                        }
                    }
                    else
                    {
                        Debug.Assert(exportAttrType == ExportType.UnmanagedCallersOnly);
                        CustomAttributeValue<KnownType> data = customAttr.DecodeValue(this.typeResolver);
                        foreach (var arg in data.NamedArguments)
                        {
                            switch (arg.Type)
                            {
                                case KnownType.I4:
                                case KnownType.CallingConvention:
                                    callConv = (CallingConvention)arg.Value switch
                                    {
                                        CallingConvention.Winapi => SignatureCallingConvention.Unmanaged,
                                        CallingConvention.Cdecl => SignatureCallingConvention.CDecl,
                                        CallingConvention.StdCall => SignatureCallingConvention.StdCall,
                                        CallingConvention.ThisCall => SignatureCallingConvention.ThisCall,
                                        CallingConvention.FastCall => SignatureCallingConvention.FastCall,
                                        _ => throw new NotSupportedException($"Unknown CallingConvention: {arg.Value}")
                                    };
                                    break;

                                case KnownType.SystemTypeArray:
                                    if (arg.Value != null)
                                    {
                                        foreach (var cct in (ImmutableArray<CustomAttributeTypedArgument<KnownType>>)arg.Value)
                                        {
                                            Debug.Assert(cct.Type == KnownType.SystemType);
                                            switch ((KnownType)cct.Value)
                                            {
                                                case KnownType.CallConvCdecl:
                                                    callConv = SignatureCallingConvention.CDecl;
                                                    break;
                                                case KnownType.CallConvStdcall:
                                                    callConv = SignatureCallingConvention.StdCall;
                                                    break;
                                                case KnownType.CallConvThiscall:
                                                    callConv = SignatureCallingConvention.ThisCall;
                                                    break;
                                                case KnownType.CallConvFastcall:
                                                    callConv = SignatureCallingConvention.FastCall;
                                                    break;
                                            }
                                        }
                                    }
                                    break;

                                case KnownType.String:
                                    exportName = (string)arg.Value;
                                    break;

                                default:
                                    throw new GeneratorException(this.assemblyPath, $"Method '{managedMethodName}' has unknown Attribute value type.");
                            }
                        }
                    }
                }

                // Didn't find target attribute. Move onto next method.
                if (exportAttrType == ExportType.None)
                {
                    continue;
                }

                // Extract method details
                var typeDef = this.mdReader.GetTypeDefinition(methodDef.GetDeclaringType());
                var enclosingTypeName = this.ComputeEnclosingTypeName(typeDef);

                // Process method signature.
                MethodSignature<string> signature;
                try
                {
                    var typeProvider = new C99TypeProvider();

                    signature = methodDef.DecodeSignature(typeProvider, null);

                    typeProvider.ThrowIfUnsupportedLastPrimitiveType();
                }
                catch (NotSupportedTypeException nste)
                {
                    throw new GeneratorException(this.assemblyPath, $"Method '{managedMethodName}' has non-exportable type '{nste.Type}'");
                }

                var returnType = signature.ReturnType;
                var argumentTypes = signature.ParameterTypes.ToArray();
                var argumentNames = new string[signature.ParameterTypes.Length];

                // Process each parameter.
                foreach (ParameterHandle paramHandle in methodDef.GetParameters())
                {
                    Parameter param = this.mdReader.GetParameter(paramHandle);

                    // Sequence number starts from 1 for arguments.
                    // Number of 0 indicates return value.
                    // Update arg index to be from [0..n-1]
                    // Return index is -1.
                    const int ReturnIndex = -1;
                    var argIndex = param.SequenceNumber - 1;
                    if (argIndex != ReturnIndex)
                    {
                        Debug.Assert(argIndex >= 0);
                        argumentNames[argIndex] = this.mdReader.GetString(param.Name);
                    }

                    // Check custom attributes for additional code.
                    foreach (var attr in param.GetCustomAttributes())
                    {
                        CustomAttribute custAttr = this.mdReader.GetCustomAttribute(attr);
                        if (TryGetC99TypeAttributeValue(custAttr, out string c99Type))
                        {
                            // Overridden type defined.
                            if (argIndex == ReturnIndex)
                            {
                                returnType = c99Type;
                            }
                            else
                            {
                                Debug.Assert(argIndex >= 0);
                                argumentTypes[argIndex] = c99Type;
                            }
                        }
                        else if (TryGetC99DeclCodeAttributeValue(custAttr, out string c99Decl))
                        {
                            additionalCodeStatements.Add(c99Decl);
                        }
                    }
                }

                var xmlDoc = FindXmlDoc(enclosingTypeName.Replace('+', '.') + Type.Delimiter + managedMethodName, argumentTypes);

                exportedMethods.Add(new ExportedMethod()
                {
                    Type = exportAttrType,
                    EnclosingTypeName = enclosingTypeName,
                    MethodName = managedMethodName,
                    ExportName = exportName,
                    CallingConvention = callConv,
                    Platforms = new PlatformSupport()
                    {
                        Assembly = this.assemblyScope,
                        Module = this.moduleScope,
                        Type = GetTypeOSPlatformScope(methodDef),
                        Method = new Scope()
                        {
                            Support = supported,
                            NoSupport = unsupported,
                        }
                    },
                    ReturnType = returnType,
                    XmlDoc = xmlDoc,
                    ArgumentTypes = ImmutableArray.Create(argumentTypes),
                    ArgumentNames = ImmutableArray.Create(argumentNames),
                });
            }

            if (exportedMethods.Count == 0)
            {
                throw new GeneratorException(this.assemblyPath, "Nothing to export.");
            }

            string assemblyName = this.mdReader.GetString(this.mdReader.GetAssemblyDefinition().Name);
            EmitC99(outputStream, assemblyName, exportedMethods, additionalCodeStatements);
        }

        private static Dictionary<string, string> LoadXmlDocumentation(string xmlDocumentation)
        {
            var actXml = new Dictionary<string, string>();
            if (xmlDocumentation is null)
                return actXml;

            // See https://docs.microsoft.com/dotnet/csharp/language-reference/xmldoc/
            // for xml documenation definition
            using XmlReader xmlReader = XmlReader.Create(xmlDocumentation);
            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "member")
                {
                    string raw_name = xmlReader["name"];
                    actXml[raw_name] = xmlReader.ReadInnerXml();
                }
            }
            return actXml;
        }

        private string FindXmlDoc(string fullMethodName, string[] argumentTypes)
        {
            string xmlDoc = "";
            foreach (var item in loadedXmlDocumentation)
            {
                if (item.Key.StartsWith("M:" + fullMethodName))
                {
                    xmlDoc = item.Value;
                    break;
                }
            }
            if (xmlDoc == "")
                return "";

            var lines = xmlDoc.TrimStart('\n').TrimEnd().Split("\n");
            string prefix = "/// ";
            var result = lines
             .Select(x => prefix + x.Trim())
             .ToList();

            return Environment.NewLine + string.Join(Environment.NewLine, result);
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.peReader.Dispose();

            this.isDisposed = true;
        }

        private enum ExportType
        {
            None,
            Export,
            UnmanagedCallersOnly,
        }

        private ExportType GetExportAttributeType(CustomAttribute attribute)
        {
            if (IsAttributeType(this.mdReader, attribute, "DNNE", "ExportAttribute"))
            {
                return ExportType.Export;
            }
            else if (IsAttributeType(this.mdReader, attribute, "System.Runtime.InteropServices", nameof(UnmanagedCallersOnlyAttribute)))
            {
                return ExportType.UnmanagedCallersOnly;
            }
            else
            {
                return ExportType.None;
            }
        }

        private string ComputeEnclosingTypeName(TypeDefinition typeDef)
        {
            var enclosingTypes = new List<string>() { this.mdReader.GetString(typeDef.Name) };
            TypeDefinition parentTypeDef = typeDef;
            while (parentTypeDef.IsNested)
            {
                parentTypeDef = this.mdReader.GetTypeDefinition(parentTypeDef.GetDeclaringType());
                enclosingTypes.Add(this.mdReader.GetString(parentTypeDef.Name));
            }

            enclosingTypes.Reverse();
            string name = string.Join('+', enclosingTypes);
            if (!parentTypeDef.Namespace.IsNil)
            {
                name = $"{this.mdReader.GetString(parentTypeDef.Namespace)}{Type.Delimiter}{name}";
            }

            return name;
        }

        private bool TryGetC99TypeAttributeValue(CustomAttribute attribute, out string c99Type)
        {
            c99Type = IsAttributeType(this.mdReader, attribute, "DNNE", "C99TypeAttribute")
                ? GetFirstFixedArgAsStringValue(this.typeResolver, attribute)
                : null;
            return !string.IsNullOrEmpty(c99Type);
        }

        private bool TryGetC99DeclCodeAttributeValue(CustomAttribute attribute, out string c99Decl)
        {
            c99Decl = IsAttributeType(this.mdReader, attribute, "DNNE", "C99DeclCodeAttribute")
                ? GetFirstFixedArgAsStringValue(this.typeResolver, attribute)
                : null;
            return !string.IsNullOrEmpty(c99Decl);
        }

        private Scope GetTypeOSPlatformScope(MethodDefinition methodDef)
        {
            TypeDefinitionHandle typeDefHandle = methodDef.GetDeclaringType();
            if (this.typePlatformScenarios.TryGetValue(typeDefHandle, out Scope scope))
            {
                return scope;
            }

            TypeDefinition typeDef = this.mdReader.GetTypeDefinition(typeDefHandle);
            var typeScope = this.GetOSPlatformScope(typeDef.GetCustomAttributes());

            // Record and return the scenarios.
            this.typePlatformScenarios.Add(typeDefHandle, typeScope);
            return typeScope;
        }

        private Scope GetOSPlatformScope(CustomAttributeHandleCollection attrs)
        {
            var supported = new List<OSPlatform>();
            var unsupported = new List<OSPlatform>();
            foreach (var customAttrHandle in attrs)
            {
                CustomAttribute customAttr = this.mdReader.GetCustomAttribute(customAttrHandle);
                if (this.TryGetOSPlatformAttributeValue(customAttr, out bool isSupported, out OSPlatform scen))
                {
                    if (isSupported)
                    {
                        supported.Add(scen);
                    }
                    else
                    {
                        unsupported.Add(scen);
                    }
                }
            }

            return new Scope()
            {
                Support = supported,
                NoSupport = unsupported
            };
        }

        private bool TryGetOSPlatformAttributeValue(
            CustomAttribute attribute,
            out bool support,
            out OSPlatform platform)
        {
            platform = default;

            support = IsAttributeType(this.mdReader, attribute, "System.Runtime.Versioning", nameof(SupportedOSPlatformAttribute));
            if (!support)
            {
                // If the unsupported attribute exists the "support" value is properly set.
                bool nosupport = IsAttributeType(this.mdReader, attribute, "System.Runtime.Versioning", nameof(UnsupportedOSPlatformAttribute));
                if (!nosupport)
                {
                    return false;
                }
            }

            string value = GetFirstFixedArgAsStringValue(this.typeResolver, attribute);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            const string platformPrefix = "DNNE_";
            if (value.Contains(nameof(OSPlatform.Windows), StringComparison.OrdinalIgnoreCase))
            {
                platform = OSPlatform.Create($"{platformPrefix}{OSPlatform.Windows}");
            }
            else if (value.Contains(nameof(OSPlatform.OSX), StringComparison.OrdinalIgnoreCase))
            {
                platform = OSPlatform.Create($"{platformPrefix}{OSPlatform.OSX}");
            }
            else if (value.Contains(nameof(OSPlatform.Linux), StringComparison.OrdinalIgnoreCase))
            {
                platform = OSPlatform.Create($"{platformPrefix}{OSPlatform.Linux}");
            }
            else if (value.Contains(nameof(OSPlatform.FreeBSD), StringComparison.OrdinalIgnoreCase))
            {
                platform = OSPlatform.Create($"{platformPrefix}{OSPlatform.FreeBSD}");
            }
            else
            {
                platform = OSPlatform.Create(value);
            }

            return true;
        }

        private static string GetFirstFixedArgAsStringValue(ICustomAttributeTypeProvider<KnownType> typeResolver, CustomAttribute attribute)
        {
            CustomAttributeValue<KnownType> data = attribute.DecodeValue(typeResolver);
            if (data.FixedArguments.Length == 1)
            {
                return (string)data.FixedArguments[0].Value;
            }

            return null;
        }

        private static bool IsAttributeType(MetadataReader reader, CustomAttribute attribute, string targetNamespace, string targetName)
        {
            StringHandle namespaceMaybe;
            StringHandle nameMaybe;
            switch (attribute.Constructor.Kind)
            {
                case HandleKind.MemberReference:
                    MemberReference refConstructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                    TypeReference refType = reader.GetTypeReference((TypeReferenceHandle)refConstructor.Parent);
                    namespaceMaybe = refType.Namespace;
                    nameMaybe = refType.Name;
                    break;

                case HandleKind.MethodDefinition:
                    MethodDefinition defConstructor = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                    TypeDefinition defType = reader.GetTypeDefinition(defConstructor.GetDeclaringType());
                    namespaceMaybe = defType.Namespace;
                    nameMaybe = defType.Name;
                    break;

                default:
                    Debug.Assert(false, "Unknown attribute constructor kind");
                    return false;
            }

#if DEBUG
            string attrNamespace = reader.GetString(namespaceMaybe);
            string attrName = reader.GetString(nameMaybe);
#endif
            return reader.StringComparer.Equals(namespaceMaybe, targetNamespace) && reader.StringComparer.Equals(nameMaybe, targetName);
        }

        private const string SafeMacroRegEx = "[^a-zA-Z0-9_]";

        private static void EmitC99(TextWriter outputStream, string assemblyName, IEnumerable<ExportedMethod> exports, IEnumerable<string> additionalCodeStatements)
        {
            // Convert the assembly name into a supported string for C99 macros.
            var assemblyNameMacroSafe = Regex.Replace(assemblyName, SafeMacroRegEx, "_");
            var generatedHeaderDefine = $"__DNNE_GENERATED_HEADER_{assemblyNameMacroSafe.ToUpperInvariant()}__";
            var compileAsSourceDefine = "DNNE_COMPILE_AS_SOURCE";

            // Emit declaration preamble
            outputStream.WriteLine(
$@"//
// Auto-generated by dnne-gen
//
// .NET Assembly: {assemblyName}
//

//
// Declare exported functions
//
#ifndef {generatedHeaderDefine}
#define {generatedHeaderDefine}

#include <stddef.h>
#include <stdint.h>
#ifdef {compileAsSourceDefine}
    #include <dnne.h>
#else
    // When used as a header file, the assumption is
    // dnne.h will be next to this file.
    #include ""dnne.h""
#endif // !{compileAsSourceDefine}
");

            // Emit additional code statements
            if (additionalCodeStatements.Any())
            {
                outputStream.WriteLine(
$@"//
// Additional code provided by user
//");
                foreach (var stmt in additionalCodeStatements)
                {
                    outputStream.WriteLine(stmt);
                }

                outputStream.WriteLine();
            }

            var implStream = new StringWriter();

            // Emit definition preamble
            implStream.WriteLine(
$@"//
// Define exported functions
//
#ifdef {compileAsSourceDefine}

#ifdef DNNE_WINDOWS
    #ifdef _WCHAR_T_DEFINED
        typedef wchar_t char_t;
    #else
        typedef unsigned short char_t;
    #endif
#else
    typedef char char_t;
#endif

//
// Forward declarations
//

extern void* get_callable_managed_function(
    const char_t* dotnet_type,
    const char_t* dotnet_type_method,
    const char_t* dotnet_delegate_type);

extern void* get_fast_callable_managed_function(
    const char_t* dotnet_type,
    const char_t* dotnet_type_method);
");

            // Emit string table
            implStream.WriteLine(
@"//
// String constants
//
");
            int count = 1;
            var map = new StringDictionary();
            foreach (var method in exports)
            {
                if (map.ContainsKey(method.EnclosingTypeName))
                {
                    continue;
                }

                string id = $"t{count++}_name";
                implStream.WriteLine(
$@"#ifdef DNNE_TARGET_NET_FRAMEWORK
    static const char_t* {id} = DNNE_STR(""{method.EnclosingTypeName}"");
#else
    static const char_t* {id} = DNNE_STR(""{method.EnclosingTypeName}, {assemblyName}"");
#endif // !DNNE_TARGET_NET_FRAMEWORK
");
                map.Add(method.EnclosingTypeName, id);
            }

            // Emit the exports
            implStream.WriteLine(
@"
//
// Exports
//
");
            foreach (var export in exports)
            {
                (var preguard, var postguard) = GetC99PlatformGuards(export.Platforms);

                // Create declaration and call signature.
                string delim = "";
                var declsig = new StringBuilder();
                var callsig = new StringBuilder();
                for (int i = 0; i < export.ArgumentTypes.Length; ++i)
                {
                    var argName = export.ArgumentNames[i] ?? $"arg{i}";
                    declsig.AppendFormat("{0}{1} {2}", delim, export.ArgumentTypes[i], argName);
                    callsig.AppendFormat("{0}{1}", delim, argName);
                    delim = ", ";
                }

                // Special casing for void signature.
                if (declsig.Length == 0)
                {
                    declsig.Append("void");
                }

                // Special casing for void return.
                string returnStatementKeyword = "return ";
                if (export.ReturnType.Equals("void"))
                {
                    returnStatementKeyword = string.Empty;
                }

                string callConv = GetC99CallConv(export.CallingConvention);

                string classNameConstant = map[export.EnclosingTypeName];
                Debug.Assert(!string.IsNullOrEmpty(classNameConstant));

                // Generate the acquire managed function based on the export type.
                string acquireManagedFunction;
                if (export.Type == ExportType.Export)
                {
                    acquireManagedFunction =
$@"const char_t* methodName = DNNE_STR(""{export.MethodName}"");
        const char_t* delegateType = DNNE_STR(""{export.EnclosingTypeName}+{export.MethodName}Delegate, {assemblyName}"");
        {export.ExportName}_ptr = ({export.ReturnType}({callConv}*)({declsig}))get_callable_managed_function({classNameConstant}, methodName, delegateType);";

                }
                else
                {
                    Debug.Assert(export.Type == ExportType.UnmanagedCallersOnly);
                    acquireManagedFunction =
$@"const char_t* methodName = DNNE_STR(""{export.MethodName}"");
        {export.ExportName}_ptr = ({export.ReturnType}({callConv}*)({declsig}))get_fast_callable_managed_function({classNameConstant}, methodName);";
                }

                // Declare export
                outputStream.WriteLine(
$@"{preguard}// Computed from {export.EnclosingTypeName}{Type.Delimiter}{export.MethodName}{export.XmlDoc}
DNNE_EXTERN_C DNNE_API {export.ReturnType} {callConv} {export.ExportName}({declsig});
{postguard}");

                // Define export in implementation stream
                implStream.WriteLine(
$@"{preguard}// Computed from {export.EnclosingTypeName}{Type.Delimiter}{export.MethodName}
static {export.ReturnType} ({callConv}* {export.ExportName}_ptr)({declsig});
DNNE_EXTERN_C DNNE_API {export.ReturnType} {callConv} {export.ExportName}({declsig})
{{
    if ({export.ExportName}_ptr == NULL)
    {{
        {acquireManagedFunction}
    }}
    {returnStatementKeyword}{export.ExportName}_ptr({callsig});
}}
{postguard}");
            }

            // Emit implementation closing
            implStream.Write($"#endif // {compileAsSourceDefine}");

            // Emit output closing for header and merge in implementation
            outputStream.WriteLine(
$@"#endif // {generatedHeaderDefine}

{implStream}");
        }

        private static (string preguard, string postguard) GetC99PlatformGuards(in PlatformSupport platformSupport)
        {
            var pre = new StringBuilder();
            var post = new StringBuilder();

            var postAssembly = ConvertScope(platformSupport.Assembly, ref pre);
            var postModule = ConvertScope(platformSupport.Module, ref pre);
            var postType = ConvertScope(platformSupport.Type, ref pre);
            var postMethod = ConvertScope(platformSupport.Method, ref pre);

            // Append the post guards in reverse order
            post.Append(postMethod);
            post.Append(postType);
            post.Append(postModule);
            post.Append(postAssembly);

            return (pre.ToString(), post.ToString());

            static string ConvertScope(in Scope scope, ref StringBuilder pre)
            {
                (string pre_support, string post_support) = ConvertCollection(scope.Support, "(", ")");
                (string pre_nosupport, string post_nosupport) = ConvertCollection(scope.NoSupport, "!(", ")");

                var post = new StringBuilder();
                if (!string.IsNullOrEmpty(pre_support)
                    || !string.IsNullOrEmpty(pre_nosupport))
                {
                    // Add the preamble for the guard
                    pre.Append("#if ");
                    post.Append("#endif // ");

                    // Append the "support" clauses because if they don't exist they are string.Empty
                    pre.Append(pre_support);
                    post.Append(post_support);

                    // Check if we need to chain the clauses
                    if (!string.IsNullOrEmpty(pre_support) && !string.IsNullOrEmpty(pre_nosupport))
                    {
                        pre.Append(" && ");
                        post.Append(" && ");
                    }

                    // Append the "nosupport" clauses because if they don't exist they are string.Empty
                    pre.Append($"{pre_nosupport}");
                    post.Append($"{post_nosupport}");

                    pre.Append('\n');
                    post.Append('\n');
                }

                return post.ToString();
            }

            static (string pre, string post) ConvertCollection(in IEnumerable<OSPlatform> platforms, in string prefix, in string suffix)
            {
                var pre = new StringBuilder();
                var post = new StringBuilder();

                var delim = prefix;
                foreach (OSPlatform os in platforms)
                {
                    if (pre.Length != 0)
                    {
                        delim = " || ";
                    }

                    var platformMacroSafe = Regex.Replace(os.ToString(), SafeMacroRegEx, "_").ToUpperInvariant();
                    pre.Append($"{delim}defined({platformMacroSafe})");
                    post.Append($"{post}{delim}{platformMacroSafe}");
                }

                if (pre.Length != 0)
                {
                    pre.Append(suffix);
                    post.Append(suffix);
                }

                return (pre.ToString(), post.ToString());
            }
        }

        private static string GetC99CallConv(SignatureCallingConvention callConv)
        {
            return callConv switch
            {
                SignatureCallingConvention.CDecl => "DNNE_CALLTYPE_CDECL",
                SignatureCallingConvention.StdCall => "DNNE_CALLTYPE_STDCALL",
                SignatureCallingConvention.ThisCall => "DNNE_CALLTYPE_THISCALL",
                SignatureCallingConvention.FastCall => "DNNE_CALLTYPE_FASTCALL",
                SignatureCallingConvention.Unmanaged => "DNNE_CALLTYPE",
                _ => throw new NotSupportedException($"Unknown CallingConvention: {callConv}"),
            };
        }

        private struct PlatformSupport
        {
            public Scope Assembly { get; init; }
            public Scope Module { get; init; }
            public Scope Type { get; init; }
            public Scope Method { get; init; }
        }

        private struct Scope
        {
            public IEnumerable<OSPlatform> Support { get; init; }
            public IEnumerable<OSPlatform> NoSupport { get; init; }
        }

        private class ExportedMethod
        {
            public ExportType Type { get; init; }
            public string EnclosingTypeName { get; init; }
            public string MethodName { get; init; }
            public string ExportName { get; init; }
            public SignatureCallingConvention CallingConvention { get; init; }
            public PlatformSupport Platforms { get; init; }
            public string ReturnType { get; init; }
            public string XmlDoc { get; init; }
            public ImmutableArray<string> ArgumentTypes { get; init; }
            public ImmutableArray<string> ArgumentNames { get; init; }
        }

        private enum KnownType
        {
            Unknown,
            I4,
            CallingConvention,
            CallConvCdecl,
            CallConvStdcall,
            CallConvThiscall,
            CallConvFastcall,
            String,
            SystemTypeArray,
            SystemType
        }

        private class TypeResolver : ICustomAttributeTypeProvider<KnownType>
        {
            public KnownType GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return typeCode switch
                {
                    PrimitiveTypeCode.Int32 => KnownType.I4,
                    PrimitiveTypeCode.String => KnownType.String,
                    _ => KnownType.Unknown
                };
            }

            public KnownType GetSystemType()
            {
                return KnownType.SystemType;
            }

            public KnownType GetSZArrayType(KnownType elementType)
            {
                if (elementType == KnownType.SystemType)
                {
                    return KnownType.SystemTypeArray;
                }

                throw new BadImageFormatException("Unexpectedly got an array of unsupported type.");
            }

            public KnownType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                return KnownType.Unknown;
            }

            public KnownType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                return KnownType.Unknown;
            }

            public KnownType GetTypeFromSerializedName(string name)
            {
                int typeAssemblySeparator = name.IndexOf(',');
                string typeName = name[..typeAssemblySeparator];
                string assemblyName = name[(typeAssemblySeparator + 1)..];
                string assemblySimpleName = assemblyName;
                int simpleNameEnd = assemblySimpleName.IndexOf(',');
                if (simpleNameEnd != -1)
                {
                    assemblySimpleName = assemblySimpleName[..simpleNameEnd];
                }

                return (typeName, assemblySimpleName.TrimStart()) switch
                {
                    ("System.Runtime.InteropServices.CallingConvention", "System.Runtime.InteropServices") => KnownType.CallingConvention,
                    ("System.Runtime.CompilerServices.CallConvCdecl", "System.Runtime") => KnownType.CallConvCdecl,
                    ("System.Runtime.CompilerServices.CallConvStdcall", "System.Runtime") => KnownType.CallConvStdcall,
                    ("System.Runtime.CompilerServices.CallConvThiscall", "System.Runtime") => KnownType.CallConvThiscall,
                    ("System.Runtime.CompilerServices.CallConvFastcall", "System.Runtime") => KnownType.CallConvFastcall,
                    _ => KnownType.Unknown
                };
            }

            public PrimitiveTypeCode GetUnderlyingEnumType(KnownType type)
            {
                if (type == KnownType.CallingConvention)
                {
                    return PrimitiveTypeCode.Int32;
                }

                throw new BadImageFormatException("Unexpectedly got an enum parameter for an attribute.");
            }

            public bool IsSystemType(KnownType type)
            {
                return type == KnownType.SystemType;
            }
        }

        private class UnusedGenericContext { }

        private class NotSupportedTypeException : Exception
        {
            public string Type { get; private set; }
            public NotSupportedTypeException(string type) { this.Type = type; }
        }

        private class C99TypeProvider : ISignatureTypeProvider<string, UnusedGenericContext>
        {
            PrimitiveTypeCode? lastUnsupportedPrimitiveType;

            public string GetArrayType(string elementType, ArrayShape shape)
            {
                throw new NotSupportedTypeException(elementType);
            }

            public string GetByReferenceType(string elementType)
            {
                throw new NotSupportedTypeException(elementType);
            }

            public string GetFunctionPointerType(MethodSignature<string> signature)
            {
                // Define the native function pointer type in a comment.
                string args = this.GetPrimitiveType(PrimitiveTypeCode.Void);
                if (signature.ParameterTypes.Length != 0)
                {
                    var argsBuffer = new StringBuilder();
                    var delim = "";
                    foreach (var type in signature.ParameterTypes)
                    {
                        argsBuffer.Append(delim);
                        argsBuffer.Append(type);
                        delim = ", ";
                    }

                    args = argsBuffer.ToString();
                }

                var callingConvention = GetC99CallConv(signature.Header.CallingConvention);
                var typeComment = $"/* {signature.ReturnType}({callingConvention} *)({args}) */ ";
                return typeComment + this.GetPrimitiveType(PrimitiveTypeCode.IntPtr);
            }

            public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            {
                throw new NotSupportedTypeException($"Generic - {genericType}");
            }

            public string GetGenericMethodParameter(UnusedGenericContext genericContext, int index)
            {
                throw new NotSupportedTypeException($"Generic - {index}");
            }

            public string GetGenericTypeParameter(UnusedGenericContext genericContext, int index)
            {
                throw new NotSupportedTypeException($"Generic - {index}");
            }

            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
            {
                throw new NotSupportedTypeException($"{modifier} {unmodifiedType}");
            }

            public string GetPinnedType(string elementType)
            {
                throw new NotSupportedTypeException($"Pinned - {elementType}");
            }

            public string GetPointerType(string elementType)
            {
                this.lastUnsupportedPrimitiveType = null;
                return elementType + "*";
            }

            public string GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                ThrowIfUnsupportedLastPrimitiveType();

                if (typeCode == PrimitiveTypeCode.Char)
                {
                    // Record the current type here with the expectation
                    // it will be of pointer type to Char, which is supported.
                    this.lastUnsupportedPrimitiveType = typeCode;
                    return "DNNE_WCHAR";
                }

                return typeCode switch
                {
                    PrimitiveTypeCode.SByte => "int8_t",
                    PrimitiveTypeCode.Byte => "uint8_t",
                    PrimitiveTypeCode.Int16 => "int16_t",
                    PrimitiveTypeCode.UInt16 => "uint16_t",
                    PrimitiveTypeCode.Int32 => "int32_t",
                    PrimitiveTypeCode.UInt32 => "uint32_t",
                    PrimitiveTypeCode.Int64 => "int64_t",
                    PrimitiveTypeCode.UInt64 => "uint64_t",
                    PrimitiveTypeCode.IntPtr => "intptr_t",
                    PrimitiveTypeCode.UIntPtr => "uintptr_t",
                    PrimitiveTypeCode.Single => "float",
                    PrimitiveTypeCode.Double => "double",
                    PrimitiveTypeCode.Void => "void",
                    _ => throw new NotSupportedTypeException(typeCode.ToString())
                };
            }

            public void ThrowIfUnsupportedLastPrimitiveType()
            {
                if (this.lastUnsupportedPrimitiveType.HasValue)
                {
                    throw new NotSupportedTypeException(this.lastUnsupportedPrimitiveType.Value.ToString());
                }
            }

            public string GetSZArrayType(string elementType)
            {
                throw new NotSupportedTypeException($"Array - {elementType}");
            }

            public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                return SupportNonPrimitiveTypes(rawTypeKind);
            }

            public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                return SupportNonPrimitiveTypes(rawTypeKind);
            }

            public string GetTypeFromSpecification(MetadataReader reader, UnusedGenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                return SupportNonPrimitiveTypes(rawTypeKind);
            }

            private static string SupportNonPrimitiveTypes(byte rawTypeKind)
            {
                // See https://docs.microsoft.com/dotnet/framework/unmanaged-api/metadata/corelementtype-enumeration
                const byte ELEMENT_TYPE_VALUETYPE = 0x11;
                if (rawTypeKind == ELEMENT_TYPE_VALUETYPE)
                {
                    return "/* SUPPLY TYPE */";
                }

                throw new NotSupportedTypeException("Non-primitive");
            }
        }
    }
}
