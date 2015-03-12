using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetRunnerDBScrapper
{
    public partial class FormDecklist : Form
    {
        public FormDecklist()
        {
            InitializeComponent();
        }

        public void SetText(string text)
        {
            this.textBox1.Text = text.Replace("é","e").Replace("à","a").Replace("'", "`"); //For copy/paste in the latex
        }
    }
}
