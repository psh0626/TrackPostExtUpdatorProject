using System;
using System.Diagnostics;

namespace TrackPostExtUpdator
{
    public static class TaskManager
    {
        public static List<TaskInfo>? GetTasks()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = "/query /fo CSV /",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if(process == null)
            {
                Console.WriteLine("Failed to start process.");
                return null;
            }
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var tasks = ParseTaskString(output);
            return tasks;
        }
        private static List<TaskInfo> ParseTaskString(string taskString)
        {
            var taskList = new List<TaskInfo>();
            var lines = taskString.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Split(",");
                if (parts.Length >= 3)
                {
                    string taskName = parts[0].Trim('"');
                    string nextRunTime = parts[1].Trim('"');
                    string status = parts[2].Trim('"');
                    // Create TaskInfo object and add to list
                    taskList.Add(new TaskInfo(taskName, nextRunTime, status));
                }
            }
            return taskList;
        }
    }
    public class TaskInfo(string taskName, string nextRunTime, string status)
    {
        public string Name { get; } = taskName;
        public string NextRunTime { get; } = nextRunTime;
        public string Status { get; } = status;
    }
}