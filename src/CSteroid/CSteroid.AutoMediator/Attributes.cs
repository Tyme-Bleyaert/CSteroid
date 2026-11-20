using System;
using Microsoft.Extensions.DependencyInjection;

namespace CSteroid.AutoMediator
{
    /// <summary>Sets the DI lifetime of a handler class. Applied to the handler class.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class HandlerLifetimeAttribute : Attribute
    {
        public HandlerLifetimeAttribute(ServiceLifetime lifetime)
        {
            Lifetime = lifetime;
        }

        public ServiceLifetime Lifetime { get; }
    }

    /// <summary>Marks the assembly that owns generated mediator dispatch and registration.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class AutoMediatorCompositionRootAttribute : Attribute
    {
    }

    /// <summary>Declares a pipeline behavior and its per-message selection configuration.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class AutoMediatorBehaviorAttribute : Attribute
    {
        public AutoMediatorBehaviorAttribute(Type behaviorType)
        {
            BehaviorType = behaviorType;
        }

        public Type BehaviorType { get; }

        public int Order { get; set; }

        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;
    }

    /// <summary>Exports a concrete handler type to referencing composition roots.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class AutoMediatorManifestAttribute : Attribute
    {
        public AutoMediatorManifestAttribute(int version, Type handlerType)
        {
            Version = version;
            HandlerType = handlerType;
        }

        public int Version { get; }

        public Type HandlerType { get; }
    }
}
