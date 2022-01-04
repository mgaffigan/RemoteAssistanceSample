# Windows Desktop Sharing Samples
Examples showing use of [Windows Desktop Sharing](https://docs.microsoft.com/en-us/windows/win32/api/_rdp/).

## Samples from MSDN
Taken from [Writing a Desktop Sharing Application - Remote Desktop Services Blog](http://web.archive.org/web/20150301182902/http://blogs.msdn.com/b/rds/archive/2007/03/23/writing-a-desktop-sharing-application.aspx) ([Alternate Link](https://techcommunity.microsoft.com/t5/security-compliance-and-identity/writing-a-desktop-sharing-application/ba-p/246500).

Useful Q&A found at [Windows Desktop Sharing API - Remote Desktop Services Blog](http://web.archive.org/web/20151105203812/http://blogs.msdn.com/b/rds/archive/2007/03/08/windows-desktop-sharing-api.aspx) ([Alternate Link](https://techcommunity.microsoft.com/t5/security-compliance-and-identity/windows-desktop-sharing-api/ba-p/246494))

## Samples showing use of `IRDPSRAPITransportStream`

`RdpDsViewer` provides an example of a pluggable transport being used.  The example only sends the data in-process, but the same could occur across the network.

One notable aspect is that calls to `IRDPSRAPITransportStreamEvents` must take place either during a call to a 
method of `IRDPSRAPITransportStream` or with the COM Context captured during a call to `IRDPSRAPITransportStream` 
(usually `Open`).  Normal COM Marshalling is insufficient.  The UI STA thread is unacceptable.  Calls from the 
wrong thread will result in `E_FAIL` being returned by `QueryInterface` on a call to `IRDPSRAPITransportStreamEvents`.

Apparent semantics observed:
 - Calls to `IRDPSRAPITransportStream` come from MTA worker thread run by RDP dll
 - Calls to `IRDPSRAPITransportStreamEvents`
   - Succeed when on the `IRDPSRAPITransportStream` worker thread
   - Succeed when invoking the COM Context captured from the RDP Worker thread (which will not invoke back to the worker thread - but a new one)
   - Fail from the UI STA Thread
   - Fail from the `ThreadPool`
   - Fail from a new background STA Thread
   - Fail from a new background MTA Thread
 - Reads are serialized
 - Writes are concurrent, but must be delivered in order
 - Close is asynchronous and may occur during a read or write (buffers can be freed while a read or write are pending)
 - `IRDPSRAPITransportStream.ReadBuffer` `PayloadSize` represents the _maximum_ number of bytes to read.  `PayloadSize` should be set to the number of read bytes before calling `IRDPSRAPITransportStreamEvents.OnReadCompleted`.
