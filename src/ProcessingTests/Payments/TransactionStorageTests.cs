using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Models.Accounts;
using Models.Payments;
using NSubstitute;
using Processing.Payments;
using Processing.Transactions;
using Shouldly;

namespace ProcessingTests.Payments;

public class TransactionStorageTests
{
    [Test]
    public async Task Processing_stores_signed_amounts_without_floating_point_rounding()
    {
        var client = Substitute.For<TableClient>();
        var stored = new List<TableEntity>();
        client.AddEntityAsync(Arg.Any<TableEntity>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                stored.Add(call.Arg<TableEntity>());
                return Substitute.For<Response>();
            });
        var handler = new PaymentHandler(new TransactionRepository(client));
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
            await handler.Handle(new Payment
                {
                    FromAccountId = 1, ToAccountId = 2, Amount = 12.34567890123456789m, Description = "Demo"
                },
                new AccountOwner(Guid.NewGuid(), Guid.NewGuid()));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        stored.Select(row => (row.PartitionKey, row.GetString("Amount")))
            .ShouldBe([("1", "-12.34567890123456789"), ("2", "12.34567890123456789")]);
    }
}
