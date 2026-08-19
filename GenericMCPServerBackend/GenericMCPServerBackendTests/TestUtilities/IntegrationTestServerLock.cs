using System.Threading;

namespace GenericMCPServerBackend.Tests.TestUtilities
{
    /// <summary>
    /// One process-wide lock shared by every test which starts an in-process server. The server binds a fixed port, so
    /// two such tests must never run at the same time. A single shared semaphore (rather than one per test-class)
    /// serializes them across all integration-test-classes, so they wait for each other instead of colliding on the
    /// port - independently of how the test-runner is configured to parallelize. A port which is still occupied while
    /// this lock is held therefore belongs to something outside the test-run.
    /// </summary>
    internal static class IntegrationTestServerLock
    {
        private static readonly SemaphoreSlim _Semaphore = new SemaphoreSlim(1, 1);

        public static void Enter()
        {
            _Semaphore.Wait();
        }

        public static void Exit()
        {
            _Semaphore.Release();
        }
    }
}
