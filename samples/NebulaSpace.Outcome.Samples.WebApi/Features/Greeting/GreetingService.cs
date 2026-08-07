using NebulaSpace.Outcome.AspNetCore.Failures;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;

public sealed class GreetingService : IGreetingService
{
    public Outcome<string> Greet(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? Outcome.Fail(new BadRequestFailure("Nama tidak boleh kosong."))
            : Outcome.Success($"Halo, {name}!");
}
