namespace PaymentsApi;

internal static class TestEndpoint
{
    internal static IResult Handle(HttpContext context) =>
        context.User.Identity.IsAuthenticated
            ? Results.Ok("Protected by the fallback policy 🤗")
            : Results.Ok("😱 You should not see this");
}
