using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
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

        public delegate void UpdateTextCallback(string text);

        public delegate void InvokeDelegate();

        //TODO: Write to a file, generate history
        /* Inactive minutes:
         * If user is inactive, start collecting inactive minutes
         * If user comes back within maxInactivity, add minutes to worktime. Otherwise, discard
         */
        public Form1()
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

        private void UpdateText()
        {
            var logs = GetCurrentLog();
            double total = 0;
            double inactive = 0;

            foreach (var entry in logs)
            {
                if (entry.Type == ActivityType.Active.ToString())
                {
                    total += (entry.End - entry.Start).TotalMinutes;
                    if (inactive <= Settings.InactivityTresholdMinutes)
                    {
                        total += inactive;
                    }
                    inactive = 0;
                }
                else
                {
                    inactive += (entry.End - entry.Start).TotalMinutes;
                }
            }

            var elapsedMinutes = (int)Math.Round(total, 0);
            var inactiveMinutes = (int)Math.Round(inactive, 0);

            this.label1.Text = GetHours(elapsedMinutes);

            if (inactiveMinutes == 0)
            {
                label2.Text = $"Status: Active";
            }
            else if(inactiveMinutes < Settings.InactivityTresholdMinutes)
            {
                label2.Text = $"Status: Active ({inactiveMinutes} minutes inactive)";
            }
            else
            {
                label2.Text = $"Status: Inactive ({inactiveMinutes} minutes)";
            }
        }

        string GetHours(int totalMinutes)
        {
            var hours = (totalMinutes / 60);
            var minutes = totalMinutes - (hours * 60);
            return $"Workhours: {hours}:{(minutes >= 10 ? minutes.ToString() : "0" + minutes.ToString())  }";
        }



        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {

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

        void WriteActivityLog()
        {
            var logs = GetCurrentLog();

            logs.Add(new LogEntry
            {
                Start = LastUpdate,
                End = DateTime.Now,
                Type = CurrentActivityType.ToString()
            });

            LastUpdate = DateTime.Now;
            UpdateLogs(logs);
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
            return Path.Combine(Settings.LogPath, $"{DateTime.Now.ToShortDateString()}.json");
        }

        List<LogEntry> GetCurrentLog()
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

        void UpdateLogs(List<LogEntry> logs)
        {
            File.WriteAllText(GetCurrentLogPath(), Newtonsoft.Json.JsonConvert.SerializeObject(logs));
        }

        T ReadJsonFile<T>(string path){
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            UpdateText();
        }
    }
}
