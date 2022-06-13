using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            CreateMyPanel();
        }

        public void CreateMyPanel()
        {
            Panel panel1 = new Panel();
            panel1.AutoScroll = true;
            TextBox textBox1 = new TextBox();
            textBox1.Width = 700;
            Label label1 = new Label();
            label1.Width = 5000;

            // Initialize the Panel control.
            panel1.Location = new Point(56, 72);
            panel1.Size = new Size(264, 152);
            // Set the Borderstyle for the Panel to three-dimensional.
            panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;

            // Initialize the Label and TextBox controls.
            label1.Location = new Point(16, 16);
            label1.Text = "label1abel1abel1abel1abel1abel1abel1abel1abel1abel1abel1abel1abel1abel1";
            textBox1.Location = new Point(32, 32);
            textBox1.Text = "";

            // Add the Panel control to the form.
            this.Controls.Add(panel1);
            // Add the Label and TextBox controls to the Panel.
            panel1.Controls.Add(label1);
            panel1.Controls.Add(textBox1);
        }
        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
