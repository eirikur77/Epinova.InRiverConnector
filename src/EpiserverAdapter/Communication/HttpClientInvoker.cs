using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Epinova.InRiverConnector.Interfaces;
using inRiver.Integration.Logging;
using inRiver.Remoting.Log;

namespace Epinova.InRiverConnector.EpiserverAdapter.Communication
{
    public class HttpClientInvoker
    {
        private static readonly HttpClient HttpClient;
        private static bool _clientPropsSet;
        private readonly string _isImportingAction;

        static HttpClientInvoker()
        {
            HttpClient = new HttpClient();
            IntegrationLogger.Write(LogLevel.Debug, $"Static constructor running.");
        }

        public HttpClientInvoker(IConfiguration config)
        {
            _isImportingAction = config.Endpoints.IsImporting;
            IntegrationLogger.Write(LogLevel.Debug,
                $"Initializing HttpClientInvoker. clientPropsSet: {_clientPropsSet}");

            // INFO: Allows multiple HttpClientInvoker classes to be created while keeping one static HttpClient.
            if (!_clientPropsSet)
            {
                IntegrationLogger.Write(LogLevel.Debug, $"Initing clientPropsSet. ApiKey => {config.EpiApiKey}");

                HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpClient.DefaultRequestHeaders.Add("apikey", config.EpiApiKey);
                HttpClient.Timeout = new TimeSpan(config.EpiRestTimeout, 0, 0);
                _clientPropsSet = true;
            }
        }

        public async Task PostAsync<T>(string url, T message)
        {
            try
            {
                IntegrationLogger.Write(LogLevel.Debug, $"Posting to {url}");
                var timer = Stopwatch.StartNew();

                var response = await HttpClient.PostAsJsonAsync(url, message);
                response.EnsureSuccessStatusCode();

                IntegrationLogger.Write(LogLevel.Debug, $"Posted to {url}, took {timer.ElapsedMilliseconds}.");
            }
            catch (TaskCanceledException)
            {
                IntegrationLogger.Write(LogLevel.Error, "Unable to connect to episerver, trying agian..");
                Thread.Sleep(15000);
                await PostAsync<T>(url, message);
            }
        }

        public async Task<string> PostWithAsyncStatusCheck<T>(string url, T message)
        {
            try
            {
                IntegrationLogger.Write(LogLevel.Debug, $"Posting to {url}");

                var response = await HttpClient.PostAsJsonAsync(url, message);

                if (response.IsSuccessStatusCode)
                {
                    var parsedResponse = await response.Content.ReadAsAsync<string>();

                    while (parsedResponse == ImportStatus.IsImporting)
                    {
                        Thread.Sleep(15000);
                        parsedResponse = await Get(_isImportingAction);
                    }

                    if (parsedResponse.StartsWith("ERROR"))
                        IntegrationLogger.Write(LogLevel.Error, parsedResponse);

                    return parsedResponse;
                }
                var errorMsg = $"Import failed: {(int) response.StatusCode} ({response.ReasonPhrase})";
                IntegrationLogger.Write(LogLevel.Error, errorMsg);
            }
            catch (TaskCanceledException)
            {
                IntegrationLogger.Write(LogLevel.Error, "Unable to connect to episerver, trying agian..");
                Thread.Sleep(15000);
                return await PostWithAsyncStatusCheck(url, message);
            }
            return "$Posting to {url} failed";
        }

        public async Task<string> Get(string uri)
        {
            var response = await HttpClient.GetAsync(uri);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsAsync<string>();
        }

        public List<string> PostWithStringListAsReturn<T>(string url, T message)
        {
            IntegrationLogger.Write(LogLevel.Debug, $"Posting to {url}");

            var uri = new Uri(url);
            var response = HttpClient.PostAsJsonAsync(uri.PathAndQuery, message).Result;

            if (response.IsSuccessStatusCode)
                return response.Content.ReadAsAsync<List<string>>().Result;
            var errorMsg = $"Import failed: {(int) response.StatusCode} ({response.ReasonPhrase})";
            IntegrationLogger.Write(LogLevel.Error, errorMsg);
            throw new HttpRequestException(errorMsg);
        }
    }
}