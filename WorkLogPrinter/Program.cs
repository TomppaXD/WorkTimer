using System;
using System.Collections.Generic;
using System.IO;
using WorkTimer;

namespace WorkLogPrinter
{
    class Program
    {
        static void Main(string[] args)
        {
            var settings = ReadJsonFile<Settings>("settings.json");
            var files = Directory.EnumerateFiles(settings.LogPath);


            foreach (var file in files)
            {
                Console.WriteLine($"{file}: {GetWorkTime(file, settings.InactivityTresholdMinutes)}");
            }

        }

        static string GetWorkTime(string path, int inactivityTreshold)
        {
            var logs = ReadJsonFile<List<LogEntry>>(path);
            double total = 0;
            double inactive = 0;
            LogEntry previous = null;

            foreach (var entry in logs)
            {
                if (previous != null && (entry.Start - previous.End).TotalMinutes > 1)
                {
                    inactive += (entry.Start - previous.End).TotalMinutes;
                }

                if (entry.Type == ActivityType.Active.ToString())
                {
                    var diff = (entry.End - entry.Start).TotalMinutes;
                    if (diff < inactivityTreshold)
                    {
                        total += diff;
                        if (inactive <= inactivityTreshold)
                        {
                            total += inactive;
                        }

                        inactive = 0;
                    }
                }
                else
                {
                    inactive += (entry.End - entry.Start).TotalMinutes;
                }

                previous = entry;
            }

            var elapsedMinutes = (int)Math.Round(total, 0);
            var inactiveMinutes = (int)Math.Round(inactive, 0);

            return GetHours(elapsedMinutes);
        }

        static string GetHours(int totalMinutes)
        {
            var hours = (totalMinutes / 60);
            var minutes = totalMinutes - (hours * 60);
            return $"Workhours: {hours}:{(minutes >= 10 ? minutes.ToString() : "0" + minutes.ToString())  }";
        }

        static T ReadJsonFile<T>(string path)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
    }
}
