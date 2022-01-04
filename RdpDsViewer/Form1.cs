using RDPCOMAPILib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RdpDsViewer
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
            pRdpViewer.Connect(textBox1.Text, "Viewer1", "");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var sharerStream = new RdsDsTransportStream("Sharer");
            var viewerStream = new RdsDsTransportStream("Viewer");
            sharerStream.Partner = viewerStream;
            viewerStream.Partner = sharerStream;

            sess = new RDPSession();
            sess.OnAttendeeConnected += (pObjAttendee) =>
            {
                IRDPSRAPIAttendee pAttendee = pObjAttendee as IRDPSRAPIAttendee;
                pAttendee.ControlLevel = CTRL_LEVEL.CTRL_LEVEL_INTERACTIVE;
            };
            sess.Open();
            sess.ConnectUsingTransportStream(sharerStream, "PresentationGroup", "Viewer1");

            textBox1.Text = sess.Invitations.CreateInvitation("WinPresenter", "PresentationGroup", "", 5).ConnectionString;

            pRdpViewer.OnConnectionTerminated += this.PRdpViewer_OnConnectionTerminated;
            pRdpViewer.OnChannelDataReceived += this.PRdpViewer_OnChannelDataReceived;
            pRdpViewer.OnConnectionFailed += this.PRdpViewer_OnConnectionFailed;
            pRdpViewer.OnError += this.PRdpViewer_OnError;
            pRdpViewer.Properties["SetNetworkStream"] = viewerStream;
            pRdpViewer.Connect(textBox1.Text, "Viewer1", "");

        }

        private void PRdpViewer_OnConnectionTerminated(object sender, AxRDPCOMAPILib._IRDPSessionEvents_OnConnectionTerminatedEvent e)
        {
        }

        private void PRdpViewer_OnChannelDataReceived(object sender, AxRDPCOMAPILib._IRDPSessionEvents_OnChannelDataReceivedEvent e)
        {
        }

        private void PRdpViewer_OnConnectionFailed(object sender, EventArgs e)
        {
        }

        private void PRdpViewer_OnError(object sender, AxRDPCOMAPILib._IRDPSessionEvents_OnErrorEvent e)
        {
        }
    }

    class HGlobalBuffer : RDPTransportStreamBuffer, IDisposable
    {
        public HGlobalBuffer(int maxSize)
        {
            this.StorageSize = maxSize;
            this.Storage = Marshal.AllocHGlobal(maxSize);
        }

        ~HGlobalBuffer() { Dispose(disposing: false); }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Storage == IntPtr.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(Storage);
            Storage = IntPtr.Zero;
        }

        private void AssertAlive()
        {
            if (Storage == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(HGlobalBuffer));
            }
        }

        public IntPtr Storage { get; private set; }
        public int StorageSize { get; }

        public int PayloadSize { get; set; }
        public int PayloadOffset { get; set; }
        public int Flags { get; set; }
        public object Context { get; set; }

        [DllImport("kernel32.dll")]
        static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

        // Copy as many bytes as possible from srcbuf, updating PayloadSize and PayloadOffset
        internal int CopyFrom(IntPtr srcBuf, int count)
        {
            if (PayloadOffset != 0 || PayloadSize > StorageSize)
            {
                throw new InvalidOperationException($"{PayloadOffset}:{PayloadSize}");
            }

            var tail = 0; //PayloadOffset + PayloadSize;
            var availableBytes = PayloadSize - tail;
            availableBytes = Math.Min(availableBytes, count);
            if (availableBytes <= 0)
            {
                throw new InvalidOperationException("No space available");
            }

            CopyMemory(Storage + tail, srcBuf, (uint)availableBytes);
            //PayloadSize += availableBytes;
            PayloadSize = availableBytes;

            return availableBytes;
        }
    }

    class ThreadAssert
    {
        private readonly Thread InitialThread = Thread.CurrentThread;
        public void Assert()
        {
            if (InitialThread != Thread.CurrentThread)
            {
                throw new InvalidOperationException();
            }
        }
        public void AssertBackground()
        {
            if (InitialThread == Thread.CurrentThread)
            {
                throw new InvalidOperationException();
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int CallbackFunc(IntPtr pParam);

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000001da-0000-0000-C000-000000000046")]
    interface IContextCallback
    {
        void ContextCallback(
            IntPtr callback, IntPtr pParam,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, int method, object pUnk
        );
    }

    class ContextCallback
    {
        private static readonly Guid
            IID_ICallbackWithNoReentrancyToApplicationSTA = new Guid("{0a299774-3e4e-fc42-1d9d-72cee105ca57}");
        private const int
            IID_ICallbackWithNoReentrancyToApplicationSTA_MethodIndex = 5;

        [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public static extern object CoGetObjectContext([In, MarshalAs(UnmanagedType.LPStruct)] Guid riid);

        private readonly IContextCallback context;

        public ContextCallback()
        {
            this.context = (IContextCallback)CoGetObjectContext(typeof(IContextCallback).GUID);
        }

        public void Invoke(Action action)
        {
            var thunk = new Thunk(action);
            context.ContextCallback(thunk.DelegatePtr, IntPtr.Zero,
                IID_ICallbackWithNoReentrancyToApplicationSTA,
                IID_ICallbackWithNoReentrancyToApplicationSTA_MethodIndex,
                null);
        }

        class Thunk
        {
            private readonly Action Actor;
            private readonly GCHandle Handle;
            public CallbackFunc Delegate { get; }
            public IntPtr DelegatePtr { get; }

            public Thunk(Action action)
            {
                this.Actor = action;
                this.Handle = GCHandle.Alloc(this);
                this.Delegate = new CallbackFunc(this.ComCallback);
                this.DelegatePtr = Marshal.GetFunctionPointerForDelegate(this.Delegate);
            }

            private int ComCallback(IntPtr pArg)
            {
                try
                {
                    Actor();
                }
                finally
                {
                    Handle.Free();
                }
                return 0 /* S_OK */;
            }
        }
    }

    class RdsDsTransportStream : IRDPSRAPITransportStream
    {
        public RdsDsTransportStream Partner;

        private object syncRead = new object();
        private HGlobalBuffer readBuff;
        private ContextCallback SyncCtx;
        private BlockingCollection<PendingWrite> Writes = new BlockingCollection<PendingWrite>();
        private readonly Thread thRead;

        public RdsDsTransportStream(string v)
        {
            this.Name = v;
            this.thRead = new Thread(thRead_Main);
            this.thRead.IsBackground = true;
            this.thRead.Name = $"{v} reader";
            this.thRead.Start();
        }

        private void thRead_Main()
        {
            foreach (var wr in Writes.GetConsumingEnumerable())
            {
                var srcBuf = wr.SrcBuf;
                //Debug.WriteLine($"{wr.Writer.Name}: Write Started   {srcBuf.PayloadOffset:0000}:{srcBuf.PayloadSize:00000} flags {srcBuf.Flags} buffer {srcBuf.GetHashCode():x8}");

                for (int offset = srcBuf.PayloadOffset, size = srcBuf.PayloadSize; size > 0;)
                {
                    HGlobalBuffer destBuff;
                    lock (syncRead)
                    {
                        while (readBuff == null)
                        {
                            //Debug.WriteLine($"{Name}: Read  Wait");
                            Monitor.Wait(syncRead);
                        }

                        destBuff = readBuff;
                        readBuff = null;
                    }

                    var copied = destBuff.CopyFrom(srcBuf.Storage + offset, size);
                    offset += copied;
                    size -= copied;

                    SyncCtx.Invoke(() =>
                    {
                        //Debug.WriteLine($"{Name}: Read  Completed {destBuff.PayloadOffset:0000}:{destBuff.PayloadSize:00000} flags {destBuff.Flags} buffer {destBuff.GetHashCode():x8}");
                        events.OnReadCompleted(destBuff);
                    });
                }

                wr.Complete();
            }
        }

        private class PendingWrite
        {
            public RdsDsTransportStream Writer { get; }
            public RDPTransportStreamBuffer SrcBuf { get; }

            public PendingWrite(RdsDsTransportStream writerStream, RDPTransportStreamBuffer srcBuf)
            {
                this.Writer = writerStream;
                this.SrcBuf = srcBuf;

                //Debug.WriteLine($"{Writer.Name}: Write Queued    {srcBuf.PayloadOffset:0000}:{srcBuf.PayloadSize:00000} flags {srcBuf.Flags} buffer {srcBuf.GetHashCode():x8}");
            }

            internal void Complete()
            {
                Writer.SyncCtx.Invoke(() =>
                {
                    //Debug.WriteLine($"{Writer.Name}: Write Completed {SrcBuf.PayloadOffset:0000}:{SrcBuf.PayloadSize:00000} flags {SrcBuf.Flags} buffer {SrcBuf.GetHashCode():x8}");
                    Writer.events.OnWriteCompleted(SrcBuf);
                });
            }
        }

        RDPTransportStreamBuffer IRDPSRAPITransportStream.AllocBuffer(int maxPayload)
        {
            ////Debug.WriteLine($"{Name}: Alloc {maxPayload}");
            return new HGlobalBuffer(maxPayload);
        }

        void IRDPSRAPITransportStream.FreeBuffer(RDPTransportStreamBuffer pBuffer)
        {
            ////Debug.WriteLine($"{Name}: Free  {pBuffer.StorageSize}");
            var buff = (HGlobalBuffer)pBuffer;
            buff.Dispose();
        }

        void IRDPSRAPITransportStream.WriteBuffer(RDPTransportStreamBuffer pBuffer)
        {
            Partner.Writes.Add(new PendingWrite(this, pBuffer));
        }

        void IRDPSRAPITransportStream.ReadBuffer(RDPTransportStreamBuffer pBuffer)
        {
            //Debug.WriteLine($"{Name}: Read  Started   {pBuffer.PayloadOffset:0000}:{pBuffer.PayloadSize:00000} flags {pBuffer.Flags} buffer {pBuffer.GetHashCode():x8}");

            lock (syncRead)
            {
                if (readBuff != null)
                {
                    throw new NotSupportedException();
                }

                readBuff = (HGlobalBuffer)pBuffer;
                Monitor.PulseAll(syncRead);
            }
        }

        IRDPSRAPITransportStreamEvents events;
        void IRDPSRAPITransportStream.Open(RDPTransportStreamEvents pCallbacks)
        {
            SyncCtx = new ContextCallback();
            events = (IRDPSRAPITransportStreamEvents)pCallbacks;
        }

        void IRDPSRAPITransportStream.Close()
        {
            //Debug.WriteLine($"{Name}: Closed");
            Partner.OnClose();
        }

        private void OnClose()
        {
            SyncCtx.Invoke(() =>
            {
                events.OnStreamClosed(0 /* S_OK */);
            });
        }

        public string Name { get; }
    }
}
