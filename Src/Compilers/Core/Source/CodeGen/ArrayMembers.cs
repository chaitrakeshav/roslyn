﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Roslyn.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using EmitContext = Microsoft.CodeAnalysis.Emit.EmitContext;


// Contains support for pseudo-methods on multidimensional arrays.
//
// Opcodes such as newarr, ldelem, ldelema, stelem do not work with
// multidimensional arrays and same functionality is available in
// a form of well known pseudo-methods "Get", "Set", "Address" and ".ctor"
//
//=========================
//
//  14.2 Arrays  (From partition II) -
//The class that the VES creates for arrays contains several methods whose implementation is supplied by the
//VES:
//
//* A constructor that takes a sequence of int32 arguments, one for each dimension of the array, that specify
//the number of elements in each dimension beginning with the first dimension. A lower bound of zero is
//assumed.
//
//* A constructor that takes twice as many int32 arguments as there are dimensions of the array. These
//arguments occur in pairs—one pair per dimension—with the first argument of each pair specifying the
//lower bound for that dimension, and the second argument specifying the total number of elements in that
//dimension. Note that vectors are not created with this constructor, since a zero lower bound is assumed for
//vectors.
//
//* A Get method that takes a sequence of int32 arguments, one for each dimension of the array, and returns
//a value whose type is the element type of the array. This method is used to access a specific element of the
//array where the arguments specify the index into each dimension, beginning with the first, of the element
//to be returned.
//
//* A Set method that takes a sequence of int32 arguments, one for each dimension of the array, followed by
//a value whose type is the element type of the array. The return type of Set is void. This method is used to
//set a specific element of the array where the arguments specify the index into each dimension, beginning
//with the first, of the element to be set and the final argument specifies the value to be stored into the target
//element.
//
//* An Address method that takes a sequence of int32 arguments, one for each dimension of the array, and
//has a return type that is a managed pointer to the array‘s element type. This method is used to return a
//managed pointer to a specific element of the array where the arguments specify the index into each
//dimension, beginning with the first, of the element whose address is to be returned.

namespace Microsoft.CodeAnalysis.CodeGen
{
    /// <summary>
    /// Constructs and caches already created pseudo-methods.
    /// Every compiled module is supposed to have one of this, created lazily 
    /// (multidimensional arrays are not common).
    /// </summary>
    internal class ArrayMethods
    {
        // There are four kinds of array pseudo-methods
        // They are specific to a given array type
        private enum ArrayMethodKind : byte
        {
            GET,
            SET,
            ADDRESS,
            CTOR,
        }

        /// <summary>
        /// Acquires an array constructor for a given array type
        /// </summary>
        public ArrayMethod GetArrayConstructor(Cci.IArrayTypeReference arrayType)
        {
            return GetArrayMethod(arrayType, ArrayMethodKind.CTOR);
        }

        /// <summary>
        /// Acquires an element getter method for a given array type
        /// </summary>
        public ArrayMethod GetArrayGet(Cci.IArrayTypeReference arrayType)
        {
            return GetArrayMethod(arrayType, ArrayMethodKind.GET);
        }

        /// <summary>
        /// Acquires an element setter method for a given array type
        /// </summary>
        public ArrayMethod GetArraySet(Cci.IArrayTypeReference arrayType)
        {
            return GetArrayMethod(arrayType, ArrayMethodKind.SET);
        }

        /// <summary>
        /// Acquires an element referencer method for a given array type
        /// </summary>
        public ArrayMethod GetArrayAddress(Cci.IArrayTypeReference arrayType)
        {
            return GetArrayMethod(arrayType, ArrayMethodKind.ADDRESS);
        }

        /// <summary>
        /// Maps {array type, method kind} tuples to implementing pseudo-methods.
        /// </summary>
        private readonly ConcurrentDictionary<ValueTuple<byte, Cci.IArrayTypeReference>, ArrayMethod> dict =
            new ConcurrentDictionary<ValueTuple<byte, Cci.IArrayTypeReference>, ArrayMethod>();

        /// <summary>
        /// lazily fetches or creates a new array method.
        /// </summary>
        private ArrayMethod GetArrayMethod(Cci.IArrayTypeReference arrayType, ArrayMethodKind id)
        {
            var key = ValueTuple.Create((byte)id, arrayType);
            ArrayMethod result;

            var dict = this.dict;
            if (!dict.TryGetValue(key, out result))
            {
                result = MakeArrayMethod(arrayType, id);
                result = dict.GetOrAdd(key, result);
            }

            return result;
        }

