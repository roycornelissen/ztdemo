using System.Globalization;
using System.Runtime.CompilerServices;
using Azure.Data.Tables;

namespace PaymentsApi.Payments;

internal sealed record PaymentHistoryEntry(string Id, decimal Amount, string? Description, DateTimeOffset? RecordedAt);

internal sealed class PaymentHistoryRepository([FromKeyedServices("transactions")] TableClient client)
{
    internal async IAsyncEnumerable<PaymentHistoryEntry> GetEntries(
        uint accountId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var partition = accountId.ToString(CultureInfo.InvariantCulture);
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {partition}");
        await foreach (var row in client.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            var amount = row["Amount"] switch
            {
                string value => decimal.Parse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture),
                double value => (decimal)value,
                int value => value,
                long value => value,
                _ => throw new InvalidDataException("The payment history entry has an unsupported amount type.")
            };
            yield return new PaymentHistoryEntry(row.RowKey, amount, row.GetString("Description"), row.Timestamp);
        }
    }
}
