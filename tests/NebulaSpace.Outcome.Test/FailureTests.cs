using NebulaSpace.Outcome;

namespace NebulaSpace.Outcome.Tests;

public class FailureTests
{
    private sealed class TestFailure : Failure
    {
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
    public void Failure_IsNullByDefault_OnSuccess()
    {
        var result = Outcome.Success(1);

        Assert.Null(result.Failure);
    }
}
