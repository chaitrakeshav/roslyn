﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Roslyn.Utilities;
using EmitContext = Microsoft.CodeAnalysis.Emit.EmitContext;

namespace Microsoft.Cci
{
    internal abstract class ReferenceIndexerBase : MetadataVisitor
    {
        protected readonly HashSet<IReference> alreadySeen = new HashSet<IReference>();
        protected readonly HashSet<IReference> alreadyHasToken = new HashSet<IReference>();
        protected bool typeReferenceNeedsToken;
        protected IModule/*?*/ module;

        internal ReferenceIndexerBase(EmitContext context)
            : base(context)
        {
        }

        public override abstract void Visit(IAssembly assembly);

        public override void Visit(IAssemblyReference assemblyReference)
        {
            if (assemblyReference != this.module.AsAssembly)
            {
                RecordAssemblyReference(assemblyReference);
            }
        }

        protected abstract void RecordAssemblyReference(IAssemblyReference assemblyReference);

        public override void Visit(ITypeExport aliasForType)
        {
            this.Visit(aliasForType.GetAttributes(Context));

            // do not visit the reference to aliased type, it does not get into the type ref table based only on its membership of the exported types collection.
            // but DO visit the reference to assembly (if any) that defines the aliased type. That assembly might not already be in the assembly reference list.
            var definingUnit = TypeHelper.GetDefiningUnitReference(aliasForType.ExportedType, Context);
            var definingAssembly = definingUnit as IAssemblyReference;
            if (definingAssembly != null)
            {
                this.Visit(definingAssembly);
            }

            var definingModule = definingUnit as IModuleReference;
            if (definingModule != null)
            {
                definingAssembly = definingModule.GetContainingAssembly(Context);
                if (definingAssembly != null && !ReferenceEquals(definingAssembly, this.module.GetContainingAssembly(Context)))
                {
                    this.Visit(definingAssembly);
                }
            }
        }

        public override void Visit(ICustomModifier customModifier)
        {
            this.typeReferenceNeedsToken = true;
            this.Visit(customModifier.GetModifier(Context));
        }

        public override void Visit(IEventDefinition eventDefinition)
        {
            this.typeReferenceNeedsToken = true;
            this.Visit(eventDefinition.GetType(Context));
            Debug.Assert(!this.typeReferenceNeedsToken);
        }

        public override void Visit(IFieldReference fieldReference)
        {
            if (!alreadySeen.Add(fieldReference))
            {
                return;
            }

            IUnitReference/*?*/ definingUnit = TypeHelper.GetDefiningUnitReference(fieldReference.GetContainingType(Context), Context);
            if (definingUnit != null && ReferenceEquals(definingUnit, this.module))
            {
                return;
            }

            this.Visit((ITypeMemberReference)fieldReference);
            this.Visit(fieldReference.GetType(Context));
            ReserveFieldToken(fieldReference);
        }

        protected abstract void ReserveFieldToken(IFieldReference fieldReference);

        public override void Visit(IFileReference fileReference)
        {
            RecordFileReference(fileReference);
        }

        protected abstract void RecordFileReference(IFileReference fileReference);

        public override void Visit(IGenericMethodInstanceReference genericMethodInstanceReference)
        {
            this.Visit(genericMethodInstanceReference.GetGenericArguments(Context));
            this.Visit(genericMethodInstanceReference.GetGenericMethod(Context));
        }

        public override void Visit(IGenericParameter genericParameter)
        {
            this.Visit(genericParameter.GetAttributes(Context));
            this.VisitTypeReferencesThatNeedTokens(genericParameter.GetConstraints(Context));
        }

        public override void Visit(IGenericTypeInstanceReference genericTypeInstanceReference)
        {
            // ^ ensures this.path.Count == old(this.path.Count);
            INestedTypeReference nestedType = genericTypeInstanceReference.AsNestedTypeReference;

            if (nestedType != null)
            {
                ITypeReference containingType = nestedType.GetContainingType(Context);

                if (containingType.AsGenericTypeInstanceReference != null ||
                    containingType.AsSpecializedNestedTypeReference != null)
                {
                    this.Visit(nestedType.GetContainingType(Context));
                }
            }

            this.Visit(genericTypeInstanceReference.GenericType);
            this.Visit(genericTypeInstanceReference.GetGenericArguments(Context));
        }

