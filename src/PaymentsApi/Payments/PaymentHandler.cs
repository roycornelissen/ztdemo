using System.Security.Claims;
using System.Text.Json;
using Azure.Storage.Queues;
using Models.Accounts;
using Models.Payments;
using Models.ResultPattern;

namespace PaymentsApi.Payments;

public class PaymentHandler(QueueClient queueClient) : IHandlePayments
{
    public async Task<ServiceResult<Payment>> Handle(Payment payment, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (!AccountOwner.TryFromClaimsPrincipal(user, out var owner))
        {
            return ServiceResult<Payment>.Forbidden("A valid tenant and object identity is required");
        }

        var evt = new PaymentAcceptedEvent(owner, payment);
        var payload = JsonSerializer.Serialize(evt, AppJsonSerializerContext.Default.PaymentAcceptedEvent);
        
        await queueClient.SendMessageAsync(payload, cancellationToken);
        
        return payment;
    }
}