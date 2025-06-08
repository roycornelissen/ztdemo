using Models.Payments;
using Models.ResultPattern;
using PaymentsApi.Payments;
using Shouldly;

namespace PaymentsApiTests.Payments;

public class PaymentValidatorTests
{
    [Test]
    public async Task Rejects_long_description()
    {
        // Arrange
        var validator = new PaymentValidator(null);
        var deposit = new Payment
        {
            Description = new string('a', 31),
            Amount = 100,
            FromAccountId = 1,
            ToAccountId = 2
        };
        
        // Act
        var result = await validator.Handle(deposit, null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldBe(
            "The field Description must be a string or array type with a maximum length of '30'.");
        result.Error!.ErrorType.ShouldBe(ErrorType.Invalid);
    }

    [Test]
    public async Task Rejects_short_description()
    {
        // Arrange
        var validator = new PaymentValidator(null);
        var deposit = new Payment
        {
            Description = "",
            Amount = 100,
            FromAccountId = 1,
            ToAccountId = 2
        };
        
        // Act
        var result = await validator.Handle(deposit, null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldBe(
            "The Description field is required.");
        result.Error!.ErrorType.ShouldBe(ErrorType.Invalid);
    }

    [TestCase(0)]
    [TestCase(-100)]
    public async Task Rejects_negative_or_zero_amount(decimal amount)
    {
        // Arrange
        var validator = new PaymentValidator(null);
        var deposit = new Payment
        {
            Description = "Valid description",
            Amount = amount,
            FromAccountId = 1,
            ToAccountId = 2
        };
        
        // Act
        var result = await validator.Handle(deposit, null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldBe(
            "Deposit amount must be greater than zero.");
        result.Error!.ErrorType.ShouldBe(ErrorType.Invalid);
    }
    
    [Test]
    public async Task Rejects_same_from_and_to_account()
    {
        // Arrange
        var validator = new PaymentValidator(null);
        var deposit = new Payment
        {
            Description = "Valid description",
            Amount = 100,
            FromAccountId = 1,
            ToAccountId = 1
        };
        
        // Act
        var result = await validator.Handle(deposit, null);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error!.Message.ShouldBe(
            "From and To account IDs must be different.");
        result.Error!.ErrorType.ShouldBe(ErrorType.Invalid);
    }

    [Test]
    public async Task Accepts_valid_deposit()
    {
        // Arrange
        var validator = new PaymentValidator(null);
        var deposit = new Payment
        {
            Description = "Valid description",
            Amount = 100,
            FromAccountId = 1,
            ToAccountId = 2
        };
        
        // Act
        var result = await validator.Handle(deposit, null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }
}