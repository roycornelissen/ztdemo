using Models.Accounts;
using Models.Payments;
using NSubstitute;
using Processing.Payments;
using Shouldly;

namespace ProcessingTests.Payments;

public class PaymentMessageValidatorTests
{
    private static readonly AccountOwner Owner = new(
        Guid.Parse("00000000-0000-0000-0000-000000000001"),
        Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Test]
    public async Task Accepts_valid_payment_when_source_owner_is_still_current()
    {
        var accounts = Substitute.For<IAccountOwnershipReader>();
        accounts.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccountOwnership?>(new AccountOwnership(Owner)));
        accounts.GetAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccountOwnership?>(new AccountOwnership(null)));

        var result = await new PaymentMessageValidator(accounts).ValidateAsync(ValidEvent());

        result.ShouldBeNull();
    }

    [Test]
    public async Task Rejects_missing_envelope_before_account_reads()
    {
        var accounts = Substitute.For<IAccountOwnershipReader>();

        var result = await new PaymentMessageValidator(accounts).ValidateAsync(null);

        result.ShouldBe(PaymentMessageRejectionReason.InvalidEnvelope);
        await accounts.DidNotReceiveWithAnyArgs().GetAsync(default, default);
    }

    [Test]
    public async Task Rejects_invalid_payment_before_account_reads()
    {
        var accounts = Substitute.For<IAccountOwnershipReader>();

        var result = await new PaymentMessageValidator(accounts).ValidateAsync(
            ValidEvent() with { Payment = ValidPayment with { Amount = 0 } });

        result.ShouldBe(PaymentMessageRejectionReason.InvalidPayment);
        await accounts.DidNotReceiveWithAnyArgs().GetAsync(default, default);
    }

    [Test]
    public async Task Rejects_missing_or_empty_owner()
    {
        var accounts = Substitute.For<IAccountOwnershipReader>();
        var validator = new PaymentMessageValidator(accounts);

        var missingOwner = await validator.ValidateAsync(ValidEvent() with { Owner = null! });
        var emptyOwner = await validator.ValidateAsync(
            ValidEvent() with { Owner = new AccountOwner(Guid.Empty, Owner.ObjectId) });

        missingOwner.ShouldBe(PaymentMessageRejectionReason.InvalidOwner);
        emptyOwner.ShouldBe(PaymentMessageRejectionReason.InvalidOwner);
        await accounts.DidNotReceiveWithAnyArgs().GetAsync(default, default);
    }

    [Test]
    public async Task Rejects_source_account_with_missing_or_changed_owner()
    {
        var accounts = Substitute.For<IAccountOwnershipReader>();
        accounts.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<AccountOwnership?>(new AccountOwnership(null)),
                Task.FromResult<AccountOwnership?>(new AccountOwnership(
                    new AccountOwner(
                        Guid.Parse("00000000-0000-0000-0000-000000000003"),
                        Guid.Parse("00000000-0000-0000-0000-000000000004")))));

        var validator = new PaymentMessageValidator(accounts);

        var legacyOwner = await validator.ValidateAsync(ValidEvent());
        var changedOwner = await validator.ValidateAsync(ValidEvent());

        legacyOwner.ShouldBe(PaymentMessageRejectionReason.SourceAccountOwnershipUnavailable);
        changedOwner.ShouldBe(PaymentMessageRejectionReason.SourceAccountOwnershipMismatch);
        await accounts.DidNotReceive().GetAsync(2, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Rejects_missing_source_or_destination_account()
    {
        var accounts = Substitute.For<IAccountOwnershipReader>();
        accounts.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccountOwnership?>(null));
        accounts.GetAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccountOwnership?>(null));

        var validator = new PaymentMessageValidator(accounts);

        var missingSource = await validator.ValidateAsync(ValidEvent());
        missingSource.ShouldBe(PaymentMessageRejectionReason.SourceAccountNotFound);

        accounts.GetAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AccountOwnership?>(new AccountOwnership(Owner)));
        var missingDestination = await validator.ValidateAsync(ValidEvent());
        missingDestination.ShouldBe(PaymentMessageRejectionReason.DestinationAccountNotFound);
    }

    private static PaymentAcceptedEvent ValidEvent() => new(Owner, ValidPayment);

    private static Payment ValidPayment => new()
    {
        FromAccountId = 1,
        ToAccountId = 2,
        Amount = 100,
        Currency = "EUR",
        Description = "Demo transfer"
    };
}
