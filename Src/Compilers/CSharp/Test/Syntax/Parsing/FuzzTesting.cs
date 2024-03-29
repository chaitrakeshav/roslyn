﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using ProprietaryTestResources = Microsoft.CodeAnalysis.Test.Resources.Proprietary;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Parsing
{
    public class FuzzTesting : CSharpTestBase
    {
        [WorkItem(540005, "DevDiv")]
        [Fact]
        public void c01()
        {
            var test = @"///   \u2750\uDFC1  = </   @goto   </  ascending abstract  + (  descending __arglist  + descending   @if   <?   @switch  + global @long  + @orderby   \u23DC\u6D71\u5070\u9350   ++  into _\u6105\uE331\u27D0   #  join [  + break   @extern   [   @char   <<  partial |  + remove + do   @else  + @typeof   @private  + 
";
            var tree = SyntaxFactory.ParseSyntaxTree(test);
        }

        [WorkItem(540006, "DevDiv")]
        [Fact]
        public void c02()
        {
            var test = @"internal int TYPES()        {             break  retVal =  @while ; __reftype             CLASS c = dynamic   descending  CLASS( % ; on             IF xx = module   _4䓞  |=              \u14DB\u0849   <![CDATA[  c =>  @default  $             retVal @assembly  += c void .Member &  -= ; @typeof 
";
            var tree = SyntaxFactory.ParseSyntaxTree(test);
        }

        [WorkItem(540007, "DevDiv")]
        [Fact]
        public void c03()
        {
            var test = @"/// </summary>        /// <returns></returns>         else  int OPERATOR @uint  $ )        { -              static ? operator  :: ]  @readonly  = @on   async  int? , [ return ] { 1 ! ,  @property  &  3 !   @case  %   partial   += ;/*[] bug*/ // YES []            int % ] endregion  var  =   ]]>   @for  |=   @struct , 3, lock  4 @on  %  5 goto  } @stackalloc  } /*,;*/            int %=  i = @fixed   ?> int << a base  <= 1] default ; 

";
            var tree = SyntaxFactory.ParseSyntaxTree(test);
        }

        [WorkItem(540007, "DevDiv")]
        [Fact]
        public void c04()
        {
            var test = @"/// </summary>        /// <returns></returns>        internal  do  OPERATOR || )        {            int?[] a = new int? \u14DB\u0849 [5] { 1, 2, 3, 4,  @using  } /= /*[] bug*/ // YES []            int[] var = { 1, 2, 3, 4, 5 } $ /*,;*/            int i =  ; int)a[1];/*[]*/            i = i  <<=   @__arglist  - i @sbyte  *  @extern  / i % i ++   %  i  ||   @checked  ^ i; 
 /*+ - * / % & | ^*/
";
            var tree = SyntaxFactory.ParseSyntaxTree(test);
        }

        [WorkItem(540007, "DevDiv")]
        [Fact]
        public void c000138()
        {
            var test = @"int?[] ///  a   = new int? .  @string ] { 1,  typeof  $  3, 4, 5 } static ; @partial /*[] bug*/ // YES []            int[] var = { 1,  else , 3 </  4 |  5 };/*,;*/            int i = (int)a @in [ __refvalue ] [ /*[]*/             @readonly  = i + i - abstract   @typevar  * i /  @this  % i & ,  i | i ^ unchecked  i; in /*+ - * / % & | ^*/            bool b = true & false +  | true ^ false readonly ;/*& | ^*/             @unsafe  = !b;/*!*/            i = ~i;/*~i*/            b = i < (i - 
  1 @for ) && (i + 1) > i;/*< && >*/
";
            var tree = SyntaxFactory.ParseSyntaxTree(test);
        }

        [WorkItem(540007, "DevDiv")]
        [Fact]
        public void c000241()
        {
            var test = @"/// </summary>        /// <returns></returns>         const   by  TYPES ascending  / ) $         { @let             int @byte   @by   |  0 
 ; 
";
            var tree = SyntaxFactory.ParseSyntaxTree(test);
        }

        [WorkItem(540007, "DevDiv")]
        [Fact]
        public void c024928()
        {
            var test = @"/// </summary>        /// <returns></returns>        internal int OPERATOR()        { //             int?[ *   @method   !  new int explicit  , [  5 --  {  \uDD48\uEF5C , 2,  @ascending , @foreach   \uD17B\u21A8  .  5  ;  { /*[] bug*/ // YES []            int ::  (  <=  var  />  { @readonly  1 <!--  2 __makeref  ?  3 @descending , 4 @float , 5 } disable ;/*,;*/            int -=   _\uAEC4   -  ( operator int <<= a =>  += ] @abstract ; property /*[]*/            i = i double  + i -  @async   -  i '   &  i  )  i &  @using   #   @byte   ,   \u7EE1\u8B45 ;/*+ - * / % & | ^*/            bool b %=  = true  }   fixed  | class   join  ^ ?>   true ;/*& | ^*/            b  ^=  ! @null ;/*!*/             @stackalloc  = @in   ==  @default ;/*~i*/            b  \  i base  <  / i -  await ) && @into  ( new i pragma  + 1 @for ) > i _\uE02B\u7325 ; else /*< && >*/             continue   @double  = _Ƚ揞   in   ^  1 internal   ::  0;/*? :*/   // YES :            i++ ~ /*++*/             _\u560C\uF27E\uB73F -- @sizeof ;/*--*/            b @public  = /=   enum  && params  false  >>  true;/*&& ||*/            i @explicit   #   @byte   >>=   await ;/*<<*/             @sbyte  = @operator  i >> 5;/*>>*/            int  @from  = i;            b  >>   @protected  == )  j && assembly  i @const  != j ""   |=  i <=  @explicit  &&  @await  >=  @typeof ;/*= == && != <= >=*/            i @long   >>=  (int ]]>  &=  ( /*+=*/            i _Ƚ揞  -= i explicit  -> /*-=*/            i  {  i -= /**=*/            if ]  ( <<= i @assembly   )  0 .                  @select ++; 
 
";
            var tree = SyntaxFactory.ParseSyntaxTree(test);
        }

        [Fact(Skip = "658140"), WorkItem(658140, "DevDiv")]
        public void ParseFileOnBinaryFile()
        {
            // This is doing the same thing as ParseFile, but using a MemoryStream
            // instead of FileStream (because I don't want to write a file to disk).
            using (var data = new MemoryStream(ProprietaryTestResources.NetFX.v4_0_30319.mscorlib))
            {
                SyntaxTree tree  = null;

                Assert.DoesNotThrow(() => tree = SyntaxFactory.ParseSyntaxTree(new EncodedStringText(data, encodingOpt: null)));

                tree.GetDiagnostics().VerifyErrorCodes(Diagnostic(ErrorCode.ERR_BinaryFile));
            }
        }
    }
}
