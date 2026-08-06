namespace NebulaSpace.Outcome.Tests.Unit;

public class FailureTests
{
    private sealed class TestFailure : Failure
    {
        public TestFailure() : base("boom", "test") { }
    }

    private sealed class NoCodeFailure : Failure
    {
        public NoCodeFailure() : base("boom") { }
    }

    [Fact]
    public void Failure_CanBeSubclassed()
    {
        var failure = new TestFailure();
        Assert.IsAssignableFrom<Failure>(failure);
    }

    [Fact]
    public void Failure_InstancesAreDistinct()
    {
        var a = new TestFailure();
        var b = new TestFailure();

        Assert.NotSame(a, b);
    }

    [Fact]
    public void Failure_ExposesMessageAndCode()
    {
        var failure = new TestFailure();

        Assert.Equal("boom", failure.Message);
        Assert.Equal("test", failure.Code);
    }

    [Fact]
    public void Failure_CodeIsNullable_WhenNotProvided()
    {
        var failure = new NoCodeFailure();

        Assert.Equal("boom", failure.Message);
        Assert.Null(failure.Code);
    }

    [Fact]
    public void Failure_SingleArgConstructor_DefaultsCodeToNull()
    {
        var failure = new NoCodeFailure();

        Assert.Null(failure.Code);
    }

    [Fact]
    public void Failure_IsNullByDefault_OnSuccess()
    {
        var result = Outcome.Success(1);

        Assert.True(result.IsSuccess);
    }
}
