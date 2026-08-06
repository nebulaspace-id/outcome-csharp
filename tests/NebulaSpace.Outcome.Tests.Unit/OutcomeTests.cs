namespace NebulaSpace.Outcome.Tests.Unit;

using Unit = NebulaSpace.Outcome.Unit;

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
            public TestFailure() : base("boom", "test") { }
        }

        [Fact]
        public void Fail_SetsFailureAndIsFailure()
        {
            var failure = new TestFailure();
            var result = Outcome.Fail(failure);

            Assert.False(result.IsSuccess);
            Assert.True(result.IsFailure);
            Assert.Equal(failure, result.Failure);
        }
    }

    public class MatchTests
    {
        private sealed class TestFailure : Failure
        {
            public TestFailure() : base("boom", "test") { }
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

            Assert.Equal(failure, received);
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
            Outcome<Unit> unit = Outcome.Success();
            Outcome<int> typed = unit;

            Assert.True(typed.IsSuccess);
            Assert.False(typed.IsFailure);
        }

        [Fact]
        public void FailedUnitOutcome_ImplicitlyConvertsToTypedOutcome()
        {
            var failure = new BadRequestFailureStub();
            Outcome<Unit> unit = Outcome.Fail(failure);
            Outcome<int> typed = unit;

            Assert.True(typed.IsFailure);
            Assert.Equal(failure, typed.Failure);
        }

        private sealed class BadRequestFailureStub : Failure
        {
            public BadRequestFailureStub() : base("bad", "bad_request") { }
        }
    }

    public class StructInvariantTests
    {
        private sealed class TestFailure : Failure
        {
            public TestFailure() : base("boom", "test") { }
        }

        [Fact]
        public void Value_OnFailure_ThrowsInvalidOperationException()
        {
            Outcome<int> outcome = Outcome.Fail(new TestFailure());

            Assert.Throws<InvalidOperationException>(() => outcome.Value);
        }

        [Fact]
        public void Failure_OnSuccess_ThrowsInvalidOperationException()
        {
            var outcome = Outcome.Success(42);

            Assert.Throws<InvalidOperationException>(() => outcome.Failure);
        }

        [Fact]
        public void Deconstruct_OnSuccess_ReturnsFlagsAndValue()
        {
            var (isSuccess, value, failure) = Outcome.Success(7);

            Assert.True(isSuccess);
            Assert.Equal(7, value);
            Assert.Null(failure);
        }

        [Fact]
        public void Deconstruct_OnFailure_ReturnsFlagsAndFailure()
        {
            var failure = new TestFailure();
            var (isSuccess, _, received) = Outcome<int>.Fail(failure);

            Assert.False(isSuccess);
            Assert.Equal(failure, received);
        }
    }
}
