using Microsoft.Extensions.DependencyInjection;
using NebulaSpace.Outcome.AspNetCore.Extensions;
using NebulaSpace.Outcome.AspNetCore.Options;
using NebulaSpace.Outcome.AspNetCore.Pagination;

namespace NebulaSpace.Outcome.AspNetCore.Tests.Unit;

public class AddOutcomePaginationTests
{
    [Fact]
    public void AddOutcome_ScanDisabled_ConfiguresExplicitlyRegisteredQuery()
    {
        // AOT-safe path: no assembly scan (reflection) — Configure is dispatched through
        // the static abstract interface member for the registered type only.
        ProbeQuery.ConfiguredWithLimit100 = false;

        new ServiceCollection().AddOutcome(options =>
        {
            options.Pagination.MaxLimit = 100;
            options.Pagination.ScanAssemblies = false;
            options.Pagination.RegisterQuery<ProbeQuery>();
        });

        Assert.True(ProbeQuery.ConfiguredWithLimit100);
    }

    [Fact]
    public void AddOutcome_ScanEnabledByDefault_ConfiguresQueryAutomatically()
    {
        // Reflection path (default): every IPaginatedQuery implementor in loaded assemblies
        // is discovered and configured — no registration needed.
        ProbeQuery.ConfiguredWithLimit50 = false;

        new ServiceCollection().AddOutcome(options =>
            options.Pagination.MaxLimit = 50);

        Assert.True(ProbeQuery.ConfiguredWithLimit50);
    }

    // Sentinel flags, not a single shared value: every AddOutcome call — including ones in
    // other test classes running in parallel — scans loaded assemblies and configures all
    // IPaginatedQuery implementors. A flag set only by a Configure call with our exact limit
    // is deterministically true once our own AddOutcome has run, no matter what concurrent
    // scans do in between.
    private sealed class ProbeQuery : IPaginatedQuery
    {
        public static bool ConfiguredWithLimit50;
        public static bool ConfiguredWithLimit100;

        public static void Configure(PaginationOptions options)
        {
            if (options.MaxLimit == 50) ConfiguredWithLimit50 = true;
            if (options.MaxLimit == 100) ConfiguredWithLimit100 = true;
        }
    }
}