        public override void Visit(IMarshallingInformation marshallingInformation)
        {
            // The type references in the marshalling information do not end up in tables, but are serialized as strings.
        }

        public override void Visit(IMethodDefinition method)
        {
            base.Visit(method);
            ProcessMethodBody(method);
        }

        protected abstract void ProcessMethodBody(IMethodDefinition method);

        public override void Visit(IMethodReference methodReference)
        {
            IGenericMethodInstanceReference/*?*/ genericMethodInstanceReference = methodReference.AsGenericMethodInstanceReference;
            if (genericMethodInstanceReference != null)
            {
                this.Visit(genericMethodInstanceReference);
                return;
            }

            if (!alreadySeen.Add(methodReference))
            {
                return;
            }

            // If we have a ref to a varargs method then we always generate an entry in the MethodRef table,
            // even if it is a method in the current module. (Note that we are not *required* to do so if 
            // in fact the number of extra arguments passed is zero; in that case we are permitted to use
            // an ordinary method def token. We consistently choose to emit a method ref regardless.)

            IUnitReference/*?*/ definingUnit = TypeHelper.GetDefiningUnitReference(methodReference.GetContainingType(Context), Context);
            if (definingUnit != null && ReferenceEquals(definingUnit, this.module) && !methodReference.AcceptsExtraArguments)
            {
                return;
            }

            this.Visit((ITypeMemberReference)methodReference);
            ISpecializedMethodReference/*?*/ specializedMethodReference = methodReference.AsSpecializedMethodReference;
            if (specializedMethodReference != null)
            {
                IMethodReference unspecializedMethodReference = specializedMethodReference.UnspecializedVersion;
                this.Visit(unspecializedMethodReference.GetType(Context));
                this.Visit(unspecializedMethodReference.GetParameters(Context));
                if (unspecializedMethodReference.ReturnValueIsModified)
                {
                    this.Visit(unspecializedMethodReference.ReturnValueCustomModifiers);
                }
            }
            else
            {
                this.Visit(methodReference.GetType(Context));
                this.Visit(methodReference.GetParameters(Context));
                if (methodReference.ReturnValueIsModified)
                {
                    this.Visit(methodReference.ReturnValueCustomModifiers);
                }
            }

            if (methodReference.AcceptsExtraArguments)
            {
                this.Visit(methodReference.ExtraParameters);
            }

            ReserveMethodToken(methodReference);
        }

        protected abstract void ReserveMethodToken(IMethodReference methodReference);

        public override abstract void Visit(IModule module);

        public override void Visit(IModuleReference moduleReference)
        {
            if (moduleReference != this.module)
            {
                RecordModuleReference(moduleReference);
            }
        }

        protected abstract void RecordModuleReference(IModuleReference moduleReference);

        public override abstract void Visit(IPlatformInvokeInformation platformInvokeInformation);

        public override void Visit(INamespaceTypeReference namespaceTypeReference)
        {
            if (!this.typeReferenceNeedsToken && namespaceTypeReference.TypeCode(Context) != PrimitiveTypeCode.NotPrimitive)
            {
                return;
            }

            RecordTypeReference(namespaceTypeReference);

            var unit = namespaceTypeReference.GetUnit(Context);

            var assemblyReference = unit as IAssemblyReference;
            if (assemblyReference != null)
            {
                this.Visit(assemblyReference);
            }
            else
            {
                var moduleReference = unit as IModuleReference;
                if (moduleReference != null)
                {
                    // If this is a module from a referenced multi-module assembly,
                    // the assembly should be used as the resolution scope. 
                    assemblyReference = moduleReference.GetContainingAssembly(Context);
                    if (assemblyReference != null && assemblyReference != this.module.AsAssembly)
                    {
                        this.Visit(assemblyReference);
                    }
                    else
                    {
                        this.Visit(moduleReference);
                    }
                }
            }
        }

        protected abstract void RecordTypeReference(ITypeReference typeReference);

        public override void Visit(INestedTypeReference nestedTypeReference)
        {
            if (!this.typeReferenceNeedsToken && nestedTypeReference.AsSpecializedNestedTypeReference != null)
            {
                return;
            }

            RecordTypeReference(nestedTypeReference);
        }

        public override void Visit(IPropertyDefinition propertyDefinition)
        {
            this.Visit(propertyDefinition.Parameters);
        }

