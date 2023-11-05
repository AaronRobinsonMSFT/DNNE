﻿using Microsoft.CodeAnalysis;

namespace DNNE;

/// <summary>
/// A generator that generates all the necessary DNNE attributes.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class AttributesGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            context.AddSource("DnneAttributes.g.cs", """
                // <auto-generated/>
                #pragma warning disable

                namespace DNNE
                {
                    /// <summary>
                    /// Provides C code to be defined early in the generated C header file.
                    /// </summary>
                    /// <remarks>
                    /// This attribute is respected on an exported method declaration or on a parameter for the method.
                    /// The following header files will be included prior to the code being defined.
                    /// <list type="bullet">
                    ///   <item><c>stddef.h</c></item>
                    ///   <item><c>stdint.h</c></item>
                    ///   <item><c>dnne.h</c></item>
                    /// </list>
                    /// </remarks>
                    internal sealed class C99DeclCodeAttribute : global::System.Attribute
                    {
                        /// <summary>
                        /// Creates a new <see cref="C99DeclCodeAttribute"/> instance with the specified parameters.
                        /// </summary>
                        /// <param name="code">The C code to be defined in the generated C header file.</param>
                        public C99DeclCodeAttribute(string code)
                        {
                        }
                    }

                    /// <summary>
                    /// Defines the C type to be used.
                    /// </summary>
                    /// <remarks>
                    /// The level of indirection should be included in the supplied string.
                    /// </remarks>
                    internal sealed class C99TypeAttribute : global::System.Attribute
                    {
                        /// <summary>
                        /// Creates a new <see cref="C99TypeAttribute"/> instance with the specified parameters.
                        /// </summary>
                        /// <param name="code">The C type to be used.</param>
                        public C99TypeAttribute(string code)
                        {
                        }
                    }
                }
                """);
        });
    }
}
