using System;
using System.Collections.Generic;
using HotChocolate.Internal;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Utilities;
using static HotChocolate.Utilities.ThrowHelper;

#nullable enable

namespace HotChocolate.Configuration;

internal sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly ServiceFactory _serviceFactory = new();
    private readonly HashSet<ITypeReference> _unresolved = new();
    private readonly HashSet<RegisteredType> _handled = new();
    private readonly TypeRegistry _typeRegistry;
    private readonly TypeLookup _typeLookup;
    private readonly IDescriptorContext _context;
    private readonly TypeInterceptor _interceptor;

    public TypeRegistrar(
        IDescriptorContext context,
        TypeRegistry typeRegistry,
        TypeLookup typeLookup,
        TypeInterceptor typeInterceptor)
    {
        _context = context ??
            throw new ArgumentNullException(nameof(context));
        _typeRegistry = typeRegistry ??
            throw new ArgumentNullException(nameof(typeRegistry));
        _typeLookup = typeLookup ??
            throw new ArgumentNullException(nameof(typeLookup));
        _interceptor = typeInterceptor ??
            throw new ArgumentNullException(nameof(typeInterceptor));
        _serviceFactory.Services = context.Services;
    }

    public void Register(
        TypeSystemObjectBase obj,
        string? scope,
        bool inferred = false,
        Action<RegisteredType>? configure = null)
    {
        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        var registeredType = InitializeType(obj, scope, inferred);

        configure?.Invoke(registeredType);

        if (registeredType.References.Count > 0)
        {
            RegisterTypeAndResolveReferences(registeredType);

            if (obj is IHasRuntimeType hasRuntimeType
                && hasRuntimeType.RuntimeType != typeof(object))
            {
                var runtimeTypeRef =
                    _context.TypeInspector.GetTypeRef(
                        hasRuntimeType.RuntimeType,
                        SchemaTypeReference.InferTypeContext(obj),
                        scope);

                var explicitBind = obj is ScalarType { Bind: BindingBehavior.Explicit };

                if (!explicitBind)
                {
                    MarkResolved(runtimeTypeRef);
                    _typeRegistry.TryRegister(runtimeTypeRef, registeredType.References[0]);
                }
            }
        }
    }

    private void RegisterTypeAndResolveReferences(RegisteredType registeredType)
    {
        _typeRegistry.Register(registeredType);

        foreach (var typeReference in registeredType.References)
        {
            MarkResolved(typeReference);
        }
    }

    public void MarkUnresolved(ITypeReference typeReference)
    {
        if (typeReference is null)
        {
            throw new ArgumentNullException(nameof(typeReference));
        }

        _unresolved.Add(typeReference);
    }

    public void MarkResolved(ITypeReference typeReference)
    {
        if (typeReference is null)
        {
            throw new ArgumentNullException(nameof(typeReference));
        }

        _unresolved.Remove(typeReference);
    }

    public bool IsResolved(ITypeReference typeReference)
    {
        if (typeReference is null)
        {
            throw new ArgumentNullException(nameof(typeReference));
        }

        return _typeRegistry.IsRegistered(typeReference);
    }

    public TypeSystemObjectBase CreateInstance(Type namedSchemaType)
    {
        try
        {
            return (TypeSystemObjectBase)_serviceFactory.CreateInstance(namedSchemaType)!;
        }
        catch (Exception ex)
        {
            throw TypeRegistrar_CreateInstanceFailed(namedSchemaType, ex);
        }
    }

    public IReadOnlyCollection<ITypeReference> Unresolved => _unresolved;

    public IReadOnlyCollection<ITypeReference> GetUnhandled()
    {
        // we are having a list and the hashset here to keep the order.
        var unhandled = new List<ITypeReference>();
        var registered = new HashSet<ITypeReference>();

        foreach (var type in _typeRegistry.Types)
        {
            if (_handled.Add(type))
            {
                foreach (var typeDep in type.Dependencies)
                {
                    if (registered.Add(typeDep.Type))
                    {
                        unhandled.Add(typeDep.Type);
                    }
                }
            }
        }

        return unhandled;
    }

    private RegisteredType InitializeType(
        TypeSystemObjectBase typeSystemObject,
        string? scope,
        bool isInferred)
    {
        try
        {
            // first we create a reference to this type-system-object and ensure that we have
            // not already registered it.
            TypeReference instanceRef = TypeReference.Create(typeSystemObject, scope);

            if (_typeRegistry.TryGetType(instanceRef, out var registeredType))
            {
                // if we already no this object we will short-circuit here and just return the
                // already registered instance.
                return registeredType;
            }

            registeredType = new RegisteredType(
                typeSystemObject,
                isInferred,
                _typeRegistry,
                _typeLookup,
                _context,
                _interceptor,
                scope);

            // if the type-system-object is not yet pre-initialized we will start the
            // standard initialization flow.
            if (!typeSystemObject.IsInitialized)
            {
                typeSystemObject.Initialize(registeredType);
            }

            if (!isInferred)
            {
                registeredType.References.TryAdd(instanceRef);
            }

            if (!ExtendedType.Tools.IsNonGenericBaseType(typeSystemObject.GetType()))
            {
                registeredType.References.TryAdd(
                    _context.TypeInspector.GetTypeRef(
                        typeSystemObject.GetType(),
                        SchemaTypeReference.InferTypeContext(typeSystemObject),
                        scope));
            }

            if (typeSystemObject is IHasTypeIdentity hasTypeIdentity &&
                hasTypeIdentity.TypeIdentity is not null)
            {
                var reference =
                    _context.TypeInspector.GetTypeRef(
                        hasTypeIdentity.TypeIdentity,
                        SchemaTypeReference.InferTypeContext(typeSystemObject),
                        scope);

                registeredType.References.TryAdd(reference);
            }

            if (_interceptor.TryCreateScope(
                registeredType,
                out var dependencies))
            {
                registeredType.Dependencies.Clear();
                registeredType.Dependencies.AddRange(dependencies);
            }

            return registeredType;
        }
        catch (Exception ex)
        {
            throw new SchemaException(
                SchemaErrorBuilder.New()
                    .SetMessage(ex.Message)
                    .SetException(ex)
                    .SetTypeSystemObject(typeSystemObject)
                    .Build());
        }
    }
}
