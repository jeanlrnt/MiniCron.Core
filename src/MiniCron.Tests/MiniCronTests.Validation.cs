using MiniCron.Core.Helpers;

namespace MiniCron.Tests;

public partial class MiniCronTests
{
    [Fact]
    public void ValidateCronExpression_ValidExpression_DoesNotThrow()
    {
        // Arrange
        var validExpressions = new[]
        {
            "* * * * *",
            "*/5 * * * *",
            "0 0 * * *",
            "0 12 * * 1",
            "0,30 * * * *",
            "0-30 * * * *",
            "0 9-17 * * *"
        };

        // Act & Assert
        foreach (var expression in validExpressions)
        {
            var exception = Record.Exception(() => CronHelper.ValidateCronExpression(expression));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void ValidateCronExpression_NullExpression_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            CronHelper.ValidateCronExpression(null!));
        
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_EmptyExpression_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            CronHelper.ValidateCronExpression(""));
        
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_WhitespaceExpression_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => 
            CronHelper.ValidateCronExpression("   "));
        
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_TooFewFields_ThrowsArgumentException()
    {
        // Arrange
        var expression = "* * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("must have exactly 5 fields", exception.Message);
        Assert.Contains("got 3 field(s)", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_TooManyFields_ThrowsArgumentException()
    {
        // Arrange
        var expression = "* * * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("must have exactly 5 fields", exception.Message);
        Assert.Contains("got 6 field(s)", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_InvalidStepSyntax_ThrowsArgumentException()
    {
        // Arrange - Step syntax must be "*/n", not "5/3"
        var expression = "5/3 * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("Step syntax must start with '*'", exception.Message);
        Assert.Contains("minute", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_StepWithZero_ThrowsArgumentException()
    {
        // Arrange
        var expression = "*/0 * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("must be greater than zero", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_StepWithNegativeValue_ThrowsArgumentException()
    {
        // Arrange
        var expression = "*/-5 * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("must be greater than zero", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_StepWithNonNumericValue_ThrowsArgumentException()
    {
        // Arrange
        var expression = "*/abc * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("Step must be a valid integer", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_InvalidListValue_ThrowsArgumentException()
    {
        // Arrange
        var expression = "0,abc,30 * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("All values must be integers", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_ListValueOutOfRange_ThrowsArgumentException()
    {
        // Arrange - Minute field can only be 0-59
        var expression = "0,30,70 * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("out of range", exception.Message);
        Assert.Contains("0-59", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_InvalidRangeSyntax_ThrowsArgumentException()
    {
        // Arrange
        var expression = "0-30-45 * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("Invalid range syntax", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_RangeWithNonNumericValues_ThrowsArgumentException()
    {
        // Arrange
        var expression = "abc-def * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("Both start and end must be integers", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_RangeValuesOutOfRange_ThrowsArgumentException()
    {
        // Arrange - Minute field can only be 0-59
        var expression = "0-70 * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("out of range", exception.Message);
        Assert.Contains("0-59", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_RangeStartGreaterThanEnd_ThrowsArgumentException()
    {
        // Arrange
        var expression = "30-10 * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("Start value must be less than or equal to end value", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_ExactValueOutOfRange_ThrowsArgumentException()
    {
        // Arrange - Hour field can only be 0-23
        var expression = "* 25 * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("out of range", exception.Message);
        Assert.Contains("hour", exception.Message);
        Assert.Contains("0-23", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_InvalidExactValue_ThrowsArgumentException()
    {
        // Arrange
        var expression = "* abc * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("Expected an integer", exception.Message);
    }

    [Theory]
    [InlineData("60 * * * *", "minute", "0-59")]  // Minute out of range
    [InlineData("* 24 * * *", "hour", "0-23")]    // Hour out of range
    [InlineData("* * 0 * *", "day", "1-31")]      // Day out of range (0)
    [InlineData("* * 32 * *", "day", "1-31")]     // Day out of range (32)
    [InlineData("* * * 0 *", "month", "1-12")]    // Month out of range (0)
    [InlineData("* * * 13 *", "month", "1-12")]   // Month out of range (13)
    [InlineData("* * * * 7", "weekday", "0-6")]   // Weekday out of range
    public void ValidateCronExpression_FieldOutOfRange_ThrowsArgumentException(
        string expression, string fieldName, string expectedRange)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("out of range", exception.Message);
        Assert.Contains(fieldName, exception.Message);
        Assert.Contains(expectedRange, exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_MultipleSlashesInStep_ThrowsArgumentException()
    {
        // Arrange
        var expression = "*//5 * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("Invalid step syntax", exception.Message);
    }

    [Fact]
    public void ValidateCronExpression_EmptyField_ThrowsArgumentException()
    {
        // Arrange - This would result in empty fields when split
        var expression = " * * * *";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            CronHelper.ValidateCronExpression(expression));
        
        Assert.Contains("Cron expression must have exactly 5 fields (min hour day month weekday), but got 4 field(s): ' * * * *'"
          , exception.Message);
    }
}
