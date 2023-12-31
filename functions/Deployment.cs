using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Developer.LoadTesting;
using System.Text;
using github_app_auth;
using System.Net.Http.Json;

namespace DeploymentGate
{
    public class Deployment
    {
        private readonly ILogger _logger;
        private readonly LoadTestRunClient _loadtest;
        private readonly HttpClient _client;
        private readonly GitHubAuth _auth;

        public Deployment(ILoggerFactory loggerFactory, LoadTestRunClient loadtest, IHttpClientFactory clientFactory, GitHubAuth auth)
        {
            _logger = loggerFactory.CreateLogger<Deployment>();
            _loadtest = loadtest;
            _client = clientFactory.CreateClient();
            _auth = auth;
        }

        [Function("deployment")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try {
                var payload = await req.ReadFromJsonAsync<Payload>();

                _logger.LogInformation($"Deployment {payload.deployment.id} to {payload.deployment.environment} started");

                var testRun = await FetchLoadTest();
                if (testRun.testResult == PFTestResult.PASSED || testRun.testResult == PFTestResult.NOT_APPLICABLE)
                {
                    _logger.LogInformation("Load test passed");
                    await PostDeploymentStatus(payload, "approved");    
                    
                }
                else
                {
                    _logger.LogInformation("Load test failed");
                    await PostDeploymentStatus(payload, "rejected");
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deployment");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                return response;
            }
        }

        // The deployment payload doesn't offer the context needed to find the specific load test results for this deployment.
        // So we just grab the most recent run from the test and validate that it passed.
        private async Task<TestRun> FetchLoadTest() 
        {
            try
            {
                TestRun testRun = null;
                // Get the most recent load test run
                await foreach (var t in _loadtest.GetTestRunsAsync(orderby: "executedDateTime asc", executionFrom: DateTime.Now.AddMinutes(-30)))
                {
                    testRun = t.ToObjectFromJson<TestRun>();
                    break;
                }

                if (testRun == null)
                {
                    _logger.LogInformation("No load test runs found");
                }

                return testRun;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing load test");
                return null;
            }

        }

        private async Task PostDeploymentStatus(Payload payload, string status) 
        {
            var token = await _auth.FetchInstallationToken(payload.installation.id.ToString());
            var content = JsonContent.Create(new { environment_name = payload.deployment.environment, state = status, comment = "load test passed" });

            using (_client)
            {
                _client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                _client.DefaultRequestHeaders.Add("User-Agent", "Azure-Load-Test-Deployment-Gate");
                _client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                _client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                var response = await _client.PostAsync(payload.deployment_callback_url, content);
                if (response.StatusCode != HttpStatusCode.NoContent)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    var req = await response.RequestMessage.Content.ReadAsStringAsync();
                    _logger.LogError($"{req} \n\n {body}");
                }
                else 
                {
                    _logger.LogInformation($"Deployment {payload.deployment.id} status set to {status}:  {response.StatusCode}");
                }

            }
        }
    }
}
