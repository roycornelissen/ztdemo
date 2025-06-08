namespace Models.Payments;

public record PaymentAcceptedEvent(string UserId, Payment Payment);