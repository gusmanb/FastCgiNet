using System;
using System.Collections.Generic;
using System.IO;
using FastCgiNet.Streams;

namespace FastCgiNet
{
	/// <summary>
	/// Stream records are records whose contents can be concatenated to form a stream of data. An empty stream record
	/// signals the end of the stream.
	/// </summary>
	public abstract class StreamRecordBase : RecordBase, IDisposable
	{
		private int addedContentLength;
		private int addedPaddingLength;

		/// <summary>
        /// Use this stream to define what content will be in this record. After disposing of this Record, this stream will also be disposed of.
		/// </summary>
        public virtual RecordContentsStream Contents { get; internal set; }

		/// <summary>
		/// Whether this record contains any content at all. Empty Content records are used to end communication from one
		/// side of the request.
		/// </summary>
		public bool EmptyContentData
		{
			get
			{
				return ContentLength == 0;
			}
		}

		/// <summary>
		/// Enumerates byte array segments that compose this record. This is useful to send through a socket, for instance.
		/// Do not modify these byte arrays as they may be the byte arrays that form the underlying stream.
		/// </summary>
		/// <remarks>The first ArraySegment enumerated is guaranteed to be the header of the record, being therefore 8 bytes long. This method rewinds the stream before and after all elements are enumerated.</remarks>
		public override IEnumerable<ArraySegment<byte>> GetBytes()
        {
            int seekContentsBackTo = -1;
            seekContentsBackTo = (int)Contents.Position;
            Contents.Position = 0;

			ContentLength = (ushort) (Contents == null ? 0 : Contents.Length);
			yield return CalculatePaddingAndGetHeaderBytes();

			foreach (var buf in Contents.MemoryBlocks)
			{
                if (buf.Length > 0)
				    yield return new ArraySegment<byte>(buf);
			}

			foreach (var segment in GetPaddingBytes())
				yield return segment;

            Contents.Position = seekContentsBackTo;
		}

		/// <summary>
		/// Adds more content data that has been read from the socket. If we have reached the end of this record's content, then <paramref name="lastByteOfContent"/> will be >= 0. Otherwise, it will be -1,
		/// meaning more data needs to be added to this Content.
		/// </summary>
		/// <remarks>Do not use this method to build a record's contents to be sent over the wire. Use the stream directly in that case.</remarks>
		internal override void FeedBytes(byte[] data, int offset, int length, out int lastByteOfRecord)
		{
			AssertArrayOperation(data, offset, length);

			// Fill up the Content if we can
			int contentNeeded = ContentLength - addedContentLength;
			if (contentNeeded <= length)
			{
				if (contentNeeded != 0)
				{
					Contents.Write(data, offset, contentNeeded);
					addedContentLength = ContentLength;
				}
				
				// Check for the end of padding too!
				int paddingAvailable = length - contentNeeded;
				int paddingNeeded = PaddingLength - addedPaddingLength;
				if (paddingAvailable >= paddingNeeded)
				{
					lastByteOfRecord = offset + contentNeeded + paddingNeeded - 1;
					addedPaddingLength = PaddingLength;
				}
				else
				{
					lastByteOfRecord = offset + contentNeeded + paddingAvailable - 1;
					addedPaddingLength += paddingAvailable;
				}
			}
			else
			{
				Contents.Write(data, offset, length);
				addedContentLength += length;
				lastByteOfRecord = -1;
			}
		}

		public void Dispose()
		{
			Contents.Dispose();
		}

		public override bool Equals(object obj)
		{
			if (obj == null)
				return false;

			var b = obj as StreamRecordBase;
			if (b == null)
				return false;

			return b.Contents.Equals(this.Contents) && base.Equals(b);
		}

		public override int GetHashCode()
		{
			return Contents.GetHashCode();
		}

        /// <summary>
        /// Base constructor that initializes this record's contents' stream to use memory.
        /// </summary>
		public StreamRecordBase(RecordType recordType, ushort requestId)
			: base(recordType, requestId)
		{
            Contents = new RecordContentsStream();
		}

        /// <summary>
        /// Base constructor that initializes this record's contents' stream to use secondary storage. The life-cycle of the supplied
        /// secondary storage stream has no relation with this record whatsoever.
        /// </summary>
        public StreamRecordBase(RecordType recordType, ushort requestId, Stream secondaryStorageStream)
            : this(recordType, requestId)
        {
            if (secondaryStorageStream == null)
                throw new ArgumentNullException("secondaryStorageOps");

            Contents = new RecordContentsStream(secondaryStorageStream);
        }

        internal StreamRecordBase(RecordType recordType, byte[] data, int offset, int length, out int endOfRecord)
            : base(data, offset, length, recordType)
        {
            Contents = new RecordContentsStream();

            if (ContentLength + PaddingLength == 0)
                endOfRecord = offset + 7;
            else if (length > 8)
                FeedBytes(data, offset + 8, length - 8, out endOfRecord);
            else
                endOfRecord = -1;
        }
	}
}
