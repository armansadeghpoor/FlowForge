namespace FlowForge.Api.Tests.Deployment;

public sealed class DeploymentPackagingTests
{
    [Fact]
    public void Dockerfile_IsPresentAndUsesBuildAndRuntimeImages()
    {
        var dockerfile = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "FlowForge.Api",
            "Dockerfile");

        Assert.True(File.Exists(dockerfile), $"Missing Dockerfile: {dockerfile}");

        var contents = File.ReadAllText(dockerfile);
        Assert.Contains("mcr.microsoft.com/dotnet/sdk:10.0 AS build", contents);
        Assert.Contains("mcr.microsoft.com/dotnet/aspnet:10.0 AS final", contents);
        Assert.Contains("dotnet publish", contents);
        Assert.Contains("ENTRYPOINT [\"dotnet\", \"FlowForge.Api.dll\"]", contents);
    }

    [Fact]
    public void DockerIgnore_IsPresentAndExcludesBuildAndTestArtifacts()
    {
        var dockerIgnore = Path.Combine(FindRepositoryRoot(), ".dockerignore");

        Assert.True(File.Exists(dockerIgnore), $"Missing Docker ignore file: {dockerIgnore}");

        var contents = File.ReadAllText(dockerIgnore);
        Assert.Contains("**/bin/", contents);
        Assert.Contains("**/obj/", contents);
        Assert.Contains(".git/", contents);
        Assert.Contains("**/TestResults/", contents);
    }

    [Fact]
    public void DeploymentDocumentation_IsPresent()
    {
        var documentation = GetDeploymentDocumentationPath();

        Assert.True(
            File.Exists(documentation),
            $"Missing deployment documentation: {documentation}");
    }

    [Fact]
    public void DeploymentDocumentation_ListsRequiredConfigurationAndHealthEndpoint()
    {
        var contents = File.ReadAllText(GetDeploymentDocumentationPath());

        Assert.Contains("ASPNETCORE_ENVIRONMENT", contents);
        Assert.Contains("FlowForge__Database__ConnectionString", contents);
        Assert.Contains("FlowForge__Security__RequireAuthentication", contents);
        Assert.Contains("FlowForge__Security__Authority", contents);
        Assert.Contains("FlowForge__Security__Audience", contents);
        Assert.Contains("GET /health", contents);
    }

    private static string GetDeploymentDocumentationPath() => Path.Combine(
        FindRepositoryRoot(),
        "docs",
        "operations",
        "deployment-configuration.md");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FlowForge.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the FlowForge repository root.");
    }
}
