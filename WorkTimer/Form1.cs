using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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
        
        public delegate void UpdateTextCallback(string text);

        public delegate void InvokeDelegate();

        public List<Form> openForms = new List<Form>();
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
                MessageBox.Show($"Could not start app: {e}", e.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                var elapsedMinutes = (int)Math.Round(total, 0);
                var inactiveMinutes = (int)Math.Round(inactive, 0);

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
                MessageBox.Show($"Error when trying to update label text: {ex}", ex.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        string GetHours(int totalMinutes)
        {
            var hours = (totalMinutes / 60);
            var minutes = totalMinutes - (hours * 60);
            return $"{hours}:{(minutes >= 10 ? minutes.ToString() : "0" + minutes.ToString())}";
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
                MessageBox.Show($"Error on timed event: {ex}", ex.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show($"Error when writing activity log: {e}", e.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                CloseForms();
            }

            base.OnFormClosing(e);
        }

        private void CloseForms()
        {
            foreach (Form form in openForms)
            {
                form.Dispose();
            }
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
                MessageBox.Show($"Error when updating logs: {e}", e.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        private double DisplayDailyHours(DateTime day, HoursOfWeek form)
        {
            int y = 1;

            
            var files = Directory.EnumerateFiles(Settings.LogPath).Where(f => f.Contains("json"));


            var logs = GetLogsByDay(files, day, Settings);
            logs = logs.OrderBy(l => l.Start).ToList();

            // filling the gaps between logs
            logs = FillingTheGaps(logs, Settings);


            var logsOfMonth = GetLogsByMonth(files, day, Settings);
            logsOfMonth = logsOfMonth.OrderBy(l => l.Start).ToList();

            double hoursOfMonth = 0;

            for (int i = 1; i < logsOfMonth.Count; i++)
            {
                if ((logsOfMonth[i].Start - logsOfMonth[i - 1].End).TotalMinutes < Settings.InactivityTresholdMinutes)
                {
                    logsOfMonth[i].Start = logsOfMonth[i - 1].End;
                }
            }
            hoursOfMonth = logsOfMonth.Sum(l => (l.End - l.Start).TotalMinutes);
            form.label7.Text = $"Total hours of this month {GetHours((int)Math.Round(hoursOfMonth))}";


            double total = 0;

            var text = new List<(string, string)>();

            foreach (var date in logs.Select(l => l.Start.Date).Distinct())
            {
                //Kaikki lokit, jotka ovat tältä päivältä
                var logsWithDate = logs.Where(l => l.Start.Date == date).ToList();

                double dailyTotal = logsWithDate.Sum(l => (l.End - l.Start).TotalMinutes);

                total += dailyTotal;

                var processNames = logsWithDate.Select(l => l.ProcessName).Distinct().ToList();

                // päivä, viikonpäivä, aika
                text.Add(($"{date.Day}.{date.Month}, {date.DayOfWeek}: {GetHours((int)Math.Round(dailyTotal))}", "b"));

                y++;

                foreach (var processName in OrderProcessNamesByMinutes(processNames, logsWithDate))
                {
                    var logsWithProcessName = logsWithDate.Where(l => l.ProcessName == processName);

                    double processNameTotal = logsWithProcessName.Sum(l => (l.End - l.Start).TotalMinutes);

                    var titles = logsWithProcessName.Select(l => l.Title).Distinct().ToList();

                    // ohjelma, aika
                    text.Add(($"   {processName} {GetHours((int)Math.Round(processNameTotal))}", "b"));
                    y++;

                    foreach (var title in OrderTitlesByMinutes(processName, titles, logs))
                    {
                        var logsWithTitle = logsWithDate.Where(l => l.Title == title);

                        double titleTotal = logsWithTitle.Sum(l => (l.End - l.Start).TotalMinutes);

                        // otsikko, aika
                        text.Add(($"      {title} {GetHours((int)Math.Round(titleTotal))}", ""));

                        y++;
                    }
                }
            }
            text.Add(($"Total: {GetHours((int)Math.Round(total))}", "b"));

            var dayOfWeek = (int)day.DayOfWeek;
            for (int i = 0; i < text.Count; i++)
            {
                AddLabel(text[i].Item1, i, dayOfWeek, form, text[i].Item2);
            }
            return total;
        }
        private List<LogEntry> GetLogsByDay(IEnumerable<string> files, DateTime day, Settings settings)
        {
            var logs = new List<LogEntry>();
            foreach (var file in files)
            {
                var datetime = GetDateTime(file);
                if (datetime.Date == day.Date)
                {
                    var dailyLogs = ReadJsonFile<List<LogEntry>>(file);
                    if (dailyLogs == null)
                    {
                        continue;
                    }

                    foreach (var dailyLog in dailyLogs)
                    {
                        foreach (var category in settings.Categories)
                        {
                            if (category.ProcessName == dailyLog.ProcessName)
                            {
                                dailyLog.ProcessName = category.Category;
                            }
                        }
                    }
                    logs.AddRange(dailyLogs);
                }
            }
            return logs;
        }
        private List<LogEntry> GetLogsByMonth(IEnumerable<string> files, DateTime day, Settings settings)
        {
            var logsOfMonth = new List<LogEntry>();
            foreach (var file in files)
            {
                var datetime = GetDateTime(file);
                if (datetime.Month == day.Month && datetime.Year == day.Year)
                {
                    var monthlyLogs = ReadJsonFile<List<LogEntry>>(file);
                    if (monthlyLogs == null)
                    {
                        continue;
                    }
                    logsOfMonth.AddRange(monthlyLogs);
                }
            }
            return logsOfMonth;
        }
        private DateTime GetDateTime(string file)
        {
            var date = file.Split('\\').Last().Replace(".json", "");
            var datetime = DateTime.ParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture);

            return datetime;
        }
        private List<LogEntry> FillingTheGaps(List<LogEntry> logs, Settings settings)
        {
            int indexOfLast = 0;
            for (int i = 0; i < logs.Count; i++)
            {
                var logsWithinTreshold = new List<LogEntry>();
                logsWithinTreshold.Add(logs[i]);

                var logsWithDateAndProcessName = logs.Where(l => l.ProcessName == logs[i].ProcessName && l.Start >= logs[i].Start).ToList();
                for (int j = 1; j < logsWithDateAndProcessName.Count; j++)
                {
                    if ((logsWithDateAndProcessName[j].Start - logsWithDateAndProcessName[j - 1].End).TotalMinutes < 10)
                    {
                        logsWithinTreshold.Add(logs[j]);
                    }
                    else
                    {
                        break;
                    }
                }

                double minutes = logsWithinTreshold.Sum(l => (l.End - l.Start).TotalMinutes);
                indexOfLast = logs.IndexOf(logsWithinTreshold[logsWithinTreshold.Count - 1]);

                var gap = new List<LogEntry>();
                for (int j = i + 1; j < indexOfLast; j++)
                {
                    if (logs[j].ProcessName == logs[i].ProcessName)
                    {
                        continue;
                    }
                    foreach (var log in logs.Skip(j))
                    {
                        if (log.ProcessName == logs[i].ProcessName)
                        {
                            break;
                        }
                        gap.Add(log);
                    }
                    if (gap.Sum(l => (l.End - l.Start).TotalMinutes) <= minutes * 0.1)
                    {
                        foreach (var log in gap)
                        {
                            log.ProcessName = logs[i].ProcessName;
                        }
                    }
                }
            }

            for (int i = 1; i < logs.Count; i++)
            {
                if ((logs[i].Start - logs[i - 1].End).TotalMinutes < settings.InactivityTresholdMinutes)
                {
                    logs[i].Start = logs[i - 1].End;
                }
            }
            return logs;
        }
        private void AddLabel(string text, int y, int dayOfWeek, HoursOfWeek form, string fontStyle)
        {
            Label label = new Label();
            if (fontStyle == "b")
            {
                label.Font = new Font(label.Font, FontStyle.Bold);
            }
            label.Text = text;
            label.AutoSize = true;
            label.Location = new Point(0, y * 16);
            if (dayOfWeek == 1)
            {
                form.panel1.Controls.Add(label);
            }
            else if (dayOfWeek == 2)
            {
                form.panel2.Controls.Add(label);
            }
            else if (dayOfWeek == 3)
            {
                form.panel3.Controls.Add(label);
            }
            else if (dayOfWeek == 4)
            {
                form.panel4.Controls.Add(label);
            }
            else if (dayOfWeek == 5)
            {
                form.panel5.Controls.Add(label);
            }
        }
        private List<string> OrderTitlesByMinutes(string processName, List<string> titles, List<LogEntry> logs)
        {
            return titles.OrderByDescending(t => logs.Where(l => l.Title == t && l.ProcessName == processName).Sum(l => (l.End - l.Start).TotalMinutes)).ToList();
        }
        private List<string> OrderProcessNamesByMinutes(List<string> processNames, List<LogEntry> logs)
        {
            return processNames.OrderByDescending(p => logs.Where(l => l.ProcessName == p).Sum(l => (l.End - l.Start).TotalMinutes)).ToList();
        }
        private void button3_Click(object sender, EventArgs e)
        {
            ChangeSettings settings = new ChangeSettings();
            settings.Show();
            openForms.Add(settings);
        }
        private void monthCalendar1_DateChanged(object sender, DateRangeEventArgs e)
        {
            var daysFromPreviousMonday = ((int)e.Start.DayOfWeek + 6) % 7; // 0 sunnuntai, 1 maanantai jne...
            var currentDate = e.Start.Date;
            
            HoursOfWeek form = new HoursOfWeek();
            form.Show();
            openForms.Add(form);

            double totalOfWeek = 0;
            var monday = currentDate.AddDays(-daysFromPreviousMonday);
            
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var date = monday.AddDays(i);
                    if (date.Month != currentDate.Month || date.Year != currentDate.Year)
                    {
                        continue;
                    }

                    if ((int)date.DayOfWeek == 1)
                    {
                        form.button1.Click += new EventHandler((sender2, e2) => buttonClick(sender2, e2, date));
                    }
                    else if ((int)date.DayOfWeek == 2)
                    {
                        form.button2.Click += new EventHandler((sender2, e2) => buttonClick(sender2, e2, date));
                    }
                    else if ((int)date.DayOfWeek == 3)
                    {
                        form.button3.Click += new EventHandler((sender2, e2) => buttonClick(sender2, e2, date));
                    }
                    else if ((int)date.DayOfWeek == 4)
                    {
                        form.button4.Click += new EventHandler((sender2, e2) => buttonClick(sender2, e2, date));
                    }
                    else if ((int)date.DayOfWeek == 5)
                    {
                        form.button5.Click += new EventHandler((sender2, e2) => buttonClick(sender2, e2, date));
                    }
                    totalOfWeek += DisplayDailyHours(date, form);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error when trying to display hours: {ex}", ex.ToString(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            form.label6.Text += GetHours((int)Math.Round(totalOfWeek)).ToString();
        }
        private void buttonClick(object sender, EventArgs e, DateTime date)
        {

            var files = Directory.EnumerateFiles(Settings.LogPath).Where(f => f.Contains("json"));

            var logs = GetLogsByDay(files, date, Settings);
            logs = logs.OrderBy(l => l.Start).ToList();
            logs = FillingTheGaps(logs, Settings);

            HoursOfDay form = new HoursOfDay();
            form.Show();

            openForms.Add(form);

            if (logs.Count == 0)
            {
                return;
            }

            string previousProcessName = "";

            var rows = new List<(string processName, string title, DateTime start, DateTime end)>();

            DateTime start = new DateTime();
            DateTime end = new DateTime();

            string startMinutes = "";
            string endMinutes = "";

            var titles = new Dictionary<string, List<LogEntry>>();
            int previousTotal = 0;
            string mostMinutesTitle = "";

            for (int i = 0; i < logs.Count; i++)
            {
                if (previousProcessName == "")
                {
                    start = logs[i].Start;
                }
                else if (previousProcessName != logs[i].ProcessName || (logs[i].Start - logs[i - 1].End).TotalMinutes > Settings.InactivityTresholdMinutes)
                {
                    end = logs[i - 1].End;
                    previousTotal = 0;
                    foreach (var titleLogs in titles)
                    {
                        int titleTotal = (int)titleLogs.Value.Sum(l => (l.End - l.Start).TotalMinutes);
                        if (titleTotal > previousTotal)
                        {
                            previousTotal = titleTotal;
                            mostMinutesTitle = titleLogs.Key;
                        }
                    }

                    rows.Add((previousProcessName, mostMinutesTitle, start, end));
                    start = logs[i].Start;
                    titles = new Dictionary<string, List<LogEntry>>();
                }

                if (!titles.ContainsKey(logs[i].Title))
                {
                    titles[logs[i].Title] = new List<LogEntry>();
                }
                titles[logs[i].Title].Add(logs[i]);
                previousProcessName = logs[i].ProcessName;
                mostMinutesTitle = logs[i].Title;
                end = logs[i].End;
            }
            end = logs[logs.Count - 1].End;

            rows.Add((previousProcessName, mostMinutesTitle, start, end));
            form.Text = $"{date.Day}.{date.Month} {date.DayOfWeek}";

            double timeMultiplier = 0;
            int previousY = 0;
            int defaultGap = 40;

            Panel panel = new Panel();
            panel.Location = new Point(20, 0);

            panel.AutoSize = true;

            form.Controls.Add(panel);

            form.AutoScroll = true;

            string previousEndTime = "";
            Random random = new Random();

            var lines = new List<(Point start, Point end, Color color, string processName)>();

            for (int i = 0; i < rows.Count; i++)
            {
                startMinutes = rows[i].start.Minute >= 10 ? rows[i].start.Minute.ToString() : "0" + rows[i].start.Minute.ToString();
                endMinutes = rows[i].end.Minute >= 10 ? rows[i].end.Minute.ToString() : "0" + rows[i].end.Minute.ToString();

                if (previousEndTime != $"{rows[i].start.Hour}:{startMinutes}")
                {
                    previousY += 20;

                    Label startLabel = new Label();
                    startLabel.AutoSize = true;
                    startLabel.Location = new Point(10, previousY);
                    startLabel.Text = $"{rows[i].start.Hour}:{startMinutes}";

                    panel.Controls.Add(startLabel);
                }
                previousEndTime = $"{rows[i].end.Hour}:{endMinutes}";
                Point startPoint = new Point(5, previousY + 8);

                Label processNameLabel = new Label();
                processNameLabel.Text = $"{rows[i].processName} ( {rows[i].title} )";
                processNameLabel.AutoSize = true;

                Label endLabel = new Label();
                endLabel.Text = $"{rows[i].end.Hour}:{endMinutes}";
                endLabel.AutoSize = true;
                
                if ((rows[i].end - rows[i].start).TotalMinutes <= 5)
                {
                    endLabel.Location = new Point(10, previousY + defaultGap);
                    processNameLabel.Location = new Point(20, defaultGap / 2 + previousY);
                    previousY += defaultGap;
                }
                else
                {
                    int totalMinutes = (int)(rows[i].end - rows[i].start).TotalMinutes;
                    timeMultiplier = (2 * Math.Sqrt(840 * totalMinutes - 4175) + 270) / 7;
                    endLabel.Location = new Point(10, previousY + (int)timeMultiplier);
                    processNameLabel.Location = new Point(20, (int)timeMultiplier / 2 + previousY);
                    previousY += (int)timeMultiplier;
                }
                panel.Controls.Add(endLabel);
                panel.Controls.Add(processNameLabel);

                Point endPoint = new Point(5, previousY + 8);

                bool found = false;
                Color c = new Color();
                foreach (var category in Settings.Categories)
                {
                    if (category.Category == rows[i].processName)
                    {
                        found = true;
                        if (!category.Color.IsEmpty)
                        {
                            c = category.Color;
                            break;
                        }
                        c = Color.FromArgb(random.Next(20, 236), random.Next(20, 236), random.Next(20, 236));
                        category.Color = c;
                        updateSettings();
                        break;
                    }
                }
                if (!found)
                {
                    c = Color.FromArgb(random.Next(20, 236), random.Next(20, 236), random.Next(20, 236));
                }

                lines.Add((startPoint, endPoint, c, rows[i].processName));
            }
            panel.Paint += new PaintEventHandler((sender2, e2) => PaintLines(sender2, e2, lines));
            Label footer = new Label();
            footer.Location = new Point(10, previousY + 16);
            panel.Controls.Add(footer);
            form.Width = panel.Width + 60;
        }
        private void PaintLines(object sender, PaintEventArgs e, List<(Point start, Point end, Color color, string processName)> lines)
        {
            Settings = ReadJsonFile<Settings>("settings.json");
            Graphics g = e.Graphics;
            foreach (var line in lines)
            {
                Color c = line.color;
                foreach (var category in Settings.Categories)
                {
                    if (category.Category == line.processName)
                    {
                        c = category.Color;
                    }
                }
                Pen pen = new Pen(c, 5);
                g.DrawLine(pen, line.start.X, line.start.Y, line.end.X, line.end.Y);
            }
        }
        private void updateSettings()
        {
            string content = JsonConvert.SerializeObject(Settings);
            File.WriteAllText("settings.json", content);
        }
    }
}