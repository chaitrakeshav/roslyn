﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Emit
{
    public partial class CompilationEmitTests : EmitMetadataTestBase
    {
        [Fact]
        public void CompilationEmitDiagnostics()
        {
            // Check that Compilation.Emit actually produces compilation errors.

            string source = @"
class X
{
    public void Main()
    {
        const int x = 5;
        x = x; // error; assigning to const.
    }
}";
            var compilation = CreateCompilationWithMscorlib(source);

            EmitResult emitResult;
            using (var output = new MemoryStream())
            {
                emitResult = compilation.Emit(output, null, null, null);
            }

            emitResult.Diagnostics.Verify(
                // (7,9): error CS0131: The left-hand side of an assignment must be a variable, property or indexer
                Diagnostic(ErrorCode.ERR_AssgLvalueExpected, "x"));
        }


        [Fact]
        public void CompilationEmitWithQuotedMainType()
        {
            // Check that compilation with quoted main switch argument produce diagnostic.
            // MSBuild can return quoted main argument value which is removed from the command line arguments or by parsing
            // command line arguments, but we DO NOT unquote arguments which are provided by 
            // the WithMainTypeName function - (was originally exposed through using 
            // a Cyrillic Namespace And building Using MSBuild.)

            string source = @"
namespace abc
{
public class X
{
    public static void Main()
    {
  
    }
}
}";
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithMainTypeName("abc.X"));
            compilation.VerifyDiagnostics();
            
            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithMainTypeName("\"abc.X\""));
            compilation.VerifyDiagnostics(// error CS1555: Could not find '"abc.X"' specified for Main method
                                          Diagnostic(ErrorCode.ERR_MainClassNotFound).WithArguments("\"abc.X\""));



            // Verify use of cyrillic namespace results in same behavior
            source = @"
namespace решения
{
public class X
{
    public static void Main()
    {
  
    }
}
}";
            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithMainTypeName("решения.X"));
            compilation.VerifyDiagnostics();

            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithMainTypeName("\"решения.X\""));
            compilation.VerifyDiagnostics(Diagnostic(ErrorCode.ERR_MainClassNotFound).WithArguments("\"решения.X\""));

        }

        [Fact]
        public void CompilationGetDiagnostics()
        {
            // Check that Compilation.GetDiagnostics and Compilation.GetDeclarationDiagnostics work as expected.

            string source = @"
class X
{
    private Blah q;
    public void Main()
    {
        const int x = 5;
        x = x; // error; assigning to const.
    }
}";

            var compilation = CreateCompilationWithMscorlib(source);
            compilation.VerifyDiagnostics(
                // (4,13): error CS0246: The type or namespace name 'Blah' could not be found (are you missing a using directive or an assembly reference?)
                //     private Blah q;
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Blah").WithArguments("Blah"),
                // (8,9): error CS0131: The left-hand side of an assignment must be a variable, property or indexer
                //         x = x; // error; assigning to const.
                Diagnostic(ErrorCode.ERR_AssgLvalueExpected, "x"),
                // (4,18): warning CS0169: The field 'X.q' is never used
                //     private Blah q;
                Diagnostic(ErrorCode.WRN_UnreferencedField, "q").WithArguments("X.q"));
        }

        // Check that Emit produces syntax, declaration, and method body errors.
        [Fact]
        public void EmitDiagnostics()
        {
            CSharpCompilation comp = CreateCompilationWithMscorlib(@"
namespace N {
     class X {
        public Blah field;
        private static readonly int ro;
        public static void Main()
        {
            ro = 4;
        }
    }
}

namespace N.Foo;
");

            EmitResult emitResult;
            using (var output = new MemoryStream())
            {
                emitResult = comp.Emit(output, null, null, null);
            }

            Assert.False(emitResult.Success);

            emitResult.Diagnostics.Verify(
                // (13,16): error CS1514: { expected
                Diagnostic(ErrorCode.ERR_LbraceExpected, ";"),
                // (13,17): error CS1513: } expected
                Diagnostic(ErrorCode.ERR_RbraceExpected, ""),
                // (4,16): error CS0246: The type or namespace name 'Blah' could not be found (are you missing a using directive or an assembly reference?)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Blah").WithArguments("Blah"),
                // (8,13): error CS0198: A static readonly field cannot be assigned to (except in a static constructor or a variable initializer)
                Diagnostic(ErrorCode.ERR_AssgReadonlyStatic, "ro"),
                // (4,21): warning CS0649: Field 'N.X.field' is never assigned to, and will always have its default value null
                Diagnostic(ErrorCode.WRN_UnassignedInternalField, "field").WithArguments("N.X.field", "null"),
                // (5,37): warning CS0414: The field 'N.X.ro' is assigned but its value is never used
                Diagnostic(ErrorCode.WRN_UnreferencedFieldAssg, "ro").WithArguments("N.X.ro"));
        }

        // Check that EmitMetadaOnly works
        [Fact]
        public void EmitMetadataOnly()
        {
            CSharpCompilation comp = CreateCompilationWithMscorlib(@"
namespace Foo.Bar
{
    public class Test1
    {
        public static void SayHello()
        {
            Console.WriteLine(""hello"");
        }
                                   
        public int x;
        private int y;
         
        public Test1()
        {
            x = 17;
        }

        public string foo(int a)
        {
            return a.ToString();
        }
    }  
}     
");

            EmitResult emitResult;
            byte[] mdOnlyImage;

            using (var output = new MemoryStream()) 
            {
                emitResult = comp.EmitMetadataOnly(output, null);
                mdOnlyImage = output.ToArray();
            }

            Assert.True(emitResult.Success);
            emitResult.Diagnostics.Verify();
            Assert.True(mdOnlyImage.Length > 0, "no metadata emitted");

            var srcUsing = @"
using System;
using Foo.Bar;

class Test2
{
    public static void Main()
    {
        Test1.SayHello();
        Console.WriteLine(new Test1().x);
    }
}  
";
            CSharpCompilation compUsing = CreateCompilationWithMscorlib(srcUsing, new[] { new MetadataImageReference(mdOnlyImage.AsImmutableOrNull()) });

            using (var output = new MemoryStream())
            {
                emitResult = compUsing.Emit(output);

                Assert.True(emitResult.Success);
                emitResult.Diagnostics.Verify();
                Assert.True(output.ToArray().Length > 0, "no metadata emitted");
            }
        }

        /// <summary>
        /// Check that when we emit metadata only, we include metadata for
        /// compiler generate methods (e.g. the ones for implicit interface
        /// implementation).
        /// </summary>
        [Fact]
        public void EmitMetadataOnly_SynthesizedExplicitImplementations()
        {
            var ilAssemblyReference = TestReferences.SymbolsTests.CustomModifiers.CppCli.dll;

            var libAssemblyName = "SynthesizedMethodMetadata";
            var exeAssemblyName = "CallSynthesizedMethod";

            // Setup: CppBase2 has methods that implement CppInterface1, but it doesn't declare
            // that it implements the interface.  Class1 does declare that it implements the
            // interface, but it's empty so it counts on CppBase2 to provide the implementations.
            // Since CppBase2 is not in the current source module, bridge methods are inserted
            // into Class1 to implement the interface methods by delegating to CppBase2.
            var libText = @"
public class Class1 : CppCli.CppBase2, CppCli.CppInterface1
{
}
";

            var libComp = CreateCompilationWithMscorlib(
                text: libText,
                references: new MetadataReference[] { ilAssemblyReference },
                compOptions: TestOptions.Dll,
                assemblyName: libAssemblyName);

            Assert.False(libComp.GetDiagnostics().Any());

            EmitResult emitResult;
            byte[] dllImage;
            using (var output = new MemoryStream())
            {
                emitResult = libComp.EmitMetadataOnly(output);
                dllImage = output.ToArray();
            }

            Assert.True(emitResult.Success);
            emitResult.Diagnostics.Verify();
            Assert.True(dllImage.Length > 0, "no metadata emitted");

            // NOTE: this DLL won't PEVerify because there are no method bodies.

            var class1 = libComp.GlobalNamespace.GetMember<SourceNamedTypeSymbol>("Class1");

            // We would prefer to check that the module used by Compiler.Emit does the right thing,
            // but we don't have access to that object, so we'll create our own and manipulate it
            // in the same way.
            var module = new PEAssemblyBuilder((SourceAssemblySymbol)class1.ContainingAssembly, null,
                OutputKind.DynamicallyLinkedLibrary, GetDefaultModulePropertiesForSerialization(), SpecializedCollections.EmptyEnumerable<ResourceDescription>());
            SynthesizedMetadataCompiler.ProcessSynthesizedMembers(libComp, module, default(CancellationToken));

            var class1TypeDef = (Cci.ITypeDefinition)class1;

            var symbolSynthesized = class1.GetSynthesizedExplicitImplementations(CancellationToken.None);
            var context = new EmitContext(module, null, new DiagnosticBag());
            var cciExplicit = class1TypeDef.GetExplicitImplementationOverrides(context);
            var cciMethods = class1TypeDef.GetMethods(context).Where(m => ((MethodSymbol)m).MethodKind != MethodKind.Constructor);

            context.Diagnostics.Verify();
            var symbolsSynthesizedCount = symbolSynthesized.Length;
            Assert.True(symbolsSynthesizedCount > 0, "Expected more than 0 synthesized method symbols.");
            Assert.Equal(symbolsSynthesizedCount, cciExplicit.Count());
            Assert.Equal(symbolsSynthesizedCount, cciMethods.Count());

            var libAssemblyReference = new MetadataImageReference(dllImage.AsImmutableOrNull());

            var exeText = @"
class Class2
{
    public static void Main()
    {
        CppCli.CppInterface1 c = new Class1();
        c.Method1(1);
        c.Method2(2);
    }
}  
";

            var exeComp = CreateCompilationWithMscorlib(
                text: exeText,
                references: new MetadataReference[] { ilAssemblyReference, libAssemblyReference },
                assemblyName: exeAssemblyName);

            Assert.False(exeComp.GetDiagnostics().Any());

            using (var output = new MemoryStream())
            {
                emitResult = exeComp.Emit(output);

                Assert.True(emitResult.Success);
                emitResult.Diagnostics.Verify();
                output.Flush();
                Assert.True(output.Length > 0, "no metadata emitted");
            }

            // NOTE: there's no point in trying to run the EXE since it depends on a DLL with no method bodies.
        }

        [WorkItem(539982, "DevDiv")]
        [Fact]
        public void EmitNestedLambdaWithAddPlusOperator()
        {
            CompileAndVerify(@"
public class C
{
    delegate int D(int i);
    delegate D E(int i);

    public static void Main()
    {
        D y = x => x + 1;
        E e = x => (y += (z => z + 1));
    }
}
");
        }

        [Fact, WorkItem(539983, "DevDiv")]
        public void EmitAlwaysFalseExpression()
        {
            CompileAndVerify(@"
class C
{
    static bool Foo(int i)
    {
        int y = 10;
        bool x = (y == null); // NYI: Implicit null conversion
        return x;
    }
}
");
        }

        [WorkItem(540146, "DevDiv")]
        [Fact]
        public void EmitLambdaInConstructorInitializer()
        {
            string source = @"
using System;
public class A
{
    public A(string x):this(()=>x) {}    
    public A(Func<string> x)
    {
        Console.WriteLine(x());
    }
    
    static void Main()
    {
        A a = new A(""Hello"");
    }
}";
            CompileAndVerify(source, expectedOutput: "Hello");
        }

        [WorkItem(540146, "DevDiv")]
        [Fact]
        public void EmitLambdaInConstructorBody()
        {
            string source = @"
using System;
public class A
{
    public string y = ""!"";

    public A(string x) {func(()=>x+y); }
    public A(Func<string> x)
    {
        Console.WriteLine(x());
    }
 
public void func(Func<string> x)
    {
        Console.WriteLine(x());
    }
    static void Main()
    {
        A a = new A(""Hello"");
    }
}";
            CompileAndVerify(source, expectedOutput: "Hello!");
        }

        [WorkItem(540146, "DevDiv")]
        [Fact]
        public void EmitLambdaInConstructorInitializerAndBody()
        {
            string source = @"
using System;
public class A
{
    public string y = ""!"";
    
    public A(string x):this(()=>x){func(()=>x+y);}    
    public A(Func<string> x)
    {
        Console.WriteLine(x());
    }
    public void func (Func<string> x)
    {
        Console.WriteLine(x());
    }
    static void Main()
    {
        A a = new A(""Hello"");
    }
}";
            CompileAndVerify(source, expectedOutput: @"
Hello
Hello!
");
        }

        [WorkItem(541786, "DevDiv")]
        [Fact]
        public void EmitInvocationExprInIfStatementNestedInsideCatch()
        {
            string source = @"
static class Test
{
    static public void Main()
    {
        int i1 = 45;

        try
        {
        }
        catch
        {
            if (i1.ToString() == null)
            {
            }
        }
        System.Console.WriteLine(i1);
    }
}";
            CompileAndVerify(source, expectedOutput: "45");
        }

        [WorkItem(541822, "DevDiv")]
        [Fact]
        public void EmitSwitchOnByteType()
        {
            string source = @"
using System;
public class Test
{
    public static object TestSwitch(byte val)
    {
        switch (val)
        {
            case (byte)0: return 0;
            case (byte)1: return 1;
            case (byte)0x7F: return (byte)0x7F;
            case (byte)0xFE: return (byte)0xFE;
            case (byte)0xFF: return (byte)0xFF;
            default: return null;
        }
    }
    public static void Main()
    {
        Console.WriteLine(TestSwitch(0));
    }
}
";
            CompileAndVerify(source, expectedOutput: "0");
        }

        [WorkItem(541823, "DevDiv")]
        [Fact]
        public void EmitSwitchOnIntTypeBoundary()
        {
            string source = @"
public class Test
{
    public static object TestSwitch(int val)
    {
        switch (val)
        {
            case (int)int.MinValue: 
            case (int)int.MinValue + 1: 
            case (int)short.MinValue: 
            case (int)short.MinValue + 1: 
            case (int)sbyte.MinValue: return 0;
            case (int)-1: return -1;
            case (int)0: return 0;
            case (int)1: return 0;
            case (int)0x7F: return 0;
            case (int)0xFE: return 0;
            case (int)0xFF: return 0;
            case (int)0x7FFE: return 0;
            case (int)0xFFFE: 
            case (int)0x7FFFFFFF: return 0;
            default: return null;
        }
    }
    public static void Main()
    {
        System.Console.WriteLine(TestSwitch(-1));
    }
}
";
            CompileAndVerify(source, expectedOutput: "-1");
        }

        [WorkItem(541824, "DevDiv")]
        [Fact]
        public void EmitSwitchOnLongTypeBoundary()
        {
            string source = @"
public class Test
{
    public static object TestSwitch(long val)
    {
        switch (val)
        {
            case (long)long.MinValue: return (long)long.MinValue;
            case (long)long.MinValue + 1: return (long)long.MinValue + 1;
            case (long)int.MinValue: return (long)int.MinValue;
            case (long)int.MinValue + 1: return (long)int.MinValue + 1;
            case (long)short.MinValue: return (long)short.MinValue;
            case (long)short.MinValue + 1: return (long)short.MinValue + 1;
            case (long)sbyte.MinValue: return (long)sbyte.MinValue;
            case (long)-1: return (long)-1;
            case (long)0: return (long)0;
            case (long)1: return (long)1;
            case (long)0x7F: return (long)0x7F;
            case (long)0xFE: return (long)0xFE;
            case (long)0xFF: return (long)0xFF;
            case (long)0x7FFE: return (long)0x7FFE;
            case (long)0x7FFF: return (long)0x7FFF;
            case (long)0xFFFE: return (long)0xFFFE;
            case (long)0xFFFF: return (long)0xFFFF;
            case (long)0x7FFFFFFE: return (long)0x7FFFFFFE;
            case (long)0x7FFFFFFF: return (long)0x7FFFFFFF;
            case (long)0xFFFFFFFE: return (long)0xFFFFFFFE;
            case (long)0xFFFFFFFF: return (long)0xFFFFFFFF;
            case (long)0x7FFFFFFFFFFFFFFE: return (long)0x7FFFFFFFFFFFFFFE;
            case (long)0x7FFFFFFFFFFFFFFF: return (long)0x7FFFFFFFFFFFFFFF;
            default: return null;
        }
    }
    public static void Main()
    {
        System.Console.WriteLine(TestSwitch(0));
    }
}
";
            CompileAndVerify(source, expectedOutput: "0");
        }

        [WorkItem(541840, "DevDiv")]
        [Fact]
        public void EmitSwitchOnLongTypeBoundary2()
        {
            string source = @"
public class Test
{
    private static int DoLong()
    {
        int ret = 2;
        long l = 0x7fffffffffffffffL;

        switch (l)
        {
            case 1L:
            case 9223372036854775807L:
                ret--;
                break;
            case -1L:
                break;
            default:
                break;
        }

        switch (l)
        {
            case 1L:
            case -1L:
                break;
            default:
                ret--;
                break;
        }
        return (ret);
    }

    public static void Main(string[] args)
    {
        System.Console.WriteLine(DoLong());
    }
}
";
            CompileAndVerify(source, expectedOutput: "0");
        }

        [WorkItem(541840, "DevDiv")]
        [Fact]
        public void EmitSwitchOnLongTypeBoundary3()
        {
            string source = @"
public class Test
{
    public static object TestSwitch(long val)
    {
        switch (val)
        {
            case (long)long.MinValue: return (long)long.MinValue;
            case (long)long.MinValue + 1: return (long)long.MinValue + 1;
            case (long)int.MinValue: return (long)int.MinValue;
            case (long)int.MinValue + 1: return (long)int.MinValue + 1;
            case (long)short.MinValue: return (long)short.MinValue;
            case (long)short.MinValue + 1: return (long)short.MinValue + 1;
            case (long)sbyte.MinValue: return (long)sbyte.MinValue;
            case (long)-1: return (long)-1;
            case (long)0: return (long)0;
            case (long)1: return (long)1;
            case (long)0x7F: return (long)0x7F;
            case (long)0xFE: return (long)0xFE;
            case (long)0xFF: return (long)0xFF;
            case (long)0x7FFE: return (long)0x7FFE;
            case (long)0x7FFF: return (long)0x7FFF;
            case (long)0xFFFE: return (long)0xFFFE;
            case (long)0xFFFF: return (long)0xFFFF;
            case (long)0x7FFFFFFE: return (long)0x7FFFFFFE;
            case (long)0x7FFFFFFF: return (long)0x7FFFFFFF;
            case (long)0xFFFFFFFE: return (long)0xFFFFFFFE;
            case (long)0xFFFFFFFF: return (long)0xFFFFFFFF;
            case (long)0x7FFFFFFFFFFFFFFE: return (long)0x7FFFFFFFFFFFFFFE;
            case (long)0x7FFFFFFFFFFFFFFF: return (long)0x7FFFFFFFFFFFFFFF;
            default: return null;
        }
    }
    public static void Main()
    {
        bool b1 = true;

        b1 = b1 && (((long)long.MinValue).Equals(TestSwitch(long.MinValue)));
        b1 = b1 && (((long)long.MinValue + 1).Equals(TestSwitch(long.MinValue + 1)));
        b1 = b1 && (((long)int.MinValue).Equals(TestSwitch(int.MinValue)));
        b1 = b1 && (((long)int.MinValue + 1).Equals(TestSwitch(int.MinValue + 1)));
        b1 = b1 && (((long)short.MinValue).Equals(TestSwitch(short.MinValue)));
        b1 = b1 && (((long)short.MinValue + 1).Equals(TestSwitch(short.MinValue + 1)));
        b1 = b1 && (((long)sbyte.MinValue).Equals(TestSwitch(sbyte.MinValue)));
        b1 = b1 && (((long)-1).Equals(TestSwitch(-1)));
        b1 = b1 && (((long)0).Equals(TestSwitch(0)));
        b1 = b1 && (((long)1).Equals(TestSwitch(1)));
        b1 = b1 && (((long)0x7F).Equals(TestSwitch(0x7F)));
        b1 = b1 && (((long)0xFE).Equals(TestSwitch(0xFE)));
        b1 = b1 && (((long)0xFF).Equals(TestSwitch(0xFF)));
        b1 = b1 && (((long)0x7FFE).Equals(TestSwitch(0x7FFE)));
        b1 = b1 && (((long)0x7FFF).Equals(TestSwitch(0x7FFF)));
        b1 = b1 && (((long)0xFFFE).Equals(TestSwitch(0xFFFE)));
        b1 = b1 && (((long)0xFFFF).Equals(TestSwitch(0xFFFF)));
        b1 = b1 && (((long)0x7FFFFFFE).Equals(TestSwitch(0x7FFFFFFE)));
        b1 = b1 && (((long)0x7FFFFFFF).Equals(TestSwitch(0x7FFFFFFF)));
        b1 = b1 && (((long)0xFFFFFFFE).Equals(TestSwitch(0xFFFFFFFE)));
        b1 = b1 && (((long)0xFFFFFFFF).Equals(TestSwitch(0xFFFFFFFF)));
        b1 = b1 && (((long)0x7FFFFFFFFFFFFFFE).Equals(TestSwitch(0x7FFFFFFFFFFFFFFE)));
        b1 = b1 && (((long)0x7FFFFFFFFFFFFFFF).Equals(TestSwitch(0x7FFFFFFFFFFFFFFF)));

        System.Console.Write(b1);
    }
}
";
            CompileAndVerify(source, expectedOutput: "True");
        }


        [WorkItem(541840, "DevDiv")]
        [Fact]
        public void EmitSwitchOnCharTypeBoundary()
        {
            string source = @"
public class Test
{
    public static object TestSwitch(char val)
    {
        switch (val)
        {
            case (char)0: return (char)0;
            case (char)1: return (char)1;
            case (char)0x7F: return (char)0x7F;
            case (char)0xFE: return (char)0xFE;
            case (char)0xFF: return (char)0xFF;
            case (char)0x7FFE: return (char)0x7FFE;
            case (char)0x7FFF: return (char)0x7FFF;
            case (char)0xFFFE: return (char)0xFFFE;
            case (char)0xFFFF: return (char)0xFFFF;
            default: return null;
        }
    }
    public static void Main()
    {
        bool b1 = true;

        b1 = b1 && (((char)0).Equals(TestSwitch((char)0)));
        b1 = b1 && (((char)1).Equals(TestSwitch((char)1)));
        b1 = b1 && (((char)0x7F).Equals(TestSwitch((char)0x7F)));
        b1 = b1 && (((char)0xFE).Equals(TestSwitch((char)0xFE)));
        b1 = b1 && (((char)0xFF).Equals(TestSwitch((char)0xFF)));
        b1 = b1 && (((char)0x7FFE).Equals(TestSwitch((char)0x7FFE)));
        b1 = b1 && (((char)0x7FFF).Equals(TestSwitch((char)0x7FFF)));
        b1 = b1 && (((char)0xFFFE).Equals(TestSwitch((char)0xFFFE)));
        b1 = b1 && (((char)0xFFFF).Equals(TestSwitch((char)0xFFFF)));

        System.Console.Write(b1);
    }
}
";
            CompileAndVerify(source, expectedOutput: "True");
        }

        [WorkItem(541840, "DevDiv")]
        [Fact]
        public void EmitSwitchOnUIntTypeBoundary()
        {
            string source = @"
public class Test
{
    public static object TestSwitch(uint val)
    {
        switch (val)
        {
            case (uint)0: return (uint)0;
            case (uint)1: return (uint)1;
            case (uint)0x7F: return (uint)0x7F;
            case (uint)0xFE: return (uint)0xFE;
            case (uint)0xFF: return (uint)0xFF;
            case (uint)0x7FFE: return (uint)0x7FFE;
            case (uint)0x7FFF: return (uint)0x7FFF;
            case (uint)0xFFFE: return (uint)0xFFFE;
            case (uint)0xFFFF: return (uint)0xFFFF;
            case (uint)0x7FFFFFFE: return (uint)0x7FFFFFFE;
            case (uint)0x7FFFFFFF: return (uint)0x7FFFFFFF;
            case (uint)0xFFFFFFFE: return (uint)0xFFFFFFFE;
            case (uint)0xFFFFFFFF: return (uint)0xFFFFFFFF;
            default: return null;
        }
    }
    public static void Main()
    {
        bool b1 = true;

        b1 = b1 && (((uint)0).Equals(TestSwitch(0)));
        b1 = b1 && (((uint)1).Equals(TestSwitch(1)));
        b1 = b1 && (((uint)0x7F).Equals(TestSwitch(0x7F)));
        b1 = b1 && (((uint)0xFE).Equals(TestSwitch(0xFE)));
        b1 = b1 && (((uint)0xFF).Equals(TestSwitch(0xFF)));
        b1 = b1 && (((uint)0x7FFE).Equals(TestSwitch(0x7FFE)));
        b1 = b1 && (((uint)0x7FFF).Equals(TestSwitch(0x7FFF)));
        b1 = b1 && (((uint)0xFFFE).Equals(TestSwitch(0xFFFE)));
        b1 = b1 && (((uint)0xFFFF).Equals(TestSwitch(0xFFFF)));
        b1 = b1 && (((uint)0x7FFFFFFE).Equals(TestSwitch(0x7FFFFFFE)));
        b1 = b1 && (((uint)0x7FFFFFFF).Equals(TestSwitch(0x7FFFFFFF)));
        b1 = b1 && (((uint)0xFFFFFFFE).Equals(TestSwitch(0xFFFFFFFE)));
        b1 = b1 && (((uint)0xFFFFFFFF).Equals(TestSwitch(0xFFFFFFFF)));

        System.Console.Write(b1);
    }
}

";
            CompileAndVerify(source, expectedOutput: "True");
        }

        [WorkItem(541824, "DevDiv")]
        [Fact]
        public void EmitSwitchOnUnsignedLongTypeBoundary()
        {
            string source = @"
public class Test
{
    public static object TestSwitch(ulong val)
    {
        switch (val)
        {
            case ulong.MinValue: return 0;
            case ulong.MaxValue: return 1;
            default: return 1;
        }
    }
    public static void Main()
    {
        System.Console.WriteLine(TestSwitch(0));
    }
}
";
            CompileAndVerify(source, expectedOutput: "0");
        }

        [WorkItem(541847, "DevDiv")]
        [Fact]
        public void EmitSwitchOnUnsignedLongTypeBoundary2()
        {
            string source = @"
public class Test
{
    public static object TestSwitch(ulong val)
    {
        switch (val)
        {
            case (ulong)0: return (ulong)0;
            case (ulong)1: return (ulong)1;
            case (ulong)0x7F: return (ulong)0x7F;
            case (ulong)0xFE: return (ulong)0xFE;
            case (ulong)0xFF: return (ulong)0xFF;
            case (ulong)0x7FFE: return (ulong)0x7FFE;
            case (ulong)0x7FFF: return (ulong)0x7FFF;
            case (ulong)0xFFFE: return (ulong)0xFFFE;
            case (ulong)0xFFFF: return (ulong)0xFFFF;
            case (ulong)0x7FFFFFFE: return (ulong)0x7FFFFFFE;
            case (ulong)0x7FFFFFFF: return (ulong)0x7FFFFFFF;
            case (ulong)0xFFFFFFFE: return (ulong)0xFFFFFFFE;
            case (ulong)0xFFFFFFFF: return (ulong)0xFFFFFFFF;
            case (ulong)0x7FFFFFFFFFFFFFFE: return (ulong)0x7FFFFFFFFFFFFFFE;
            case (ulong)0x7FFFFFFFFFFFFFFF: return (ulong)0x7FFFFFFFFFFFFFFF;
            case (ulong)0xFFFFFFFFFFFFFFFE: return (ulong)0xFFFFFFFFFFFFFFFE;
            case (ulong)0xFFFFFFFFFFFFFFFF: return (ulong)0xFFFFFFFFFFFFFFFF;
            default: return null;
        }
    }
    public static void Main()
    {
        bool b1 = true;
        b1 = b1 && (((ulong)0).Equals(TestSwitch(0)));
        b1 = b1 && (((ulong)1).Equals(TestSwitch(1)));
        b1 = b1 && (((ulong)0x7F).Equals(TestSwitch(0x7F)));
        b1 = b1 && (((ulong)0xFE).Equals(TestSwitch(0xFE)));
        b1 = b1 && (((ulong)0xFF).Equals(TestSwitch(0xFF)));
        b1 = b1 && (((ulong)0x7FFE).Equals(TestSwitch(0x7FFE)));
        b1 = b1 && (((ulong)0x7FFF).Equals(TestSwitch(0x7FFF)));
        b1 = b1 && (((ulong)0xFFFE).Equals(TestSwitch(0xFFFE)));
        b1 = b1 && (((ulong)0xFFFF).Equals(TestSwitch(0xFFFF)));
        b1 = b1 && (((ulong)0x7FFFFFFE).Equals(TestSwitch(0x7FFFFFFE)));
        b1 = b1 && (((ulong)0x7FFFFFFF).Equals(TestSwitch(0x7FFFFFFF)));
        b1 = b1 && (((ulong)0xFFFFFFFE).Equals(TestSwitch(0xFFFFFFFE)));
        b1 = b1 && (((ulong)0xFFFFFFFF).Equals(TestSwitch(0xFFFFFFFF)));
        b1 = b1 && (((ulong)0x7FFFFFFFFFFFFFFE).Equals(TestSwitch(0x7FFFFFFFFFFFFFFE)));
        b1 = b1 && (((ulong)0x7FFFFFFFFFFFFFFF).Equals(TestSwitch(0x7FFFFFFFFFFFFFFF)));
        b1 = b1 && (((ulong)0xFFFFFFFFFFFFFFFE).Equals(TestSwitch(0xFFFFFFFFFFFFFFFE)));
        b1 = b1 && (((ulong)0xFFFFFFFFFFFFFFFF).Equals(TestSwitch(0xFFFFFFFFFFFFFFFF)));

        System.Console.Write(b1);
    }
}
";
            CompileAndVerify(source, expectedOutput: "True");
        }

        [WorkItem(541839, "DevDiv")]
        [Fact]
        public void EmitSwitchOnShortTypeBoundary()
        {
            string source = @"
public class Test
{
    public static object TestSwitch(short val)
    {
        switch (val)
        {
            case (short)short.MinValue: return (short)short.MinValue;
            case (short)short.MinValue + 1: return (short)short.MinValue + 1;
            case (short)sbyte.MinValue: return (short)sbyte.MinValue;
            case (short)-1: return (short)-1;
            case (short)0: return (short)0;
            case (short)1: return (short)1;
            case (short)0x7F: return (short)0x7F;
            case (short)0xFE: return (short)0xFE;
            case (short)0xFF: return (short)0xFF;
            case (short)0x7FFE: return (short)0x7FFE;
            case (short)0x7FFF: return (short)0x7FFF;
            default: return null;
        }
    }

    public static void Main()
    {
        System.Console.WriteLine(TestSwitch(1));
    }
}
";
            CompileAndVerify(source, expectedOutput: "1");
        }

        [WorkItem(542563, "DevDiv")]
        [Fact]
        public void IncompleteIndexerDeclWithSyntaxErrors()
        {
            string source = @"
public class Test
{
    public sealed object this";

            var compilation = CreateCompilationWithMscorlib(source);

            EmitResult emitResult;
            using (var output = new MemoryStream())
            {
                emitResult = compilation.Emit(output, null, null, null);
            }

            Assert.False(emitResult.Success);
            Assert.NotEmpty(emitResult.Diagnostics);
        }

        [WorkItem(541639, "DevDiv")]
        [Fact]
        public void VariableDeclInsideSwitchCaptureInLambdaExpr()
        {
            string source = @"
using System;

class C
{
    public static void Main()
    {
        switch (10)
        {
            default:
                int i = 10;
                Func<int> f1 = () => i;
                break;
        }
    }
}";

            var compilation = CreateCompilationWithMscorlib(source);

            EmitResult emitResult;
            using (var output = new MemoryStream())
            {
                emitResult = compilation.Emit(output, null, null, null);
            }

            Assert.True(emitResult.Success);
        }

        [WorkItem(541639, "DevDiv")]
        [Fact]
        public void MultipleVariableDeclInsideSwitchCaptureInLambdaExpr()
        {
            string source = @"
using System;

class C
{
    public static void Main()
    {
        int i = 0;
        switch (i)
        {
            case 0:
                int j = 0;
                Func<int> f1 = () => i + j;
                break;

            default:
                int k = 0;
                Func<int> f2 = () => i + k;
                break;
        }
    }
}";

            var compilation = CreateCompilationWithMscorlib(source);

            EmitResult emitResult;
            using (var output = new MemoryStream())
            {
                emitResult = compilation.Emit(output, null, null, null);
            }

            Assert.True(emitResult.Success);
        }
        #region "PE and metadata bits"

        [Fact]
        public void CheckRuntimeMDVersion()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            var compilation = CSharpCompilation.Create(
                "v2Fx.exe", 
                new[] { Parse(source) }, 
                new[] { TestReferences.NetFx.v2_0_50727.mscorlib });

            //EDMAURER this is built with a 2.0 mscorlib. The runtimeMetadataVersion should be the same as the runtimeMetadataVersion stored in the assembly
            //that contains System.Object.
            var metadataReader = ModuleMetadata.CreateFromImageStream(compilation.EmitToStream()).MetadataReader;
            Assert.Equal("v2.0.50727", metadataReader.MetadataVersion);
        }

        [Fact]
        public void CheckCorflags()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            PEHeaders peHeaders;

            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.AnyCpu));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(CorFlags.ILOnly, peHeaders.CorHeader.Flags);

            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.X86));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(CorFlags.ILOnly | CorFlags.Requires32Bit, peHeaders.CorHeader.Flags);

            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.X64));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(CorFlags.ILOnly, peHeaders.CorHeader.Flags);
            Assert.True(peHeaders.Requires64Bits());
            Assert.True(peHeaders.RequiresAmdInstructionSet());

            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.AnyCpu32BitPreferred));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.False(peHeaders.Requires64Bits());
            Assert.False(peHeaders.RequiresAmdInstructionSet());
            Assert.Equal(CorFlags.ILOnly | CorFlags.Requires32Bit | CorFlags.Prefers32Bit, peHeaders.CorHeader.Flags);

            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.Arm));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.False(peHeaders.Requires64Bits());
            Assert.False(peHeaders.RequiresAmdInstructionSet());
            Assert.Equal(CorFlags.ILOnly, peHeaders.CorHeader.Flags);
        }

        [Fact]
        public void CheckCOFFAndPEOptionalHeaders32()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            var compilation = CreateCompilationWithMscorlib(source,
                compOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var peHeaders = new PEHeaders(compilation.EmitToStream());

            //interesting COFF bits
            Assert.False(peHeaders.Requires64Bits());
            Assert.True(peHeaders.IsDll);
            Assert.False(peHeaders.IsExe);
            //interesting Optional PE header bits
            //We will use a range beginning with 0x30 to identify the Roslyn compiler family.
            Assert.Equal(0x30, peHeaders.PEHeader.MajorLinkerVersion);   
            Assert.Equal(0, peHeaders.PEHeader.MinorLinkerVersion); 
            Assert.Equal(0x10000000u, peHeaders.PEHeader.ImageBase);
            Assert.Equal(0x200, peHeaders.PEHeader.FileAlignment);
            Assert.Equal(0x8540u, (ushort)peHeaders.PEHeader.DllCharacteristics);  //DYNAMIC_BASE | NX_COMPAT | NO_SEH | TERMINAL_SERVER_AWARE
            //Verify additional items 
            Assert.Equal(0x00100000u, peHeaders.PEHeader.SizeOfStackReserve);
            Assert.Equal(0x1000u, peHeaders.PEHeader.SizeOfStackCommit);
            Assert.Equal(0x00100000u, peHeaders.PEHeader.SizeOfHeapReserve);
            Assert.Equal(0x1000u, peHeaders.PEHeader.SizeOfHeapCommit);
        }

        [Fact]
        public void CheckCOFFAndPEOptionalHeaders64()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            var compilation = CreateCompilationWithMscorlib(source,
                compOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithPlatform(Platform.X64));

            var peHeaders = new PEHeaders(compilation.EmitToStream());

            //interesting COFF bits
            Assert.True(peHeaders.Requires64Bits());
            Assert.True(peHeaders.IsDll);
            Assert.False(peHeaders.IsExe);
            //interesting Optional PE header bits
            //We will use a range beginning with 0x30 to identify the Roslyn compiler family.
            Assert.Equal(0x30, peHeaders.PEHeader.MajorLinkerVersion);
            Assert.Equal(0, peHeaders.PEHeader.MinorLinkerVersion);
            // the default value is the same as the 32 bit default value
            Assert.Equal(0x0000000180000000u, peHeaders.PEHeader.ImageBase); 
            Assert.Equal(0x00000200, peHeaders.PEHeader.FileAlignment);      //doesn't change based on architecture.
            Assert.Equal(0x8540u, (ushort)peHeaders.PEHeader.DllCharacteristics);  //DYNAMIC_BASE | NX_COMPAT | NO_SEH | TERMINAL_SERVER_AWARE
            //Verify additional items
            Assert.Equal(0x00400000u, peHeaders.PEHeader.SizeOfStackReserve);
            Assert.Equal(0x4000u, peHeaders.PEHeader.SizeOfStackCommit);
            Assert.Equal(0x00100000u, peHeaders.PEHeader.SizeOfHeapReserve);
            Assert.Equal(0x2000u, peHeaders.PEHeader.SizeOfHeapCommit);
            Assert.Equal(0x8664, (ushort)peHeaders.CoffHeader.Machine);     //AMD64 (K8)

            //default for non-arm, non-appcontainer outputs. EDMAURER: This is an intentional change from Dev11.
            //Should we find that it is too disruptive. We will consider rolling back.
            //It turns out to be too disruptive. Rolling back to 4.0
            Assert.Equal(4, peHeaders.PEHeader.MajorSubsystemVersion);    
            Assert.Equal(0, peHeaders.PEHeader.MinorSubsystemVersion);

            //The following ensure that the runtime startup stub was not emitted. It is not needed on modern operating systems.
            Assert.Equal(0, peHeaders.PEHeader.ImportAddressTableDirectory.RelativeVirtualAddress);
            Assert.Equal(0, peHeaders.PEHeader.ImportAddressTableDirectory.Size);
            Assert.Equal(0, peHeaders.PEHeader.ImportTableDirectory.RelativeVirtualAddress);
            Assert.Equal(0, peHeaders.PEHeader.ImportTableDirectory.Size);
            Assert.Equal(0, peHeaders.PEHeader.BaseRelocationTableDirectory.RelativeVirtualAddress);
            Assert.Equal(0, peHeaders.PEHeader.BaseRelocationTableDirectory.Size);
        }

        [Fact]
        public void CheckCOFFAndPEOptionalHeadersARM()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            var compilation = CreateCompilationWithMscorlib(source,
                compOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithPlatform(Platform.Arm));

            var peHeaders = new PEHeaders(compilation.EmitToStream());

            //interesting COFF bits
            Assert.False(peHeaders.Requires64Bits());
            Assert.True(peHeaders.IsDll);
            Assert.False(peHeaders.IsExe);
            //interesting Optional PE header bits
            //We will use a range beginning with 0x30 to identify the Roslyn compiler family.
            Assert.Equal(0x30, peHeaders.PEHeader.MajorLinkerVersion);
            Assert.Equal(0, peHeaders.PEHeader.MinorLinkerVersion);
            // the default value is the same as the 32 bit default value
            Assert.Equal(0x10000000u, peHeaders.PEHeader.ImageBase);
            Assert.Equal(0x200, peHeaders.PEHeader.FileAlignment);
            Assert.Equal(0x8540u, (ushort)peHeaders.PEHeader.DllCharacteristics);  //DYNAMIC_BASE | NX_COMPAT | NO_SEH | TERMINAL_SERVER_AWARE
            Assert.Equal(0x01c4, (ushort)peHeaders.CoffHeader.Machine);
            Assert.Equal(6, peHeaders.PEHeader.MajorSubsystemVersion);    //Arm targets only run on 6.2 and above
            Assert.Equal(2, peHeaders.PEHeader.MinorSubsystemVersion);
            //The following ensure that the runtime startup stub was not emitted. It is not needed on modern operating systems.
            Assert.Equal(0, peHeaders.PEHeader.ImportAddressTableDirectory.RelativeVirtualAddress);
            Assert.Equal(0, peHeaders.PEHeader.ImportAddressTableDirectory.Size);
            Assert.Equal(0, peHeaders.PEHeader.ImportTableDirectory.RelativeVirtualAddress);
            Assert.Equal(0, peHeaders.PEHeader.ImportTableDirectory.Size);
            Assert.Equal(0, peHeaders.PEHeader.BaseRelocationTableDirectory.RelativeVirtualAddress);
            Assert.Equal(0, peHeaders.PEHeader.BaseRelocationTableDirectory.Size);
        }

        [Fact]
        public void CheckCOFFAndPEOptionalHeaders32Exe()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            var compilation = CreateCompilationWithMscorlib(source,
                compOptions: TestOptions.Exe.WithPlatform(Platform.AnyCpu));

            var peHeaders = new PEHeaders(compilation.EmitToStream());

            //interesting COFF bits
            Assert.False(peHeaders.Requires64Bits());
            Assert.True(peHeaders.IsExe);
            Assert.False(peHeaders.IsDll);
            //interesting Optional PE header bits
            //We will use a range beginning with 0x30 to identify the Roslyn compiler family.
            Assert.Equal(0x30, peHeaders.PEHeader.MajorLinkerVersion);
            Assert.Equal(0, peHeaders.PEHeader.MinorLinkerVersion);
            Assert.Equal(0x00400000ul, peHeaders.PEHeader.ImageBase);
            Assert.Equal(0x00000200, peHeaders.PEHeader.FileAlignment);
            Assert.True(peHeaders.IsConsoleApplication); //should change if this is a windows app.
            Assert.Equal(0x8540u, (ushort)peHeaders.PEHeader.DllCharacteristics);  //DYNAMIC_BASE | NX_COMPAT | NO_SEH | TERMINAL_SERVER_AWARE
            Assert.Equal(0x00100000u, peHeaders.PEHeader.SizeOfStackReserve);
            Assert.Equal(0x1000u, peHeaders.PEHeader.SizeOfStackCommit);
            Assert.Equal(0x00100000u, peHeaders.PEHeader.SizeOfHeapReserve);
            Assert.Equal(0x1000u, peHeaders.PEHeader.SizeOfHeapCommit);

            //The following ensure that the runtime startup stub was emitted. It is not needed on modern operating systems.
            Assert.NotEqual(0, peHeaders.PEHeader.ImportAddressTableDirectory.RelativeVirtualAddress);
            Assert.NotEqual(0, peHeaders.PEHeader.ImportAddressTableDirectory.Size);
            Assert.NotEqual(0, peHeaders.PEHeader.ImportTableDirectory.RelativeVirtualAddress);
            Assert.NotEqual(0, peHeaders.PEHeader.ImportTableDirectory.Size);
            Assert.NotEqual(0, peHeaders.PEHeader.BaseRelocationTableDirectory.RelativeVirtualAddress);
            Assert.NotEqual(0, peHeaders.PEHeader.BaseRelocationTableDirectory.Size);
        }

        [Fact]
        public void CheckCOFFAndPEOptionalHeaders64Exe()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.X64));
            var peHeaders = new PEHeaders(compilation.EmitToStream());

            //interesting COFF bits
            Assert.True(peHeaders.Requires64Bits());
            Assert.True(peHeaders.IsExe);
            Assert.False(peHeaders.IsDll);
            //interesting Optional PE header bits
            //We will use a range beginning with 0x30 to identify the Roslyn compiler family.
            Assert.Equal(0x30, peHeaders.PEHeader.MajorLinkerVersion);
            Assert.Equal(0, peHeaders.PEHeader.MinorLinkerVersion);
            Assert.Equal(0x0000000140000000ul, peHeaders.PEHeader.ImageBase); 
            Assert.Equal(0x200, peHeaders.PEHeader.FileAlignment);  //doesn't change based on architecture
            Assert.True(peHeaders.IsConsoleApplication); //should change if this is a windows app.
            Assert.Equal(0x8540u, (ushort)peHeaders.PEHeader.DllCharacteristics);  //DYNAMIC_BASE | NX_COMPAT | NO_SEH | TERMINAL_SERVER_AWARE
            Assert.Equal(0x00400000u, peHeaders.PEHeader.SizeOfStackReserve);
            Assert.Equal(0x4000u, peHeaders.PEHeader.SizeOfStackCommit);
            Assert.Equal(0x00100000u, peHeaders.PEHeader.SizeOfHeapReserve); //no sure why we don't bump this up relative to 32bit as well.
            Assert.Equal(0x2000u, peHeaders.PEHeader.SizeOfHeapCommit);
        }

        [Fact]
        public void CheckDllCharacteristicsHighEntropyVA()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithHighEntropyVirtualAddressSpace(true));
            var peHeaders = new PEHeaders(compilation.EmitToStream());

            //interesting COFF bits
            Assert.Equal(0x8560u, (ushort)peHeaders.PEHeader.DllCharacteristics);  //DYNAMIC_BASE | NX_COMPAT | NO_SEH | TERMINAL_SERVER_AWARE | HIGH_ENTROPY_VA (0x20)
        }

        [WorkItem(764418, "DevDiv")]
        [Fact]
        public void CheckDllCharacteristicsWinRtApp()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.WinRtExe);
            var peHeaders = new PEHeaders(compilation.EmitToStream());

            //interesting COFF bits
            Assert.Equal(0x9540u, (ushort)peHeaders.PEHeader.DllCharacteristics);  //DYNAMIC_BASE | NX_COMPAT | NO_SEH | TERMINAL_SERVER_AWARE | IMAGE_DLLCHARACTERISTICS_APPCONTAINER (0x1000)
        }

        [Fact]
        public void CheckBaseAddress()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            // last four hex digits get zero'ed
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithBaseAddress(0x0000000010111111));
            var peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(0x10110000ul, peHeaders.PEHeader.ImageBase);

            // test rounding up of values
            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithBaseAddress(0x8000));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(0x10000ul, peHeaders.PEHeader.ImageBase);

            // values less than 0x8000 get default baseaddress
            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithBaseAddress(0x7fff));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(0x00400000u, peHeaders.PEHeader.ImageBase);

            // default for 32bit
            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.X86));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(0x00400000u, peHeaders.PEHeader.ImageBase);

            // max for 32bit
            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.X86).WithBaseAddress(0xffff7fff));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(0xffff0000ul, peHeaders.PEHeader.ImageBase);

            // max+1 for 32bit
            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.X86).WithBaseAddress(0xffff8000));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(0x00400000u, peHeaders.PEHeader.ImageBase);

            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.X64));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(0x0000000140000000u, peHeaders.PEHeader.ImageBase);

            // max for 64bit
            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.X64).WithBaseAddress(0xffffffffffff7fff));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(0xffffffffffff0000ul, peHeaders.PEHeader.ImageBase);

            // max+1 for 64bit
            compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithPlatform(Platform.X64).WithBaseAddress(0xffffffffffff8000));
            peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(0x0000000140000000u, peHeaders.PEHeader.ImageBase);
        }

        [Fact]
        public void CheckFileAlignment()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.Exe.WithFileAlignment(1024));
            var peHeaders = new PEHeaders(compilation.EmitToStream());
            Assert.Equal(1024, peHeaders.PEHeader.FileAlignment);
        }

        #endregion

        [Fact]
        public void Bug10273()
        {
            string source = @"
using System;

    public struct C1
    {
        public int C;
        public static int B = 12;

        public void F(){}

        public int A;
    }

    public delegate void B();

    public class A1
    {
        public int C;
        public static int  B = 12;

        public void F(){}

        public int A;

        public int I {get; set;}

        public void E(){}

        public int H {get; set;}
        public int G {get; set;}

        public event Action L;
        public void D(){}

        public event Action K;
        public event Action J;

        public partial class O { }
        public partial class N { }
        public partial class M { }

        public partial class N{}
        public partial class M{}
        public partial class O{}

        public void F(int x){}
        public void E(int x){}
        public void D(int x){}
    }

    namespace F{}

    public class G {}

    namespace E{}
    namespace D{}
";

            CompileAndVerify(source,
                             sourceSymbolValidator: delegate(ModuleSymbol m)
                             {
                                 string[] expectedGlobalMembers = { "C1", "B", "A1", "F", "G", "E", "D" };
                                 var actualGlobalMembers = m.GlobalNamespace.GetMembers().ToArray();
                                 for (int i = 0; i < System.Math.Max(expectedGlobalMembers.Length, actualGlobalMembers.Length); i++)
                                 {
                                     Assert.Equal(expectedGlobalMembers[i], actualGlobalMembers[i].Name);
                                 }

                                 string[] expectedAMembers = {".cctor",
                                                        "C", "B", "F", "A",
                                                        "<I>k__BackingField", "I", "get_I", "set_I",
                                                        "E",
                                                        "<H>k__BackingField", "H", "get_H", "set_H",
                                                        "<G>k__BackingField", "G", "get_G", "set_G",
                                                        "add_L", "remove_L", "L",
                                                        "D",
                                                        "add_K", "remove_K", "K",
                                                        "add_J", "remove_J", "J",
                                                        "O", "N", "M",
                                                        "F", "E", "D", 
                                                        ".ctor"};

                                 var actualAMembers = ((SourceModuleSymbol)m).GlobalNamespace.GetTypeMembers("A1").Single().GetMembers().ToArray();

                                 for (int i = 0; i < System.Math.Max(expectedAMembers.Length, actualAMembers.Length); i++)
                                 {
                                     Assert.Equal(expectedAMembers[i], actualAMembers[i].Name);
                                 }

                                 string[] expectedBMembers = { ".ctor", "BeginInvoke", "EndInvoke", "Invoke" };
                                 var actualBMembers = ((SourceModuleSymbol)m).GlobalNamespace.GetTypeMembers("B").Single().GetMembers().ToArray();

                                 for (int i = 0; i < System.Math.Max(expectedBMembers.Length, actualBMembers.Length); i++)
                                 {
                                     Assert.Equal(expectedBMembers[i], actualBMembers[i].Name);
                                 }

                                 string[] expectedCMembers = {".cctor",
                                                            "C", "B", "F", "A", 
                                                            ".ctor"};
                                 var actualCMembers = ((SourceModuleSymbol)m).GlobalNamespace.GetTypeMembers("C1").Single().GetMembers().ToArray();

                                 AssertEx.SetEqual(expectedCMembers, actualCMembers.Select(s => s.Name));
                             },
                             symbolValidator: delegate(ModuleSymbol m)
                             {
                                 string[] expectedAMembers = {"C", "B", "A",
                                                        "F",
                                                        "get_I", "set_I",
                                                        "E",
                                                        "get_H", "set_H",
                                                        "get_G", "set_G",
                                                        "add_L", "remove_L",
                                                        "D",
                                                        "add_K", "remove_K",
                                                        "add_J", "remove_J",
                                                        "F", "E", "D",
                                                        ".ctor",
                                                        "I", "H", "G",
                                                        "L", "K", "J",
                                                        "O", "N", "M",
                                                        };

                                 var actualAMembers = m.GlobalNamespace.GetTypeMembers("A1").Single().GetMembers().ToArray();

                                 AssertEx.SetEqual(expectedAMembers, actualAMembers.Select(s => s.Name));

                                 string[] expectedBMembers = { ".ctor", "BeginInvoke", "EndInvoke", "Invoke" };
                                 var actualBMembers = m.GlobalNamespace.GetTypeMembers("B").Single().GetMembers().ToArray();

                                 AssertEx.SetEqual(expectedBMembers, actualBMembers.Select(s => s.Name));

                                 string[] expectedCMembers = { "C", "B", "A", ".ctor", "F" };
                                 var actualCMembers = m.GlobalNamespace.GetTypeMembers("C1").Single().GetMembers().ToArray();

                                 AssertEx.SetEqual(expectedCMembers, actualCMembers.Select(s => s.Name));
                             }
                            );

        }

        [WorkItem(543763, "DevDiv")]
        [Fact()]
        public void OptionalParamTypeAsDecimal()
        {
            string source = @"
public class Test
{
    public static decimal Foo(decimal d = 0)
    {
        return d;
    }

    public static void Main()
    {
        System.Console.WriteLine(Foo());
    }
}
";
            CompileAndVerify(source, expectedOutput: "0");
        }

        [WorkItem(543932, "DevDiv")]
        [Fact]
        public void BranchCodeGenOnConditionDebug()
        {
            string source = @"
public class Test
{
    public static void Main()
    {
        int a_int = 0;
        if ((a_int != 0) || (false))
        {
            System.Console.WriteLine(""CheckPoint-1"");
        }

        System.Console.WriteLine(""CheckPoint-2"");
    }
}";

            var compilation = CreateCompilationWithMscorlib(source);

            CompileAndVerify(source, expectedOutput: "CheckPoint-2", emitPdb: false);
            CompileAndVerify(source, expectedOutput: "CheckPoint-2", emitPdb: true);
        }

        [Fact]
        public void EmitAssemblyWithGivenName()
        {
            var name = "a";
            var extension = ".dll";
            var nameWithExtension = name + extension;

            var compilation = CreateCompilationWithMscorlib("class A { }", compOptions: TestOptions.Dll, assemblyName: name);
            compilation.VerifyDiagnostics();

            var assembly = compilation.Assembly;
            Assert.Equal(name, assembly.Name);

            var module = assembly.Modules.Single();
            Assert.Equal(nameWithExtension, module.Name);

            var stream = new MemoryStream();
            Assert.True(compilation.Emit(stream, nameWithExtension).Success);

            using (ModuleMetadata metadata = ModuleMetadata.CreateFromImage(stream.ToImmutable()))
            {
                var peReader = metadata.Module.GetMetadataReader();

                Assert.True(peReader.IsAssembly);

                Assert.Equal(name, peReader.GetString(peReader.GetAssemblyDefinition().Name));
                Assert.Equal(nameWithExtension, peReader.GetString(peReader.GetModuleDefinition().Name));
            }
        }

        // a.netmodule to b.netmodule
        [Fact]
        public void EmitModuleWithDifferentName()
        {
            var name = "a";
            var extension = ".netmodule";
            var outputName = "b";

            var compilation = CreateCompilationWithMscorlib("class A { }", compOptions: TestOptions.NetModule.WithModuleName(name + extension), assemblyName: null);
            compilation.VerifyDiagnostics();

            var assembly = compilation.Assembly;
            Assert.Equal("?", assembly.Name);

            var module = assembly.Modules.Single();
            Assert.Equal(name + extension, module.Name);

            var stream = new MemoryStream();
            Assert.True(compilation.Emit(stream, outputName + extension).Success);

            using (ModuleMetadata metadata = ModuleMetadata.CreateFromImage(stream.ToImmutable()))
            {
                var peReader = metadata.Module.GetMetadataReader();

                Assert.False(peReader.IsAssembly);

                Assert.Equal(outputName + extension, peReader.GetString(peReader.GetModuleDefinition().Name));
            }
        }

        // a.dll to b.dll - expected use case
        [Fact]
        public void EmitAssemblyWithDifferentName1()
        {
            var name = "a";
            var extension = ".dll";
            var nameOverride = "b";

            var compilation = CreateCompilationWithMscorlib("class A { }", compOptions: TestOptions.Dll, assemblyName: name);
            compilation.VerifyDiagnostics();

            var assembly = compilation.Assembly;
            Assert.Equal(name, assembly.Name);

            var module = assembly.Modules.Single();
            Assert.Equal(name + extension, module.Name);

            var stream = new MemoryStream();
            Assert.True(compilation.Emit(stream, nameOverride + extension).Success);

            using (ModuleMetadata metadata = ModuleMetadata.CreateFromImage(stream.ToImmutable()))
            {
                var peReader = metadata.Module.GetMetadataReader();

                Assert.True(peReader.IsAssembly);

                Assert.Equal(nameOverride, peReader.GetString(peReader.GetAssemblyDefinition().Name));
                Assert.Equal(nameOverride + extension, peReader.GetString(peReader.GetModuleDefinition().Name));
            }
        }

        // a.dll to b - odd, but allowable
        [Fact]
        public void EmitAssemblyWithDifferentName2()
        {
            var name = "a";
            var extension = ".dll";
            var nameOverride = "b";

            var compilation = CreateCompilationWithMscorlib("class A { }", compOptions: TestOptions.Dll, assemblyName: name);
            compilation.VerifyDiagnostics();

            var assembly = compilation.Assembly;
            Assert.Equal(name, assembly.Name);

            var module = assembly.Modules.Single();
            Assert.Equal(name + extension, module.Name);

            var stream = new MemoryStream();
            Assert.True(compilation.Emit(stream, nameOverride).Success);

            using (ModuleMetadata metadata = ModuleMetadata.CreateFromImage(stream.ToImmutable()))
            {
                var peReader = metadata.Module.GetMetadataReader();

                Assert.True(peReader.IsAssembly);

                Assert.Equal(nameOverride, peReader.GetString(peReader.GetAssemblyDefinition().Name));
                Assert.Equal(nameOverride, peReader.GetString(peReader.GetModuleDefinition().Name));
            }
        }

        // a to b.dll - odd, but allowable
        [Fact]
        public void EmitAssemblyWithDifferentName3()
        {
            var name = "a";
            var extension = ".dll";
            var nameOverride = "b";

            var compilation = CreateCompilationWithMscorlib("class A { }", compOptions: TestOptions.Dll, assemblyName: name);
            compilation.VerifyDiagnostics();

            var assembly = compilation.Assembly;
            Assert.Equal(name, assembly.Name);

            var module = assembly.Modules.Single();
            Assert.Equal(name + extension, module.Name);

            var stream = new MemoryStream();
            Assert.True(compilation.Emit(stream, nameOverride + extension).Success);

            using (ModuleMetadata metadata = ModuleMetadata.CreateFromImage(stream.ToImmutable()))
            {
                var peReader = metadata.Module.GetMetadataReader();

                Assert.True(peReader.IsAssembly);

                Assert.Equal(nameOverride, peReader.GetString(peReader.GetAssemblyDefinition().Name));
                Assert.Equal(nameOverride + extension, peReader.GetString(peReader.GetModuleDefinition().Name));
            }
        }

        // a to b - odd, but allowable
        [Fact]
        public void EmitAssemblyWithDifferentName4()
        {
            var name = "a";
            var extension = ".dll";
            var nameOverride = "b";

            var compilation = CreateCompilationWithMscorlib("class A { }", compOptions: TestOptions.Dll, assemblyName: name);
            compilation.VerifyDiagnostics();

            var assembly = compilation.Assembly;
            Assert.Equal(name, assembly.Name);

            var module = assembly.Modules.Single();
            Assert.Equal(name + extension, module.Name);

            var stream = new MemoryStream();
            Assert.True(compilation.Emit(stream, nameOverride).Success);

            using (ModuleMetadata metadata = ModuleMetadata.CreateFromImage(stream.ToImmutable()))
            {
                var peReader = metadata.Module.GetMetadataReader();

                Assert.True(peReader.IsAssembly);

                Assert.Equal(nameOverride, peReader.GetString(peReader.GetAssemblyDefinition().Name));
                Assert.Equal(nameOverride, peReader.GetString(peReader.GetModuleDefinition().Name));
            }
        }

        [WorkItem(570975, "DevDiv")]
        [Fact]
        public void Bug570975()
        {
            var source = @"
public sealed class ContentType
{       
	public void M(System.Collections.Generic.Dictionary<object, object> p)
	{   
		foreach (object paramterKey in p.Keys)
		{
		}
	}
}";

            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.NetModule, assemblyName: "ContentType");
            compilation.VerifyDiagnostics();

            using (ModuleMetadata block = ModuleMetadata.CreateFromImageStream(compilation.EmitToStream()))
            {
                var reader = block.MetadataReader;
                foreach (var typeRef in reader.TypeReferences)
                {
                    Handle scope = reader.GetTypeReference(typeRef).ResolutionScope;
                    if (scope.HandleType == HandleType.TypeReference)
                    {
                        Assert.InRange(reader.GetRowNumber(scope), 1, reader.GetRowNumber(typeRef) - 1);
                    }
                }
            }
        }

        [Fact]
        public void IllegalNameOverride()
        {
            var compilation = CreateCompilationWithMscorlib("class A { }", compOptions: TestOptions.Dll);
            compilation.VerifyDiagnostics();

            Assert.Throws<ArgumentException>(() => compilation.Emit(new MemoryStream(), outputName: " "));
        }

        // Verify via MetadataReader - comp option
        [Fact]
        public void CheckUnsafeAttributes3()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            // Setting the CompilationOption.AllowUnsafe causes an entry to be inserted into the DeclSecurity table
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.UnsafeDll);
            compilation.VerifyDiagnostics();
            ValidateDeclSecurity(compilation,
                new DeclSecurityEntry
                {
                    ActionFlags = DeclarativeSecurityAction.RequestMinimum,
                    ParentKind = SymbolKind.Assembly,
                    PermissionSet =
                        "." + // always start with a dot
                        "\u0001" + // number of attributes (small enough to fit in 1 byte)
                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u0015" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u0010" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "SkipVerification" + // property name
                        "\u0001", // argument value (true)
                });
        }

        // Verify via MetadataReader - comp option, module case
        [Fact]
        public void CheckUnsafeAttributes4()
        {
            string source = @"
class C
{
    public static void Main()
    {
    }
}";
            // Setting the CompilationOption.AllowUnsafe causes an entry to be inserted into the DeclSecurity table
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.UnsafeDll.WithOutputKind(OutputKind.NetModule));
            compilation.VerifyDiagnostics();
            ValidateDeclSecurity(compilation); //no assembly => no decl security row
        }

        // Verify via MetadataReader - attr in source
        [Fact]
        public void CheckUnsafeAttributes5()
        {
            // Writing the attributes in the source should have the same effect as the compilation option.
            string source = @"
using System.Security;
using System.Security.Permissions;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[module: UnverifiableCode]

class C
{
    public static void Main()
    {
    }
}";

            var compilation = CreateCompilationWithMscorlib(source);
            compilation.VerifyDiagnostics(
                // (5,31): warning CS0618: 'System.Security.Permissions.SecurityAction.RequestMinimum' is obsolete: 'Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.'
                // [assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
                Diagnostic(ErrorCode.WRN_DeprecatedSymbolStr, "SecurityAction.RequestMinimum").WithArguments("System.Security.Permissions.SecurityAction.RequestMinimum", "Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information."));

            ValidateDeclSecurity(compilation, 
                new DeclSecurityEntry
                {
                    ActionFlags = DeclarativeSecurityAction.RequestMinimum,
                    ParentKind = SymbolKind.Assembly,
                    PermissionSet =  
                        "." + // always start with a dot
                        "\u0001" + // number of attributes (small enough to fit in 1 byte)
                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u0015" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u0010" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "SkipVerification" + // property name
                        "\u0001", // argument value (true)
                });
        }

        // Verify via MetadataReader - two attrs in source, same action
        [Fact]
        public void CheckUnsafeAttributes6()
        {
            string source = @"
using System.Security;
using System.Security.Permissions;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, RemotingConfiguration = true)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, UnmanagedCode = true)]
[module: UnverifiableCode]

