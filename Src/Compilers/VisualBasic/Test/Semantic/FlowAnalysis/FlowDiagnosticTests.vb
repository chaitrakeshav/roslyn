﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System
Imports System.[Text]
Imports System.Collections.Generic
Imports System.Linq
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests
    Public Class SimpleFlowTests
        Inherits FlowTestBase

        <Fact>
        Public Sub TestUninitializedIntegerLocal()
            Dim program = <compilation name="TestUninitializedIntegerLocal">
                              <file name="a.b">
                                Module Module1

                                    Sub Foo(z As Integer)
                                        Dim x As Integer
                                        If z = 2 Then
                                            Dim y As Integer = x : x = y ' ok to use unassigned integer local
                                        Else
                                            dim y as integer = x : x = y ' no diagnostic in unreachable code
                                        End If
                                    End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()
        End Sub

        <Fact>
        Public Sub TestUnusedIntegerLocal()
            Dim program = <compilation name="TestUnusedIntegerLocal">
                              <file name="a.b">
                                Module Module1

                                Public Sub SubWithByRef(ByRef i As Integer)
                                End Sub

                                Sub TestInitialized1()
                                    Dim x as integer = 1 ' No warning for a variable assigned a value but not used
                                    Dim i1 As Integer
                                    Dim i2 As Integer

                                    i2 = i1             ' OK to use an uninitialized integer. Spec says all variables are initialized

                                    Dim i3 As Integer
                                    SubWithByRef(i3)    ' Ok to pass an uninitialized integer byref

                                    Dim i4 As Integer   ' Warning - unused local
                                End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               <expected>
BC42024: Unused local variable: 'i4'.
                                    Dim i4 As Integer   ' Warning - unused local
                                        ~~                                                   
                                               </expected>)
        End Sub

        <Fact>
        Public Sub TestStructure1()
            Dim program = <compilation name="TestStructure1">
                              <file name="a.b">
                                Module Module1

                                Structure s1
                                    Dim i As Integer
                                    Dim o As Object
                                End Structure

                                Public Sub SubWithByRef(ByRef i As s1, j as integer)
                                End Sub

                                Sub TestInitialized1()
                                    Dim i1 As s1
                                    Dim i2 As s1

                                    i2 = i1             ' Warning- use of uninitialized variable

                                    Dim i3 As s1
                                    SubWithByRef(j := 1, i := i3)    ' Warning- use of uninitialized variable

                                    Dim i4 As s1        ' Warning - unused local
                                End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<expected>
BC42109: Variable 'i1' is used before it has been assigned a value. A null reference exception could result at runtime. Make sure the structure or all the reference members are initialized before use
                                    i2 = i1             ' Warning- use of uninitialized variable
                                         ~~
BC42108: Variable 'i3' is passed by reference before it has been assigned a value. A null reference exception could result at runtime. Make sure the structure or all the reference members are initialized before use
                                    SubWithByRef(j := 1, i := i3)    ' Warning- use of uninitialized variable
                                                              ~~
BC42024: Unused local variable: 'i4'.
                                    Dim i4 As s1        ' Warning - unused local
                                        ~~                                                   
                                               </expected>))
        End Sub

        <Fact>
        Public Sub TestObject1()
            Dim program = <compilation name="TestObject1">
                              <file name="a.b">
                                Module Module1

                                Class C1
                                    Public i As Integer
                                    Public o As Object
                                End Class

                                Public Sub SubWithByRef(ByRef i As C1)
                                End Sub

                                Sub TestInitialized1()
                                    Dim i1 As C1
                                    Dim i2 As C1

                                    i2 = i1             ' Warning- use of uninitialized variable

                                    Dim i3 As MyClass
                                    SubWithByRef(i3)    ' Warning- use of uninitialized variable

                                    Dim i4 As C1   ' Warning - unused local
                                End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<expected>
BC42104: Variable 'i1' is used before it has been assigned a value. A null reference exception could result at runtime.
                                    i2 = i1             ' Warning- use of uninitialized variable
                                         ~~
BC42104: Variable 'i3' is used before it has been assigned a value. A null reference exception could result at runtime.
                                    SubWithByRef(i3)    ' Warning- use of uninitialized variable
                                                 ~~
BC42024: Unused local variable: 'i4'.
                                    Dim i4 As C1   ' Warning - unused local
                                        ~~    
                                               </expected>))
        End Sub

        <Fact()>
        Public Sub LambdaInUnimplementedPartial_1()
            Dim program = <compilation name="LambdaInUnimplementedPartial_1">
                              <file name="a.b">
