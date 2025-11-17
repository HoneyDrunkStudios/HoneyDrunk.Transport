using HoneyDrunk.Kernel.DI;
using HoneyDrunk.Transport.Abstractions;
using HoneyDrunk.Transport.Configuration;
using HoneyDrunk.Transport.Context;
using HoneyDrunk.Transport.Pipeline;
using HoneyDrunk.Transport.Pipeline.Middleware;
using HoneyDrunk.Transport.Primitives;
using HoneyDrunk.Transport.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Transport.DependencyInjection;

/// <summary>
/// Extension methods for registering transport services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the HoneyDrunk Transport core services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>A transport builder for fluent configuration.</returns>
    public static ITransportBuilder AddHoneyDrunkTransportCore(
        this IServiceCollection services,
        Action<TransportCoreOptions>? configure = null)
    {
        services.AddKernelDefaults();
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<TransportCoreOptions>(_ => { });
        }

        services.TryAddSingleton<IKernelContextFactory, KernelContextFactory>();
        services.TryAddSingleton<EnvelopeFactory>();
        services.TryAddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.TryAddSingleton<IMessagePipeline, MessagePipeline>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessageMiddleware, CorrelationMiddleware>());

        services.AddSingleton<IMessageMiddleware>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TransportCoreOptions>>().Value;
            return options.EnableTelemetry ? new TelemetryMiddleware() : new NoOpMiddleware();
        });

        services.AddSingleton<IMessageMiddleware>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<TransportCoreOptions>>().Value;
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LoggingMiddleware>>();
            return options.EnableLogging ? new LoggingMiddleware(logger) : new NoOpMiddleware();
        });

        // Register default error handling strategy if none supplied
        services.TryAddSingleton<IErrorHandlingStrategy, DefaultErrorHandlingStrategy>();

        return new TransportBuilder(services);
    }

    /// <summary>
    /// Registers a message handler.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(this IServiceCollection services)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddScoped<IMessageHandler<TMessage>, THandler>();
        return services;
    }

    /// <summary>
    /// Registers a message handler using a delegate.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="handler">The handler delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageHandler<TMessage>(
        this IServiceCollection services,
        MessageHandler<TMessage> handler)
        where TMessage : class
    {
        services.AddScoped<IMessageHandler<TMessage>>(sp => new DelegateMessageHandler<TMessage>(handler));
        return services;
    }

    /// <summary>
    /// Adds custom middleware to the pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageMiddleware<TMiddleware>(this IServiceCollection services)
        where TMiddleware : class, IMessageMiddleware
    {
        services.AddSingleton<IMessageMiddleware, TMiddleware>();
        return services;
    }

    /// <summary>
    /// Adds middleware using a delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="middleware">The middleware delegate.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessageMiddleware(
        this IServiceCollection services,
        MessageMiddleware middleware)
    {
        services.AddSingleton<IMessageMiddleware>(new DelegateMessageMiddleware(middleware));
        return services;
    }
}
