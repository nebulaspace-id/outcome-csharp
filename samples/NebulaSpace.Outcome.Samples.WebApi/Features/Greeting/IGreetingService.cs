using NebulaSpace.Outcome;

namespace NebulaSpace.Outcome.Samples.WebApi.Features.Greeting;

public interface IGreetingService
{
    Outcome<string> Greet(string name);
}
