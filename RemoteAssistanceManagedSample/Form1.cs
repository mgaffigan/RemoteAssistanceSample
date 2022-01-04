using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteAssistanceManagedSample
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var novice = new RASessionImpl(RENDEZVOUS_SESSION_FLAGS.RSF_INVITER);
            var expert = new RASessionImpl(RENDEZVOUS_SESSION_FLAGS.RSF_INVITEE);
            novice.Partner = expert;
            expert.Partner = novice;
            novice.Start();
            expert.Start();
        }
    }

    [ComSourceInterfaces(typeof(DRendezvousSessionEvents))]
    class RASessionImpl : IRendezvousSession
    {
        private IRendezvousApplication App;
        public RASessionImpl Partner;

        public event OnStateChangedEvent OnStateChanged;
        public event OnTerminationEvent OnTermination;
        public event OnContextDataEvent OnContextData;
        public event OnSendErrorEvent OnSendError;

        public RASessionImpl(RENDEZVOUS_SESSION_FLAGS flags)
        {
            this.Flags = flags;
        }

        public void Start()
        {
            this.State = RENDEZVOUS_SESSION_STATE.RSS_CONNECTED;

            this.App = (IRendezvousApplication)new RendezvousApplication();
            this.App.SetRendezvousSession(this);
        }

        public string RemoteUser => $"ID {GetHashCode()} flags {Flags}";

        public RENDEZVOUS_SESSION_STATE State { get; private set; }
        public RENDEZVOUS_SESSION_FLAGS Flags { get; private set; }

        public void SendContextData([In, MarshalAs(UnmanagedType.BStr)] string bstrData)
        {
            Partner?.OnContextData(bstrData);
        }

        public void Terminate([In, MarshalAs(UnmanagedType.Error)] int hr, [In, MarshalAs(UnmanagedType.BStr)] string bstrAppData)
        {
            Partner?.OnTermination(hr, bstrAppData);
        }
    }

#pragma warning disable IDE1006 // Naming Styles

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("9BA4B1DD-8B0C-48B7-9E7C-2F25857C8DF5")]
    public interface IRendezvousSession
    {
        RENDEZVOUS_SESSION_STATE State { get; }
        string RemoteUser { [return: MarshalAs(UnmanagedType.BStr)] get; }
        RENDEZVOUS_SESSION_FLAGS Flags { get; }
        void SendContextData([In, MarshalAs(UnmanagedType.BStr)] string bstrData);
        void Terminate([In, MarshalAs(UnmanagedType.Error)] int hr, [In, MarshalAs(UnmanagedType.BStr)] string bstrAppData);
    }

    public delegate void OnStateChangedEvent(RENDEZVOUS_SESSION_STATE prevState);
    public delegate void OnTerminationEvent(int lReturnCode, [MarshalAs(UnmanagedType.BStr)] string bstrData);
    public delegate void OnContextDataEvent([MarshalAs(UnmanagedType.BStr)] string bstrData);
    public delegate void OnSendErrorEvent(int lReturnCode);

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIDispatch), Guid("3FA19CF8-64C4-4F53-AE60-635B3806ECA6")]
    public interface DRendezvousSessionEvents
    {
        [DispId(5)]
        void OnStateChanged(RENDEZVOUS_SESSION_STATE prevState);
        [DispId(6)]
        void OnTermination(int lReturnCode, [MarshalAs(UnmanagedType.BStr)] string bstrData);
        [DispId(7)]
        void OnContextData([MarshalAs(UnmanagedType.BStr)] string bstrData);
        [DispId(8)]
        void OnSendError(int lReturnCode);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4F4D070B-A275-49FB-B10D-8EC26387B50D")]
    public interface IRendezvousApplication
    {
        void SetRendezvousSession([In, MarshalAs(UnmanagedType.IUnknown)] object pRendezvousSession);
    }

    [ComImport, ClassInterface(ClassInterfaceType.None), Guid("0B7E019A-B5DE-47FA-8966-9082F82FB192")]
    public class RendezvousApplication
    {
    }

    public enum RENDEZVOUS_SESSION_STATE
    {
        RSS_UNKNOWN,
        RSS_READY,
        RSS_INVITATION,
        RSS_ACCEPTED,
        RSS_CONNECTED,
        RSS_CANCELLED,
        RSS_DECLINED,
        RSS_TERMINATED
    }

    public enum RENDEZVOUS_SESSION_FLAGS
    {
        RSF_INVITEE = 2,
        RSF_INVITER = 1,
        RSF_NONE = 0,
        RSF_ORIGINAL_INVITER = 4,
        RSF_REMOTE_LEGACYSESSION = 8,
        RSF_REMOTE_WIN7SESSION = 0x10
    }

#pragma warning restore IDE1006 // Naming Styles
}