Imports System
Partial Class C
    Partial Private Shared Sub Foo(a As action)
    End Sub

    Public Shared Sub Main()
        Foo(DirectCast(Sub()
                           Dim x As Integer
                           Dim y As Integer = x
                       End Sub, Action))
    End Sub
End Class
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs, (<errors></errors>))
        End Sub

        <Fact()>
        Public Sub LambdaInUnimplementedPartial_2()
            Dim program = <compilation name="LambdaInUnimplementedPartial_2">
                              <file name="a.b">
Imports System
Partial Class C
    Partial Private Shared Sub Foo(a As action)
    End Sub

    Public Shared Sub Main()
        Foo(DirectCast(Sub()
                           Dim x As Integer
                       End Sub, Action))
    End Sub
End Class
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
(<errors>
BC42024: Unused local variable: 'x'.
                           Dim x As Integer
                               ~
</errors>))
        End Sub

        <WorkItem(722619, "DevDiv")>
        <Fact()>
        Public Sub Bug_722619()
            Dim program =
<compilation>
    <file name="a.b">
Imports System

Friend Module SubMod
    Sub Main()
        Dim x As Exception
        Try
            Throw New DivideByZeroException
L1:
            'COMPILEWARNING: BC42104, "x"
            Console.WriteLine(x.Message)
        Catch ex As DivideByZeroException
            GoTo L1
        Finally
            x = New Exception("finally")
        End Try
    End Sub
End Module
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntimeAndReferences(program, additionalRefs:={SystemCoreRef})
            CompilationUtils.AssertTheseDiagnostics(comp,
<errors>
BC42104: Variable 'x' is used before it has been assigned a value. A null reference exception could result at runtime.
            Console.WriteLine(x.Message)
                              ~
</errors>)
        End Sub

        <WorkItem(722575, "DevDiv")>
        <Fact()>
        Public Sub Bug_722575a()
            Dim program =
<compilation>
    <file name="a.b">
Imports System

Friend Module TestNone
    Structure Str1(Of T)
        Dim x As T

        Sub foo()
            Dim o As Object
            Dim s1 As Str1(Of T)
            o = s1
            Dim s2 As Str1(Of T)
            o = s2.x
            Dim s3 As Str1(Of T)
            s3.x = Nothing
            o = s3.x
            Dim s4 As Str1(Of T)
            s4.x = Nothing
            o = s4
        End Sub
    End Structure
End Module
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntimeAndReferences(program, additionalRefs:={SystemCoreRef})
            CompilationUtils.AssertTheseDiagnostics(comp, <errors></errors>)
        End Sub

        <WorkItem(722575, "DevDiv")>
        <Fact()>
        Public Sub Bug_722575b()
            Dim program =
<compilation>
    <file name="a.b">
Imports System

Friend Module TestStruct
    Structure Str1(Of T As Structure)
        Dim x As T

        Sub foo()
            Dim o As Object
            Dim s1 As Str1(Of T)
            o = s1
            Dim s2 As Str1(Of T)
            o = s2.x
            Dim s3 As Str1(Of T)
            s3.x = Nothing
            o = s3.x
            Dim s4 As Str1(Of T)
            s4.x = Nothing
            o = s4
        End Sub
    End Structure
End Module
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntimeAndReferences(program, additionalRefs:={SystemCoreRef})
            CompilationUtils.AssertTheseDiagnostics(comp, <errors></errors>)
        End Sub

        <WorkItem(722575, "DevDiv")>
        <Fact()>
        Public Sub Bug_722575c()
            Dim program =
<compilation>
    <file name="a.b">
Imports System

Friend Module TestClass
    Structure Str1(Of T As Class)
        Dim x As T

        Sub foo()
            Dim o As Object
            Dim s1 As Str1(Of T)
            o = s1
            Dim s2 As Str1(Of T)
            o = s2.x
            Dim s3 As Str1(Of T)
            s3.x = Nothing
            o = s3.x
            Dim s4 As Str1(Of T)
            s4.x = Nothing
            o = s4
        End Sub
    End Structure
