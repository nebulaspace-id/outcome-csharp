using NebulaSpace.Outcome;

namespace NebulaSpace.Outcome.Tests;

public class OutcomeTests
{
    public class SuccessTests
    {
        [Fact]
        public void Success_WithValue_ReturnsSuccessOutcome()
        {
            var result = Outcome.Success("hello");

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal("hello", result.Value);
        }

        [Fact]
        public void Success_Unit_ReturnsSuccessWithoutValue()
        {
            var result = Outcome.Success();

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
        }

        [Fact]
        public void Success_IsDistinctFromSuccess_WithSameValue()
        {
            // The unit success has no Value set; ensure the static factory behaves.
            var unit = Outcome.Success();
            var valued = Outcome.Success(42);

            Assert.True(unit.IsSuccess);
            Assert.True(valued.IsSuccess);
            Assert.Equal(42, valued.Value);
        }
    }

    public class FailureTests
    {
        private sealed class TestFailure : Failure
        {
        }

        [Fact]
        public void Fail_SetsFailureAndIsFailure()
        {
            var failure = new TestFailure();
            var result = Outcome.Fail(failure);

            Assert.False(result.IsSuccess);
            Assert.True(result.IsFailure);
            Assert.Same(failure, result.Failure);
        }
    }

    public class MatchTests
    {
        private sealed class TestFailure : Failure
        {
        }

        [Fact]
        public void Match_OnSuccess_InvokesOnSuccess()
        {
            var result = Outcome.Success(10);

            var output = result.Match(
                onSuccess: value => value * 2,
                onFailure: _ => -1);

            Assert.Equal(20, output);
        }

        [Fact]
        public void Match_OnFailure_InvokesOnFailure()
        {
            Outcome<int> result = Outcome.Fail(new TestFailure());

            var output = result.Match(
                onSuccess: value => value * 2,
                onFailure: _ => -1);

            Assert.Equal(-1, output);
        }

        [Fact]
        public void Match_OnFailure_ReceivesTheFailure()
        {
            var failure = new TestFailure();
            var result = Outcome.Fail(failure);

            Failure? received = null;
            result.Match(
                onSuccess: _ => 0,
                onFailure: f => { received = f; return 1; });

            Assert.Same(failure, received);
        }
    }

    public class ImplicitConversionTests
    {
        [Fact]
        public void Value_ImplicitlyConvertsToSuccessOutcome()
        {
            Outcome<string> result = "value";

            Assert.True(result.IsSuccess);
            Assert.Equal("value", result.Value);
        }

        [Fact]
        public void UnitOutcome_ImplicitlyConvertsToTypedOutcome()
        {
            Outcome unit = Outcome.Success();
            Outcome<int> typed = unit;

            Assert.True(typed.IsSuccess);
            Assert.True(typed.IsFailure == false);
        }

        [Fact]
        public void FailedUnitOutcome_ImplicitlyConvertsToTypedOutcome()
        {
            var failure = new BadRequestFailureStub();
            Outcome unit = Outcome.Fail(failure);
            Outcome<int> typed = unit;

            Assert.True(typed.IsFailure);
            Assert.Same(failure, typed.Failure);
        }

        private sealed class BadRequestFailureStub : Failure
        {
        }
    }
}
