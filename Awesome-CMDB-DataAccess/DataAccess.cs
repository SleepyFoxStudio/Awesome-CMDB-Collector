using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Awesome_CMDB_DataAccess_Models;
using IdentityModel.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Awesome_CMDB_DataAccess
{
    public class DataAccess
    {
        private readonly string _discoEndPoint;
        private readonly string _clientId;
        private readonly string _secret;
        private string _accessToken;
        private DateTime _tokenExpiresDateTime = DateTime.MinValue;
        private readonly HttpClient _client;

        public DataAccess(string discoEndPoint, string clientId, string secret)
        {
            _discoEndPoint = discoEndPoint;
            _clientId = clientId;
            _secret = secret;
            _client = new HttpClient();
        }


        public async Task GetAccountSummary()
        {
            var content = await CallGetApi("https://localhost:6001/accountSummary");
        }

        private async Task<string> CallGetApi(string uri)
        {
            await RefreshBearerTokenIfRequired();
            var response = await _client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error calling {uri} returned {response.StatusCode}");
            }
            return await response.Content.ReadAsStringAsync();
        }
        private async Task<string> CallPostApi<T>(string uri, T content)
        {
            await RefreshBearerTokenIfRequired();
            var response = await _client.PostAsJsonAsync(uri, content);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error calling {uri} returned {response.StatusCode}");
            }
            return await response.Content.ReadAsStringAsync();
        }


        private async Task RefreshBearerTokenIfRequired()
        {
            if (_tokenExpiresDateTime > DateTime.UtcNow)
            {
                return;
            }
            // discover endpoints from metadata

            var disco = await _client.GetDiscoveryDocumentAsync(_discoEndPoint);
            if (disco.IsError)
            {
                Console.WriteLine(disco.Error);
                return;
            }

            // request token
            var tokenResponse = await _client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = _clientId,
                ClientSecret = _secret,

                Scope = "api1"
            });

            if (tokenResponse.IsError)
            {
                Console.WriteLine(tokenResponse.Error);
                return;
            }


            Console.WriteLine(tokenResponse.Json);
            Console.WriteLine("\n\n");
            _accessToken = tokenResponse.AccessToken;
            _tokenExpiresDateTime = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 10);
            _client.SetBearerToken(_accessToken);
        }

        public async Task PostAccountServerGroups(List<ServerGroup> serverGroups)
        {
            var content = await CallPostApi("https://localhost:6001/account", serverGroups);
        }
    }
}
