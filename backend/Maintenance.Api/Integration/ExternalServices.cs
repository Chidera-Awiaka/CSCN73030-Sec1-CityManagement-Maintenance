namespace Maintenance.Api.Integration;

/// <summary>Names of the external City Management services Maintenance talks to (TEC-02).</summary>
public static class ExternalServiceNames
{
    /// <summary>E-services (citizen portal): incident reports, weather alerts, road-closure information.</summary>
    public const string EServices = "E-services";
    /// <summary>Property Registration: property data, utilities, service disruptions.</summary>
    public const string PropertyRegistration = "Property Registration";
    /// <summary>Budget: cost estimates and approval decisions.</summary>
    public const string Budget = "Budget";
    /// <summary>Security: assistance requests, road closures, work-status updates.</summary>
    public const string Security = "Security";
    /// <summary>Public Transport: routes, schedules, diversions, road closures.</summary>
    public const string PublicTransport = "Public Transport";
    /// <summary>Staff: personnel and specializations.</summary>
    public const string Staff = "Staff";

    /// <summary>Services that receive outbound messages from Maintenance.</summary>
    public static readonly string[] Outbound = { EServices, PropertyRegistration, Budget, Security, PublicTransport };
}

/// <summary>Result of sending one message to an external service.</summary>
/// <param name="Success">True when the service accepted the message.</param>
/// <param name="Reference">Reference returned by the service (e.g. its request ID).</param>
/// <param name="Error">Error message when the call failed.</param>
public record ExternalCallResult(bool Success, string? Reference, string? Error);

/// <summary>
/// Client for sending a message to one external service. The mock below is used until
/// each team publishes its REST API; a real HTTP client implements the same interface
/// and follows the contracts in docs/integration-contracts.md (TEC-02, TEC-03).
/// </summary>
public interface IExternalServiceClient
{
    /// <summary>Service name, see <see cref="ExternalServiceNames"/>.</summary>
    string ServiceName { get; }

    /// <summary>Sends one message.</summary>
    /// <param name="operation">Operation name from the contract, e.g. RoadClosureNotice.</param>
    /// <param name="payloadJson">Message body (JSON).</param>
    /// <returns>Success with the service's reference, or failure with an error.</returns>
    ExternalCallResult Send(string operation, string payloadJson);
}

/// <summary>A message received by a mock service (lets tests and demos see what was sent).</summary>
/// <param name="Operation">Operation name.</param>
/// <param name="PayloadJson">Message body.</param>
/// <param name="Reference">Reference the mock returned.</param>
/// <param name="ReceivedAt">When the mock received it (UTC).</param>
public record MockMessage(string Operation, string PayloadJson, string Reference, DateTime ReceivedAt);

/// <summary>
/// Mock external service (TEC-03). Accepts messages and returns a reference such as
/// "BUD-1001". Set <see cref="IsAvailable"/> to false to test failure and retry paths.
/// </summary>
public class MockExternalService : IExternalServiceClient
{
    private readonly object _lock = new();
    private readonly List<MockMessage> _received = new();
    private readonly string _prefix;
    private int _next = 1000;

    /// <summary>Creates a mock.</summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="referencePrefix">Prefix of the references it returns.</param>
    public MockExternalService(string serviceName, string referencePrefix)
    {
        ServiceName = serviceName;
        _prefix = referencePrefix;
    }

    /// <inheritdoc />
    public string ServiceName { get; }

    /// <summary>When false, every call fails as if the service were down.</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>Messages received so far.</summary>
    public IReadOnlyList<MockMessage> Received
    {
        get { lock (_lock) { return _received.ToList(); } }
    }

    /// <inheritdoc />
    public ExternalCallResult Send(string operation, string payloadJson)
    {
        lock (_lock)
        {
            if (!IsAvailable)
            {
                return new ExternalCallResult(false, null, $"{ServiceName} is unavailable (mock).");
            }

            var reference = $"{_prefix}-{++_next}";
            _received.Add(new MockMessage(operation, payloadJson, reference, DateTime.UtcNow));
            return new ExternalCallResult(true, reference, null);
        }
    }
}

/// <summary>Looks up the client for each external service (TEC-03).</summary>
public class ExternalServiceHub
{
    private readonly Dictionary<string, IExternalServiceClient> _clients;

    /// <summary>Creates the hub.</summary>
    /// <param name="clients">One client per service.</param>
    public ExternalServiceHub(IEnumerable<IExternalServiceClient> clients)
    {
        _clients = clients.ToDictionary(c => c.ServiceName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Creates a hub with a mock for every outbound service.</summary>
    /// <returns>The result.</returns>
    public static ExternalServiceHub WithMocks() => new(new IExternalServiceClient[]
    {
        new MockExternalService(ExternalServiceNames.EServices, "ESV"),
        new MockExternalService(ExternalServiceNames.PropertyRegistration, "REG"),
        new MockExternalService(ExternalServiceNames.Budget, "BUD"),
        new MockExternalService(ExternalServiceNames.Security, "SEC"),
        new MockExternalService(ExternalServiceNames.PublicTransport, "TRP")
    });

    /// <summary>The client for a service, or null when unknown.</summary>
    /// <param name="serviceName">The serviceName.</param>
    /// <returns>The result.</returns>
    public IExternalServiceClient? Get(string serviceName) =>
        _clients.TryGetValue(serviceName, out var client) ? client : null;

    /// <summary>All clients.</summary>
    public IReadOnlyCollection<IExternalServiceClient> All => _clients.Values;
}
