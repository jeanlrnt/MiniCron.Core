using MiniCron.Core.Helpers;

namespace MiniCron.Tests;

public partial class MiniCronTests
{
    [Fact]
    public void IsDue_ValidStepSyntax_ReturnsTrue()
    {
        // Arrange - Every 5 minutes
        var cronExpression = "*/5 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 0, 0); // minute 0 is divisible by 5

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDue_ValidStepSyntax_ReturnsFalse()
    {
        // Arrange - Every 5 minutes
        var cronExpression = "*/5 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 3, 0); // minute 3 is not divisible by 5

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDue_InvalidStepSyntax_WithNumberBeforeSlash_ReturnsFalse()
    {
        // Arrange - Invalid syntax "5/3" should not match
        var cronExpression = "5/3 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 0, 0); // minute 0 is divisible by 3

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because "5/3" is invalid syntax
        Assert.False(result);
    }

    [Fact]
    public void IsDue_InvalidStepSyntax_WithNumberBeforeSlash_DoesNotMatchDivisible()
    {
        // Arrange - Invalid syntax "10/5" should not match even when value is divisible
        var cronExpression = "10/5 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 15, 0); // minute 15 is divisible by 5

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because "10/5" is invalid step syntax
        Assert.False(result);
    }

    [Fact]
    public void IsDue_ExactMinuteMatch_ReturnsTrue()
    {
        // Arrange - Match exact minute 42
        var cronExpression = "42 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 42, 0);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDue_WildcardMatch_ReturnsTrue()
    {
        // Arrange - All wildcards should always match
        var cronExpression = "* * * * *";
        var time = new DateTime(2024, 1, 1, 12, 42, 30);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDue_StepSyntaxWithZero_ReturnsFalse()
    {
        // Arrange - "*/0" should be invalid and not match (prevents DivideByZeroException)
        var cronExpression = "*/0 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 0, 0);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because step cannot be zero
        Assert.False(result);
    }

    [Fact]
    public void IsDue_StepSyntaxWithNegativeValue_ReturnsFalse()
    {
        // Arrange - "*/-5" should be invalid and not match
        var cronExpression = "*/-5 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 0, 0);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because step cannot be negative
        Assert.False(result);
    }

    [Fact]
    public void IsDue_StepSyntaxWithNonNumericValue_ReturnsFalse()
    {
        // Arrange - "*/abc" should be invalid and not match
        var cronExpression = "*/abc * * * *";
        var time = new DateTime(2024, 1, 1, 12, 0, 0);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because step must be numeric
        Assert.False(result);
    }

    [Fact]
    public void IsDue_StepSyntaxWithEmptyParts_ReturnsFalse()
    {
        // Arrange - "/" should be invalid and not match
        var cronExpression = "/ * * * *";
        var time = new DateTime(2024, 1, 1, 12, 0, 0);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because syntax is malformed
        Assert.False(result);
    }

    [Fact]
    public void IsDue_StepSyntaxWithMultipleSlashes_ReturnsFalse()
    {
        // Arrange - "*//5" should be invalid and not match
        var cronExpression = "*//5 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 0, 0);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because syntax is malformed
        Assert.False(result);
    }

    [Fact]
    public void IsDue_StepSyntaxWithThreeSlashParts_ReturnsFalse()
    {
        // Arrange - "*/5/3" should be invalid and not match
        var cronExpression = "*/5/3 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 0, 0);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because syntax is malformed
        Assert.False(result);
    }

    [Fact]
    public void IsDue_ValidStepSyntaxInHourField_ReturnsTrue()
    {
        // Arrange - Every 6 hours
        var cronExpression = "* */6 * * *";
        var time = new DateTime(2024, 1, 1, 12, 30, 0); // hour 12 is divisible by 6

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDue_InvalidStepSyntaxInHourField_ReturnsFalse()
    {
        // Arrange - Invalid "5/3" in hour field (must be "*/3" for step syntax)
        var cronExpression = "* 5/3 * * *";
        var time = new DateTime(2024, 1, 1, 12, 30, 0);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because "5/3" is invalid step syntax
        Assert.False(result);
    }

    [Fact]
    public void IsDue_ValidStepSyntaxInDayField_ReturnsTrue()
    {
        // Arrange - Every 2 days
        var cronExpression = "* * */2 * *";
        var time = new DateTime(2024, 1, 10, 12, 30, 0); // day 10 is divisible by 2

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDue_InvalidStepSyntaxInDayField_ReturnsFalse()
    {
        // Arrange - Invalid "5/2" in day field
        var cronExpression = "* * 5/2 * *";
        var time = new DateTime(2024, 1, 10, 12, 30, 0); // day 10 is divisible by 2

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because "5/2" is invalid step syntax
        Assert.False(result);
    }

    [Fact]
    public void IsDue_ValidStepSyntaxInMonthField_ReturnsTrue()
    {
        // Arrange - Every 3 months
        var cronExpression = "* * * */3 *";
        var time = new DateTime(2024, 3, 15, 12, 30, 0); // month 3 is divisible by 3

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDue_ValidStepSyntaxInWeekdayField_ReturnsTrue()
    {
        // Arrange - Every 2 weekdays (testing with Sunday = 0)
        var cronExpression = "* * * * */2";
        var time = new DateTime(2024, 1, 7, 12, 30, 0); // Sunday (DayOfWeek = 0)

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDue_CommaListValidValues_ReturnsTrue()
    {
        // Arrange - List of valid minutes "10,20,30"
        var cronExpression = "10,20,30 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 20, 0); // minute 20 is in the list

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsDue_CommaListValidValues_ReturnsFalse()
    {
        // Arrange - List of valid minutes "10,20,30"
        var cronExpression = "10,20,30 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 15, 0); // minute 15 is not in the list

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsDue_CommaListWithInvalidValue_IgnoresInvalid()
    {
        // Arrange - List with one invalid value "10,abc,30"
        var cronExpression = "10,abc,30 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 30, 0); // minute 30 is a valid value in the list

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return true because 30 is valid and matches
        Assert.True(result);
    }

    [Fact]
    public void IsDue_CommaListWithInvalidValue_DoesNotMatch()
    {
        // Arrange - List with one invalid value "10,abc,30"
        var cronExpression = "10,abc,30 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 15, 0); // minute 15 is not in any valid value

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because 15 doesn't match any valid value
        Assert.False(result);
    }

    [Fact]
    public void IsDue_CommaListAllInvalidValues_ReturnsFalse()
    {
        // Arrange - List with all invalid values "abc,def,xyz"
        var cronExpression = "abc,def,xyz * * * *";
        var time = new DateTime(2024, 1, 1, 12, 30, 0);

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return false because no valid values to match
        Assert.False(result);
    }

    [Fact]
    public void IsDue_CommaListWithEmptyValue_IgnoresEmpty()
    {
        // Arrange - List with empty value "10,,30"
        var cronExpression = "10,,30 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 30, 0); // minute 30 is a valid value in the list

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return true because 30 is valid and matches
        Assert.True(result);
    }

    [Fact]
    public void IsDue_CommaListMixedValidInvalid_MatchesValid()
    {
        // Arrange - Mixed list "5,10,abc,20,xyz,30"
        var cronExpression = "5,10,abc,20,xyz,30 * * * *";
        var time = new DateTime(2024, 1, 1, 12, 10, 0); // minute 10 is a valid value

        // Act
        var result = CronHelper.IsDue(cronExpression, time);

        // Assert - Should return true because 10 is valid and matches
        Assert.True(result);
    }
}