End Module
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntimeAndReferences(program, additionalRefs:={SystemCoreRef})
            CompilationUtils.AssertTheseDiagnostics(comp,
<errors>
BC42109: Variable 's1' is used before it has been assigned a value. A null reference exception could result at runtime. Make sure the structure or all the reference members are initialized before use
            o = s1
                ~~
BC42104: Variable 'x' is used before it has been assigned a value. A null reference exception could result at runtime.
            o = s2.x
                ~~~~
</errors>)
        End Sub

        <WorkItem(722575, "DevDiv")>
        <Fact()>
        Public Sub Bug_722575d()
            Dim program =
<compilation>
    <file name="a.b">
Imports System

Friend Module TestNewAndDisposable
    Structure Str1(Of T As {IDisposable, New})
        Dim x As T
        Sub foo()
            Dim o As Object
            Dim s1 As Str1(Of T)
            o = s1
            Dim s2 As Str1(Of T)
            o = s2.x
            Dim s3 As Str1(Of T)
            s3.x = Nothing
            o = s3.x
            Dim s4 As Str1(Of T)
            s4.x = Nothing
            o = s4
        End Sub
    End Structure
End Module
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntimeAndReferences(program, additionalRefs:={SystemCoreRef})
            CompilationUtils.AssertTheseDiagnostics(comp, <errors></errors>)
        End Sub

        <WorkItem(722575, "DevDiv")>
        <Fact()>
        Public Sub Bug_722575e()
            Dim program =
<compilation>
    <file name="a.b">
Imports System

Friend Class TestNone(Of T)
    Structure Str1
        Dim x As T
        Sub foo()
            Dim o As Object
            Dim s1 As Str1
            o = s1
            Dim s2 As Str1
            o = s2.x
            Dim s3 As Str1
            s3.x = Nothing
            o = s3.x
            Dim s4 As Str1
            s4.x = Nothing
            o = s4
        End Sub
    End Structure
End Class

Friend Class TestStruct(Of T As Structure)
    Structure Str1
        Dim x As T
        Sub foo()
            Dim o As Object
            Dim s1 As Str1
            o = s1
            Dim s2 As Str1
            o = s2.x
            Dim s3 As Str1
            s3.x = Nothing
            o = s3.x
            Dim s4 As Str1
            s4.x = Nothing
            o = s4
        End Sub
    End Structure
End Class

Friend Class TestClass(Of T As Class)
    Structure Str1
        Dim x As T
        Sub foo()
            Dim o As Object
            Dim s1 As Str1
            o = s1
            Dim s2 As Str1
            o = s2.x
            Dim s3 As Str1
            s3.x = Nothing
            o = s3.x
            Dim s4 As Str1
            s4.x = Nothing
            o = s4
        End Sub
    End Structure
End Class

Friend Class TestNewAndDisposable(Of T As {IDisposable, New})
    Structure Str1
        Dim x As T
        Sub foo()
            Dim o As Object
            Dim s1 As Str1
            o = s1
            Dim s2 As Str1
            o = s2.x
            Dim s3 As Str1
            s3.x = Nothing
            o = s3.x
            Dim s4 As Str1
            s4.x = Nothing
            o = s4
        End Sub
    End Structure
End Class
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntimeAndReferences(program, additionalRefs:={SystemCoreRef})
            CompilationUtils.AssertTheseDiagnostics(comp,
<errors>
BC42109: Variable 's1' is used before it has been assigned a value. A null reference exception could result at runtime. Make sure the structure or all the reference members are initialized before use
            o = s1
                ~~
BC42104: Variable 'x' is used before it has been assigned a value. A null reference exception could result at runtime.
            o = s2.x
                ~~~~
</errors>)
        End Sub

        <WorkItem(617061, "DevDiv")>
        <Fact()>
        Public Sub Bug_617061()
            Dim program =
<compilation>
    <file name="a.b">
Imports System
Imports System.Collections.Generic
Imports System.Linq

Module Program
    Sub Main(args As String())
        Dim a(100) As MEMORY_BASIC_INFORMATION
        Dim b = From x In a Select x.BaseAddress
    End Sub
End Module

Structure MEMORY_BASIC_INFORMATION
    Dim BaseAddress As UIntPtr
End Structure
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntimeAndReferences(program, additionalRefs:={SystemCoreRef})
            CompilationUtils.AssertTheseDiagnostics(comp, <errors></errors>)
        End Sub

        <WorkItem(544072, "DevDiv")>
        <Fact()>
        Public Sub TestBug12221a()
            Dim program =
<compilation name="TestBug12221a">
    <file name="a.b">
Imports System

Structure SS
    Public S As String
End Structure

