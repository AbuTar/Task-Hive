using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Timers;
using System.Drawing;



using Timer = System.Timers.Timer;

namespace Task_Hive
{
    internal class Program
    {
        public static class MethodManager
        {
            private static List<Item> tasks = new List<Item>();
            private static Dictionary<int, Timer> taskTimers = new Dictionary<int, Timer>();
            private static string connectionString = $"Data Source=\"{AppDomain.CurrentDomain.BaseDirectory}myTo-DoList.db\"";

            public static void SaveTasksToDatabase()
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();

                    // Insert new tasks into the database
                    foreach (var task in tasks)
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT COUNT(*) FROM Tasks WHERE taskId=@TaskId";
                            command.Parameters.AddWithValue("@TaskId", task.taskId);
                            var count = Convert.ToInt32(command.ExecuteScalar());

                            // If a task with the same taskId already exists, skip it.
                            if (count != 0)
                            {
                                continue;
                            }

                            // Find an available taskId.
                            List<int> existingTaskIds;
                            using (var readerCommand = connection.CreateCommand())
                            {
                                readerCommand.CommandText = "SELECT taskId FROM Tasks";
                                using (var reader = readerCommand.ExecuteReader())
                                {
                                    existingTaskIds = new List<int>();
                                    while (reader.Read())
                                    {
                                        existingTaskIds.Add(reader.GetInt32(0));
                                    }
                                }
                            }

                            int newTaskId = 1;
                            while (existingTaskIds.Contains(newTaskId))
                            {
                                newTaskId++;
                            }
                            task.taskId = newTaskId;
                            // Insert the new task into the database.
                            command.CommandText = "INSERT INTO Tasks (description, duration, priority, taskId) " +
                                                  "VALUES (@Description, @Duration, @Priority, @TaskId)";

                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("@Description", task.description);
                            command.Parameters.AddWithValue("@Duration", task.duration);
                            command.Parameters.AddWithValue("@Priority", task.priority);
                            command.Parameters.AddWithValue("@TaskId", task.taskId);

