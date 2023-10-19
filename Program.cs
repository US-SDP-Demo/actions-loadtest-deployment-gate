using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Developer.LoadTesting;
using Azure.Identity;
using github_app_auth;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddSingleton<LoadTestRunClient>((sp) => {
            var uri = new Uri(Environment.GetEnvironmentVariable("LOADTEST_DATAPLANE_URI"));
            return new LoadTestRunClient(uri, new DefaultAzureCredential());
        });
        services.AddSingleton<GitHubAuth>();
    })
    .Build();

host.Run();
