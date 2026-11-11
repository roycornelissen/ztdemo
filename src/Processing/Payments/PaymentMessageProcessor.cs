using System.Text.Json;
using Azure.Storage.Queues;
using Models.Payments;

namespace Processing.Payments;

public class PaymentMessageProcessor(
    QueueClient queueClient,
    IHandlePayments handler,
    PaymentMessageValidator validator,
    ILogger<PaymentMessageProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await queueClient.ReceiveMessageAsync(cancellationToken: stoppingToken);

            if (message.HasValue && message.Value != null)
            {
                PaymentAcceptedEvent? paymentEvent;
                try
                {
                    paymentEvent = message.Value.Body.ToObjectFromJson<PaymentAcceptedEvent>();
                }
                catch (JsonException)
                {
                    await RejectMessage(
                        message.Value.MessageId,
                        message.Value.PopReceipt,
                        PaymentMessageRejectionReason.MalformedJson,
                        stoppingToken);
                    continue;
                }

                var rejection = await validator.ValidateAsync(paymentEvent, stoppingToken);
                if (rejection is { } reason)
                {
                    await RejectMessage(
                        message.Value.MessageId,
                        message.Value.PopReceipt,
                        reason,
                        stoppingToken);
                    continue;
                }

                if (paymentEvent is null)
                {
                    throw new InvalidOperationException("The payment validator accepted an empty message.");
                }

                try
                {
                    await handler.Handle(paymentEvent.Payment, paymentEvent.Owner, stoppingToken);
                }
                catch (PaymentMessageRejectedException exception)
                {
                    await RejectMessage(
                        message.Value.MessageId,
                        message.Value.PopReceipt,
                        exception.Reason,
                        stoppingToken);
                    continue;
                }

                await queueClient.DeleteMessageAsync(message.Value.MessageId, message.Value.PopReceipt, stoppingToken);
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task RejectMessage(
        string messageId,
        string popReceipt,
        PaymentMessageRejectionReason reason,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(new EventId(1003, "PaymentMessageRejected"),
            "Payment queue message rejected with {Reason}; message {MessageId}",
            reason, messageId);
        await queueClient.DeleteMessageAsync(messageId, popReceipt, cancellationToken);
    }
}