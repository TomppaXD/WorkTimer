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
            return $"Workhours: {hours}:{(minutes >= 10 ? minutes.ToString() : "0" + minutes.ToString())  }";
        }



        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            try
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

                logs.Add(new LogEntry
                {
                    Start = LastUpdate,
                    End = DateTime.Now,
                    Type = CurrentActivityType.ToString()
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
            return Path.Combine(Settings.LogPath, $"{DateTime.Now.ToShortDateString()}.json");
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
                if (!File.Exists(GetCurrentLogPath()))
                {
                    File.Create(GetCurrentLogPath()).Close();
                }

                File.WriteAllText(GetCurrentLogPath(), Newtonsoft.Json.JsonConvert.SerializeObject(logs));
            }
            catch (Exception e)
            {
                throw new Exception("Error when updating logs", e);
            }
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
