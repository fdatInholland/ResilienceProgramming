using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Polly;
using RestSharp;
using System.Net;

namespace Retry;

public class FunctionPollyRetry
{
    private readonly ILogger _logger;

    private int[] httpStatusCodesWorthRetrying = { 0, 404, 408, 500, 502, 503, 504 };

    public FunctionPollyRetry(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<FunctionPollyRetry>();
    }

    [Function("Retry")]
    public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        //handles the (forced) exception thrown- from the restresponse...
        var RetryPolicy = Policy.Handle<Exception>().WaitAndRetry(3,
               //instantiate the delay strategy
               attempt => TimeSpan.FromSeconds(0.1 * Math.Pow(2, attempt)),
               //onRetry delegate : log stuff
               (exception, calculatedWaitDuration) =>
               {
                   _logger.LogError($"exception: {exception.Message}");
               });
        try
        {
            RetryPolicy.Execute(() =>
            {
                // Call a non-existent Webservice
                var client = new RestClient("http://pslice.net/blahblah");
                var request = new RestRequest();
                RestResponse response = client.Execute(request);

                //statuscode is 0 , as the domain does not exist (DNS resolution failure)

                // Force a retry
                if (httpStatusCodesWorthRetrying.Contains((int)response.StatusCode))
                    throw new Exception("http request failed");

                _logger.LogError($" result: {response.StatusCode}");
            });
        }
        catch (Exception e)
        {
            _logger.LogCritical($"critical error: {e.Message}");
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        return response;
    }
}
