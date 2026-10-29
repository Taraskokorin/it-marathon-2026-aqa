// Scenarios are independent (own location, table, users and group), so they run in parallel.
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: LevelOfParallelism(4)]
