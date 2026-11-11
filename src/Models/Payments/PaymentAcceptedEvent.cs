using Models.Accounts;

namespace Models.Payments;

public record PaymentAcceptedEvent(AccountOwner Owner, Payment Payment);