using Azure.Core;

namespace Infrastructure;

public interface ITokenCredentialProvider
{
    TokenCredential Instance { get; }
}

public class TokenCredentialProvider(TokenCredential instance) : ITokenCredentialProvider
{
    public TokenCredential Instance => instance;
}