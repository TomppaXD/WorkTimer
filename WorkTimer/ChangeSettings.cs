using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WorkTimer
{
    public partial class ChangeSettings : Form
    {
        public ChangeSettings()
        {
            InitializeComponent();

            var settings = Form1.ReadJsonFile<Settings>("settings.json");
            textBox1.Text = settings.LogPath;
            numericUpDown1.Text = settings.InactivityTresholdMinutes.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var setting = new Settings();
            setting.LogPath = textBox1.Text;
            setting.InactivityTresholdMinutes = (int)numericUpDown1.Value;
            string path = "settings.json";
            string content = JsonConvert.SerializeObject(setting);
            File.WriteAllText(path, content);
        }
    }
}
