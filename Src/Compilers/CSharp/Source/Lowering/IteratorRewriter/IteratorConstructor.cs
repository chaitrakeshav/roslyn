﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp
{
    /// <summary>
    /// The constructor of the class that is the translation of an iterator method.
    /// </summary>
    internal sealed class IteratorConstructor : SynthesizedInstanceConstructor, ISynthesizedMethodBodyImplementationSymbol
    {
        private readonly ImmutableArray<ParameterSymbol> parameters;

        internal IteratorConstructor(IteratorStateMachine container)
            : base(container)
        {
            var intType = container.DeclaringCompilation.GetSpecialType(SpecialType.System_Int32);
            parameters = ImmutableArray.Create<ParameterSymbol>(
                new SynthesizedParameterSymbol(this, intType, 0, RefKind.None, GeneratedNames.MakeStateMachineStateName()));
        }

        internal override void AddSynthesizedAttributes(ref ArrayBuilder<SynthesizedAttributeData> attributes)
        {
            base.AddSynthesizedAttributes(ref attributes);

            var compilation = this.DeclaringCompilation;
            AddSynthesizedAttribute(ref attributes, compilation.SynthesizeAttribute(WellKnownMember.System_Diagnostics_DebuggerHiddenAttribute__ctor));
        }

        public override ImmutableArray<ParameterSymbol> Parameters
        {
            get { return parameters; }
        }

        public override Accessibility DeclaredAccessibility
        {
            get { return Accessibility.Public; }
        }

        IMethodSymbol ISynthesizedMethodBodyImplementationSymbol.Method
        {
            get { return ((ISynthesizedMethodBodyImplementationSymbol)this.ContainingSymbol).Method; }
        }

        bool ISynthesizedMethodBodyImplementationSymbol.HasMethodBodyDependency
        {
            get { return false; }
        }
    }
}