        private static ArrayMethod MakeArrayMethod(Cci.IArrayTypeReference arrayType, ArrayMethodKind id)
        {
            switch (id)
            {
                case ArrayMethodKind.CTOR:
                    return new ArrayConstructor(arrayType);

                case ArrayMethodKind.GET:
                    return new ArrayGet(arrayType);

                case ArrayMethodKind.SET:
                    return new ArraySet(arrayType);

                case ArrayMethodKind.ADDRESS:
                    return new ArrayAddress(arrayType);
            }

            throw ExceptionUtilities.UnexpectedValue(id);
        }


        /// <summary>
        /// "newobj ArrayConstructor"  is equivalent of "newarr ElementType" 
        /// when working with multidimentsional arrays
        /// </summary>
        private sealed class ArrayConstructor : ArrayMethod
        {
            public ArrayConstructor(Cci.IArrayTypeReference arrayType) : base(arrayType) { }

            public override string Name
            {
                get { return ".ctor"; }
            }

            public override Cci.ITypeReference GetType(EmitContext context)
            {
                return context.Module.GetPlatformType(Cci.PlatformType.SystemVoid, context);
            }
        }

        /// <summary>
        /// "call ArrayGet"  is equivalent of "ldelem ElementType" 
        /// when working with multidimentsional arrays
        /// </summary>
        private sealed class ArrayGet : ArrayMethod
        {
            public ArrayGet(Cci.IArrayTypeReference arrayType) : base(arrayType) { }

            public override string Name
            {
                get { return "Get"; }
            }

            public override Cci.ITypeReference GetType(EmitContext context)
            {
                return arrayType.GetElementType(context);
            }
        }

        /// <summary>
        /// "call ArrayAddress"  is equivalent of "ldelema ElementType" 
        /// when working with multidimentsional arrays
        /// </summary>
        private sealed class ArrayAddress : ArrayMethod
        {
            public ArrayAddress(Cci.IArrayTypeReference arrayType) : base(arrayType) { }

            public override bool ReturnValueIsByRef
            {
                get { return true; }
            }

            public override Cci.ITypeReference GetType(EmitContext context)
            {
                return arrayType.GetElementType(context);
            }

            public override string Name
            {
                get { return "Address"; }
            }
        }

        /// <summary>
        /// "call ArraySet"  is equivalent of "stelem ElementType" 
        /// when working with multidimentsional arrays
        /// </summary>
        private sealed class ArraySet : ArrayMethod
        {
            public ArraySet(Cci.IArrayTypeReference arrayType) : base(arrayType) { }

            public override string Name
            {
                get { return "Set"; }
            }

            public override Cci.ITypeReference GetType(EmitContext context)
            {
                return context.Module.GetPlatformType(Cci.PlatformType.SystemVoid, context);
            }

            protected override ImmutableArray<ArrayMethodParameterInfo> MakeParameters()
            {
                int rank = (int)arrayType.Rank;
                var parameters = ArrayBuilder<ArrayMethodParameterInfo>.GetInstance(rank + 1);

                for (int i = 0; i < rank; i++)
                {
                    parameters.Add(ArrayMethodParameterInfo.GetIndexParameter((ushort)i));
                }

                parameters.Add(new ArraySetValueParameterInfo((ushort)rank, arrayType));
                return parameters.ToImmutableAndFree();
           }
        }
    }

    /// <summary>
    /// Represents a parameter in an array pseudo-method.
    /// 
    /// NOTE: It appears that that only number of indeces is used for verification, 
    /// types just have to be Int32.
    /// Even though actual arguments can be native ints.
    /// </summary>
    internal class ArrayMethodParameterInfo : Cci.IParameterTypeInformation
    {
        // position in the signature
        private readonly ushort index;

        // cache common parameter instances 
        // (we can do this since the only data we have is the index)
        private static readonly ArrayMethodParameterInfo Index0 = new ArrayMethodParameterInfo(0);
        private static readonly ArrayMethodParameterInfo Index1 = new ArrayMethodParameterInfo(1);
        private static readonly ArrayMethodParameterInfo Index2 = new ArrayMethodParameterInfo(2);
        private static readonly ArrayMethodParameterInfo Index3 = new ArrayMethodParameterInfo(3);

        protected ArrayMethodParameterInfo(ushort index)
        {
            this.index = index;
        }

        public static ArrayMethodParameterInfo GetIndexParameter(ushort index)
        {
            switch (index)
            {
                case 0: return Index0;
                case 1: return Index1;
                case 2: return Index2;
                case 3: return Index3;
            }

            return new ArrayMethodParameterInfo(index);
        }

