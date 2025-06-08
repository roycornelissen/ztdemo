using Azure.Storage.Queues;
using Models.Payments;

namespace Processing.Payments;

public class PaymentMessageProcessor(QueueClient queueClient, IHandlePayments handler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await queueClient.ReceiveMessageAsync(cancellationToken: stoppingToken);

            if (message.HasValue && message.Value != null)
            {
                var e = message.Value.Body.ToObjectFromJson<PaymentAcceptedEvent>();
                
                if (e != null)
                {
                    await handler.Handle(e.Payment, e.UserId, stoppingToken);
                    await queueClient.DeleteMessageAsync(message.Value.MessageId, message.Value.PopReceipt, stoppingToken);
                }
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}