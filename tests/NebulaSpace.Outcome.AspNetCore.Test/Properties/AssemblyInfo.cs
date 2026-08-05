// HttpResult tests mutate the static ResultPattern.Options holder, so tests
// must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
