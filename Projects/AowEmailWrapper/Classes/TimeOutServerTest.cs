using System;
using System.Threading;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Security;
using Mozilla.Autoconfig;

namespace AowEmailWrapper.Classes
{
    /// <summary>
    /// Checks that a mail server candidate from autoconfiguration accepts a connection
    /// (and works out whether it wants plain or STARTTLS) within a time limit.
    /// </summary>
    public class TimeOutServerTest : IDisposable
    {
        #region Private Members

        private ManualResetEvent _finished;
        private CancellationTokenSource _cancellation;

        private bool _isSuccess = false;
        private bool _isDisposed = false;
        private bool _incoming;
        private int _timeoutMs;

        private IncomingServer _incomingServer;
        private OutgoingServer _outgoingServer;

        #endregion

        #region Public Properties

        public bool IsSuccess
        {
            get { return _isSuccess; }
        }

        public IncomingServer IncomingServer
        {
            get { return _incomingServer; }
        }

        public OutgoingServer OutgoingServer
        {
            get { return _outgoingServer; }
        }

        #endregion

        #region Constructors

        public TimeOutServerTest(IncomingServer incomingServer)
        {
            _incomingServer = incomingServer;
            _finished = new ManualResetEvent(false);
            _incoming = true;
        }

        public TimeOutServerTest(OutgoingServer outgoingServer)
        {
            _outgoingServer = outgoingServer;
            _finished = new ManualResetEvent(false);
            _incoming = false;
        }

        #endregion

        #region Public Methods

        public void Test(int timeoutMs)
        {
            _timeoutMs = timeoutMs;
            _cancellation = new CancellationTokenSource(timeoutMs);
            _finished.Reset();

            Thread testThread = new Thread(new ThreadStart(this.RunTest));
            testThread.IsBackground = true;
            testThread.Start();
        }

        /// <summary>
        /// Blocks until the test has finished or the given time has passed.
        /// </summary>
        public bool Wait(int timeoutMs)
        {
            return _finished.WaitOne(timeoutMs);
        }

        #endregion

        #region Private Methods

        private void RunTest()
        {
            try
            {
                if (_incoming)
                {
                    switch (_incomingServer.Type)
                    {
                        case ServerType.IMAP:
                            _isSuccess = TryConnect(() => new ImapClient(), _incomingServer.Hostname, _incomingServer.Port, _incomingServer.SocketType, type => _incomingServer.SocketType = type);
                            break;
                        case ServerType.POP3:
                            _isSuccess = TryConnect(() => new Pop3Client(), _incomingServer.Hostname, _incomingServer.Port, _incomingServer.SocketType, type => _incomingServer.SocketType = type);
                            break;
                    }
                }
                else
                {
                    _isSuccess = TryConnect(() => new SmtpClient(), _outgoingServer.Hostname, _outgoingServer.Port, _outgoingServer.SocketType, type => _outgoingServer.SocketType = type);
                }
            }
            catch
            {
                _isSuccess = false;
            }
            finally
            {
                if (!_isDisposed)
                {
                    _finished.Set();
                }
            }
        }

        private bool TryConnect(Func<IMailService> createClient, string host, int port, SocketType socketType, Action<SocketType> setSocketType)
        {
            if (socketType == SocketType.SSL)
            {
                return Connect(createClient, host, port, SecureSocketOptions.SslOnConnect);
            }

            //Unknown, plain or STARTTLS: prefer STARTTLS and fall back to plain if the server cannot upgrade
            if (Connect(createClient, host, port, SecureSocketOptions.StartTls))
            {
                setSocketType(SocketType.STARTTLS);
                return true;
            }

            if (Connect(createClient, host, port, SecureSocketOptions.None))
            {
                setSocketType(SocketType.Plain);
                return true;
            }

            return false;
        }

        private bool Connect(Func<IMailService> createClient, string host, int port, SecureSocketOptions options)
        {
            try
            {
                using (IMailService client = createClient())
                {
                    client.Timeout = _timeoutMs;
                    client.Connect(host, port, options, _cancellation.Token);

                    try
                    {
                        client.Disconnect(true, _cancellation.Token);
                    }
                    catch
                    {
                        //The connection itself succeeded, a rough disconnect does not matter
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _isDisposed = true;

            if (_cancellation != null)
            {
                _cancellation.Cancel();
                _cancellation.Dispose();
                _cancellation = null;
            }

            _finished.Close();
        }

        #endregion
    }
}
