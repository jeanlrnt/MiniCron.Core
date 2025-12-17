using MiniCron.Core.Helpers;

namespace MiniCron.Test;

public class CronHelperTests
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
}
