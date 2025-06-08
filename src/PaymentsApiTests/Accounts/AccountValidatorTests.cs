using System.Security.Claims;
using Models.Accounts;
using Models.ResultPattern;
using NSubstitute;
using PaymentsApi.Accounts;
using Shouldly;

namespace PaymentsApiTests.Accounts;

public class AccountValidatorTests
{
    [Test]
    public async Task Rejects_non_existent_from_account()
    {
        var repository = Substitute.For<IAccountsRepository>();
        
        var validator = new AccountValidator(null, repository);
        var payment = new Models.Payments.Payment
        {
            FromAccountId = 1,
            ToAccountId = 2
        };
        
        var result = await validator.Handle(payment, new ClaimsPrincipal(), CancellationToken.None);
        
        result.IsSuccess.ShouldBeFalse();
        result.Error!.ErrorType.ShouldBe(ErrorType.Invalid);
        result.Error!.Message.ShouldBe("From account is invalid");
    }
    
    [Test]
    public async Task Rejects_non_existent_to_account()
    {
        var repository = Substitute.For<IAccountsRepository>();
        
        repository.GetAccount(Arg.Is<uint>(1), Arg.Any<CancellationToken>())
            .Returns(new Account
            {
                Id = 1,
                Description = "Test Account",
                UserId = "testuser"
            });
        
        var validator = new AccountValidator(null, repository);
        var payment = new Models.Payments.Payment
        {
            FromAccountId = 1,
            ToAccountId = 2
        };
        
        var result = await validator.Handle(payment, new ClaimsPrincipal(), CancellationToken.None);
        
        result.IsSuccess.ShouldBeFalse();
        result.Error!.ErrorType.ShouldBe(ErrorType.Invalid);
        result.Error!.Message.ShouldBe("To account is invalid");
    }

    [Test]
    public async Task Rejects_when_account_belongs_to_another_user()
    {
        var repository = Substitute.For<IAccountsRepository>();
        
        repository.GetAccount(Arg.Any<uint>(), Arg.Any<CancellationToken>())
            .Returns(info => new Account
            {
                Id = (uint)info[0],
                Description = "Test Account",
                UserId = "testuser"
            });
        
        var validator = new AccountValidator(null, repository);
        var payment = new Models.Payments.Payment
        {
            FromAccountId = 1,
            ToAccountId = 2
        };
        
        var result = await validator.Handle(payment, new ClaimsPrincipal(), CancellationToken.None);
        
        result.IsSuccess.ShouldBeFalse();
        result.Error!.ErrorType.ShouldBe(ErrorType.Forbidden);
        result.Error!.Message.ShouldBe("You are not allowed to make payments from this account");
    }
}