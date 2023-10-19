using System;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Net;
using System.Security.Cryptography;
using System.Net.Http.Json;
using Octokit;

namespace github_app_auth
{
    public class GitHubAuth
    {
        private readonly ILogger _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GitHubClient _ghclient;

        public GitHubAuth(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, GitHubClient ghclient)
        {
            _logger = loggerFactory.CreateLogger<GitHubAuth>();
            _httpClientFactory = httpClientFactory;
            _ghclient = ghclient;
        }

        private string GenerateJwtToken(string installationId)
        {
            try {            
                var rawkey = Environment.GetEnvironmentVariable("APP_PRIVATE_KEY");
                var rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(rawkey), out _);
                var key = new RsaSecurityKey(rsa);
                var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

                var payload = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor()
                {
                    Claims  = new Dictionary<string, object>()
                    {
                        { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                        { "exp", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() },
                        { "iss", Environment.GetEnvironmentVariable("APP_ID") },
                        { "sub", installationId }
                    },
                    Issuer = Environment.GetEnvironmentVariable("APP_ID"),
                    Audience = null,
                    Expires = DateTimeOffset.UtcNow.AddMinutes(10).DateTime,
                    SigningCredentials = creds
                };

                var token = new JsonWebTokenHandler().CreateToken(payload);

                return token.ToString();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token");
                return null;
            }
        }

        internal async Task<GitHubClient> FetchInstallationClient (string installationId)
        {
            try 
            {
                var jwt = GenerateJwtToken(installationId);
                _ghclient.Credentials = new Credentials(jwt, AuthenticationType.Bearer);

                var response = await _ghclient.GitHubApps.CreateInstallationToken(int.Parse(installationId));

                var installationClient = new GitHubClient(new ProductHeaderValue("Azure-Load-Test-Deployment-Gate"))
                {
                    Credentials = new Credentials(response.Token)
                };

                return installationClient;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error fetching installation token");
                return null;
            }
        }

        [Function("tokentest")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get")]HttpRequestData req, string id)
        {            
            var token = await FetchInstallationToken(id);

            using (var client = _httpClientFactory.CreateClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                client.DefaultRequestHeaders.Add("User-Agent", "Azure-Load-Test-Deployment-Gate");
                client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                var response = client.GetAsync("https://api.github.com/app").Result;
                var body = response.Content.ReadAsStringAsync().Result;
                _logger.LogInformation(body);
            }

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}