Module Program222
    Sub Main(args As String())
        Dim s As SS
        Dim dict As New Dictionary(Of String, SS)
        If dict.TryGetValue("", s) Then
        End If
    End Sub
End Module
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
(<expected>
BC42109: Variable 's' is used before it has been assigned a value. A null reference exception could result at runtime. Make sure the structure or all the reference members are initialized before use
        If dict.TryGetValue("", s) Then
                                ~
</expected>))
        End Sub

        <WorkItem(544072, "DevDiv")>
        <Fact()>
        Public Sub TestBug12221b()
            Dim program =
<compilation name="TestBug12221b">
    <file name="a.b">
Imports System

Structure SS
    Public S As Integer
End Structure

Module Program222
    Sub Main(args As String())
        Dim s As SS
        Dim dict As New Dictionary(Of String, SS)
        If dict.TryGetValue("", s) Then
        End If
    End Sub
End Module
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
(<expected>
 </expected>))
        End Sub

        <WorkItem(544072, "DevDiv")>
        <Fact()>
        Public Sub TestBug12221c()
            Dim program =
<compilation name="TestBug12221c">
    <file name="a.b">
Module Program222
    Interface I
    End Interface

    Sub Main(Of T As I)(args As String())
        Dim s As T
        Dim dict As New Dictionary(Of String, T)
        If dict.TryGetValue("", s) Then
        End If
    End Sub
End Module
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
(<expected>
 </expected>))
        End Sub

        <WorkItem(544072, "DevDiv")>
        <Fact()>
        Public Sub TestBug12221d()
            Dim program =
<compilation name="TestBug12221d">
    <file name="a.b">
Module Program222
    Sub Main(Of T As Class)(args As String())
        Dim s As T
        Dim dict As New Dictionary(Of String, T)
        If dict.TryGetValue("", s) Then
        End If
    End Sub
End Module
    </file>
</compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
(<expected>
BC42104: Variable 's' is used before it has been assigned a value. A null reference exception could result at runtime.
        If dict.TryGetValue("", s) Then
                                ~
</expected>))
        End Sub

        <Fact()>
        Public Sub TestReachable1()
            Dim program = <compilation name="TestReachable1">
                              <file name="a.b">
                                Imports System  
                                Module Module1

                                 Sub TestUnreachable1()
                                    Dim i As Integer    ' Dev10 Warning - unused local 
                                    Return

                                    Dim j As Integer    ' Dev10 No warning because this is unreachable 
                                    Console.WriteLine(i, j)
                                End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<expected>
                                                </expected>))
        End Sub

        <Fact>
        Public Sub TestReachable2()
            Dim program = <compilation name="TestReachable2">
                              <file name="a.b">
                                Imports System  
                                Module Module1

                                 Sub TestUnreachable1()
                                    Dim i As Integer    ' Dev10 Warning - unused local 
                                    Return

                                    Dim j As Integer = 1    ' Dev10 No warning because this is unreachable 
                                    Console.WriteLine(i, j)
                                End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<expected>
                                                </expected>))
        End Sub

        <Fact>
        Public Sub TestGoto1()
            Dim program = <compilation name="TestGoto1">
                              <file name="a.b">
                                Imports System  
                                Module Module1

                                Sub TestGoto1()
                                    Dim o1, o2 As Object

                                    GoTo l1

                            l2:
                                    o1 = o2
                                    return

                            l1:
                                    If false Then
                                        o2 = "a"
                                     end if
                                     GoTo l2
                                End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<expected>
BC42104: Variable 'o2' is used before it has been assigned a value. A null reference exception could result at runtime.
                                    o1 = o2
                                         ~~
                                                   
                                               </expected>))
        End Sub

        <Fact>
        Public Sub LambdaEntryPointIsReachable1()
            Dim program = <compilation name="LambdaEntryPointIsReachable1">
                              <file name="a.b">

Imports System
Public Module Program
    Public Sub Main(args As String())
        Dim i As Integer
        Return

        Dim x As Integer = i
        Dim a As action = DirectCast(Sub()
                                         Dim j As Integer = i + j
                                     End Sub, action)
    End Sub
End Module

                            </file>
                          </compilation>

            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
(<expected>
 </expected>))
        End Sub

        <Fact>
        Public Sub LambdaEntryPointIsReachable2()
            Dim program = <compilation name="LambdaEntryPointIsReachable2">
                              <file name="a.b">