        public override void Visit(ManagedResource resourceReference)
        {
            this.Visit(resourceReference.Attributes);

            IFileReference file = resourceReference.ExternalFile;
            if (file != null)
            {
                this.Visit(file);
            }
        }

        public override void Visit(SecurityAttribute securityAttribute)
        {
            this.Visit(securityAttribute.Attribute);
        }

        public void VisitTypeDefinitionNoMembers(ITypeDefinition typeDefinition)
        {
            this.Visit(typeDefinition.GetAttributes(Context));

            var baseType = typeDefinition.GetBaseClass(Context);
            if (baseType != null)
            {
                this.typeReferenceNeedsToken = true;
                this.Visit(baseType);
                Debug.Assert(!this.typeReferenceNeedsToken);
            }

            this.Visit(typeDefinition.GetExplicitImplementationOverrides(Context));
            if (typeDefinition.HasDeclarativeSecurity)
            {
                this.Visit(typeDefinition.SecurityAttributes);
            }

            this.VisitTypeReferencesThatNeedTokens(typeDefinition.Interfaces(Context));
            if (typeDefinition.IsGeneric)
            {
                this.Visit(typeDefinition.GenericParameters);
            }
        }

        public override void Visit(ITypeDefinition typeDefinition)
        {
            VisitTypeDefinitionNoMembers(typeDefinition);

            this.Visit(typeDefinition.Events);
            this.Visit(typeDefinition.GetFields(Context));
            this.Visit(typeDefinition.GetMethods(Context));
            this.VisitNestedTypes(typeDefinition.GetNestedTypes(Context));
            this.Visit(typeDefinition.GetProperties(Context));
        }

        public void VisitTypeReferencesThatNeedTokens(IEnumerable<ITypeReference> typeReferences)
        {
            foreach (ITypeReference typeReference in typeReferences)
            {
                VisitTypeReferencesThatNeedTokens(typeReference);
            }
        }

        private void VisitTypeReferencesThatNeedTokens(ITypeReference typeReference)
        {
            this.typeReferenceNeedsToken = true;
            this.Visit(typeReference);
            Debug.Assert(!this.typeReferenceNeedsToken);
        }

        public override void Visit(ITypeMemberReference typeMemberReference)
        {
            RecordTypeMemberReference(typeMemberReference);

            //This code was in CCI, but appears wrong to me. There is no need to visit attributes of members that are
            //being referenced, only those being defined. This code causes additional spurious typerefs and memberrefs to be
            //emitted. If the attributes can't be resolved, it causes a NullReference.
            //
            //if ((typeMemberReference.AsDefinition(Context) == null))
            //{
            //    this.Visit(typeMemberReference.GetAttributes(Context));
            //}

            this.typeReferenceNeedsToken = true;
            this.Visit(typeMemberReference.GetContainingType(Context));
            Debug.Assert(!this.typeReferenceNeedsToken);
        }

        protected abstract void RecordTypeMemberReference(ITypeMemberReference typeMemberReference);

        // Array and pointer types might cause deep recursions; visit them iteratively
        // rather than recursively.
        public override void Visit(IArrayTypeReference arrayTypeReference)
        {
            // We don't visit the current array type; it has already been visited.
            // We go straight to the element type and visit it.
            ITypeReference current = arrayTypeReference.GetElementType(Context);
            while (true)
            {
                bool mustVisitChildren = VisitTypeReference(current);
                if (!mustVisitChildren)
                {
                    return;
                }
                else if (current is IArrayTypeReference)
                {
                    // The element type is itself an array type, and we must visit *its* element type.
                    // Iterate rather than recursing.
                    current = ((IArrayTypeReference)current).GetElementType(Context);
                    continue;
                }
                else
                {
                    // The element type is not an array type and we must visit its children.
                    // Dispatch the type in order to visit its children.
                    DispatchAsReference(current);
                    return;
                }
            }
        }

