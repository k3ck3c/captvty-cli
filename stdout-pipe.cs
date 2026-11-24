using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

sealed class StdoutPipeClosedException : IOException
{
    public StdoutPipeClosedException(IOException inner)
        : base("Standard output pipe closed", inner)
    {
    }
}

sealed class StdoutPipeWriter : TextWriter
{
    readonly TextWriter inner;

    [StructLayout(LayoutKind.Sequential)]
    struct PollFd
    {
        public int fd;
        public short events;
        public short revents;
    }

    [DllImport("libc", SetLastError = true)]
    static extern int poll(
        ref PollFd fds, UIntPtr nfds, int timeout);

    [DllImport("libc", SetLastError = true)]
    static extern IntPtr readlink(
        string path, byte[] buffer, UIntPtr size);

    public StdoutPipeWriter(TextWriter writer)
    {
        inner = writer;
    }

    public override Encoding Encoding
    {
        get { return inner.Encoding; }
    }

    static bool IsClosedPipe(IOException ex)
    {
        if (ex.HResult != unchecked((int)0x8007001D))
            return false;

        try
        {
            byte[] buffer = new byte[256];

            long n = readlink(
                "/proc/self/fd/1",
                buffer,
                new UIntPtr((uint)buffer.Length)
            ).ToInt64();

            if (n <= 0 || n >= buffer.Length)
                return false;

            string target = Encoding.UTF8.GetString(
                buffer, 0, (int)n);

            if (!target.StartsWith(
                    "pipe:[", StringComparison.Ordinal))
                return false;

            PollFd p = new PollFd();
            p.fd = 1;
            p.events = 4; // POLLOUT

            return poll(ref p, new UIntPtr(1), 0) == 1
                && (p.revents & 8) != 0; // POLLERR
        }
        catch
        {
            return false;
        }
    }

    void Execute(Action action)
    {
        try
        {
            action();
        }
        catch (IOException ex)
        {
            if (IsClosedPipe(ex))
                throw new StdoutPipeClosedException(ex);

            throw;
        }
    }

    public override void Write(char value)
    {
        Execute(() => inner.Write(value));
    }

    public override void Write(string value)
    {
        Execute(() => inner.Write(value));
    }

    public override void Write(
        char[] buffer, int index, int count)
    {
        Execute(() => inner.Write(buffer, index, count));
    }

    public override void WriteLine(string value)
    {
        Execute(() => inner.WriteLine(value));
    }

    public override void WriteLine()
    {
        Execute(() => inner.WriteLine());
    }

    public override void Flush()
    {
        Execute(() => inner.Flush());
    }
}