Imports System
Public Module Program
    Public Sub Main(args As String())
        Dim i As Integer
        Return

        Dim a As action = DirectCast(Sub()
                                         Dim j As Integer = i + j
                                     End Sub, action)
    End Sub
End Module

                            </file>
                          </compilation>

            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
(<expected>
 </expected>))
        End Sub

        <Fact>
        Public Sub LambdaEntryPointIsReachable3()
            Dim program = <compilation name="LambdaEntryPointIsReachable3">
                              <file name="a.b">

Imports System
Public Module Program
    Public Sub Main(args As String())
        Dim i As Integer
        Return

        Dim a As action = DirectCast(Sub()
                                         Dim j As Integer = i + j
                                         Return

                                         Dim k = j
                                     End Sub, action)
    End Sub
End Module
                            </file>
                          </compilation>

            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
(<expected>
 </expected>))
        End Sub

        <Fact>
        Public Sub TestDoLoop1()
            Dim program = <compilation name="TestDoLoop1">
                              <file name="a.b">
                                Imports System  
                                Module Module1

                                Sub TestGoto1()
                                    Dim o1, o2 As Object

                                    GoTo l1

                            l2:
                                    o1 = o2
                                    return

                            l1:     
                                    do 
                                       exit do 
                                       o2 = "a"
                                    loop 

                                    GoTo l2
                                End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<expected>
BC42104: Variable 'o2' is used before it has been assigned a value. A null reference exception could result at runtime.
                                    o1 = o2
                                         ~~
                                               </expected>))
        End Sub

        <Fact>
        Public Sub TestDoLoop2()
            Dim program = <compilation name="TestDoLoop2">
                              <file name="a.b">
                                Imports System  
                                Module Module1

                                Sub TestGoto1()
                                    Dim o1, o2 As Object

                                    GoTo l1
                            l2:
                                    o1 = o2
                                    return                    

                                   do 
                            l1:
                                       o2 = "a"
                                       exit do
                                    loop 

                                    goto l2
                                End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()

        End Sub

        <Fact>
        Public Sub TestBlocks()
            Dim program = <compilation name="TestDoLoop2">
                              <file name="a.b">
                                Imports System  
                                Module Module1

                                Sub TestGoto1()
                                    do until false
                                        while false
                                        end while

                                    loop


                                    do while true
                                        while true
                                        end while
                                    loop        

                     
                                End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()

        End Sub

        <Fact>
        Public Sub RefParameter01()
            Dim program = <compilation name="RefParameter01">
                              <file name="a.b">
class Program
    public shared Sub Main(args as string())
        dim i as string
        F(i) ' use of unassigned local variable &apos;i&apos;
    end sub
    shared sub F(byref i as string) 
    end sub
end class</file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Assert.NotEmpty(Me.FlowDiagnostics(comp).AsEnumerable().Where(Function(e) e.Severity = DiagnosticSeverity.[Warning]))
        End Sub

        <Fact>
        Public Sub FunctionDoesNotReturnAValue()
            Dim program = <compilation name="FunctionDoesNotReturnAValue">
                              <file name="a.b">
class Program
    public function foo() as integer
    end function
end class</file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<errors>
BC42353: Function 'foo' doesn't return a value on all code paths. Are you missing a 'Return' statement?
    end function
    ~~~~~~~~~~~~                                                   
                                               </errors>))
        End Sub

        <Fact>
        Public Sub FunctionReturnsAValue()
            Dim program = <compilation name="FunctionDoesNotReturnAValue">
                              <file name="a.b">
class Program
    public function foo() as integer
        return 0
    end function

    public function bar() as integer
        bar = 0
    end function
end class</file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<errors>
                                                </errors>))
        End Sub

        <WorkItem(540687, "DevDiv")>
        <Fact>
        Public Sub FunctionDoesNotReturnAnEnumValue()
            Dim program = <compilation name="FunctionDoesNotReturnAValue">
                              <file name="a.b">
Imports System
Imports System.Collections.Generic
Imports System.Linq

Enum e1
    a
    b
End Enum

Module Program
    Function f As e1
    End Function

    Sub Main(args As String())
        Dim x As Func(Of e1) = Function()

                               End Function

    End Sub