class C
{
    public static void Main()
    {
    }
}";
            // The attributes have the SecurityAction, so they should be merged into a single permission set.
            var compilation = CreateCompilationWithMscorlib(source);
            compilation.VerifyDiagnostics(
                // (5,31): warning CS0618: 'System.Security.Permissions.SecurityAction.RequestMinimum' is obsolete: 'Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.'
                // [assembly: SecurityPermission(SecurityAction.RequestMinimum, RemotingConfiguration = true)]
                Diagnostic(ErrorCode.WRN_DeprecatedSymbolStr, "SecurityAction.RequestMinimum").WithArguments("System.Security.Permissions.SecurityAction.RequestMinimum", "Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information."),
                // (6,31): warning CS0618: 'System.Security.Permissions.SecurityAction.RequestMinimum' is obsolete: 'Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.'
                // [assembly: SecurityPermission(SecurityAction.RequestMinimum, UnmanagedCode = true)]
                Diagnostic(ErrorCode.WRN_DeprecatedSymbolStr, "SecurityAction.RequestMinimum").WithArguments("System.Security.Permissions.SecurityAction.RequestMinimum", "Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information."));

            ValidateDeclSecurity(compilation,
                new DeclSecurityEntry
                {
                    ActionFlags = DeclarativeSecurityAction.RequestMinimum,
                    ParentKind = SymbolKind.Assembly,
                    PermissionSet =
                        "." + // always start with a dot
                        "\u0002" + // number of attributes (small enough to fit in 1 byte)
                        
                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u001a" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u0015" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "RemotingConfiguration" + // property name
                        "\u0001" + // argument value (true)
                        
                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u0012" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u000d" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "UnmanagedCode" + // property name
                        "\u0001", // argument value (true)
                });
        }

        // Verify via MetadataReader - two attrs in source, different actions
        [Fact]
        public void CheckUnsafeAttributes7()
        {
            string source = @"
using System.Security;
using System.Security.Permissions;

[assembly: SecurityPermission(SecurityAction.RequestOptional, RemotingConfiguration = true)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, UnmanagedCode = true)]
[module: UnverifiableCode]

