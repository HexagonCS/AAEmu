// Authors: AAGene, ZeromusXYZ

using System.Collections.Concurrent;
using AAEmu.Commons.Utils;
using NCrontab;
using Task = AAEmu.Game.Models.Tasks.Task;
using NLog;
using AAEmu.Game.Models.Tasks.Skills;

namespace AAEmu.Game.Core.Managers;

// ReSharper disable once ClassNeverInstantiated.Global
public class TaskManager : Singleton<TaskManager>, ITaskManager
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
    private readonly ConcurrentDictionary<uint, Task> _queue = new();
    private readonly HashSet<uint> _taskIds = [];
    private readonly object _taskIdLock = new();
    private uint _taskIdIndex = 1;
    private DateTime _lastNowUtc = DateTime.MinValue; // monotonic guard

    public static readonly CrontabSchedule.ParseOptions s_crontabScheduleParseOptions = new() { IncludingSeconds = true };

    public void Initialize()
    {
        _queue.Clear();
        _lastNowUtc = DateTime.UtcNow;
    }

    public void Start()
    {
        TickManager.Instance.OnTick.Subscribe(Tick, TimeSpan.FromMilliseconds(50), true);
    }

    public void Stop()
    {
        // TODO: Wait for still running Tasks before returning
    }

    private void Tick(TimeSpan delta)
    {
        try
        {
            var rawNow = DateTime.UtcNow;
            // Monotonic guard: never allow time to move backwards for scheduling
            var now = rawNow;
            if (now < _lastNowUtc)
            {
                var clockSkew = _lastNowUtc - now;
                s_logger.Warn("TaskManager detected system clock moved backwards by {0} ms; clamping now to last value.", clockSkew.TotalMilliseconds.ToString("F0"));
                now = _lastNowUtc;
            }
            else
            {
                _lastNowUtc = now;
            }
            var initialCount = _queue.Count;
            s_logger.Debug("TaskManager.Tick enter: now={0:O}, queued={1}", now, initialCount);
            var toRemove = new List<uint>();
            var dueCount = 0;
            var executedCount = 0;
            var earliestTrigger = DateTime.MaxValue;
            var farFutureCount = 0; // tasks scheduled >30s ahead
            foreach (var (id, task) in _queue.ToArray())
            {
                try
                {
                    // Track earliest trigger and far-future stats for health logging
                    if (task.TriggerTime < earliestTrigger)
                        earliestTrigger = task.TriggerTime;
                    if (task.TriggerTime - now > TimeSpan.FromSeconds(30))
                        farFutureCount++;

                    if (task.TriggerTime >= now)
                        continue;

                    dueCount++;
                    if (task is SkillTask st && st.Skill?.Template != null)
                    {
                        s_logger.Debug("TaskManager executing SkillTask: name={0}, id={1}, skill={2}, tlId={3}, triggerAt={4:O}, now={5:O}",
                            task.Name, id, st.Skill.Template.Id, st.Skill.TlId, task.TriggerTime, now);
                    }

                    System.Threading.Tasks.Task.Run(task.ExecuteAsync);
                    task.ExecuteCount++;
                    executedCount++;

                    // Check if there still needs to be executions done
                    if ((task.RepeatCount < 0) || (task.ExecuteCount < task.RepeatCount))
                    {
                        // If there is a CronSchedule set, use that to calculate the next TriggerTime
                        if (task.CronSchedule != null)
                            task.TriggerTime = task.CronSchedule.GetNextOccurrence(now);

                        // If there is an interval set, add it for the next TriggerTime
                        if (task.RepeatInterval != TimeSpan.Zero)
                            task.TriggerTime = now + task.RepeatInterval;

                        continue; // Don't remove this Task from the queue yet
                    }

                    toRemove.Add(id);
                }
                catch (Exception e)
                {
                    s_logger.Error("TaskManager.Tick task-loop exception for task {0} (id={1}): {2}\n{3}", task?.Name, id, e.Message, e.StackTrace);
                    toRemove.Add(id);
                }
            }

            foreach (var objId in toRemove)
            {
                _queue.Remove(objId, out _);
                ReleaseId(objId);
            }
            var earliestDeltaMs = earliestTrigger == DateTime.MaxValue ? (double?)null : (earliestTrigger - now).TotalMilliseconds;
            s_logger.Debug("TaskManager.Tick exit: due={0}, executed={1}, removed={2}, queuedNow={3}, earliestDeltaMs={4}, farFuture={5}",
                dueCount, executedCount, toRemove.Count, _queue.Count,
                earliestDeltaMs?.ToString("F0") ?? "n/a", farFutureCount);
        }
        catch (Exception e)
        {
            s_logger.Error("TaskManager.Tick exception: {0}\n{1}", e.Message, e.StackTrace);
        }
    }

    /// <summary>
    /// Schedules a task to be executed in the future
    /// </summary>
    /// <param name="task">Task to Execute</param>
    /// <param name="startDelay">First trigger is startDelay time from now</param>
    /// <param name="repeatInterval">Time between Task Executions, needs to be set to allow usage of count</param>
    /// <param name="count">Number of times to repeat this action, -1 means infinite and 0 is the same as 1 time</param>
    /// <returns></returns>
    public bool Schedule(Task task, TimeSpan? startDelay = null, TimeSpan? repeatInterval = null, int count = -1)
    {
        var taskId = NextId();
        task.Id = taskId;

        // If it's only supposed to run once and immediately, then don't queue it, and just run now
        if ((startDelay.HasValue && startDelay.Value == TimeSpan.Zero) && (count >= 0) && (count <= 1))
        {
            task.Execute();
            ReleaseId(task.Id);
            return true;
        }

        task.TriggerTime = startDelay.HasValue ? DateTime.UtcNow + startDelay.Value : DateTime.UtcNow;

        if (repeatInterval.HasValue)
        {
            task.RepeatInterval = repeatInterval.Value;
            task.RepeatCount = count;
        }
        else
        {
            task.RepeatCount = 1;
        }

        var added = _queue.TryAdd(taskId, task);
        if (added && task is SkillTask st && st.Skill?.Template != null)
        {
            s_logger.Debug("TaskManager scheduled SkillTask: name={0}, id={1}, skill={2}, tlId={3}, triggerAt={4:O}, queuedNow={5}",
                task.Name, taskId, st.Skill.Template.Id, st.Skill.TlId, task.TriggerTime, _queue.Count);
        }
        return added;
    }

    /// <summary>
    /// Schedules a task to be executed in the future
    /// </summary>
    /// <param name="task">Task to Execute</param>
    /// <param name="cronExpression">Cron expression that defines the trigger conditions</param>
    /// <param name="startDelay">First trigger is only possible startDelay time from now</param>
    /// <param name="count">Number of times to repeat this action, -1 means infinite and 0 is the same as 1 time</param>
    /// <returns></returns>
    public bool CronSchedule(Task task, string cronExpression, TimeSpan? startDelay = null, int count = -1)
    {
        var taskId = NextId();
        task.Id = taskId;

        if (startDelay.HasValue && startDelay.Value == TimeSpan.Zero)
        {
            task.Execute();
            ReleaseId(task.Id);
            return true;
        }

        var firstPossibleTriggerTime = startDelay.HasValue ? DateTime.UtcNow + startDelay.Value : DateTime.UtcNow;

        task.CronSchedule = CrontabSchedule.Parse(cronExpression, s_crontabScheduleParseOptions);
        task.TriggerTime = task.CronSchedule.GetNextOccurrence(firstPossibleTriggerTime);
        task.RepeatCount = count;

        return _queue.TryAdd(taskId, task);
    }

    /// <summary>
    /// Cancels a Task
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    public bool Cancel(Task task)
    {
        var res = _queue.Remove(task.Id, out _);

        if (res)
        {
            task.Cancelled = true;
            s_logger.Debug("TaskManager cancelled task: name={0}, id={1}", task?.Name, task?.Id);
            ReleaseId(task.Id);
        }

        return res;
    }
    public void RemoveTasks(Func<Task, bool> predicate)
    {
        // Take a snapshot of the current tasks to avoid modifying the collection while iterating.
        var snapshot = _queue.ToArray();
        var removed = 0;
        foreach (var kvp in snapshot)
        {
            if (predicate(kvp.Value))
            {
                _queue.Remove(kvp.Key, out _);
                ReleaseId(kvp.Key);
                removed++;
            }
        }
        if (removed > 0)
            s_logger.Debug("TaskManager removed {0} tasks via predicate", removed);
    }
    private uint NextId()
    {
        lock (_taskIdLock)
        {
            var id = _taskIdIndex;
            while (_taskIds.Contains(id))
            {
                if (id == uint.MaxValue)
                    id = 1;
                else
                    id++;
            }
            _taskIds.Add(id);
            _taskIdIndex = id + 1u;
            if (_taskIdIndex == 0)
                _taskIdIndex = 1;

            return id;
        }
    }

    private void ReleaseId(uint id)
    {
        lock (_taskIdLock)
        {
            _taskIds.Remove(id);
        }
    }

    public int GetQueueCount()
    {
        return _queue.Count;
    }
}
