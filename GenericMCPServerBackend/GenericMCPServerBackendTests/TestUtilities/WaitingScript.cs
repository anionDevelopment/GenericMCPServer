using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GenericMCPServerBackend.Tests.TestUtilities
{
    /// <summary>
    /// The script of a tool which runs until the testcase lets it end, together with the folder it reports into.
    /// </summary>
    /// <remarks>
    /// A cancellation can only be exercised with a call which is still running when the cancellation arrives. A script
    /// which simply sleeps for a while would make that a question of timing, so this one runs until it is released: it
    /// reports that it started, waits for the release and writes its canary once it ran to its end.
    ///
    /// That canary is what a testcase checks instead of the answer of the call: "the call ended with a cancellation"
    /// only states that whoever waited stopped waiting, while a canary which stays absent even after the script was
    /// released states that the script itself is gone.
    /// </remarks>
    public sealed class WaitingScript : IDisposable
    {
        /// <summary>The name under which the script is written, which is what a declaration has to state as its <c>calls</c>.</summary>
        public const string FileName = "Waiting.py";

        /// <summary>The file the script writes before it starts to wait, so that a testcase knows it is running.</summary>
        private const string StartedFileName = "started";

        /// <summary>The file which lets the script run to its end.</summary>
        private const string ReleaseFileName = "release";

        /// <summary>The canary. A script which was stopped never writes it, not even after it was released.</summary>
        private const string FinishedFileName = "finished";

        /// <summary>What the script writes to its caller once it ran to its end.</summary>
        public const string AnswerOfTheScript = "the script ran to its end";

        /// <summary>
        /// How long a testcase waits for the script to report that it is running. It is generous on purpose: the value
        /// only decides how long a testcase which is going to fail takes.
        /// </summary>
        private static readonly TimeSpan _DurationUntilTheScriptRuns = TimeSpan.FromSeconds(60);

        /// <summary>
        /// How long a testcase waits for the canary after it released the script. A script which is still running needs
        /// no longer than this to write it, so a canary which is absent afterwards is one which will never exist.
        /// </summary>
        private static readonly TimeSpan _DurationInWhichAReleasedScriptWritesItsCanary = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The script itself. The limit of its wait keeps it from running forever when nobody releases it, which is
        /// exactly the case in which it is expected to be stopped from outside.
        /// </summary>
        public const string Content = @"import argparse
import os
import sys
import time

parser = argparse.ArgumentParser()
parser.add_argument('--value', required=True)
arguments = parser.parse_args()
with open(os.path.join(arguments.value, '" + StartedFileName + @"'), 'w', encoding='utf-8') as started_file:
    started_file.write('the script is running')
end_of_the_wait = time.monotonic() + 120
while not os.path.exists(os.path.join(arguments.value, '" + ReleaseFileName + @"')) and time.monotonic() < end_of_the_wait:
    time.sleep(0.05)
with open(os.path.join(arguments.value, '" + FinishedFileName + @"'), 'w', encoding='utf-8') as finished_file:
    finished_file.write('" + AnswerOfTheScript + @"')
sys.stdout.write('" + AnswerOfTheScript + @"')
";

        /// <summary>
        /// The folder the script reports into, which is the value a call of it has to state.
        /// </summary>
        /// <remarks>
        /// It is a folder of its own and not the folder of the script: the question a testcase asks is whether a
        /// certain file exists, and the folder of the script already contains files.
        /// </remarks>
        public string ReportFolder { get; }

        public WaitingScript()
        {
            this.ReportFolder = Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(this.ReportFolder);
        }

        /// <summary>
        /// Writes the script into the given folder, from where a declaration can call it.
        /// </summary>
        public static void WriteInto(string folder)
        {
            File.WriteAllText(Path.Combine(folder, FileName), Content);
        }

        /// <summary>
        /// Waits until the script reports that it is running, so that whatever the testcase does afterwards happens
        /// while it really runs.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the script never started, because a testcase which builds on that would fail somewhere else and for a reason which is not the actual one.</exception>
        public void WaitUntilItRuns()
        {
            if (!TheFileAppears(Path.Combine(this.ReportFolder, StartedFileName), _DurationUntilTheScriptRuns))
            {
                throw new InvalidOperationException($"The script did not report within {_DurationUntilTheScriptRuns.TotalSeconds} seconds that it is running, so it never started.");
            }
        }

        /// <summary>
        /// Releases the script and states whether it ran to its end. A script which was stopped is not there anymore to
        /// notice that it was released, so this is what answers whether the program behind the call really is gone.
        /// </summary>
        public bool ItStillRunsToItsEndAfterItWasReleased()
        {
            File.WriteAllText(Path.Combine(this.ReportFolder, ReleaseFileName), "the script may run to its end");
            return TheFileAppears(Path.Combine(this.ReportFolder, FinishedFileName), _DurationInWhichAReleasedScriptWritesItsCanary);
        }

        /// <summary>
        /// Whether the script started at all.
        /// </summary>
        public bool ItStarted()
        {
            return File.Exists(Path.Combine(this.ReportFolder, StartedFileName));
        }

        private static bool TheFileAppears(string file, TimeSpan timeout)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!File.Exists(file))
            {
                if (timeout < stopwatch.Elapsed)
                {
                    return false;
                }
                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }
            return true;
        }

        public void Dispose()
        {
            if (Directory.Exists(this.ReportFolder))
            {
                Directory.Delete(this.ReportFolder, true);
            }
        }
    }
}