class C
{
    public static void Main()
    {
    }
}";
            // The attributes have different SecurityActions, so they should not be merged into a single permission set.
            var compilation = CreateCompilationWithMscorlib(source);
            compilation.VerifyDiagnostics(
                // (5,31): warning CS0618: 'System.Security.Permissions.SecurityAction.RequestOptional' is obsolete: 'Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.'
                // [assembly: SecurityPermission(SecurityAction.RequestOptional, RemotingConfiguration = true)]
                Diagnostic(ErrorCode.WRN_DeprecatedSymbolStr, "SecurityAction.RequestOptional").WithArguments("System.Security.Permissions.SecurityAction.RequestOptional", "Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information."),
                // (6,31): warning CS0618: 'System.Security.Permissions.SecurityAction.RequestMinimum' is obsolete: 'Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.'
                // [assembly: SecurityPermission(SecurityAction.RequestMinimum, UnmanagedCode = true)]
                Diagnostic(ErrorCode.WRN_DeprecatedSymbolStr, "SecurityAction.RequestMinimum").WithArguments("System.Security.Permissions.SecurityAction.RequestMinimum", "Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information."));

            ValidateDeclSecurity(compilation,
                new DeclSecurityEntry
                {
                    ActionFlags = DeclarativeSecurityAction.RequestOptional,
                    ParentKind = SymbolKind.Assembly,
                    PermissionSet =
                        "." + // always start with a dot
                        "\u0001" + // number of attributes (small enough to fit in 1 byte)
                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u001a" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u0015" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "RemotingConfiguration" + // property name
                        "\u0001", // argument value (true)
                },
                new DeclSecurityEntry
                {
                    ActionFlags = DeclarativeSecurityAction.RequestMinimum,
                    ParentKind = SymbolKind.Assembly,
                    PermissionSet =
                        "." + // always start with a dot
                        "\u0001" + // number of attributes (small enough to fit in 1 byte)
                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u0012" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u000d" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "UnmanagedCode" + // property name
                        "\u0001", // argument value (true)
                });
        }

        // Verify via MetadataReader - one attr in source, one synthesized, same action
        [Fact]
        public void CheckUnsafeAttributes8()
        {
            string source = @"
using System.Security;
using System.Security.Permissions;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, RemotingConfiguration = true)]
[module: UnverifiableCode]

