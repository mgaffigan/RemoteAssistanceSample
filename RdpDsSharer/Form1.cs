using RDPCOMAPILib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RdpDsSharer
{
    public partial class Form1 : Form
    {
        private RDPSession sess;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            sess = new RDPSession();
            sess.OnAttendeeConnected += (pObjAttendee) =>
            {
                IRDPSRAPIAttendee pAttendee = pObjAttendee as IRDPSRAPIAttendee;
                pAttendee.ControlLevel = CTRL_LEVEL.CTRL_LEVEL_INTERACTIVE;
            };
            sess.Open();
            textBox1.Text = sess.Invitations.CreateInvitation("WinPresenter", "PresentationGroup", "", 5).ConnectionString;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            sess?.Close();
            base.OnClosing(e);
        }
    }
}
