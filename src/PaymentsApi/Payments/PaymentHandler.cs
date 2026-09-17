using System.Security.Claims;
using System.Text.Json;
using Azure.Storage.Queues;
using Models.Payments;
using Models.ResultPattern;

namespace PaymentsApi.Payments;

public class PaymentHandler(QueueClient queueClient) : IHandlePayments
{
    public async Task<ServiceResult<Payment>> Handle(Payment payment, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var evt = new PaymentAcceptedEvent(user.Identity?.Name ?? "anonymous", payment);
        var payload = JsonSerializer.Serialize(evt, AppJsonSerializerContext.Default.PaymentAcceptedEvent);
        
        await queueClient.SendMessageAsync(payload, cancellationToken);
        
        return payment;
    }
}