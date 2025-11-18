using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Polly;
using System.Net;

namespace Circuitbreaker;

public class CircuitBreakerFunction
{

    // A static variable to hold the shared state of the circuit breaker
    private static readonly ISyncPolicy circuitBreaker;
    private static int _simulatedFailureCount = 0; // For testing only

    static CircuitBreakerFunction()
    {
        // Define the policy statically so its state persists across function calls
        circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreaker(
                // 3 consecutive failures to open the circuit
                exceptionsAllowedBeforeBreaking: 3,
                // Circuit stays open for 5 seconds
                durationOfBreak: TimeSpan.FromSeconds(5),
                onBreak: (ex, breakDelay) =>
                {
                    Console.WriteLine($"*** Circuit breaker opened after 3 failures. Break delay: {breakDelay.TotalSeconds}s. ***");
                },
                onReset: () =>
                {
                    Console.WriteLine("*** Circuit breaker reset (closed). ***");
                    _simulatedFailureCount = 0; // Reset simulated count on a successful reset
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine("*** Circuit breaker moved to Half-Open. ***");
                });
    }

    [Function("PenzleCall")]
    public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        var response = req.CreateResponse();
        string message;

        try
        {
            // The circuit breaker determines if the execution is allowed
            circuitBreaker.Execute(() => MakePenzleCall(req));
            message = "Service call succeeded.";
            response.WriteString(message);
            response.StatusCode = HttpStatusCode.OK;
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            // Catch this specific exception when the circuit is OPEN
            message = "Service call skipped: Circuit is OPEN.";
            response.WriteString(message);
            response.StatusCode = HttpStatusCode.ServiceUnavailable;
        }
        catch (Exception ex)
        {
            // Catch any exception thrown by MakePenzleCall that led to the break
            message = $"Service call failed: {ex.Message}";
            response.WriteString(message);
            response.StatusCode = HttpStatusCode.InternalServerError;
        }

        return response;
    }

    // This method now simulates a service call that either succeeds or throws
    private static void MakePenzleCall(HttpRequestData req)
    {
        // Use a query parameter to simulate a failure state for testing
        bool shouldFail = bool.Parse(req.Query["fail"] ?? "false");

        if (shouldFail && _simulatedFailureCount < 4)
        {
            _simulatedFailureCount++;
            Console.WriteLine($"Simulated service call failed. Failure count: {_simulatedFailureCount}");
            // A real service call would throw an exception here (e.g., HttpClient exception)
            throw new Exception($"Simulated external service failure {_simulatedFailureCount}.");
        }

        Console.WriteLine("Simulated service call succeeded (or count reached 4).");
        // Reset simulated count after a successful call in Half-Open state
        _simulatedFailureCount = 0;
    }
}



