using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MyMES.EventBus;
using MyMES.PluginSystem;
using Shouldly;

namespace MyMES.UnitTests.Platform;

/// <summary>
/// The three extension mechanisms, tested as behaviour rather than described in documentation.
/// If these break, every plugin written against them breaks with them.
/// </summary>
public sealed class ExtensibilityTests
{
    [Fact]
    public void Replace_substitutes_the_registered_implementation()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, DefaultGreeter>();

        services.ReplaceService<IGreeter, CustomerGreeter>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGreeter>().Greet().ShouldBe("customer");
    }

    [Fact]
    public void Decorate_wraps_the_existing_implementation_without_losing_it()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, DefaultGreeter>();

        services.DecorateService<IGreeter, ShoutingGreeter>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGreeter>().Greet().ShouldBe("DEFAULT");
    }

    [Fact]
    public void Decorating_nothing_fails_with_an_explanation_rather_than_silently()
    {
        var services = new ServiceCollection();

        var ex = Should.Throw<InvalidOperationException>(
            () => services.DecorateService<IGreeter, ShoutingGreeter>());

        ex.Message.ShouldContain("registration order");
    }

    [Fact]
    public void Extend_adds_an_implementation_beside_the_existing_ones()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, DefaultGreeter>();

        services.ExtendWith<IGreeter, CustomerGreeter>();

        using var provider = services.BuildServiceProvider();
        var all = provider.GetServices<IGreeter>().Select(g => g.Greet()).ToList();

        all.ShouldContain("default");
        all.ShouldContain("customer");
    }

    [Fact]
    public async Task The_event_bus_reaches_every_registered_handler()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services.AddInProcessEventBus();
        services.AddScoped<IEventHandler<ThingHappened>, FirstHandler>();
        services.AddScoped<IEventHandler<ThingHappened>, SecondHandler>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IEventBus>()
            .PublishAsync(new ThingHappened("x"));

        provider.GetRequiredService<Recorder>().Seen.ShouldBe(["first", "second"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_failing_handler_does_not_stop_the_others()
    {
        // One module's bug must not stop another module from reacting: the publisher has usually
        // already committed its work by the time handlers run.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Recorder>();
        services.AddInProcessEventBus();
        services.AddScoped<IEventHandler<ThingHappened>, ThrowingHandler>();
        services.AddScoped<IEventHandler<ThingHappened>, SecondHandler>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IEventBus>()
            .PublishAsync(new ThingHappened("x"));

        provider.GetRequiredService<Recorder>().Seen.ShouldContain("second");
    }

    [Fact]
    public async Task Publishing_an_event_with_no_handler_is_not_an_error()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInProcessEventBus();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        await Should.NotThrowAsync(() => scope.ServiceProvider.GetRequiredService<IEventBus>()
            .PublishAsync(new ThingHappened("x")));
    }

    // -- Test doubles ---------------------------------------------------------

    public interface IGreeter
    {
        string Greet();
    }

    private sealed class DefaultGreeter : IGreeter
    {
        public string Greet() => "default";
    }

    private sealed class CustomerGreeter : IGreeter
    {
        public string Greet() => "customer";
    }

    private sealed class ShoutingGreeter(IGreeter inner) : IGreeter
    {
        public string Greet() => inner.Greet().ToUpperInvariant();
    }

    public sealed record ThingHappened(string Name);

    public sealed class Recorder
    {
        private readonly List<string> _seen = [];
        private readonly Lock _gate = new();

        public IReadOnlyList<string> Seen
        {
            get
            {
                lock (_gate)
                {
                    return [.. _seen];
                }
            }
        }

        public void Record(string name)
        {
            lock (_gate)
            {
                _seen.Add(name);
            }
        }
    }

    private sealed class FirstHandler(Recorder recorder) : IEventHandler<ThingHappened>
    {
        public Task HandleAsync(ThingHappened message, CancellationToken cancellationToken = default)
        {
            recorder.Record("first");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondHandler(Recorder recorder) : IEventHandler<ThingHappened>
    {
        public Task HandleAsync(ThingHappened message, CancellationToken cancellationToken = default)
        {
            recorder.Record("second");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IEventHandler<ThingHappened>
    {
        public Task HandleAsync(ThingHappened message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("handler is broken");
    }
}