class C
{
    public static void Main()
    {
    }
}";
            // The attributes have the SecurityAction, so they should be merged into a single permission set.
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.UnsafeExe);
            compilation.VerifyDiagnostics(
                // (5,31): warning CS0618: 'System.Security.Permissions.SecurityAction.RequestMinimum' is obsolete: 'Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.'
                // [assembly: SecurityPermission(SecurityAction.RequestMinimum, RemotingConfiguration = true)]
                Diagnostic(ErrorCode.WRN_DeprecatedSymbolStr, "SecurityAction.RequestMinimum").WithArguments("System.Security.Permissions.SecurityAction.RequestMinimum", "Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information."));

            ValidateDeclSecurity(compilation,
                new DeclSecurityEntry
                {
                    ActionFlags = DeclarativeSecurityAction.RequestMinimum,
                    ParentKind = SymbolKind.Assembly,
                    PermissionSet =
                        "." + // always start with a dot
                        "\u0002" + // number of attributes (small enough to fit in 1 byte)

                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u001a" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u0015" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "RemotingConfiguration" + // property name
                        "\u0001" + // argument value (true)

                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u0015" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u0010" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "SkipVerification" + // property name
                        "\u0001", // argument value (true)
                });
        }

        // Verify via MetadataReader - one attr in source, one synthesized, different actions
        [Fact]
        public void CheckUnsafeAttributes9()
        {
            string source = @"
using System.Security;
using System.Security.Permissions;

[assembly: SecurityPermission(SecurityAction.RequestOptional, RemotingConfiguration = true)]
[module: UnverifiableCode]

class C
{
    public static void Main()
    {
    }
}";
            // The attributes have different SecurityActions, so they should not be merged into a single permission set.
            var compilation = CreateCompilationWithMscorlib(source, compOptions: TestOptions.UnsafeExe);
            compilation.VerifyDiagnostics(
                // (5,31): warning CS0618: 'System.Security.Permissions.SecurityAction.RequestOptional' is obsolete: 'Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information.'
                // [assembly: SecurityPermission(SecurityAction.RequestOptional, RemotingConfiguration = true)]
                Diagnostic(ErrorCode.WRN_DeprecatedSymbolStr, "SecurityAction.RequestOptional").WithArguments("System.Security.Permissions.SecurityAction.RequestOptional", "Assembly level declarative security is obsolete and is no longer enforced by the CLR by default. See http://go.microsoft.com/fwlink/?LinkID=155570 for more information."));

            ValidateDeclSecurity(compilation,
                new DeclSecurityEntry
                {
                    ActionFlags = DeclarativeSecurityAction.RequestOptional,
                    ParentKind = SymbolKind.Assembly,
                    PermissionSet =
                        "." + // always start with a dot
                        "\u0001" + // number of attributes (small enough to fit in 1 byte)
                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u001a" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u0015" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "RemotingConfiguration" + // property name
                        "\u0001", // argument value (true)
                },
                new DeclSecurityEntry
                {
                    ActionFlags = DeclarativeSecurityAction.RequestMinimum,
                    ParentKind = SymbolKind.Assembly,
                    PermissionSet =
                        "." + // always start with a dot
                        "\u0001" + // number of attributes (small enough to fit in 1 byte)
                        "\u0080\u0084" + // length of UTF-8 string (0x80 indicates a 2-byte encoding)
                        "System.Security.Permissions.SecurityPermissionAttribute, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" + // attr type name
                        "\u0015" + // number of bytes in the encoding of the named arguments
                        "\u0001" + // number of named arguments
                        "\u0054" + // property (vs field)
                        "\u0002" + // type bool
                        "\u0010" + // length of UTF-8 string (small enough to fit in 1 byte)
                        "SkipVerification" + // property name
                        "\u0001", // argument value (true)
                });
        }

        [Fact]
        [WorkItem(545651, "DevDiv")]
        void TestReferenceToNestedGenericType()
        {
            string p1 = @"public class Foo<T> { }";
            string p2 = @"using System;

public class Test
{
    public class C<T> {}
    public class J<T> : C<Foo<T>> { }
    
    public static void Main()
    {
        Console.WriteLine(typeof(J<int>).BaseType.Equals(typeof(C<Foo<int>>)) ? 0 : 1);
    }
}";
            var c1 = CreateCompilationWithMscorlib(p1, compOptions: TestOptions.Dll, assemblyName: Guid.NewGuid().ToString());
            CompileAndVerify(p2, new[] { new MetadataImageReference(c1.EmitToStream()) }, expectedOutput: "0");
        }

        [WorkItem(546450, "DevDiv")]
        [Fact]
        void EmitNetModuleWithReferencedNetModule()
        {
            string source1 = @"public class A {}";
            string source2 = @"public class B: A {}";
            var comp = CreateCompilationWithMscorlib(source1, compOptions: TestOptions.NetModule);
            var metadataRef = new MetadataImageReference(ModuleMetadata.CreateFromImageStream(comp.EmitToStream()));
            CompileAndVerify(source2, additionalRefs: new[] { metadataRef }, options: TestOptions.NetModule, emitOptions: EmitOptions.RefEmitBug, verify: false);
        }

        [Fact]
        [WorkItem(530879, "DevDiv")]
        public void TestCompilationEmitUsesDifferentStreamsForBinaryAndPdb()
        {
            string p1 = @"public class C1 { }";

            var c1 = CreateCompilationWithMscorlib(p1);
            var tmpDir = Temp.CreateDirectory();

            var dllPath = Path.Combine(tmpDir.Path, "assemblyname.dll");
            var pdbPath = Path.Combine(tmpDir.Path, "assemblyname.pdb");

            var result = c1.Emit(dllPath, pdbPath);

            Assert.True(result.Success);
            Assert.Empty(result.Diagnostics);

            Assert.True(File.Exists(dllPath));
            Assert.True(File.Exists(pdbPath));
        }

        [Fact, WorkItem(540777, "DevDiv"), WorkItem(546354, "DevDiv")]
        public void CS0219WRN_UnreferencedVarAssg_ConditionalOperator()
        {
            var text = @"
class Program
{
    static void Main(string[] args)
    {
        bool b;
        int s = (b = false) ? 5 : 100; // Warning
    }
}
";
            var opt = new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimize: true);

            var comp = CreateCompilationWithMscorlib(text, compOptions: opt).VerifyDiagnostics(
                // (7,18): warning CS0665: Assignment in conditional expression is always constant; did you mean to use == instead of = ?
                //         int s = (b = false) ? 5 : 100; 		// Warning
                Diagnostic(ErrorCode.WRN_IncorrectBooleanAssg, "b = false"),
                // (6,14): warning CS0219: The variable 'b' is assigned but its value is never used
                //         bool b;
                Diagnostic(ErrorCode.WRN_UnreferencedVarAssg, "b").WithArguments("b"));
        }

        [Fact]
        public void PlatformMismatch_01()
        {
            string refSource = @"
public interface ITestPlatform
{}
";
            var refCompilation = CreateCompilation(refSource, compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"), assemblyName: "PlatformMismatch");

            refCompilation.VerifyEmitDiagnostics();
            var compRef = new CSharpCompilationReference(refCompilation);
            var imageRef = refCompilation.EmitToImageReference();

            string useSource = @"
public interface IUsePlatform
{
    ITestPlatform M();
}
";
            var useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.AnyCpu).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.AnyCpu).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.AnyCpu).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.AnyCpu).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.X86).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
    // warning CS8012: Referenced assembly 'PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' targets a different processor.
    Diagnostic(ErrorCode.WRN_ConflictingMachineAssembly).WithArguments("PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                );

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.X86).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
    // warning CS8012: Referenced assembly 'PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' targets a different processor.
    Diagnostic(ErrorCode.WRN_ConflictingMachineAssembly).WithArguments("PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                );

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.X86).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
                // warning CS8012: Referenced assembly 'PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' targets a different processor.
    Diagnostic(ErrorCode.WRN_ConflictingMachineAssembly).WithArguments("PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                );

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.X86).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
                // warning CS8012: Referenced assembly 'PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' targets a different processor.
    Diagnostic(ErrorCode.WRN_ConflictingMachineAssembly).WithArguments("PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                );

            // Confirm that suppressing the old alink warning 1607 shuts off WRN_ConflictingMachineAssembly
            var warnings = new System.Collections.Generic.Dictionary<string, ReportDiagnostic>();
            warnings.Add(MessageProvider.Instance.GetIdForErrorCode((int)ErrorCode.WRN_ALinkWarn), ReportDiagnostic.Suppress);
            useCompilation = useCompilation.WithOptions(useCompilation.Options.WithSpecificDiagnosticOptions(warnings));
            useCompilation.VerifyEmitDiagnostics();
        }

        [Fact]
        public void PlatformMismatch_02()
        {
            string refSource = @"
public interface ITestPlatform
{}
";
            var refCompilation = CreateCompilation(refSource, compOptions: TestOptions.NetModule.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"), assemblyName: "PlatformMismatch" );

            refCompilation.VerifyEmitDiagnostics();
            var imageRef = refCompilation.EmitToImageReference();

            string useSource = @"
public interface IUsePlatform
{
    ITestPlatform M();
}
";
            var useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.AnyCpu).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
    // error CS8010: Agnostic assembly cannot have a processor specific module 'PlatformMismatch.netmodule'.
    Diagnostic(ErrorCode.ERR_AgnosticToMachineModule).WithArguments("PlatformMismatch.netmodule")
                );

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.X86).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
    // error CS8011: Assembly and module 'PlatformMismatch.netmodule' cannot target different processors.
    Diagnostic(ErrorCode.ERR_ConflictingMachineModule).WithArguments("PlatformMismatch.netmodule")
                );

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] {imageRef},
                compOptions: TestOptions.NetModule.WithPlatform(Platform.AnyCpu).WithRuntimeMetadataVersion("v4.0.31019"));

            // no CS8010 when building a module and adding a module that has a conflict.
            useCompilation.VerifyEmitDiagnostics();
        }

        [Fact]
        public void PlatformMismatch_03()
        {
            string refSource = @"
public interface ITestPlatform
{}
";
            var refCompilation = CreateCompilation(refSource, compOptions: TestOptions.Dll.WithPlatform(Platform.X86).WithRuntimeMetadataVersion("v4.0.31019"), assemblyName: "PlatformMismatch");

            refCompilation.VerifyEmitDiagnostics();
            var compRef = new CSharpCompilationReference(refCompilation);
            var imageRef = refCompilation.EmitToImageReference();

            string useSource = @"
public interface IUsePlatform
{
    ITestPlatform M();
}
";

            var useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
    // warning CS8012: Referenced assembly 'PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' targets a different processor.
    Diagnostic(ErrorCode.WRN_ConflictingMachineAssembly).WithArguments("PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                );

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
    // warning CS8012: Referenced assembly 'PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' targets a different processor.
    Diagnostic(ErrorCode.WRN_ConflictingMachineAssembly).WithArguments("PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                );

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
    // warning CS8012: Referenced assembly 'PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' targets a different processor.
    Diagnostic(ErrorCode.WRN_ConflictingMachineAssembly).WithArguments("PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                );

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
    // warning CS8012: Referenced assembly 'PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null' targets a different processor.
    Diagnostic(ErrorCode.WRN_ConflictingMachineAssembly).WithArguments("PlatformMismatch, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null")
                );
        }

        [Fact]
        public void PlatformMismatch_04()
        {
            string refSource = @"
public interface ITestPlatform
{}
";
            var refCompilation = CreateCompilation(refSource, compOptions: TestOptions.NetModule.WithPlatform(Platform.X86).WithRuntimeMetadataVersion("v4.0.31019"), assemblyName: "PlatformMismatch");

            refCompilation.VerifyEmitDiagnostics();
            var imageRef = refCompilation.EmitToImageReference();

            string useSource = @"
public interface IUsePlatform
{
    ITestPlatform M();
}
";

            var useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics(
    // error CS8011: Assembly and module 'PlatformMismatch.netmodule' cannot target different processors.
    Diagnostic(ErrorCode.ERR_ConflictingMachineModule).WithArguments("PlatformMismatch.netmodule")
                );
        }

        [Fact]
        public void PlatformMismatch_05()
        {
            string refSource = @"
public interface ITestPlatform
{}
";
            var refCompilation = CreateCompilation(refSource, compOptions: TestOptions.Dll.WithPlatform(Platform.AnyCpu).WithRuntimeMetadataVersion("v4.0.31019"), assemblyName: "PlatformMismatch");

            refCompilation.VerifyEmitDiagnostics();
            var compRef = new CSharpCompilationReference(refCompilation);
            var imageRef = refCompilation.EmitToImageReference();

            string useSource = @"
public interface IUsePlatform
{
    ITestPlatform M();
}
";

            var useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();
        }

        [Fact]
        public void PlatformMismatch_06()
        {
            string refSource = @"
public interface ITestPlatform
{}
";
            var refCompilation = CreateCompilation(refSource, compOptions: TestOptions.NetModule.WithPlatform(Platform.AnyCpu).WithRuntimeMetadataVersion("v4.0.31019"), assemblyName: "PlatformMismatch");

            refCompilation.VerifyEmitDiagnostics();
            var imageRef = refCompilation.EmitToImageReference();

            string useSource = @"
public interface IUsePlatform
{
    ITestPlatform M();
}
";

            var useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();
        }

        [Fact]
        public void PlatformMismatch_07()
        {
            string refSource = @"
public interface ITestPlatform
{}
";
            var refCompilation = CreateCompilation(refSource, compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"), assemblyName: "PlatformMismatch");

            refCompilation.VerifyEmitDiagnostics();
            var compRef = new CSharpCompilationReference(refCompilation);
            var imageRef = refCompilation.EmitToImageReference();

            string useSource = @"
public interface IUsePlatform
{
    ITestPlatform M();
}
";

            var useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { compRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();

            useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.NetModule.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();
        }

        [Fact]
        public void PlatformMismatch_08()
        {
            string refSource = @"
public interface ITestPlatform
{}
";
            var refCompilation = CreateCompilation(refSource, compOptions: TestOptions.NetModule.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"), assemblyName: "PlatformMismatch");

            refCompilation.VerifyEmitDiagnostics();
            var imageRef = refCompilation.EmitToImageReference();

            string useSource = @"
public interface IUsePlatform
{
    ITestPlatform M();
}
";

            var useCompilation = CreateCompilation(useSource,
                new MetadataReference[] { imageRef },
                compOptions: TestOptions.Dll.WithPlatform(Platform.Itanium).WithRuntimeMetadataVersion("v4.0.31019"));

            useCompilation.VerifyEmitDiagnostics();
        }

        [Fact, WorkItem(769741, "DevDiv")]
        public void Bug769741()
        {
            // modules not supported in ref emit
            CompileAndVerify("", new[] { TestReferences.SymbolsTests.netModule.x64COFF }, emitOptions: EmitOptions.RefEmitBug, verify: false);
        }

        [Fact]
        public void FoldMethods()
        {
            string source = @"
class Viewable
{
    static void Main()
    {
        var v = new Viewable();
        var x = v.P1;
        var y = x && v.P2;
    }

    bool P1 { get { return true; } } 
    bool P2 { get { return true; } }
}
";
            var compilation = CreateCompilationWithMscorlib(source, null, OptionsDll.WithOptimizations(true));
            var peReader = ModuleMetadata.CreateFromImageStream(compilation.EmitToStream()).Module.GetMetadataReader();

            int P1RVA = 0;
            int P2RVA = 0;

            foreach (var handle in peReader.TypeDefinitions)
            {
                var typeDef = peReader.GetTypeDefinition(handle);
                
                if (peReader.StringEquals(typeDef.Name, "Viewable"))
                {
                    foreach (var m in typeDef.GetMethods())
                    {
                        var method = peReader.GetMethod(m);
                        if (peReader.StringEquals(method.Name, "get_P1"))
                        {
                            P1RVA = method.RelativeVirtualAddress;
                        }
                        if (peReader.StringEquals(method.Name, "get_P2"))
                        {
                            P2RVA = method.RelativeVirtualAddress;
                        }
                    }
                }
            }

            Assert.NotEqual(0, P1RVA);
            Assert.Equal(P2RVA, P1RVA);
        }

        [Fact]
        public void BrokenOutStream()
        {
            //These tests ensure that users supplying a broken stream implementation via the emit API 
            //get exceptions enabling them to attribute the failure to their code and to debug.
            string source = @"class Foo {}";
            var compilation = CreateCompilationWithMscorlib(source);

            var output = new BrokenStream();
            Assert.Throws<IOException>(() => compilation.Emit(output));

            output.BreakHow = 1;
            Assert.Throws<NotSupportedException>(() => compilation.Emit(output));

            var outReal = new MemoryStream();
            Func<EmitResult> f = () => compilation.Emit(outReal);
            outReal.Dispose();
            Assert.Throws<ObjectDisposedException>(() => f());
        }

        [Fact]
        public void BrokenPDBStream()
        {
            string source = @"class Foo {}";
            var compilation = CreateCompilationWithMscorlib(source, null, OptionsDll.WithDebugInformationKind(DebugInformationKind.Full));

            var output = new MemoryStream();
            var pdb = new BrokenStream();
            pdb.BreakHow = 2;
            var result = compilation.Emit(output, GetUniqueName(), GetUniqueName(), pdb);
            result.Diagnostics.Verify(
                    Diagnostic(ErrorCode.FTL_DebugEmitFailure).WithArguments("Exception from HRESULT: 0x806D0004")
                );

            Func<EmitResult> f = () => compilation.Emit(output, GetUniqueName(), GetUniqueName(), pdb);
            pdb.Dispose();
            result = f();

            result.Diagnostics.Verify(
                    Diagnostic(ErrorCode.FTL_DebugEmitFailure).WithArguments("Exception from HRESULT: 0x806D0004")
                );
        }
    }
}
