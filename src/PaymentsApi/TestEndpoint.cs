namespace PaymentsApi;

internal static class TestEndpoint
{
    internal static IResult Handle() =>
        Results.Ok("The fallback policy protects this endpoint.");
}
