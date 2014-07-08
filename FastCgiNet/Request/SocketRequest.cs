using System;
using System.Collections.Generic;
using System.Net.Sockets;
using FastCgiNet.Streams;
using System.Linq;

namespace FastCgiNet.Requests
{
	/// <summary>
	/// This is the base class for FastCgi Requests running over a socket.
	/// </summary>
    public abstract class SocketRequest : FastCgiRequest
	{
		public Socket Socket { get; private set; }

        protected override void AddReceivedRecord(RecordBase rec)
        {
            base.AddReceivedRecord(rec);

            var beginRequestRec = rec as BeginRequestRecord;
            if (beginRequestRec != null)
            {
                ((SocketStream)Data).RequestId = beginRequestRec.RequestId;
                ((SocketStream)Params).RequestId = beginRequestRec.RequestId;
                ((SocketStream)Stdin).RequestId = beginRequestRec.RequestId;
                ((SocketStream)Stdout).RequestId = beginRequestRec.RequestId;
                ((SocketStream)Stderr).RequestId = beginRequestRec.RequestId;
            }
        }

		/// <summary>
		/// Sends a record over the wire. This method could throw if the connection has been closed prematurely by the other side or if the socket
        /// is in the process of being closed by your code or is already closed.
		/// </summary>
        /// <exception cref="System.ObjectDisposedException">If the Socket was disposed.</exception>
        /// <exception cref="System.SocketException">If the Socket was prematurely closed by the other side or was in the process of being closed.</exception>
		protected override void Send(RecordBase rec)
		{
            base.Send(rec);

            Socket.Send(rec.GetBytes().ToList());
            /*
            foreach (var seg in rec.GetBytes())
            {
                if (seg.Offset != 0 || seg.Count != seg.Array.Length)
                    seg.Array[0] = 0;

               Socket.Send(seg.Array, seg.Offset, seg.Count, SocketFlags.None);
            }

            /*
            

            foreach (var arrSegment in rec.GetBytes())
            {
                Socket.Send(arrSegment.Array, arrSegment.Offset, arrSegment.Count, SocketFlags.None);
            }*/
		}

		public SocketRequest(Socket s)
		{
			if (s == null)
				throw new ArgumentNullException("s");
			
			this.Socket = s;
		}
	}
}
