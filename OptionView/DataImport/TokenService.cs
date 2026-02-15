using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace TtTokenService
{ 
    public class TokenInfo
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public int ExpiresIn { get; set; }
        public DateTime ObtainedAtUtc { get; set; }
        public DateTime ExpiresAtUtc => ObtainedAtUtc.AddSeconds(ExpiresIn);

        public void Clear()
        {
            AccessToken = "";
            ExpiresIn = 0;
            ObtainedAtUtc = DateTime.MinValue;
        }
    }

    internal class TokenService
    {
        private readonly RestClient _client;
        private readonly string _clientSecret;
        private TokenInfo _token = new TokenInfo();

        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public TokenService(string baseUrl, string clientSecret, string initToken)
        {
            _client = new RestClient(new RestClientOptions(baseUrl)
            {
                Timeout = TimeSpan.FromSeconds(30),
                ThrowOnAnyError = false
            });

            _clientSecret = clientSecret;
            _token.RefreshToken = initToken;
        }

        // ==========================
        // PUBLIC METHOD
        // ==========================


        public string GetAccessToken()
        {
            if (_token.RefreshToken == "")
                throw new Exception("TokenService not correctly initialized");

            if (TokenNeedsRefresh())
                RefreshToken();

            return _token.AccessToken;
        }

        public void ClearToken()
        {
            _token.Clear();
        }

        // ==========================
        // REFRESH LOGIC
        // ==========================
        private bool TokenNeedsRefresh()
        {
            if (_token.ExpiresAtUtc == DateTime.MinValue) return true;

            // Refresh 60 seconds early
            return DateTime.UtcNow >= _token.ExpiresAtUtc.AddMinutes(-1);
        }


        public void RefreshToken()
        {
            RefreshTokenAsync().GetAwaiter().GetResult();
            return;
        }

        private async Task RefreshTokenAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                var request = new RestRequest("oauth/token", Method.Post);
                request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                request.AddParameter("grant_type", "refresh_token");
                request.AddParameter("refresh_token", _token.RefreshToken);
                request.AddParameter("client_secret", _clientSecret);

                var response = await _client.ExecuteAsync(request);

                if (!response.IsSuccessful || string.IsNullOrWhiteSpace(response.Content))
                {
                    ClearToken();
                    throw new Exception("Refresh failed.");
                }

                var newToken = JsonConvert.DeserializeObject<TokenInfoResponse>(response.Content);

                _token.AccessToken = newToken.AccessToken;
                _token.RefreshToken = newToken.RefreshToken ?? _token.RefreshToken;
                _token.ExpiresIn = newToken.ExpiresIn;
                _token.ObtainedAtUtc = DateTime.UtcNow;

            }
            finally
            {
                _refreshLock.Release();
            }
        }


        // ==========================
        // RESPONSE DTO
        // ==========================
        private class TokenInfoResponse
        {
            [JsonProperty("access_token")]
            public string AccessToken { get; set; } = "";

            [JsonProperty("refresh_token")]
            public string RefreshToken { get; set; }

            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }
        }
    }
    
}
