using FlowForge.Abstractions.Configuration;
using FlowForge.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FlowForge.Api.Tests.Configuration;

public sealed class ConfigurationTests
{
    [Fact]
    public void OptionsExposeSafeDefaults()
    {
        var runtime = new RuntimeOptions();
        var database = new DatabaseOptions();
        var execution = new ExecutionOptions();

        Assert.Equal("flowforge-runtime", runtime.OwnerId);
        Assert.Equal(TimeSpan.FromSeconds(30), runtime.HeartbeatInterval);
        Assert.Equal(TimeSpan.FromMinutes(2), runtime.StaleExecutionThreshold);
        Assert.Equal(string.Empty, database.ConnectionString);
        Assert.Equal(TimeSpan.FromSeconds(30), execution.DefaultNodeTimeout);
        Assert.Equal(1, execution.MaxNodeAttempts);
    }

    [Fact]
    public void EnvironmentVariablesOverrideConfiguredValues()
    {
        var prefix = $"FLOWFORGE_TEST_{Guid.NewGuid():N}_";
        var variableName = $"{prefix}FlowForge__Runtime__OwnerId";
        const string expectedOwnerId = "runtime-from-environment";

        Environment.SetEnvironmentVariable(variableName, expectedOwnerId);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix)
                .Build();
            var services = new ServiceCollection();
            services.AddFlowForgeConfiguration(configuration);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RuntimeOptions>>().Value;

            Assert.Equal(expectedOwnerId, options.OwnerId);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    public async Task MissingDatabaseConnectionIsRejectedDuringStartup()
    {
        using var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(configuration =>
            {
                configuration.Sources.Clear();
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{DatabaseOptions.SectionName}:ConnectionString"] = string.Empty
                });
            })
            .ConfigureServices((context, services) =>
                services.AddFlowForgeConfiguration(context.Configuration))
            .Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(CancellationToken.None));

        Assert.Contains(
            $"{DatabaseOptions.SectionName}:ConnectionString is required.",
            exception.Failures);
    }
}
