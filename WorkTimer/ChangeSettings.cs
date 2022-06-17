using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Drawing;

namespace WorkTimer
{
    public partial class ChangeSettings : Form
    {
        Panel panels = new Panel();

        private Settings setting;
        public ChangeSettings()
        {
            InitializeComponent();

            setting = Form1.ReadJsonFile<Settings>("settings.json");
            textBox1.Text = setting.LogPath;
            numericUpDown1.Text = setting.InactivityTresholdMinutes.ToString();

            panels.Height = 0;
            panels.AutoSize = true;
            panels.Location = new Point(10, 150);
            this.Controls.Add(panels);

            int y = 1;
            for (int i = 0; i < setting.Categories.Count; i++)
            {
                createPanel(i);
                y++;
            }
            createRowAddingPanel(y);
        }
        private void createRowAddingPanel(int y)
        {
            Panel panel = new Panel();
            panel.Height = 0;
            panel.AutoSize = true;
            panel.Name = y.ToString();

            TextBox processName = new TextBox();
            processName.Width = 100;
            processName.Height = 17;
            processName.Location = new Point(0, 2);
            processName.Name = "processName";

            TextBox category = new TextBox();
            category.Width = 100;
            category.Height = 17;
            category.Location = new Point(103, 2);
            category.Name = "category";

            Button add = new Button();
            add.Text = "Add";
            add.Name = "add";
            add.Location = new Point(210, 0);
            add.AutoSize = true;
            add.Click += (sender, e) => addButton(sender, e);

            panel.Location = new Point(0, 27 * y);
            panel.Controls.Add(processName);
            panel.Controls.Add(category);
            panel.Controls.Add(add);
            panels.Controls.Add(panel);
        }
        private void createPanel(int y)
        {
            Panel panel = new Panel();
            panel.AutoSize = true;
            panel.Height = 0;
            panel.Name = y.ToString();
            panel.Location = new Point(0, 27 * y);

            panel.Controls.Add(createLabel(0, setting.Categories[y].ProcessName));
            panel.Controls.Add(createLabel(100, setting.Categories[y].Category));
            panel.Controls.Add(createDeleteButton(y));

            panels.Controls.Add(panel);
        }
        private Label createLabel(int x, string text)
        {
            Label label = new Label();
            label.Location = new Point(x, 0);
            label.Text = text;
            label.AutoSize = true;

            return label;
        }
        private Button createDeleteButton(int y)
        {
            Button delete = new Button();
            delete.Location = new Point(210, 0);
            delete.Text = "Delete";
            delete.AutoSize = true;
            delete.Click += new EventHandler(deleteButtonClick);

            return delete;
        }
        private void deleteButtonClick(object sender, EventArgs e)
        {
            int y = int.Parse(((Button)sender).Parent.Name);

            setting.Categories.RemoveAt(y);
            updateSettings();
            panels.Controls.RemoveByKey(y.ToString());

            foreach (Control control in panels.Controls)
            {
                if (control is Panel && int.Parse(control.Name) > y)
                {
                    control.Location = new Point(0, control.Location.Y - 27);
                    control.Name = (int.Parse(control.Name) - 1).ToString();
                }
            }
        }
        private void addButton(object sender, EventArgs e)
        {
            var rowAddingPanel = ((Button)sender).Parent;
            int y = int.Parse(rowAddingPanel.Name);
            string processName = "";
            string category = "";
            foreach (Control c in rowAddingPanel.Controls)
            {
                if (c.Name == "processName")
                {
                    processName = c.Text;
                    c.Text = "";
                }
                else if (c.Name == "category")
                {
                    category = c.Text;
                    c.Text = "";
                }
            }

            setting.Categories.Add(new CategorySettings
            {
                Category = category,
                ProcessName = processName
            }
            );
            updateSettings();

            rowAddingPanel.Name = (y + 1).ToString();
            rowAddingPanel.Location = new Point(0, ((Button)sender).Parent.Location.Y + 27);

            createPanel(setting.Categories.Count - 1);
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(textBox1.Text))
            {
                textBox1.Text = "Folder does not exist.";
                return;
            }
            setting.LogPath = textBox1.Text;
            setting.InactivityTresholdMinutes = (int)numericUpDown1.Value;
            updateSettings();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.ShowDialog();
            if (dialog.SelectedPath != "")
            {
                textBox1.Text = dialog.SelectedPath;
            }
        }
        private void updateSettings()
        {
            string content = JsonConvert.SerializeObject(setting);
            File.WriteAllText("settings.json", content);
        }
    }
}