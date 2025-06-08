using System.Security.Claims;
using Models.Payments;
using Models.ResultPattern;

namespace PaymentsApi.Payments;

public interface IHandlePayments
{
    Task<ServiceResult<Payment>> Handle(
        Payment payment,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}