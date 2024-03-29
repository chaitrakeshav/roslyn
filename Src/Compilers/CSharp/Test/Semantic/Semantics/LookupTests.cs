﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class GetSemanticInfoTests : SemanticModelTestBase
    {
        #region helpers

        internal List<string> GetLookupNames(string testSrc)
        {
            var compilation = CreateCompilationWithMscorlib(testSrc);
            var tree = compilation.SyntaxTrees.Single();
            var model = compilation.GetSemanticModel(tree);
            var position = testSrc.Contains("/*<bind>*/") ? GetPositionForBinding(tree) : GetPositionForBinding(testSrc);
            return model.LookupNames(position);
        }

        internal List<ISymbol> GetLookupSymbols(string testSrc, NamespaceOrTypeSymbol container = null, string name = null, int? arity = null, bool isScript = false, IEnumerable<string> globalUsings = null)
        {
            var tree = Parse(testSrc, options: isScript ? TestOptions.Script : TestOptions.Regular);
            var compOptions = TestOptions.Dll.WithUsings(globalUsings);
            var compilation = CreateCompilationWithMscorlib(tree, compOptions: compOptions);
            var model = compilation.GetSemanticModel(tree);
            var position = testSrc.Contains("/*<bind>*/") ? GetPositionForBinding(tree) : GetPositionForBinding(testSrc);
            return model.LookupSymbols(position, container, name).Where(s => !arity.HasValue || arity == ((Symbol)s).GetMemberArity()).ToList();
        }

        #endregion helpers

        #region tests

        [WorkItem(538262, "DevDiv")]
        [Fact]
        public void LookupCompilationUnitSyntax()
        {
            var testSrc = @"
/*<bind>*/
class Test
{
}
/*</bind>*/
";

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            Assert.DoesNotThrow(() => GetLookupNames(testSrc));

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            Assert.DoesNotThrow(() => GetLookupSymbols(testSrc));
        }

        [WorkItem(527476, "DevDiv")]
        [Fact]
        public void LookupConstrAndDestr()
        {
            var testSrc = @"
class Test
{
    Test()
    {
    }

    Test(int i)
    {
    }

    ~Test()
    {
    }

    static /*<bind>*/void/*</bind>*/Main()
    {
    }
}
";
            List<string> expected_lookupNames = new List<string>
            {
                "Equals",
                "Finalize",
                "GetHashCode",
                "GetType",
                "Main",
                "MemberwiseClone",
                "Microsoft",
                "ReferenceEquals",
                "System",
                "Test",
                "ToString"
            };

            List<string> expected_lookupSymbols = new List<string>
            {
                "Microsoft",
                "System",
                "System.Boolean System.Object.Equals(System.Object obj)",
                "System.Boolean System.Object.Equals(System.Object objA, System.Object objB)",
                "System.Boolean System.Object.ReferenceEquals(System.Object objA, System.Object objB)",
                "System.Int32 System.Object.GetHashCode()",
                "System.Object System.Object.MemberwiseClone()",
                "void System.Object.Finalize()",
                "System.String System.Object.ToString()",
                "System.Type System.Object.GetType()",
                "void Test.Finalize()",
                "void Test.Main()",
                "Test"
            };

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupSymbols = GetLookupSymbols(testSrc);

            Assert.Equal(expected_lookupNames.ListToSortedString(), actual_lookupNames.ListToSortedString());
            Assert.Equal(expected_lookupSymbols.ListToSortedString(), actual_lookupSymbols.ListToSortedString());
        }

        [WorkItem(527477, "DevDiv")]
        [Fact]
        public void LookupNotYetDeclLocalVar()
        {
            var testSrc = @"
class Test
{
    static void Main()
    {
        int j = /*<bind>*/9/*</bind>*/ ;
        int k = 45;
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "j",
                "k"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "j",
                "k"
            };

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupNames[1], actual_lookupNames);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
        }

        [WorkItem(538301, "DevDiv")]
        [Fact]
        public void LookupByNameIncorrectArity()
        {
            var testSrc = @"
class Test
{
    public static void Main()
    {
        int i = /*<bind>*/10/*</bind>*/;
    }
}
";

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            Assert.DoesNotThrow(() => GetLookupSymbols(testSrc, name: "i", arity: 1));

            var actual_lookupSymbols = GetLookupSymbols(testSrc, name: "i", arity: 1);

            Assert.Empty(actual_lookupSymbols);
        }

        [WorkItem(538310, "DevDiv")]
        [Fact]
        public void LookupInProtectedNonNestedType()
        {
            var testSrc = @"
protected class MyClass {
    /*<bind>*/public static void Main()/*</bind>*/ {}	
}
";

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            Assert.DoesNotThrow(() => GetLookupNames(testSrc));

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            Assert.DoesNotThrow(() => GetLookupSymbols(testSrc));
        }

        [WorkItem(538311, "DevDiv")]
        [Fact]
        public void LookupClassContainsVolatileEnumField()
        {
            var testSrc = @"
enum E{} 
class Test {
    static volatile E x;
    static /*<bind>*/int/*</bind>*/ Main() { 
        return 1;
    }
}
";

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            Assert.DoesNotThrow(() => GetLookupNames(testSrc));

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            Assert.DoesNotThrow(() => GetLookupSymbols(testSrc));
        }

        [WorkItem(538312, "DevDiv")]
        [Fact]
        public void LookupUsingAlias()
        {
            var testSrc = @"
using T2 = System.IO;

namespace T1
{
    class Test
    {
        static /*<bind>*/void/*</bind>*/ Main()
        {
        }
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "T1",
                "T2"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "T1",
                "T2"
            };

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[1], actual_lookupNames);

            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
        }

        [WorkItem(538313, "DevDiv")]
        [Fact]
        public void LookupUsingNameSpaceContSameTypeNames()
        {
            var testSrc = @"
namespace T1
{
    using T2;
    public class Test
    {
        static /*<bind>*/int/*</bind>*/ Main()
        {
            return 1;
        }
    }
}

namespace T2
{
    public class Test
    {
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "T1",
                "T2",
                "Test"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "T1",
                "T2",
                "T1.Test",
                //"T2.Test" this is hidden by T1.Test
            };

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[1], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[2], actual_lookupNames);

            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[2], actual_lookupSymbols_as_string);
        }

        [WorkItem(527489, "DevDiv")]
        [Fact]
        public void LookupMustNotBeNonInvocableMember()
        {
            var testSrc = @"
class Test
{
    public void TestMeth(int i, int j)
    {
        int m = /*<bind>*/10/*</bind>*/;
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "TestMeth",
                "i",
                "j",
                "m",
                "System",
                "Microsoft",
                "Test"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "void Test.TestMeth(System.Int32 i, System.Int32 j)",
                "System.Int32 i",
                "System.Int32 j",
                "System.Int32 m",
                "System",
                "Microsoft",
                "Test"
            };

            var comp = CreateCompilationWithMscorlib(testSrc);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var position = GetPositionForBinding(tree);
            var binder = ((CSharpSemanticModel)model).GetEnclosingBinder(position);

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var info = LookupSymbolsInfo.GetInstance();
            binder.AddLookupSymbolsInfo(info, LookupOptions.MustBeInvocableIfMember);
            var actual_lookupNames = info.Names;

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupSymbols = actual_lookupNames.SelectMany(name =>
            {
                var lookupResult = LookupResult.GetInstance();
                HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                binder.LookupSymbolsSimpleName(
                    lookupResult,
                    qualifierOpt: null,
                    plainName: name,
                    arity: 0,
                    basesBeingResolved: null,
                    options: LookupOptions.MustBeInvocableIfMember,
                    diagnose: false,
                    useSiteDiagnostics: ref useSiteDiagnostics);
                Assert.Null(useSiteDiagnostics);
                Assert.True(lookupResult.IsMultiViable || lookupResult.Kind == LookupResultKind.NotReferencable);
                var result = lookupResult.Symbols.ToArray();
                lookupResult.Free();
                return result;
            });
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[1], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[2], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[3], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[4], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[5], actual_lookupNames);

            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[2], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[3], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[4], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[5], actual_lookupSymbols_as_string);

            info.Free();
        }

        [WorkItem(538365, "DevDiv")]
        [Fact]
        public void LookupWithNameZeroArity()
        {
            var testSrc = @"
class Test
{
    private void F<T>(T i)
    {
    }

    private void F<T, U>(T i, U j)
    {
    }

    private void F(int i)
    {
    }

    private void F(int i, int j)
    {
    }

    public static /*<bind>*/void/*</bind>*/ Main()
    {
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "F"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "void Test.F(System.Int32 i)",
                "void Test.F(System.Int32 i, System.Int32 j)"
            };

            List<string> not_expected_in_lookupSymbols = new List<string>
            {
                "void Test.F<T>(T i)",
                "void Test.F<T, U>(T i, U j)"
            };

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupSymbols = GetLookupSymbols(testSrc, name: "F", arity: 0);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);

            Assert.Equal(2, actual_lookupSymbols.Count);
            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
            Assert.DoesNotContain(not_expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.DoesNotContain(not_expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
        }

        [WorkItem(538365, "DevDiv")]
        [Fact]
        public void LookupWithNameZeroArityAndLookupOptionsAllMethods()
        {
            var testSrc = @"
class Test
{
    public void F<T>(T i)
    {
    }

    public void F<T, U>(T i, U j)
    {
    }

    public void F(int i)
    {
    }

    public void F(int i, int j)
    {
    }

    public void Main()
    {
        return;
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "F"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "void Test.F(System.Int32 i)",
                "void Test.F(System.Int32 i, System.Int32 j)",
                "void Test.F<T>(T i)",
                "void Test.F<T, U>(T i, U j)"
            };

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var comp = CreateCompilationWithMscorlib(testSrc);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var position = testSrc.IndexOf("return");
            var binder = ((CSharpSemanticModel)model).GetEnclosingBinder(position);
            var lookupResult = LookupResult.GetInstance();
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            binder.LookupSymbolsSimpleName(lookupResult, qualifierOpt: null, plainName: "F", arity: 0, basesBeingResolved: null, options: LookupOptions.AllMethodsOnArityZero, diagnose: false, useSiteDiagnostics: ref useSiteDiagnostics);
            Assert.Null(useSiteDiagnostics);
            Assert.True(lookupResult.IsMultiViable);
            var actual_lookupSymbols_as_string = lookupResult.Symbols.Select(e => e.ToTestDisplayString()).ToArray();
            lookupResult.Free();

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupNames = model.LookupNames(position);

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);

            Assert.Equal(4, actual_lookupSymbols_as_string.Length);
            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[2], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[3], actual_lookupSymbols_as_string);
        }

        [WorkItem(539160, "DevDiv")]
        [Fact]
        public void LookupExcludeInAppropriateNS()
        {
            var testSrc = @"
class Test
{
   public static /*<bind>*/void/*</bind>*/ Main()
   {
   }
}
";
            var srcTrees = new SyntaxTree[] { Parse(testSrc) };
            var refs = new MetadataReference[] { SystemDataRef };
            CSharpCompilation compilation = CSharpCompilation.Create("Test.dll", srcTrees, refs);

            var tree = srcTrees[0];
            var model = compilation.GetSemanticModel(tree);

            List<string> not_expected_in_lookup = new List<string>
            {
                "<CrtImplementationDetails>",
                "<CppImplementationDetails>"
            };

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupNames = model.LookupNames(GetPositionForBinding(tree), null).ToList();
            var actual_lookupNames_ignoreAcc = model.LookupNames(GetPositionForBinding(tree), null).ToList();

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupSymbols = model.LookupSymbols(GetPositionForBinding(tree));
            var actual_lookupSymbols_ignoreAcc = model.LookupSymbols(GetPositionForBinding(tree));
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());
            var actual_lookupSymbols_ignoreAcc_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.DoesNotContain(not_expected_in_lookup[0], actual_lookupNames);
            Assert.DoesNotContain(not_expected_in_lookup[1], actual_lookupNames);
            Assert.DoesNotContain(not_expected_in_lookup[0], actual_lookupNames_ignoreAcc);
            Assert.DoesNotContain(not_expected_in_lookup[1], actual_lookupNames_ignoreAcc);

            Assert.DoesNotContain(not_expected_in_lookup[0], actual_lookupSymbols_as_string);
            Assert.DoesNotContain(not_expected_in_lookup[1], actual_lookupSymbols_as_string);
            Assert.DoesNotContain(not_expected_in_lookup[0], actual_lookupSymbols_ignoreAcc_as_string);
            Assert.DoesNotContain(not_expected_in_lookup[1], actual_lookupSymbols_ignoreAcc_as_string);
        }

        [WorkItem(539814, "DevDiv")]
        /// <summary>
        /// Verify that there's a way to look up only the members of the base type that are visible
        /// from the current type.
        /// </summary>
        [Fact]
        public void LookupProtectedInBase()
        {
            var testSrc = @"
class A
{
    private void Hidden() { }
    protected void Foo() { }
}
 
class B : A
{
    void Bar()
    {
        /*<bind>*/base/*</bind>*/.Foo();
    }
}
";
            var srcTrees = new SyntaxTree[] { Parse(testSrc) };
            var refs = new MetadataReference[] { SystemDataRef };
            CSharpCompilation compilation = CSharpCompilation.Create("Test.dll", srcTrees, refs);

            var tree = srcTrees[0];
            var model = compilation.GetSemanticModel(tree);

            var baseExprNode = GetSyntaxNodeForBinding(GetSyntaxNodeList(tree));
            Assert.Equal("base", baseExprNode.ToString());

            var baseExprLocation = baseExprNode.SpanStart;
            Assert.NotEqual(0, baseExprLocation);

            var baseExprInfo = model.GetTypeInfo((ExpressionSyntax)baseExprNode);
            Assert.NotNull(baseExprInfo);

            var baseExprType = (NamedTypeSymbol)baseExprInfo.Type;
            Assert.NotNull(baseExprType);
            Assert.Equal("A", baseExprType.Name);

            var symbols = model.LookupBaseMembers(baseExprLocation);
            Assert.Equal("void A.Foo()", symbols.Single().ToTestDisplayString());

            var names = model.LookupNames(baseExprLocation, useBaseReferenceAccessibility: true);
            Assert.Equal("Foo", names.Single());
        }

        [WorkItem(528263, "DevDiv")]
        [Fact]
        public void LookupStartOfScopeMethodBody()
        {
            var testSrc = @"public class start
{
       static public void Main()
/*pos*/{
          int num=10;
       } 
";
            List<string> expected_in_lookupNames = new List<string>
            {
                "Main",
                "start",
                "num"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "void start.Main()",
                "start",
                "System.Int32 num"
            };

            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Equal('{', testSrc[GetPositionForBinding(testSrc)]);

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[1], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[2], actual_lookupNames);

            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[2], actual_lookupSymbols_as_string);
        }

        [WorkItem(528263, "DevDiv")]
        [Fact]
        public void LookupEndOfScopeMethodBody()
        {
            var testSrc = @"public class start
{
       static public void Main()
       {
          int num=10;
/*pos*/} 
";
            List<string> expected_in_lookupNames = new List<string>
            {
                "Main",
                "start"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "void start.Main()",
                "start"
            };

            List<string> not_expected_in_lookupNames = new List<string>
            {
                "num"
            };

            List<string> not_expected_in_lookupSymbols = new List<string>
            {
                "System.Int32 num"
            };

            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Equal('}', testSrc[GetPositionForBinding(testSrc)]);

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[1], actual_lookupNames);
            Assert.DoesNotContain(not_expected_in_lookupNames[0], actual_lookupNames);

            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
            Assert.DoesNotContain(not_expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
        }

        [WorkItem(540888, "DevDiv")]
        [Fact]
        public void LookupLambdaParamInConstructorInitializer()
        {
            var testSrc = @"
using System;

class MyClass
{
    public MyClass(Func<int, int> x)
    {
    }

    public MyClass(int j, int k)
        : this(lambdaParam => /*pos*/lambdaParam)
    {
    }
}
";
            List<string> expected_in_lookupNames = new List<string>
            {
                "j",
                "k",
                "lambdaParam"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "System.Int32 j",
                "System.Int32 k",
                "System.Int32 lambdaParam"
            };


            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[1], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[2], actual_lookupNames);

            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[2], actual_lookupSymbols_as_string);
        }

        [WorkItem(540893, "DevDiv")]
        [Fact]
        public void TestForLocalVarDeclLookupAtForKeywordInForStmt()
        {
            var testSrc = @"
class MyClass
{
    static void Main()
    {
        /*pos*/for (int forVar = 10; forVar < 10; forVar++)
        {
        }
    }
}
";
            List<string> not_expected_in_lookupNames = new List<string>
            {
                "forVar"
            };

            List<string> not_expected_in_lookupSymbols = new List<string>
            {
                "System.Int32 forVar",
            };


            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.DoesNotContain(not_expected_in_lookupNames[0], actual_lookupNames);

            Assert.DoesNotContain(not_expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
        }

        [WorkItem(540894, "DevDiv")]
        [Fact]
        public void TestForeachIterVarLookupAtForeachKeyword()
        {
            var testSrc = @"
class MyClass
{
    static void Main()
    {
        System.Collections.Generic.List<int> listOfNumbers = new System.Collections.Generic.List<int>();

        /*pos*/foreach (int number in listOfNumbers)
        {
        }
    }
}
";
            List<string> not_expected_in_lookupNames = new List<string>
            {
                "number"
            };

            List<string> not_expected_in_lookupSymbols = new List<string>
            {
                "System.Int32 number",
            };


            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.DoesNotContain(not_expected_in_lookupNames[0], actual_lookupNames);

            Assert.DoesNotContain(not_expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
        }

        [WorkItem(540912, "DevDiv")]
        [Fact]
        public void TestLookupInConstrInitIncompleteConstrDecl()
        {
            var testSrc = @"
class MyClass
{
    public MyClass(int x)
    {
    }

    public MyClass(int j, int k) :this(/*pos*/k)
";
            List<string> expected_in_lookupNames = new List<string>
            {
                "j",
                "k"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "System.Int32 j",
                "System.Int32 k",
            };


            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupNames[1], actual_lookupNames);

            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
            Assert.Contains(expected_in_lookupSymbols[1], actual_lookupSymbols_as_string);
        }

        [WorkItem(541060, "DevDiv")]
        [Fact]
        public void TestLookupInsideIncompleteNestedLambdaBody()
        {
            var testSrc = @"
class C
{
    C()
    {
        D(() =>
        {
            D(() =>
            {
            }/*pos*/
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "C"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "C"
            };

            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.NotEmpty(actual_lookupNames);
            Assert.NotEmpty(actual_lookupSymbols);

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
        }

        [WorkItem(541611, "DevDiv")]
        [Fact]
        public void LookupLambdaInsideAttributeUsage()
        {
            var testSrc = @"
using System;

class Program
{
    [ObsoleteAttribute(x=>x/*pos*/
    static void Main(string[] args)
    {       
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "x"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "? x"
            };

            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
        }

        [WorkItem(541909, "DevDiv")]
        [Fact]
        public void LookupFromRangeVariableAfterFromClause()
        {
            var testSrc = @"
class Program
{
    static void Main(string[] args)
    {
        var q = from i in new int[] { 4, 5 } where /*pos*/

    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "i"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "? i"
            };

            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
        }

        [WorkItem(541921, "DevDiv")]
        [Fact]
        public void LookupFromRangeVariableInsideNestedFromClause()
        {
            var testSrc = @"
class Program
{
    static void Main(string[] args)
    {
        string[] strings = { };

        var query = from s in strings 
                    from s1 in /*pos*/
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "s"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "? s"
            };

            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
        }

        [WorkItem(541919, "DevDiv")]
        [Fact]
        public void LookupLambdaVariableInQueryExpr()
        {
            var testSrc = @"
class Program
{
    static void Main(string[] args)
    {
        Func<int, IEnumerable<int>> f1 = (x) => from n in /*pos*/
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "x"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "x"
            };

            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.Name);

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
        }

        [WorkItem(541910, "DevDiv")]
        [Fact]
        public void LookupInsideQueryExprOutsideTypeDecl()
        {
            var testSrc = @"var q = from i in/*pos*/ f";

            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.NotEmpty(actual_lookupNames);
            Assert.NotEmpty(actual_lookupSymbols_as_string);
        }

        [WorkItem(542203, "DevDiv")]
        [Fact]
        public void LookupInsideQueryExprInMalformedFromClause()
        {
            var testSrc = @"
using System;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        int[] numbers = new int[] { 4, 5 };

        var q1 = from I<x/*pos*/ in numbers.Where(x1 => x1 > 2) select x;
    }
}
";
            // Get the list of LookupNames at the location at the end of the /*pos*/ tag
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location at the end of the /*pos*/ tag
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToTestDisplayString());

            Assert.NotEmpty(actual_lookupNames);
            Assert.NotEmpty(actual_lookupSymbols_as_string);
        }

        [WorkItem(543295, "DevDiv")]
        [Fact]
        public void MultipleOverlappingInterfaceConstraints()
        {
            var testSrc =
@"public interface IEntity
{
    object Key { get; }
}

public interface INumberedProjectChild
 : IEntity
{ }

public interface IAggregateRoot : IEntity
{
}

public interface ISpecification<TCandidate>
{
    void IsSatisfiedBy(TCandidate candidate);
}

public abstract class Specification<TCandidate> : ISpecification<TCandidate>
{
    public abstract void IsSatisfiedBy(TCandidate candidate);
}

public class NumberSpecification<TCandidate>
    : Specification<TCandidate> where TCandidate : IAggregateRoot,
    INumberedProjectChild
{
    public override void IsSatisfiedBy(TCandidate candidate)
    {
        var key = candidate.Key;
    }
}";
            CreateCompilationWithMscorlib(testSrc).VerifyDiagnostics();
        }

        [WorkItem(529406, "DevDiv")]
        [Fact]
        public void FixedPointerInitializer()
        {
            var testSrc = @"
class Program
{
    static int num = 0;
    unsafe static void Main(string[] args)
    {
        fixed(int* p1 = /*pos*/&num, p2 = &num)
        {
        }
    }
}
";

            List<string> expected_in_lookupNames = new List<string>
            {
                "p2"
            };

            List<string> expected_in_lookupSymbols = new List<string>
            {
                "p2"
            };

            // Get the list of LookupNames at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupNames = GetLookupNames(testSrc);

            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode enclosed within the <bind> </bind> tags
            var actual_lookupSymbols = GetLookupSymbols(testSrc);
            var actual_lookupSymbols_as_string = actual_lookupSymbols.Select(e => e.ToString()).ToList();

            Assert.Contains(expected_in_lookupNames[0], actual_lookupNames);
            Assert.Contains(expected_in_lookupSymbols[0], actual_lookupSymbols_as_string);
        }

        [Fact]
        public void LookupSymbolsAtEOF()
        {
            var source =
@"class
{
}";
            var tree = Parse(source);
            var comp = CreateCompilationWithMscorlib(tree);
            var model = comp.GetSemanticModel(tree);
            var eof = tree.GetCompilationUnitRoot().FullSpan.End;
            Assert.NotEqual(eof, 0);
            var symbols = model.LookupSymbols(eof);
            CompilationUtils.CheckSymbols(symbols, "System", "Microsoft");
        }

        [Fact, WorkItem(546523, "DevDiv")]
        public void TestLookupSymbolsNestedNamespacesNotImportedByUsings_01()
        {
            var source =
@"
using System;
 
class Program
{
    static void Main(string[] args)
    {
        /*pos*/
    }
}
";
            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode
            var actual_lookupSymbols = GetLookupSymbols(source);

            // Verify nested namespaces *are not* imported.
            var systemNS = (NamespaceSymbol)actual_lookupSymbols.Where((sym) => sym.Name.Equals("System") && sym.Kind == SymbolKind.Namespace).Single();
            NamespaceSymbol systemXmlNS = systemNS.GetNestedNamespace("Xml");
            Assert.DoesNotContain(systemXmlNS, actual_lookupSymbols);
        }

        [Fact, WorkItem(546523, "DevDiv")]
        public void TestLookupSymbolsNestedNamespacesNotImportedByUsings_02()
        {
            var usings = new [] { "using X;" };

            var source =
@"
using aliasY = X.Y;

namespace X
{
    namespace Y
    {
        public class InnerZ
        {
        }
    }

    public class Z
    {
    }

    public static class StaticZ
    {
    }
}

public class A
{
    public class B
    {
    }
}
 
class Program
{
    public static void Main()
    {
        /*pos*/
    }
}
";
            // Get the list of LookupSymbols at the location of the CSharpSyntaxNode
            var actual_lookupSymbols = GetLookupSymbols(usings.ToString() + source, isScript: false);
            TestLookupSymbolsNestedNamespaces(actual_lookupSymbols);

            actual_lookupSymbols = GetLookupSymbols(source, isScript: true, globalUsings: usings);
            TestLookupSymbolsNestedNamespaces(actual_lookupSymbols);
            
            Action<ModuleSymbol> validator = (module) =>
            {
                NamespaceSymbol globalNS = module.GlobalNamespace;

                Assert.Equal(1, globalNS.GetMembers("X").Length);
                Assert.Equal(1, globalNS.GetMembers("A").Length);
                Assert.Equal(1, globalNS.GetMembers("Program").Length);

                Assert.Empty(globalNS.GetMembers("Y"));
                Assert.Empty(globalNS.GetMembers("Z"));
                Assert.Empty(globalNS.GetMembers("StaticZ"));
                Assert.Empty(globalNS.GetMembers("B"));
            };

            CompileAndVerify(source, sourceSymbolValidator: validator, symbolValidator: validator);
        }

        [Fact]
        [WorkItem(530826, "DevDiv")]
        public void TestAmbiguousInterfaceLookup()
        {
            var source =
@"delegate void D();
interface I1
{
    void M();
}

interface I2
{
    event D M;
}

interface I3 : I1, I2 { }
public class P : I3
{
    event D I2.M { add { } remove { } }
    void I1.M() { }
}

class Q : P
{
    static int Main(string[] args)
    {
        Q p = new Q();
        I3 m = p;
        if (m.M is object) {}
        return 0;
    }
}";
            var compilation = CreateCompilationWithMscorlib(source);
            var tree = compilation.SyntaxTrees[0];
            var model = compilation.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<ExpressionSyntax>().Where(n => n.ToString() == "m.M").Single();
            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Equal("M", symbolInfo.Symbol.Name);
            Assert.Equal(SymbolKind.Method, symbolInfo.Symbol.Kind);
            Assert.Equal(CandidateReason.None, symbolInfo.CandidateReason);
            var node2 = (ExpressionSyntax)SyntaxFactory.SyntaxTree(node).GetRoot();
            symbolInfo = model.GetSpeculativeSymbolInfo(node.Position, node2, SpeculativeBindingOption.BindAsExpression);
            Assert.Equal("M", symbolInfo.Symbol.Name);
            Assert.Equal(SymbolKind.Method, symbolInfo.Symbol.Kind);
            Assert.Equal(CandidateReason.None, symbolInfo.CandidateReason);
        }

        private void TestLookupSymbolsNestedNamespaces(List<ISymbol> actual_lookupSymbols)
        {
            var namespaceX = (NamespaceSymbol)actual_lookupSymbols.Where((sym) => sym.Name.Equals("X") && sym.Kind == SymbolKind.Namespace).Single();

            // Verify nested namespaces within namespace X *are not* present in lookup symbols.
            NamespaceSymbol namespaceY = namespaceX.GetNestedNamespace("Y");
            Assert.DoesNotContain(namespaceY, actual_lookupSymbols);
            NamedTypeSymbol typeInnerZ = namespaceY.GetTypeMembers("InnerZ").Single();
            Assert.DoesNotContain(typeInnerZ, actual_lookupSymbols);

            // Verify nested types *are not* present in lookup symbols.
            var typeA = (NamedTypeSymbol)actual_lookupSymbols.Where((sym) => sym.Name.Equals("A") && sym.Kind == SymbolKind.NamedType).Single();
            NamedTypeSymbol typeB = typeA.GetTypeMembers("B").Single();
            Assert.DoesNotContain(typeB, actual_lookupSymbols);

            // Verify aliases to nested namespaces within namespace X *are* present in lookup symbols.
            var aliasY = (AliasSymbol)actual_lookupSymbols.Where((sym) => sym.Name.Equals("aliasY") && sym.Kind == SymbolKind.Alias).Single();
            Assert.Contains(aliasY, actual_lookupSymbols);
        }

        #endregion tests

        #region regressions

        [Fact]
        [WorkItem(552472, "DevDiv")]
        public void BrokenCode01()
        {
            var source =
@"Dele<Str> d3 = delegate (Dele<Str> d2 = delegate ()
{
    returne<double> d1 = delegate () { return 1; };
    {
        int result = 0;
        Dels Test : Base";
            var compilation = CreateCompilationWithMscorlib(source);
            var tree = compilation.SyntaxTrees[0];
            var model = compilation.GetSemanticModel(tree);
            SemanticModel imodel = model;
            var node = tree.GetRoot().DescendantNodes().Where(n => n.ToString() == "returne<double>").First();
            imodel.GetSymbolInfo(node, default(CancellationToken));
        }

        [Fact]
        [WorkItem(552472, "DevDiv")]
        public void BrokenCode02()
        {
            var source =
@"public delegate D D(D d);

class Program
{
    public D d3 = delegate(D d2 = delegate
        {
            System.Object x = 3;
            return null;
        }) {};
    public static void Main(string[] args)
    {
    }
}
";
            var compilation = CreateCompilationWithMscorlib(source);
            var tree = compilation.SyntaxTrees[0];
            var model = compilation.GetSemanticModel(tree);
            SemanticModel imodel = model;
            var node = tree.GetRoot().DescendantNodes().Where(n => n.ToString() == "System.Object").First();
            imodel.GetSymbolInfo(node, default(CancellationToken));
        }

        [Fact]
        public void InterfaceDiamondHiding()
        {
            var source = @"
interface T
{
    int P { get; set; }
    int Q { get; set; }
}

interface L : T
{
    new int P { get; set; }
}

interface R : T
{
    new int Q { get; set; }
}

interface B : L, R
{
}

class Test
{
    int M(B b)
    {
        return b.P + b.Q;
    }
}
";

            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics();

            var global = comp.GlobalNamespace;

            var interfaceT = global.GetMember<NamedTypeSymbol>("T");
            var interfaceL = global.GetMember<NamedTypeSymbol>("L");
            var interfaceR = global.GetMember<NamedTypeSymbol>("R");
            var interfaceB = global.GetMember<NamedTypeSymbol>("B");

            var propertyTP = interfaceT.GetMember<PropertySymbol>("P");
            var propertyTQ = interfaceT.GetMember<PropertySymbol>("Q");
            var propertyLP = interfaceL.GetMember<PropertySymbol>("P");
            var propertyRQ = interfaceR.GetMember<PropertySymbol>("Q");

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            var syntaxes = tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>().ToArray();
            Assert.Equal(2, syntaxes.Length);

            // The properties in T are hidden - we bind to the properties on more-derived interfaces
            Assert.Equal(propertyLP, model.GetSymbolInfo(syntaxes[0]).Symbol);
            Assert.Equal(propertyRQ, model.GetSymbolInfo(syntaxes[1]).Symbol);

            int position = source.IndexOf("return");

            // We do the right thing with diamond inheritance (i.e. member is hidden along all paths
            // if it is hidden along any path) because we visit base interfaces in topological order.
            Assert.Equal(propertyLP, model.LookupSymbols(position, interfaceB, "P").Single());
            Assert.Equal(propertyRQ, model.LookupSymbols(position, interfaceB, "Q").Single());
        }

        [Fact]
        public void SemanticModel_OnlyInvalid()
        {
            var source = @"
public class C
{
    void M()
    {
        return;
    }
}
";

            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            int position = source.IndexOf("return");

            var symbols = model.LookupNamespacesAndTypes(position, name: "M");
            Assert.Equal(0, symbols.Length);
        }

        [Fact]
        public void SemanticModel_InvalidHidingValid()
        {
            var source = @"
public class C<T>
{
    public class Inner
    {
        void T()
        {
            return;
        }
    }
}
";

            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics();

            var classC = comp.GlobalNamespace.GetMember<NamedTypeSymbol>("C");
            var methodT = classC.GetMember<NamedTypeSymbol>("Inner").GetMember<MethodSymbol>("T");

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            int position = source.IndexOf("return");

            var symbols = model.LookupSymbols(position, name: "T");
            Assert.Equal(methodT, symbols.Single()); // Hides type parameter.

            symbols = model.LookupNamespacesAndTypes(position, name: "T");
            Assert.Equal(classC.TypeParameters.Single(), symbols.Single()); // Ignore intervening method.
        }

        [Fact]
        public void SemanticModel_MultipleValid()
        {
            var source = @"
public class Outer
{
    void M(int x)
    {
    }

    void M()
    {
        return;
    }
}
";

            var comp = CreateCompilationWithMscorlib(source);
            comp.VerifyDiagnostics();

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            int position = source.IndexOf("return");

            var symbols = model.LookupSymbols(position, name: "M");
            Assert.Equal(2, symbols.Length);
        }

        #endregion
    }
}