End Module</file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<errors>
                                                    <![CDATA[
BC42353: Function 'f' doesn't return a value on all code paths. Are you missing a 'Return' statement?
    End Function
    ~~~~~~~~~~~~
BC42353: Function '<anonymous method>' doesn't return a value on all code paths. Are you missing a 'Return' statement?
                               End Function
                               ~~~~~~~~~~~~                                                   
]]>
                                                </errors>))
        End Sub

        <WorkItem(541005, "DevDiv")>
        <Fact>
        Public Sub TestLocalsInitializedByAsNew()
            Dim program = <compilation name="TestLocalsInitializedByAsNew">
                              <file name="a.b">
                                Module Module1

                                    Class C
                                        Public Sub New()
                                        End Class
                                    End Class

                                    Sub Foo()
                                        Dim x, y, z as New C
                                    End Sub

                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()
        End Sub

        <WorkItem(542817, "DevDiv")>
        <Fact>
        Public Sub ConditionalStateInsideQueryLambda()
            Dim program = <compilation name="ConditionalStateInsideQueryLambda">
                              <file name="a.b">
Imports System.Linq
        Class PEModuleSymbol
            Function IsNoPiaLocalType(i As Integer) As Boolean
            End Function
        End Class

        Class PENamedTypeSymbol
            Friend Sub New(
                moduleSymbol As PEModuleSymbol,
                containingNamespace As PENamespaceSymbol,
                typeRid As Integer
            )
            End Sub
        End Class

        Class PENamespaceSymbol

            Private Sub LazyInitializeTypes(types As IEnumerable(Of IGrouping(Of String, Integer)))

                Dim moduleSymbol As PEModuleSymbol = Nothing

                Dim children As IEnumerable(Of PENamedTypeSymbol)

                children = (From g In types, t In g
                            Where Not moduleSymbol.IsNoPiaLocalType(t)
                            Select New PENamedTypeSymbol(moduleSymbol, Me, t))
            End Sub
        End Class
                            </file>
                          </compilation>

            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim diags = comp.GetDiagnostics()

        End Sub

        <Fact>
        Public Sub TestSelectCase_CaseClauseExpression_NeverConstantExpr()
            Dim program = <compilation name="TestUninitializedIntegerLocal">
                              <file name="a.b">
                                  <![CDATA[
                                    Imports System        
                                    Module M1
                                        Sub Main()
                                            For x = 0 to 11
                                                Console.Write(x.ToString() + ":")
                                                Test(x)
                                            Next
                                        End Sub

                                        Sub Test(number as Integer)
                                            ' No unreachable code warning for any case block
                                            Select Case 1
                                                Case 1
                                                    Console.WriteLine("Equal to 1")
                                                Case Is < 1
                                                    Console.WriteLine("Less than 1")
                                                Case 1 To 5
                                                    Console.WriteLine("Between 2 and 5, inclusive")
                                                Case 6, 7, 8
                                                    Console.WriteLine("Between 6 and 8, inclusive")
                                                Case 9 To 10
                                                    Console.WriteLine("Equal to 9 or 10")
                                                Case Else
                                                    Console.WriteLine("Greater than 10")
                                            End Select
                                        End Sub
                                    End Module
                                        ]]>
                              </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()
        End Sub

        <Fact>
        Public Sub TestSelectCase_NoCaseBlocks_NoUnusedLocalWarning()
            Dim program = <compilation name="TestUnusedIntegerLocal">
                              <file name="a.b">
                                Imports System        
                                Module M1
                                    Sub Main()
                                        Dim number as Integer = 10
                                        Select Case number      ' no unused integer warning even though select statement is optimized away
                                        End Select
                                    End Sub
                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()
        End Sub

        <Fact>
        Public Sub TestSelectCase_UninitializedLocalWarning()
            Dim program = <compilation name="TestUnusedIntegerLocal">
                              <file name="a.b">
                                Imports System        
                                Module M1
                                    Sub Main()
                                        Dim obj as Object
                                        Select Case 1
                                            Case 1      ' Case clause expression are never compile time constants, hence Case Else is reachable.
                                                obj = new Object()
                                            Case Else
                                        End Select
                                        Console.WriteLine(obj)      ' Use of uninitialized local warning
                                    End Sub
                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                               (<errors>
BC42104: Variable 'obj' is used before it has been assigned a value. A null reference exception could result at runtime.
                                        Console.WriteLine(obj)      ' Use of uninitialized local warning
                                                          ~~~
                                               </errors>))
        End Sub

        <Fact>
        Public Sub TestSelectCase_NoUninitializedLocalWarning()
            Dim program = <compilation name="TestUnusedIntegerLocal">
                              <file name="a.b">
                                Imports System        
                                Module M1
                                    Sub Main()
                                        Dim obj as Object
                                        Select Case 1
                                            Case 1
                                                obj = new Object()
                                            Case Is > 1, 2
                                                obj = new Object()
                                            Case 1 To 10
                                                obj = new Object()
                                            Case Else
                                                obj = new Object()
                                        End Select
                                        Console.WriteLine(obj)
                                    End Sub
                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()
        End Sub

        <Fact>
        Public Sub TestSelectCase_UninitializedLocalWarning_InvalidRangeClause()
            Dim program = <compilation name="TestUnusedIntegerLocal">
                              <file name="a.b">
                                Imports System        
                                Module M1
                                    Sub Main()
                                        Dim obj as Object
                                        Select Case 1
                                            Case 10 To 1                ' Invalid range clause
                                                obj = new Object()
                                        End Select
                                        Console.WriteLine(obj)      ' Use of uninitialized local warning
                                    End Sub
                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            CompilationUtils.AssertTheseDiagnostics(errs,
                                    (<errors>
BC42104: Variable 'obj' is used before it has been assigned a value. A null reference exception could result at runtime.
                                        Console.WriteLine(obj)      ' Use of uninitialized local warning
                                                          ~~~
                                    </errors>))
        End Sub

        <Fact>
        Public Sub TestSelectCase_NoUninitializedLocalWarning_JumpToAnotherCaseBlock()
            Dim program = <compilation name="TestUnusedIntegerLocal">
                              <file name="a.b">
                                Imports System        
                                Module M1
                                    Sub Main()
                                        Dim obj as Object
                                        Select Case 1
                                            Case 1      ' Case clause expression are never compile time constants, hence Case Else is reachable.
                                              Label1:
                                                obj = new Object()
                                            Case Else
                                                Goto Label1
                                        End Select
                                        Console.WriteLine(obj)      ' Use of uninitialized local warning
                                    End Sub
                                End Module
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()
        End Sub

        <WorkItem(543095, "DevDiv")>
        <Fact()>
        Public Sub TestSelectCase_Error_MissingCaseStatement()
            Dim program = <compilation name="TestSelectCase_Error_MissingCaseStatement">
                              <file name="a.b">
                                Imports System
                                Module Program
                                    Sub Main(args As String())
                                        Dim x As New myclass1
                                        Select x
                                Ca
                                End Sub
                                End Module
                                Structure myclass1
                                    Implements IDisposable
                                    Public Sub dispose() Implements IDisposable.Dispose
                                    End Sub
                                End Structure
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()
        End Sub

        <WorkItem(543095, "DevDiv")>
        <Fact()>
        Public Sub TestSelectCase_Error_MissingCaseExpression()
            Dim program = <compilation name="TestSelectCase_Error_MissingCaseExpression">
                              <file name="a.b">
                                Imports System
                                Module Program
                                    Sub Main(args As String())
                                        Dim x As New myclass1
                                        Select x
                                            Case
                                        End Select
                                End Sub
                                End Module
                                Structure myclass1
                                    Implements IDisposable
                                    Public Sub dispose() Implements IDisposable.Dispose
                                    End Sub
                                End Structure
                            </file>
                          </compilation>
            Dim comp = CompilationUtils.CreateCompilationWithMscorlib(program)
            Dim errs = Me.FlowDiagnostics(comp)
            errs.AssertNoErrors()
        End Sub

        <Fact(Skip:="529405"), WorkItem(529405, "DevDiv")>
        Public Sub TestThrowDoNotReportUnreachable()
            Dim program =
