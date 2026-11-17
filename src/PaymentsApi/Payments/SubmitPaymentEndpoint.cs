using Microsoft.AspNetCore.Mvc;
using Models.Payments;

namespace PaymentsApi.Payments;

internal static class SubmitPaymentEndpoint
{
    internal static async Task<IResult> Handle(
        [FromBody] CreatePaymentRequest request,
        IHandlePayments handler,
        HttpContext context,
        CancellationToken cancellation)
    {
        var result = await handler.Handle(request.ToPayment(), context.User, cancellation);
        return result.IsSuccess
            ? Results.Accepted()
            : result.Error.ToApiResult();
    }
}
