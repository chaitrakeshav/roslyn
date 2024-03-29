﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    /// <summary>
    /// A container synthesized for a lambda, iterator method, async method, or dynamic-sites.
    /// </summary>
    internal abstract class SynthesizedContainer : NamedTypeSymbol
    {
        private readonly string name;
        private readonly TypeMap typeMap;
        private readonly ImmutableArray<TypeParameterSymbol> typeParameters;

        protected SynthesizedContainer(string name, int parameterCount, bool returnsVoid)
        {
            Debug.Assert(name != null);
            this.name = name;
            this.typeMap = TypeMap.Empty;
            this.typeParameters = CreateTypeParameters(parameterCount, returnsVoid);
        }

        protected SynthesizedContainer(string name, MethodSymbol topLevelMethod)
        {
            Debug.Assert(name != null);
            Debug.Assert(topLevelMethod != null);

            this.name = name;
            this.typeMap = TypeMap.Empty.WithAlphaRename(topLevelMethod, this, out this.typeParameters);
        }

        protected SynthesizedContainer(string name, ImmutableArray<TypeParameterSymbol> typeParameters, TypeMap typeMap)
        {
            Debug.Assert(name != null);
            Debug.Assert(!typeParameters.IsDefault);
            Debug.Assert(typeMap != null);

            this.name = name;
            this.typeParameters = typeParameters;
            this.typeMap = typeMap;
        }

        private ImmutableArray<TypeParameterSymbol> CreateTypeParameters(int parameterCount, bool returnsVoid)
        {
            var typeParameters = ArrayBuilder<TypeParameterSymbol>.GetInstance(parameterCount + (returnsVoid ? 0 : 1));
            for (int i = 0; i < parameterCount; i++)
            {
                typeParameters.Add(new AnonymousTypeManager.AnonymousTypeParameterSymbol(this, i, "T" + (i + 1)));
            }

            if (!returnsVoid)
            {
                typeParameters.Add(new AnonymousTypeManager.AnonymousTypeParameterSymbol(this, parameterCount, "TResult"));
            }

            return typeParameters.ToImmutableAndFree();
        }

        internal TypeMap TypeMap
        {
            get { return typeMap; }
        }

        internal virtual MethodSymbol Constructor
        {
            get { return null; }
        }

        internal sealed override bool IsInterface
        {
            get { return this.TypeKind == TypeKind.Interface; }
        }

        internal override void AddSynthesizedAttributes(ref ArrayBuilder<SynthesizedAttributeData> attributes)
        {
            base.AddSynthesizedAttributes(ref attributes);

            if (ContainingSymbol.Kind == SymbolKind.NamedType && ContainingSymbol.IsImplicitlyDeclared)
            {
                return;
            }

            var compilation = ContainingSymbol.DeclaringCompilation;

            // this can only happen if frame is not nested in a source type/namespace (so far we do not do this)
            // if this happens for whatever reason, we do not need "CompilerGenerated" anyways
            Debug.Assert(compilation != null, "SynthesizedClass is not contained in a source module?");

            AddSynthesizedAttribute(ref attributes, compilation.SynthesizeAttribute(
                WellKnownMember.System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor));
        }

        public sealed override ImmutableArray<TypeParameterSymbol> TypeParameters
        {
            get { return typeParameters; }
        }

        public sealed override string Name
        {
            get { return name; }
        }

        public override ImmutableArray<Location> Locations
        {
            get { return ImmutableArray<Location>.Empty; }
        }

        public override ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
        {
            get { return ImmutableArray<SyntaxReference>.Empty; }
        }

        public override IEnumerable<string> MemberNames
        {
            get { return SpecializedCollections.EmptyEnumerable<string>(); }
        }

        public override NamedTypeSymbol ConstructedFrom
        {
            get { return this; }
        }

        public override bool IsSealed
        {
            get { return true; }
        }

        public override bool IsAbstract
        {
            get { return (object)Constructor == null; }
        }

        internal override ImmutableArray<TypeSymbol> TypeArgumentsNoUseSiteDiagnostics
        {
            get { return StaticCast<TypeSymbol>.From(TypeParameters); }
        }

        public override ImmutableArray<Symbol> GetMembers()
        {
            Symbol constructor = this.Constructor;
            return (object)constructor == null ? ImmutableArray<Symbol>.Empty : ImmutableArray.Create(constructor);
        }

        public override ImmutableArray<Symbol> GetMembers(string name)
        {
            var ctor = Constructor;
            return ((object)ctor != null && name == ctor.Name) ? ImmutableArray.Create<Symbol>(ctor) : ImmutableArray<Symbol>.Empty;
        }

        internal override IEnumerable<FieldSymbol> GetFieldsToEmit()
        {
            foreach (var m in this.GetMembers())
            {
                switch (m.Kind)
                {
                    case SymbolKind.Field:
                        yield return (FieldSymbol)m;
                        break;
                }
            }
        }

        internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers()
        {
            return this.GetMembersUnordered();
        }

        internal override ImmutableArray<Symbol> GetEarlyAttributeDecodingMembers(string name)
        {
            return this.GetMembers(name);
        }

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers()
        {
            return ImmutableArray<NamedTypeSymbol>.Empty;
        }

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name)
        {
            return ImmutableArray<NamedTypeSymbol>.Empty;
        }

        public override ImmutableArray<NamedTypeSymbol> GetTypeMembers(string name, int arity)
        {
            return ImmutableArray<NamedTypeSymbol>.Empty;
        }

        public override Accessibility DeclaredAccessibility
        {
            get { return Accessibility.Private; }
        }

        public override bool IsStatic
        {
            get { return false; }
        }

        internal override ImmutableArray<NamedTypeSymbol> InterfacesNoUseSiteDiagnostics
        {
            get { return ImmutableArray<NamedTypeSymbol>.Empty; }
        }

        internal override ImmutableArray<NamedTypeSymbol> GetInterfacesToEmit()
        {
            return CalculateInterfacesToEmit();
        }

        internal override NamedTypeSymbol BaseTypeNoUseSiteDiagnostics
        {
            get { return ContainingAssembly.GetSpecialType(this.TypeKind == TypeKind.Struct ? SpecialType.System_ValueType : SpecialType.System_Object); }
        }

        internal override NamedTypeSymbol GetDeclaredBaseType(ConsList<Symbol> basesBeingResolved)
        {
            return BaseTypeNoUseSiteDiagnostics;
        }

        internal override ImmutableArray<NamedTypeSymbol> GetDeclaredInterfaces(ConsList<Symbol> basesBeingResolved)
        {
            return InterfacesNoUseSiteDiagnostics;
        }

        public override bool MightContainExtensionMethods
        {
            get { return false; }
        }

        public override int Arity
        {
            get { return TypeParameters.Length; }
        }

        internal override bool MangleName
        {
            get { return Arity > 0; }
        }

        public override bool IsImplicitlyDeclared
        {
            get { return true; }
        }

        internal override bool ShouldAddWinRTMembers
        {
            get { return false; }
        }

        internal override bool IsWindowsRuntimeImport
        {
            get { return false; }
        }

        internal override bool IsComImport
        {
            get { return false; }
        }

        internal sealed override ObsoleteAttributeData ObsoleteAttributeData
        {
            get { return null; }
        }

        internal sealed override ImmutableArray<string> GetAppliedConditionalSymbols()
        {
            return ImmutableArray<string>.Empty;
        }

        internal override bool HasDeclarativeSecurity
        {
            get { return false; }
        }

        internal override CharSet MarshallingCharSet
        {
            get { return DefaultMarshallingCharSet; }
        }

        internal override bool IsSerializable
        {
            get { return false; }
        }

        internal override IEnumerable<Cci.SecurityAttribute> GetSecurityInformation()
        {
            throw ExceptionUtilities.Unreachable;
        }

        internal override AttributeUsageInfo GetAttributeUsageInfo()
        {
            return default(AttributeUsageInfo);
        }

        internal override TypeLayout Layout
        {
            get { return default(TypeLayout); }
        }

        internal override bool HasSpecialName
        {
            get { return false; }
        }
    }
}
