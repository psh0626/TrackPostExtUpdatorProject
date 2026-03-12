using Microsoft.Win32.TaskScheduler;
using System;

public static class TaskManager
{
    public static void GetTasks()
    {
        using TaskService ts = new();
        foreach (var task in ts.AllTasks)
        {
            Console.WriteLine($"Task Name: {task.Name}");
            Console.WriteLine($"  State: {task.State}");
            Console.WriteLine($"  Next Run Time: {task.NextRunTime}");
            Console.WriteLine($"  Last Run Time: {task.LastRunTime}");
            Console.WriteLine($"  Last Task Result: {task.LastTaskResult}");
            Console.WriteLine();
        }
    }
}