        public IEnumerable<Cci.ICustomModifier> CustomModifiers
        {
            get { return SpecializedCollections.EmptyEnumerable<Cci.ICustomModifier>(); }
        }

        public bool IsByReference
        {
            get { return false; }
        }

        public bool IsModified
        {
            get { return false; }
        }

        public bool HasByRefBeforeCustomModifiers
        {
            get { return false; }
        }

        public virtual Cci.ITypeReference GetType(EmitContext context)
        {
            return context.Module.GetPlatformType(Cci.PlatformType.SystemInt32, context);
        }

        public ushort Index
        {
            get { return index; }
        }
    }

    /// <summary>
    /// Represents the "value" parameter of the Set pseudo-method.
    /// 
    /// NOTE: unlike index parameters, type of the value parameter must match 
    /// the actual element type.
    /// </summary>
    internal sealed class ArraySetValueParameterInfo : ArrayMethodParameterInfo
    {
        private readonly Cci.IArrayTypeReference arrayType;

        internal ArraySetValueParameterInfo(ushort index, Cci.IArrayTypeReference arrayType)
            : base(index)
        {
            this.arrayType = arrayType;
        }

        public override Cci.ITypeReference GetType(EmitContext context)
        {
            return arrayType.GetElementType(context);
        }
    }

    /// <summary>
    /// Base of all array methods. They have a lot in common.
    /// </summary>
    internal abstract class ArrayMethod : Cci.IMethodReference
    {
        private readonly ImmutableArray<ArrayMethodParameterInfo> parameters;
        protected readonly Cci.IArrayTypeReference arrayType;

        protected ArrayMethod(Cci.IArrayTypeReference arrayType)
        {
            this.arrayType = arrayType;
            this.parameters = MakeParameters();
        }

        public abstract string Name { get; }
        public abstract Cci.ITypeReference GetType(EmitContext context);

        // Address overrides this to "true"
        public virtual bool ReturnValueIsByRef
        {
            get { return false; }
        }

        // Set overrides this to include "value" parameter.
        protected virtual ImmutableArray<ArrayMethodParameterInfo> MakeParameters()
        {
            int rank = (int)arrayType.Rank;
            var parameters = ArrayBuilder<ArrayMethodParameterInfo>.GetInstance(rank);

            for (int i = 0; i < rank; i++)
            {
                parameters.Add(ArrayMethodParameterInfo.GetIndexParameter((ushort)i));
            }

            return parameters.ToImmutableAndFree();
        }

        public ImmutableArray<Cci.IParameterTypeInformation> GetParameters(EmitContext context)
        {
            return StaticCast<Cci.IParameterTypeInformation>.From(parameters);
        }

        public bool AcceptsExtraArguments
        {
            get { return false; }
        }

        public ushort GenericParameterCount
        {
            get { return 0; }
        }

        public bool IsGeneric
        {
            get { return false; }
        }

        public Cci.IMethodDefinition GetResolvedMethod(EmitContext context)
        {
            return null;
        }

        public ImmutableArray<Cci.IParameterTypeInformation> ExtraParameters
        {
            get { return ImmutableArray<Cci.IParameterTypeInformation>.Empty; }
        }

        public Cci.IGenericMethodInstanceReference AsGenericMethodInstanceReference
        {
            get { return null; }
        }

        public Cci.ISpecializedMethodReference AsSpecializedMethodReference
        {
            get { return null; }
        }

        public Cci.CallingConvention CallingConvention
        {
            get { return Cci.CallingConvention.HasThis; }
        }

        public ushort ParameterCount
        {
            get { return (ushort)parameters.Length; }
        }

        public IEnumerable<Cci.ICustomModifier> ReturnValueCustomModifiers
        {
            get { return SpecializedCollections.EmptyEnumerable<Cci.ICustomModifier>(); }
        }

        public bool ReturnValueIsModified
        {
            get { return false; }
        }

        public Cci.ITypeReference GetContainingType(EmitContext context)
        {
            // We are not translating arrayType. 
            // It is an array type and it is never generic or contained in a generic.
            return this.arrayType;
        }

        public IEnumerable<Cci.ICustomAttribute> GetAttributes(EmitContext context)
        {
            return SpecializedCollections.EmptyEnumerable<Cci.ICustomAttribute>();
        }

        public void Dispatch(Cci.MetadataVisitor visitor)
        {
            visitor.Visit((Cci.IMethodReference)this);
        }

        public Cci.IDefinition AsDefinition(EmitContext context)
        {
            return null;
        }

        public override string ToString()
        {
            return arrayType.ToString() + "." + Name;
        }
    }
}
