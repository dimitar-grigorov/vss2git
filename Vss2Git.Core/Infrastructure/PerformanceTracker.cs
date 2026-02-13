using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Hpdi.Vss2Git
{
    /// <summary>
    /// Tracks cumulative time and invocation counts for named operations.
    /// Thread-safe for concurrent timing from multiple threads.
    /// </summary>
    public class PerformanceTracker
    {
        private readonly object syncLock = new object();
        private readonly Dictionary<string, OperationStats> stats = new Dictionary<string, OperationStats>();

        private class OperationStats
        {
            public long ElapsedTicks;
            public int Count;
            public long MaxTicks;
            public long MinTicks = long.MaxValue;
        }

        /// <summary>
        /// Starts a timing scope for the named operation.
        /// Dispose the returned object to stop timing.
        /// </summary>
        public TimingScope Start(string operationName)
        {
            return new TimingScope(this, operationName);
        }

        internal void Record(string operationName, long elapsedTicks)
        {
            lock (syncLock)
            {
                if (!stats.TryGetValue(operationName, out var op))
                {
                    op = new OperationStats();
                    stats[operationName] = op;
                }
                op.ElapsedTicks += elapsedTicks;
                op.Count++;
                if (elapsedTicks > op.MaxTicks) op.MaxTicks = elapsedTicks;
                if (elapsedTicks < op.MinTicks) op.MinTicks = elapsedTicks;
            }
        }

        /// <summary>
        /// Writes a detailed performance summary to the logger.
        /// </summary>
        public void WriteSummary(Logger logger, TimeSpan totalElapsed)
        {
            lock (syncLock)
            {
                logger.WriteSectionSeparator();
                logger.WriteLine("PERFORMANCE SUMMARY");
                logger.WriteSectionSeparator();
                logger.WriteLine("Total elapsed: {0:hh\\:mm\\:ss\\.fff}", totalElapsed);
                logger.WriteLine("");

                if (stats.Count == 0)
                {
                    logger.WriteLine("No operations tracked.");
                    return;
                }

                // Group by category prefix (text before ':')
                var groups = stats
                    .GroupBy(kv => GetCategory(kv.Key))
                    .OrderByDescending(g => g.Sum(kv => kv.Value.ElapsedTicks));

                foreach (var group in groups)
                {
                    var groupTotalTicks = group.Sum(kv => kv.Value.ElapsedTicks);
                    var groupTotalCount = group.Sum(kv => kv.Value.Count);
                    var groupElapsed = TimeSpan.FromTicks(groupTotalTicks);
                    var pctOfTotal = totalElapsed.Ticks > 0
                        ? (double)groupTotalTicks / totalElapsed.Ticks * 100.0
                        : 0.0;

                    logger.WriteLine("[{0}] {1:hh\\:mm\\:ss\\.fff} ({2:F1}%) - {3} calls",
                        group.Key, groupElapsed, pctOfTotal, groupTotalCount);

                    // Show individual operations sorted by total time descending
                    foreach (var kv in group.OrderByDescending(kv => kv.Value.ElapsedTicks))
                    {
                        var op = kv.Value;
                        var opElapsed = TimeSpan.FromTicks(op.ElapsedTicks);
                        var opAvg = op.Count > 0
                            ? TimeSpan.FromTicks(op.ElapsedTicks / op.Count)
                            : TimeSpan.Zero;
                        var opMax = TimeSpan.FromTicks(op.MaxTicks);
                        var opMin = op.MinTicks == long.MaxValue
                            ? TimeSpan.Zero
                            : TimeSpan.FromTicks(op.MinTicks);

                        logger.WriteLine("  {0,-40} {1,10:hh\\:mm\\:ss\\.fff}  count={2,-6}  avg={3,8:F1}ms  min={4,8:F1}ms  max={5,8:F1}ms",
                            GetOperationName(kv.Key),
                            opElapsed,
                            op.Count,
                            opAvg.TotalMilliseconds,
                            opMin.TotalMilliseconds,
                            opMax.TotalMilliseconds);
                    }
                    logger.WriteLine("");
                }

                var accountedTicks = stats.Values.Sum(s => s.ElapsedTicks);
                var unaccountedTicks = totalElapsed.Ticks - accountedTicks;
                if (unaccountedTicks > 0)
                {
                    var unaccountedPct = (double)unaccountedTicks / totalElapsed.Ticks * 100.0;
                    logger.WriteLine("[Other] {0:hh\\:mm\\:ss\\.fff} ({1:F1}%) - not tracked (VSS reads, file I/O, changeset building)",
                        TimeSpan.FromTicks(unaccountedTicks), unaccountedPct);
                }
            }
        }

        private static string GetCategory(string operationName)
        {
            var colonIdx = operationName.IndexOf(':');
            return colonIdx > 0 ? operationName.Substring(0, colonIdx) : operationName;
        }

        private static string GetOperationName(string fullName)
        {
            var colonIdx = fullName.IndexOf(':');
            return colonIdx > 0 ? fullName.Substring(colonIdx + 1) : fullName;
        }

        public struct TimingScope : IDisposable
        {
            private readonly PerformanceTracker tracker;
            private readonly string operationName;
            private readonly long startTicks;

            internal TimingScope(PerformanceTracker tracker, string operationName)
            {
                this.tracker = tracker;
                this.operationName = operationName;
                this.startTicks = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                var elapsed = Stopwatch.GetTimestamp() - startTicks;
                // Convert from Stopwatch ticks to TimeSpan ticks
                var timeSpanTicks = (long)((double)elapsed / Stopwatch.Frequency * TimeSpan.TicksPerSecond);
                tracker.Record(operationName, timeSpanTicks);
            }
        }
    }
}