        // Array and pointer types might cause deep recursions; visit them iteratively
        // rather than recursively.
        public override void Visit(IPointerTypeReference pointerTypeReference)
        {
            // We don't visit the current pointer type; it has already been visited.
            // We go straight to the target type and visit it.
            ITypeReference current = pointerTypeReference.GetTargetType(Context);
            while (true)
            {
                bool mustVisitChildren = VisitTypeReference(current);
                if (!mustVisitChildren)
                {
                    return;
                }
                else if (current is IPointerTypeReference)
                {
                    // The target type is itself an pointer type, and we must visit *its* target type.
                    // Iterate rather than recursing.
                    current = ((IPointerTypeReference)current).GetTargetType(Context);
                    continue;
                }
                else
                {
                    // The target type is not an pointer type and we must visit its children.
                    // Dispatch the type in order to visit its children.
                    DispatchAsReference(current);
                    return;
                }
            }
        }

        public override void Visit(ITypeReference typeReference)
        {
            if (VisitTypeReference(typeReference))
            {
                DispatchAsReference(typeReference);
            }
        }

        // Returns true if we need to look at the children, false otherwise.
        private bool VisitTypeReference(ITypeReference typeReference)
        {
            if (!this.alreadySeen.Add(typeReference))
            {
                if (!this.typeReferenceNeedsToken)
                {
                    return false;
                }

                this.typeReferenceNeedsToken = false;
                if (!this.alreadyHasToken.Add(typeReference))
                {
                    return false;
                }

                RecordTypeReference(typeReference);

                return false;
            }

            INestedTypeReference/*?*/ nestedTypeReference = typeReference.AsNestedTypeReference;
            if (this.typeReferenceNeedsToken || nestedTypeReference != null ||
              (typeReference.TypeCode(Context) == PrimitiveTypeCode.NotPrimitive && typeReference.AsNamespaceTypeReference != null))
            {
                ISpecializedNestedTypeReference/*?*/ specializedNestedTypeReference = (nestedTypeReference == null ? null :
                                                                                            nestedTypeReference.AsSpecializedNestedTypeReference);
                if (specializedNestedTypeReference != null)
                {
                    INestedTypeReference unspecializedNestedTypeReference = specializedNestedTypeReference.UnspecializedVersion;
                    if (this.alreadyHasToken.Add(unspecializedNestedTypeReference))
                    {
                        RecordTypeReference(unspecializedNestedTypeReference);
                    }
                }

                if (this.typeReferenceNeedsToken && this.alreadyHasToken.Add(typeReference))
                {
                    RecordTypeReference(typeReference);
                }

                if (nestedTypeReference != null)
                {
                    this.typeReferenceNeedsToken = (typeReference.AsSpecializedNestedTypeReference == null);
                    this.Visit(nestedTypeReference.GetContainingType(Context));
                }
            }

            //This code was in CCI, but appears wrong to me. There is no need to visit attributes of types that are
            //being referenced, only those being defined. This code causes additional spurious typerefs and memberrefs to be
            //emitted. If the attributes can't be resolved, it causes a NullReference.
            //
            //if ((typeReference.AsTypeDefinition(Context) == null))
            //{
            //    this.Visit(typeReference.GetAttributes(Context));
            //}

            this.typeReferenceNeedsToken = false;
            return true;
        }

        public override void Visit(IManagedPointerTypeReference managedPointerTypeReference)
        {
            Debug.Assert(false, "Unexpected ref type!");
        }
    }

    internal abstract class ReferenceIndexer : ReferenceIndexerBase
    {
        protected readonly PeWriter peWriter;

        internal ReferenceIndexer(PeWriter peWriter)
            : base(peWriter.Context)
        {
            this.peWriter = peWriter;
        }

        public override void Visit(IAssembly assembly)
        {
            this.module = assembly;
            this.Visit((IModule)assembly);
            this.Visit(assembly.GetFiles(Context));
            this.Visit(assembly.GetResources(Context));
        }

        public override void Visit(IModule module)
        {
            this.module = module;

            //EDMAURER visit these assembly-level attributes even when producing a module.
            //They'll be attached off the "AssemblyAttributesGoHere" typeRef if a module is being produced.

            this.Visit(module.AssemblyAttributes);
            this.Visit(module.AssemblySecurityAttributes);

            this.Visit(module.GetAssemblyReferences(Context));
            this.Visit(module.ModuleReferences);
            this.Visit(module.ModuleAttributes);
            this.Visit(module.GetTopLevelTypes(Context));
            this.Visit(module.GetExportedTypes(Context));

            if (module.AsAssembly == null)
            {
                this.Visit(module.GetResources(Context));
            }
        }

