using System;
using Microsoft.Extensions.DependencyInjection;

namespace CSteroid.Medium
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

    /// <summary>Sets the publication order of a message handler. Lower values run first.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MessageHandlerAttribute : Attribute
    {
        public int Order { get; set; }
    }

    /// <summary>Marks the assembly that owns generated mediator dispatch and registration.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class MediumCompositionRootAttribute : Attribute
    {
    }

    /// <summary>Declares a pipeline behavior and its per-message selection configuration.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class MediumBehaviorAttribute : Attribute
    {
        public MediumBehaviorAttribute(Type behaviorType)
        {
            BehaviorType = behaviorType;
        }

        public Type BehaviorType { get; }

        public int Order { get; set; }

        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;
    }

    /// <summary>Exports a concrete handler type to referencing composition roots.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class MediumManifestAttribute : Attribute
    {
        public MediumManifestAttribute(int version, Type handlerType)
        {
            Version = version;
            HandlerType = handlerType;
        }

        public int Version { get; }

        public Type HandlerType { get; }
    }
}