<compilation>
    <file name="a.b">
Imports System
Class Test
    Sub Method1()
        Throw New Exception()
        Return
    End Sub
    Function Method2(x As Integer) As Integer
        If x &lt; 0 Then
            Return -1
        Else
            Return 1
        End If
        Throw New System.InvalidOperationException()
    End Function
End Class
    </file>
</compilation>
            CompilationUtils.CreateCompilationWithMscorlib(program).VerifyDiagnostics()
        End Sub

        <WorkItem(531310, "DevDiv")>
        <Fact()>
        Public Sub Bug_17926()
            Dim program =
<compilation>
    <file name="a.b">
Imports System
Imports System.Collections.Generic

Module Module1
    Public Function RemoveTextWriterTraceListener() As Boolean
        Try
            RemoveTextWriterTraceListener = False


            'Return true to indicate that the TextWriterTraceListener was removed
            RemoveTextWriterTraceListener = True
        Catch e As Exception
            Console.WriteLine("")
        End Try
        Return RemoveTextWriterTraceListener
    End Function
End Module
    </file>
</compilation>
            Dim compilation = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(program)
            CompilationUtils.AssertTheseDiagnostics(compilation, <errors></errors>)
        End Sub

        <WorkItem(531529, "DevDiv")>
        <Fact()>
        Public Sub Bug_18255()
            Dim program =