        public void VisitMethodBodyTypes(IModule module)
        {
            // Emulates ReferenceIndexerBase.Visit(IOperation operation)

            this.module = module;

            int count;
            foreach (IReference o in module.ReferencesInIL(out count))
            {
                ITypeReference/*?*/ typeReference = o as ITypeReference;
                if (typeReference != null)
                {
                    this.typeReferenceNeedsToken = true;
                    this.Visit(typeReference);
                    Debug.Assert(!this.typeReferenceNeedsToken);
                }
                else
                {
                    IFieldReference/*?*/ fieldReference = o as IFieldReference;
                    if (fieldReference != null)
                    {
                        if (fieldReference.IsContextualNamedEntity)
                        {
                            ((IContextualNamedEntity)fieldReference).AssociateWithPeWriter(this.peWriter);
                        }

                        this.Visit(fieldReference);
                    }
                    else
                    {
                        IMethodReference/*?*/ methodReference = o as IMethodReference;
                        if (methodReference != null)
                        {
                            this.Visit(methodReference);
                        }
                    }
                }
            }
        }

        protected override void RecordAssemblyReference(IAssemblyReference assemblyReference)
        {
            this.peWriter.GetAssemblyRefIndex(assemblyReference);
        }

        protected override void ProcessMethodBody(IMethodDefinition method)
        {
            if (method.HasBody())
            {
                var body = method.GetBody(Context);

                if (body != null)
                {
                    this.Visit(body);
                }
                else if (!peWriter.allowMissingMethodBodies)
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }
        }

        protected override void RecordTypeReference(ITypeReference typeReference)
        {
            this.peWriter.RecordTypeReference(typeReference);
        }

        protected override void RecordTypeMemberReference(ITypeMemberReference typeMemberReference)
        {
            this.peWriter.GetMemberRefIndex(typeMemberReference);
        }

        protected override void RecordFileReference(IFileReference fileReference)
        {
            this.peWriter.GetFileRefIndex(fileReference);
        }

        protected override void ReserveMethodToken(IMethodReference methodReference)
        {
            this.peWriter.GetMethodToken(methodReference);
        }

        protected override void ReserveFieldToken(IFieldReference fieldReference)
        {
            this.peWriter.GetFieldToken(fieldReference);
        }

        protected override void RecordModuleReference(IModuleReference moduleReference)
        {
            this.peWriter.GetModuleRefIndex(moduleReference.Name);
        }

        public override void Visit(IPlatformInvokeInformation platformInvokeInformation)
        {
            this.peWriter.GetModuleRefIndex(platformInvokeInformation.ModuleName);
        }
    }

    /// <summary>
    /// Visitor to force translation of all symbols that will be referred to
    /// in metadata. Allows us to build the set of types that must be embedded
    /// as local types.
    /// </summary>
    internal sealed class NoPiaReferenceIndexer : ReferenceIndexerBase
    {
        internal NoPiaReferenceIndexer(EmitContext context)
            : base(context)
        {
            this.module = context.Module;
        }

        public override void Visit(IAssembly assembly)
        {
            Debug.Assert(assembly == module);
            this.Visit((IModule)assembly);
        }

        public override void Visit(IModule module)
        {
            Debug.Assert(this.module == module);

            //EDMAURER visit these assembly-level attributes even when producing a module.
            //They'll be attached off the "AssemblyAttributesGoHere" typeRef if a module is being produced.

            this.Visit(module.AssemblyAttributes);
            this.Visit(module.AssemblySecurityAttributes);
            this.Visit(module.ModuleAttributes);
        }

        protected override void RecordAssemblyReference(IAssemblyReference assemblyReference)
        {
        }

        protected override void RecordFileReference(IFileReference fileReference)
        {
        }

        protected override void RecordModuleReference(IModuleReference moduleReference)
        {
        }

        public override void Visit(IPlatformInvokeInformation platformInvokeInformation)
        {
        }

        protected override void ProcessMethodBody(IMethodDefinition method)
        {
        }

        protected override void RecordTypeReference(ITypeReference typeReference)
        {
        }

        protected override void ReserveFieldToken(IFieldReference fieldReference)
        {
        }

        protected override void ReserveMethodToken(IMethodReference methodReference)
        {
        }

        protected override void RecordTypeMemberReference(ITypeMemberReference typeMemberReference)
        {
        }
    }
}