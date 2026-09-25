using Xunit;

// Program configures Serilog's process-wide bootstrap logger.  Multiple TestServer hosts built
// concurrently freeze that singleton twice, so HTTP integration tests must create hosts serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
