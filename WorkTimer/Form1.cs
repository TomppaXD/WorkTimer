using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WorkTimer
{

    public partial class Form1 : Form
    {
        private System.Timers.Timer Timer { get; set; }
        Point LastLocation { get; set; } = new Point();

        DateTime LastUpdate { get; set; }

        ActivityType CurrentActivityType { get; set; } = ActivityType.Active;

        Settings Settings { get; set; }

        private string ErrorLogPath => Path.Combine($"{Settings.LogPath}", $"error_{DateTime.Now.Ticks}.txt");

        public delegate void UpdateTextCallback(string text);

        public delegate void InvokeDelegate();

        //TODO: Write to a file, generate history
        /* Inactive minutes:
         * If user is inactive, start collecting inactive minutes
         * If user comes back within maxInactivity, add minutes to worktime. Otherwise, discard
         */
        public Form1()
        {
            try
            {
                InitializeComponent();
                Settings = ReadJsonFile<Settings>("settings.json");
                Timer = new System.Timers.Timer();
                Timer.Interval = 60000;
                Timer.Elapsed += OnTimedEvent;

                Timer.AutoReset = true;
                Timer.Enabled = true;
                this.notifyIcon1.Visible = true;
                notifyIcon1.Tag = "Activity tracker";
                notifyIcon1.Text = "Activity tracker";

                this.Hide();
                this.ShowInTaskbar = false;

                var menu = new System.Windows.Forms.ContextMenuStrip();

                menu.Items.Add("Show", null, this.Show);
                menu.Items.Add("Shut down", null, this.Close);
                this.notifyIcon1.ContextMenuStrip = menu;

                LastUpdate = DateTime.Now;
            }
            catch (Exception e)
            {
                File.WriteAllText(ErrorLogPath, $"Could not start app: {e}");
            }

        }

        private void UpdateText()
        {
            try
            {
                var logs = GetCurrentLog();
                double total = 0;
                double inactive = 0;

                LogEntry previous = null;

                if (logs != null)
                {

                    foreach (var entry in logs)
                    {
                        if (previous != null && (entry.Start - previous.End).TotalMinutes > 1)
                        {
                            inactive += (entry.Start - previous.End).TotalMinutes;
                        }

                        if (entry.Type == ActivityType.Active.ToString())
                        {
                            var diff = (entry.End - entry.Start).TotalMinutes;
                            if (diff < Settings.InactivityTresholdMinutes)
                            {
                                total += diff;
                                if (inactive <= Settings.InactivityTresholdMinutes)
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
                }

                var elapsedMinutes = (int) Math.Round(total, 0);
                var inactiveMinutes = (int) Math.Round(inactive, 0);

                this.label1.Text = GetHours(elapsedMinutes);

                if (inactiveMinutes == 0)
                {
                    label2.Text = $"Status: Active";
                }
                else if (inactiveMinutes < Settings.InactivityTresholdMinutes)
                {
                    label2.Text = $"Status: Active ({inactiveMinutes} minutes inactive)";
                }
                else
                {
                    label2.Text = $"Status: Inactive ({inactiveMinutes} minutes)";
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(ErrorLogPath, $"Error when trying to update label text: {ex}");
            }
        }

        string GetHours(int totalMinutes)
        {
            var hours = (totalMinutes / 60);
            var minutes = totalMinutes - (hours * 60);
            return $"{hours}:{(minutes >= 10 ? minutes.ToString() : "0" + minutes.ToString())  }";
        }

        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                ProcessInfo.GetActiveWindowTitle();
                var position = Cursor.Position;

                if (LastLocation.X != position.X || LastLocation.Y != position.Y)
                {
                    CurrentActivityType = ActivityType.Active;
                }
                else
                {
                    CurrentActivityType = ActivityType.Inactive;
                }

                WriteActivityLog();
                LastLocation = position;

                label2.Invoke(new InvokeDelegate(UpdateText));
            }
            catch (Exception ex)
            {
                File.WriteAllText(ErrorLogPath, $"Error on timed event: {ex}");
            }

        }

        void WriteActivityLog()
        {
            try
            {
                var logs = GetCurrentLog() ?? new List<LogEntry>();

                if ((DateTime.Now - LastUpdate).TotalMinutes > Settings.InactivityTresholdMinutes)
                {
                    logs.Add(new LogEntry
                    {
                        Start = LastUpdate,
                        End = DateTime.Now - new TimeSpan(0, 0, 1, 0),
                        Type = ActivityType.Inactive.ToString()
                    });

                    LastUpdate = DateTime.Now - new TimeSpan(0, 0, 1, 0);
                }

                var openWindow = ProcessInfo.GetActiveWindowTitle();
                logs.Add(new LogEntry
                {
                    Start = LastUpdate,
                    End = DateTime.Now,
                    Type = CurrentActivityType.ToString(),
                    Title = openWindow.Item1,
                    ProcessName = openWindow.Item2
                });

                LastUpdate = DateTime.Now;
                UpdateLogs(logs);
            }
            catch (Exception e)
            {
                throw new Exception("Error when writing activity log", e);
            }
        }

        void Close(object sender, EventArgs e)
        {
            CurrentActivityType = ActivityType.Active;
            WriteActivityLog();
            Timer.Dispose();
            Application.Exit();
        }

        void Show(object sender, EventArgs e)
        {
            this.Show();
            this.ShowInTaskbar = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                this.Hide();
                this.ShowInTaskbar = false;
                e.Cancel = true;
            }

            base.OnFormClosing(e);
        }

        string GetCurrentLogPath()
        {
            return Path.Combine(Settings.LogPath, $"{DateTime.Now.ToString("yyyyMMdd")}.json");
        }

        List<LogEntry> GetCurrentLog()
        {
            try
            {
                var path = GetCurrentLogPath();
                if (!File.Exists(path))
                {
                    return new List<LogEntry>();
                }
                else
                {
                    return ReadJsonFile<List<LogEntry>>(path);
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error when getting logs", e);
            }
        }

        void UpdateLogs(List<LogEntry> logs)
        {
            try
            {
                File.WriteAllText(GetCurrentLogPath(), Newtonsoft.Json.JsonConvert.SerializeObject(logs));
            }
            catch (Exception e)
            {
                throw new Exception("Error when updating logs", e);
            }
        }

        public static T ReadJsonFile<T>(string path)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            UpdateText();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            int month = DateTime.Now.Month;
            int year = DateTime.Now.Year;
            DisplayMonthlyHours(month, year);
        }
        private void button2_Click(object sender, EventArgs e)
        {
            var month = DateTime.Now.Month - 1;
            int year = DateTime.Now.Year;
            if (month == 0)
            {
                month = 12;
                year--;
            }
            DisplayMonthlyHours(month, year);
        }
        private void DisplayMonthlyHours(int month, int year)
        {
            int y = 1;
            Historyform form = new Historyform();
            form.Show();
            form.AutoScroll = true;

            var settings = ReadJsonFile<Settings>("settings.json");
            var files = Directory.EnumerateFiles(settings.LogPath).Where(f => f.Contains("json"));

            //All logs of the month
            var logs = new List<LogEntry>();

            foreach (var file in files)
            {
                var date = file.Split('\\').Last().Replace(".json", "");
                var datetime = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);
                if (datetime.Month != month || datetime.Year != year)
                {
                    continue;
                }
                var dailyLogs = ReadJsonFile<List<LogEntry>>(file);
                logs.AddRange(dailyLogs);
            }

            logs = logs.OrderBy(l => l.Start).ToList();

            for (int i = 1; i < logs.Count; i++)
            {
                if ((logs[i].Start - logs[i - 1].End).TotalMinutes < settings.InactivityTresholdMinutes) // <-- 10 mins
                {
                    logs[i].Start = logs[i - 1].End;
                }
            }
            
            double total = 0;
            foreach (var date in logs.Select(l => l.Start.Date).Distinct())
            {
                //Kaikki lokit, jotka ovat tältä päivältä
                var logsWithDate = logs.Where(l => l.Start.Date == date).ToList();

                double dailyTotal = logsWithDate.Sum(l => (l.End - l.Start).TotalMinutes);

                total += dailyTotal;
                
                var processNames = logsWithDate.Select(l => l.ProcessName).Distinct().ToList();

                // päivä, viikonpäivä, aika
                string text = $"{date.Day}.{date.Month}, {date.DayOfWeek}: {GetHours((int)Math.Round(dailyTotal))}";
                AddLabel(1, y, text, form);
                y++;

                foreach (var processName in OrderProcessNamesByMinutes(processNames, logsWithDate))
                {
                    var logsWithProcessName = logsWithDate.Where(l => l.ProcessName == processName);

                    double processNameTotal = logsWithProcessName.Sum(l => (l.End - l.Start).TotalMinutes);

                    var titles = logsWithProcessName.Select(l => l.Title).Distinct().ToList();

                    // ohjelma, aika
                    string text2 = $"{processName} {GetHours((int)Math.Round(processNameTotal))}";
                    AddLabel(2, y, text2, form);
                    y++;

                    foreach (var title in OrderTitlesByMinutes(processName, titles, logs))
                    {
                        var logsWithTitle = logsWithDate.Where(l => l.Title == title);

                        double titleTotal = logsWithTitle.Sum(l => (l.End - l.Start).TotalMinutes);

                        // otsikko, aika
                        string text3 = $"{title} {GetHours((int)Math.Round(titleTotal))}";
                        AddLabel(3, y, text3, form);
                        y++;
                    }
                }
            }
            AddLabel(1, y, $"Total: {GetHours((int)Math.Round(total))}", form);
        }

        private List<string> OrderTitlesByMinutes(string processName, List<string> titles, List<LogEntry> logs)
        {
            return titles.OrderByDescending(t => logs.Where(l => l.Title == t && l.ProcessName == processName).Sum(l => (l.End - l.Start).TotalMinutes)).ToList();
        }
        private List<string> OrderProcessNamesByMinutes(List<string> processNames, List<LogEntry> logs)
        {
            return processNames.OrderByDescending(p => logs.Where(l => l.ProcessName == p).Sum(l => (l.End - l.Start).TotalMinutes)).ToList();
        }
        private void AddLabel(int x, int y, string text, Historyform form)
        {
            var label = new Label();
            label.Location = new Point(x * 20, y * 25);
            label.Width = 700;
            label.Text = text;
            label.AccessibleName = text;
            form.Controls.Add(label);
        }
        (string, int) GetWorkTime(List<LogEntry> logs, int inactivityTreshold) 
        {
            double total = 0;
            double inactive = 0;
            LogEntry previous = null;

            var timesByProcessAndTitle = new Dictionary<string, Dictionary<string, double>>();

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

                        timesByProcessAndTitle[entry.ProcessName][entry.Title] += diff;

                        if (inactive <= inactivityTreshold)
                        {
                            total += inactive;
                            timesByProcessAndTitle[entry.ProcessName][entry.Title] += inactive;
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

            return (GetHours(elapsedMinutes), elapsedMinutes);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            ChangeSettings settings = new ChangeSettings();
            settings.Show();
        }

        private void monthCalendar1_DateChanged(object sender, DateRangeEventArgs e)
        {
            var daysFromPreviousMonday = ((int)e.Start.DayOfWeek + 6) % 7; // 0 sunnuntai, 1 maanantai jne...
            var currentDate = e.Start.Date;

            var monday = currentDate.AddDays(-daysFromPreviousMonday);
            for (int i = 0; i < 5; i++)
            {
                var date = monday.AddDays(i);
            }
        }
    }
}
