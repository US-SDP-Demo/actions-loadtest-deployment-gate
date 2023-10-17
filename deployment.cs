using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace actions_loadtest_deployment_gate
{
    public class deployment
    {
        private readonly ILogger _logger;

        public deployment(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<deployment>();
        }

        [Function("deployment")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            if (req.Method == "POST")
            {
                var body = req.ReadAsString(System.Text.Encoding.UTF8);
                _logger.LogInformation(body);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }
    }
}
