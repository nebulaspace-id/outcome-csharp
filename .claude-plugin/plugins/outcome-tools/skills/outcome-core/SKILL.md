---
name: outcome-core
description: >
  Use when writing code that creates, returns, matches, or handles
  Outcome<T>, Failure, or Unit from the NebulaSpace.Outcome NuGet package.
license: MIT
---

# NebulaSpace.Outcome — Core Types

## Installation

```bash
dotnet add package NebulaSpace.Outcome
```

Targets **netstandard2.0**, zero dependencies. Works on .NET Framework 4.6.1+ and all modern .NET versions.

## Creating Outcomes

```csharp
using NebulaSpace.Outcome;

// Success with a value
Outcome<string> ok = Outcome.Success("hello");

// Success with no payload (Unit)
Outcome<Unit> done = Outcome.Success();

// Failure
Outcome<string> failed = Outcome.Fail(new NotFoundFailure("Order not found"));

// Implicit conversion from a raw value
Outcome<Order> order = new Order(...);
```

`Outcome<T>` is an **immutable `readonly struct`**. Its state is fixed at creation.

## Reading the State

| Member | Description |
| --- | --- |
| `IsSuccess` / `IsFailure` | Which branch you're in |
| `Value` | Payload on success (throws `InvalidOperationException` on failure) |
| `Failure` | The `Failure` on failure (throws on success) |
| `Match(onSuccess, onFailure)` | Collapse both branches into one value |
| `Deconstruct(...)` | C# `switch` / pattern matching support |

Always check `IsSuccess` before accessing `Value`, or use `Match` / pattern matching.

## Matching (the Idiomatic Handler)

`Match` forces you to handle both branches and collapses them into one result:

```csharp
var display = result.Match(
    onSuccess: value   => $"OK: {value}",
    onFailure: failure => $"Error: {failure.Message}");
```

## Pattern Matching with `switch`

`Deconstruct` plays nicely with C# `switch` expressions:

```csharp
string message = result switch
{
    (true, var value, _)    => $"Value is {value}",
    (false, _, var failure) => $"Failed with {failure.Message}",
    _                       => "Unreachable",
};
```

## The `Failure` Base Class

```csharp
public abstract class Failure
{
    public string Message { get; }
    public string? Code { get; }         // optional, omit when there's no meaningful code

    protected Failure(string message);              // code = null
    protected Failure(string message, string? code);
}
```

Create custom failures by subclassing:

```csharp
public sealed class NotFoundFailure : Failure
{
    public NotFoundFailure(string message) : base(message, "not_found") { }
}
```

## Unit — the Void Substitute

`Unit` is a `readonly struct` representing "no payload". Use it with operations that succeed but return nothing:

```csharp
public Outcome<Unit> DeleteOrder(int id)
{
    if (!_store.Remove(id))
        return Outcome.Fail(new NotFoundFailure("Order not found"));
    return Outcome.Success();   // returns Outcome<Unit>
}
```

## Service Method Patterns

The idiomatic way to return outcomes from services:

```csharp
// Success returns the value directly (implicit conversion)
// Failure returns an explicit Outcome.Fail(...)
public async Task<Outcome<Order>> GetOrder(int id)
{
    var order = await _repo.FindById(id);
    if (order is null)
        return Outcome.Fail(new NotFoundFailure("Order not found"));

    return order;   // implicit conversion to Outcome<Order>
}

public async Task<Outcome<Unit>> DeleteOrder(int id)
{
    if (!await _repo.Exists(id))
        return Outcome.Fail(new NotFoundFailure("Order not found"));

    await _repo.Remove(id);
    return Outcome.Success();
}
```

## See Also

- `outcome-aspnetcore` — for returning outcomes from ASP.NET Core endpoints, response envelopes, and pagination
- Full documentation: https://github.com/nebulaspace-id/outcome-csharp
