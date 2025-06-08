using System.Text.Json;

namespace Processing.ExchangeRates;

public class ExchangeRateService(HttpClient httpClient) : IExchangeRateService
{
    public async Task<decimal> ConvertRate(
        decimal amount, 
        string fromCurrency, 
        string toCurrency,
        CancellationToken cancellationToken)
    {
        var url = $"https://open.er-api.com/v6/latest/{fromCurrency.ToUpperInvariant()}";

        var response = await httpClient.GetAsync(new Uri(url, UriKind.Absolute), cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to fetch exchange rate: {response.ReasonPhrase}");
        }
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        if (!root.TryGetProperty("rates", out var rates) ||
            !rates.TryGetProperty(toCurrency.ToUpperInvariant(), out var rateElement) ||
            !rateElement.TryGetDecimal(out var rate))
        {
            throw new InvalidOperationException($"Exchange rate for {toCurrency} not found.");
        }

        return amount * rate;
    }
}