<compilation>
    <file name="a.b">
Imports System
Public Class TestState
    ReadOnly Property IsImmediateWindow As Boolean
        Get
            Return IsImmediateWindow
        End Get
    End Property
End Class    
    </file>
</compilation>
            Dim compilation = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(program)
            CompilationUtils.AssertTheseDiagnostics(compilation,
<errors>
</errors>)
        End Sub

        <WorkItem(531530, "DevDiv")>
        <Fact()>
        Public Sub Bug_18256()
            Dim program =
<compilation>
    <file name="a.b">
Imports System

Structure SSSS1
End Structure

Structure SSSS2
    Private s As String
End Structure

Enum e1
    a
End Enum

Class CCCCCC
    Public ReadOnly Property Prop1 As System.Threading.CancellationToken
        Get
        End Get
    End Property
    Public ReadOnly Property Prop2 As SSSS1
        Get
        End Get
    End Property
    Public ReadOnly Property Prop3 As SSSS2
        Get
        End Get
    End Property
    Public ReadOnly Property Prop4 As e1
        Get
        End Get
    End Property
    Public ReadOnly Property Prop5 As String
        Get
        End Get
    End Property
    Public ReadOnly Property Prop6 As Integer
        Get
        End Get
    End Property
End Class
    </file>
</compilation>

            Dim compilation = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntime(program)
            CompilationUtils.AssertTheseDiagnostics(compilation,
<errors>
BC42107: Property 'Prop3' doesn't return a value on all code paths. A null reference exception could occur at run time when the result is used.
        End Get
        ~~~~~~~
BC42355: Property 'Prop4' doesn't return a value on all code paths. Are you missing a 'Return' statement?
        End Get
        ~~~~~~~
BC42107: Property 'Prop5' doesn't return a value on all code paths. A null reference exception could occur at run time when the result is used.
        End Get
        ~~~~~~~
BC42355: Property 'Prop6' doesn't return a value on all code paths. Are you missing a 'Return' statement?
        End Get
        ~~~~~~~
</errors>)
        End Sub

        <Fact(), WorkItem(531237, "DevDiv")>
        Public Sub Bug17796()
            Dim program = <compilation>
                              <file name="a.b"><![CDATA[
Imports System, System.Runtime.CompilerServices

Public Module Program
    Sub Main()
    End Sub
    Interface ITextView
        Event Closed As EventHandler
    End Interface
    <Extension()>
    Public Sub AddOneTimeCloseHandler(ByVal view As ITextView, ByVal del As Action)
        Dim del2 As EventHandler = Sub(notUsed1, notUsed2)
                                       del()
                                       RemoveHandler view.Closed, del2 
                                   End Sub

        Dim del3 As Object = Function() As Object
                                 del()
                                 return del3 
                             End Function.Invoke()

        Dim del4 As EventHandler
        del4 = Sub(notUsed1, notUsed2)
                   del()
                   RemoveHandler view.Closed, del4 
               End Sub

        Dim del5 As Object
        del5 = Function() As Object
                   del()
                   return del5 
               End Function.Invoke()

        Dim del6 As EventHandler = DirectCast(TryCast(CType(
                                   Sub(notUsed1, notUsed2)
                                       del()
                                       RemoveHandler view.Closed, del6 
                                   End Sub, EventHandler), EventHandler), EventHandler)

        Dim del7 As EventHandler = (Sub(notUsed1, notUsed2)
                                       del()
                                       RemoveHandler view.Closed, del7 
                                   End Sub)
    End Sub
End Module
                            ]]></file>
                          </compilation>

            Dim comp = CompilationUtils.CreateCompilationWithMscorlibAndVBRuntimeAndReferences(program, {SystemCoreRef})

            CompilationUtils.AssertTheseDiagnostics(comp,
<errors>
BC42104: Variable 'del3' is used before it has been assigned a value. A null reference exception could result at runtime.
                                 return del3 
                                        ~~~~
BC42104: Variable 'del5' is used before it has been assigned a value. A null reference exception could result at runtime.
                   return del5 
                          ~~~~
</errors>)
        End Sub

    End Class

End Namespace
