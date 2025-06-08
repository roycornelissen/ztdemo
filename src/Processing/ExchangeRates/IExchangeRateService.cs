namespace Processing.ExchangeRates;

public interface IExchangeRateService
{
    Task<decimal> ConvertRate(
        decimal amount, 
        string fromCurrency, 
        string toCurrency,
        CancellationToken cancellationToken);
}