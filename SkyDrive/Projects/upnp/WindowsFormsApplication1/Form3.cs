using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApplication1
{
    public partial class RegisterForm : Form
    {
        public string returnvalue{ get; set; }

        public RegisterForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            returnvalue = RegisterPin.Text;

            this.Close();
        }
    }
}
