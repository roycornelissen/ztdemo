using Microsoft.Extensions.Logging;
using Models.Accounts;
using Models.Payments;
using NSubstitute;
using Processing.Payments;
using Shouldly;

namespace ProcessingTests.Payments;

public class HighAmountPolicyTests
{
    private static readonly AccountOwner Owner = new(
        Guid.Parse("00000000-0000-0000-0000-000000000001"),
        Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [TestCase(10000, 0)]
    [TestCase(10001, 1)]
    public async Task Review_events_do_not_expose_user_or_payment_data(decimal amount, int expectedEvents)
    {
        var inner = Substitute.For<IHandlePayments>();
        var logger = Substitute.For<ILogger<HighAmountPolicy>>();
        var payment = new Payment { Amount = amount, Description = "private-description" };

        await new HighAmountPolicy(inner, logger).Handle(payment, Owner);

        var logs = logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log").ToArray();
        logs.Length.ShouldBe(expectedEvents);
        if (expectedEvents != 0)
        {
            var args = logs[0].GetArguments();
            ((EventId)args[1]!).Id.ShouldBe(1002);
            args[2]!.ToString().ShouldBe("Payment exceeded the high-amount review threshold");
        }
        await inner.Received(1).Handle(payment, Owner, Arg.Any<CancellationToken>());
    }
}
