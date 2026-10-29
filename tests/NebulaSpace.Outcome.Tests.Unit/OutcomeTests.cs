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

    public class HardeningTests
    {
        private sealed class TestFailure : Failure
        {
            public TestFailure() : base("boom", "test") { }
        }

        [Fact]
        public void Default_IsSuccess_Throws()
        {
            Outcome<int> outcome = default;

            Assert.Throws<InvalidOperationException>(() => outcome.IsSuccess);
        }

        [Fact]
        public void Default_IsFailure_Throws()
        {
            Outcome<int> outcome = default;

            Assert.Throws<InvalidOperationException>(() => outcome.IsFailure);
        }

        [Fact]
        public void Default_Value_Throws()
        {
            Outcome<int> outcome = default;

            Assert.Throws<InvalidOperationException>(() => outcome.Value);
        }

        [Fact]
        public void Default_Match_Throws()
        {
            Outcome<int> outcome = default;

            Assert.Throws<InvalidOperationException>(() => outcome.Match(v => v, _ => 0));
        }

        [Fact]
        public void Default_ToString_IsUninitialized()
        {
            Outcome<int> outcome = default;

            Assert.Equal("Uninitialized", outcome.ToString());
        }

        [Fact]
        public void ImplicitConversion_FromNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Outcome<string> outcome = (string)null!;
                _ = outcome;
            });
        }

        [Fact]
        public void Success_ExplicitNull_IsAllowed()
        {
            var outcome = Outcome<string>.Success(null!);

            Assert.True(outcome.IsSuccess);
            Assert.Null(outcome.Value);
        }

        [Fact]
        public void Fail_WithNull_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Outcome<int>.Fail(null!));
        }

        [Fact]
        public void TryGetValue_OnSuccess_ReturnsTrue()
        {
            var outcome = Outcome.Success("value");

            Assert.True(outcome.TryGetValue(out var value));
            Assert.Equal("value", value);
        }

        [Fact]
        public void TryGetValue_OnFailure_ReturnsFalse()
        {
            Outcome<string> outcome = Outcome.Fail(new TestFailure());

            Assert.False(outcome.TryGetValue(out var value));
            Assert.Null(value);
        }

        [Fact]
        public void TryGetFailure_OnFailure_ReturnsTrue()
        {
            var failure = new TestFailure();
            Outcome<string> outcome = Outcome.Fail(failure);

            Assert.True(outcome.TryGetFailure(out var received));
            Assert.Same(failure, received);
        }

        [Fact]
        public void SaferAccessors_EchoTheState()
        {
            var success = Outcome.Success(1);
            var failure = Outcome<int>.Fail(new TestFailure());

            Assert.True(success.IsSuccess);
            Assert.False(success.IsFailure);
            Assert.False(failure.IsSuccess);
            Assert.True(failure.IsFailure);
        }

        [Fact]
        public void Equality_ComparesStateAndPayload()
        {
            Assert.Equal(Outcome.Success(1), Outcome.Success(1));
            Assert.NotEqual(Outcome.Success(1), Outcome.Success(2));

            var failure = new TestFailure();
            Assert.Equal(Outcome<int>.Fail(failure), Outcome<int>.Fail(failure));
            Assert.NotEqual(Outcome<int>.Fail(failure), Outcome<int>.Fail(new TestFailure()));
        }
    }
}