                            command.ExecuteNonQuery();
                        }
                    }
                }
            }


       
            public static void AddTask(string x)
            {
                string descriptionIn = x.Substring(2);

                // Use a do-while loop to keep asking for the duration until a valid integer is entered.
                int durationMinutes;
                do
                {
                    Console.WriteLine("Please enter the duration of the task in minutes");
                    string durationInput = Console.ReadLine();
                    bool success = int.TryParse(durationInput, out durationMinutes);

                    if (success)
                    {
                        int hours = durationMinutes / 60;
                        int minutes = durationMinutes % 60;

                        string durationCompound;
                        if (hours > 0)
                        {
                            durationCompound = $"{hours} hour(s) and {minutes} minute(s)";
                        }
                        else
                        {
                            durationCompound = $"{minutes} minute(s)";
                        }

                        Console.WriteLine("Duration entered: " + durationCompound);
                    }
                    else
                    {
                        Console.WriteLine("Invalid input. Please enter a valid integer value for the duration.");
                    }
                } while (!int.TryParse(Console.ReadLine(), out durationMinutes));

                // Use a do-while loop to keep asking for the priority until a valid string is entered.
                string priorityIn;
                do
                {
                    Console.WriteLine("Please enter the Priority of the task:\nHigh \nMedium \nLow");
                    priorityIn = Console.ReadLine();

                    if (string.IsNullOrEmpty(priorityIn))
                    {
                        Console.WriteLine("Invalid input. Please enter a valid string value for the priority.");
                    }
                } while (string.IsNullOrEmpty(priorityIn));

                Item task = new Item(descriptionIn, durationMinutes, priorityIn);
                tasks.Add(task);

                

                SaveTasksToDatabase(); // Save the tasks to the database
            }

            public static void SortTasks(string criterion)
            {
                switch (criterion.ToLower())
                {
                    case "priority":
                        tasks.Sort((task1, task2) =>
                        {
                            string[] priorityOrder = { "High", "Medium", "Low" };
                            int index1 = Array.IndexOf(priorityOrder, task1.priority);
                            int index2 = Array.IndexOf(priorityOrder, task2.priority);
                            return index1.CompareTo(index2);
                        });
                        break;
                    case "duration":
                        tasks.Sort((task1, task2) => task1.duration.CompareTo(task2.duration));
                        break;
                    case "description":
                        tasks.Sort((task1, task2) => task1.description.CompareTo(task2.description));
                        break;
                    default:
                        Console.WriteLine("Invalid criterion. Please enter 'priority', 'duration', or 'description'.");
                        break;
                }
            }



            public static void ClearTasks()
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM Tasks";
                        command.ExecuteNonQuery();
                    }
                }

                // Clear the tasks 
                tasks.Clear();

                Console.WriteLine("All tasks have been removed.");
            }

            public static void ShowTasks()
            {
                foreach (var task in tasks)
                {
                    Console.WriteLine($"Task ID: {task.taskId}, Description: {task.description}, Priority: {task.priority}");
                }
            }

            public static void RemoveTask()
            {
                Console.WriteLine("Tasks:");

                foreach (var task in tasks)
                {
                    Console.WriteLine($"Task ID: {task.taskId}, Description: {task.description}, Priority: {task.priority}");
                }

                Console.WriteLine("Please enter the Task ID of the task you want to remove:");
                string taskIdInput = Console.ReadLine();
                int submittedtaskId;
                bool success = int.TryParse(taskIdInput, out submittedtaskId);

                if (success)
                {
                    // Find the task with the specified taskId
                    Item taskToRemove = tasks.Find(task => task.taskId == submittedtaskId);

                    if (taskToRemove != null)
                    {
                        tasks.Remove(taskToRemove);

                        if (taskTimers.ContainsKey(taskToRemove.taskId))
                        {
                            taskTimers[taskToRemove.taskId].Enabled = false; // Stop the timer
                            taskTimers.Remove(taskToRemove.taskId);
                        }
                        else
                        {
                            Console.WriteLine("Task not found.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid input. Please enter a valid integer value for the Task ID.");
                    }

                    SaveTasksToDatabase(); // Save the tasks to the database
                }




            }

            private static void LoadTasksFromDatabase()
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT * FROM Tasks";

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string description = reader.GetString(0);
                                int duration = reader.GetInt32(1);
                                string priority = reader.GetString(2);
                                int taskId = reader.GetInt32(3);

                                Item task = new Item(description, duration, priority, taskId);
                                tasks.Add(task);
                            }
                        }
                    }
                }

                // Sort the tasks based on their priority.
                tasks.Sort((task1, task2) =>
                {
                    // Define the priority order.
                    string[] priorityOrder = { "High", "Medium", "Low" };

                    // Get the index of the priority in the priority order array.
                    int index1 = Array.IndexOf(priorityOrder, task1.priority);
                    int index2 = Array.IndexOf(priorityOrder, task2.priority);

                    // Sort the tasks based on the index in the priority order array.
                    return index1.CompareTo(index2);
                });
            }


            static void Main(string[] args)
            {
                LoadTasksFromDatabase(); // Load tasks from the database at startup

                bool continueProgram = true;

                while (continueProgram)
                {
                    Console.WriteLine("Please select what you would like to do\nStarting with  Add, Show, Remove, Complete, Sort, Clear or Exit:\nAdd a task \nShow your tasks\nRemove your Tasks\nComplete a Task\nSort your Tasks\nClear your tasks\nExit the Program\n ");

                    string selection = Console.ReadLine();
                    selection = selection.ToUpper();

                    if (selection.StartsWith("ADD"))
                    {
                        AddTask(selection);
                    }
                    else if (selection.StartsWith("SHOW"))
                    {
                        ShowTasks();
                    }
                    else if (selection.StartsWith("REMOVE"))
                    {
                        RemoveTask();
                    }
                    else if (selection.StartsWith("COMPLETE"))
                    {
                        Console.WriteLine("Please enter the Task ID of the task you want to complete:");
                        string taskIdInput = Console.ReadLine();
                        int submittedtaskId;
                        bool success = int.TryParse(taskIdInput, out submittedtaskId);

                        if (success)
                        {
                            string taskIdString = submittedtaskId.ToString();
                            SortTasks(taskIdString);
                        }
                        else
                        {
                            Console.WriteLine("Invalid input. Please enter a valid integer value for the Task ID.");
                        }
                    }
                    else if (selection.StartsWith("SORT"))
                    {
                        Console.WriteLine("Please enter the criteria to sort your tasks by (priority, duration, or description):");
                        string criteria = Console.ReadLine();
                        SortTasks(criteria);
                    }
                    else if (selection.StartsWith("CLEAR"))
                    {
                        ClearTasks();
                    }
                    else if (selection.StartsWith("EXIT"))
                    {
                        continueProgram = false;
                    }
                    else
                    {
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                    }

                    if (continueProgram)
                    {
                        Console.WriteLine("Do you want to continue? (Y/N)");
                        string continueAnswer = Console.ReadLine();
                        Console.Clear();

                        if (string.IsNullOrEmpty(continueAnswer) || continueAnswer.StartsWith("N") || continueAnswer.StartsWith("n"))
                        {
                            continueProgram = false;
                        }
                        else if (continueAnswer.StartsWith("Y") || continueAnswer.StartsWith("y"))
                        {
                            continueProgram = true;
                        }
                    }
                }
            }


            class Item
            {
                public string description { get; set; }
                public int duration { get; set; }
                public string priority { get; set; }
                public int taskId { get; set; }

                private static int uniqueTaskIDCounter = 0;

                public Item(string aDescription, int aDuration, string aPriority, int aTaskId = 0)
                {
                    description = aDescription;
                    duration = aDuration;
                    priority = aPriority;
                    taskId = aTaskId != 0 ? aTaskId : uniqueTaskIDCounter++;
                }
            }
        }
    }
}