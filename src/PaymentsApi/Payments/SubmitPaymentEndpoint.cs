using Microsoft.AspNetCore.Mvc;
using Models.Payments;

namespace PaymentsApi.Payments;

internal static class SubmitPaymentEndpoint
{
    internal static async Task<IResult> Handle(
        [FromBody] Payment payment,
        IHandlePayments handler,
        HttpContext context,
        CancellationToken cancellation)
    {
        var result = await handler.Handle(payment, context.User, cancellation);
        return result.IsSuccess
            ? Results.Accepted()
            : result.Error.ToApiResult();
    }
}
