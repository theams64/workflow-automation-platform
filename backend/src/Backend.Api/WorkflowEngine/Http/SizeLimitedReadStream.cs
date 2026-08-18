using Backend.Api.WorkflowEngine.Execution;

namespace Backend.Api.WorkflowEngine.Http
{
    public sealed class SizeLimitedReadStream(Stream inner, long maximumBytes, string errorCode = ExecutionErrorCodes.ResponseTooLarge) : Stream
    {
        private long _bytesRead;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            Track(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            Track(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            Track(read);
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadArrayAsync(buffer, offset, count, cancellationToken);
        }

        private async Task<int> ReadArrayAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            Track(read);
            return read;
        }

        private void Track(int read)
        {
            _bytesRead += read;

            if (_bytesRead > maximumBytes)
            {
                throw new SafeHttpException(errorCode, "The HTTP response exceeded the allowed size.");
            }
        }

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
