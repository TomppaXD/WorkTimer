using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using WorkTimer;

namespace WorkLogPrinter
{
    class Program
    {
        static void Main(string[] args)
        {
            var settings = ReadJsonFile<Settings>("settings.json");
            var files = Directory.EnumerateFiles(settings.LogPath).Where(f => f.Contains("json"));

            Console.WriteLine("Month:");
            var month = Int32.Parse(Console.ReadLine());

            var rows = new List<(DateTime date, string row, int totalMinutes)>();

            foreach (var file in files)
            {
                var date = file.Split('\\').Last().Split(".json").First();
                var datetime = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
                var time = GetWorkTime(file, settings.InactivityTresholdMinutes);
                rows.Add((datetime, $"{date} ({datetime.DayOfWeek}): {time.Item1}", time.Item2));
            }

            rows = rows.Where(r => r.date.Month == month).OrderBy(r => r.date).ToList();

            foreach (var row in rows)
            {
                Console.WriteLine(row.row);
            }
            Console.WriteLine($"Total: {GetHours(rows.Sum(r => r.totalMinutes))}");
        }

        static (string, int) GetWorkTime(string path, int inactivityTreshold)
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

            return (GetHours(elapsedMinutes), elapsedMinutes);
        }

        static string GetHours(int totalMinutes)
        {
            var hours = (totalMinutes / 60);
            var minutes = totalMinutes - (hours * 60);
            return $"Workhours: {hours}:{(minutes >= 10 ? minutes.ToString() : "0" + minutes.ToString())}";
        }

        static T ReadJsonFile<T>(string path)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
    }
}
