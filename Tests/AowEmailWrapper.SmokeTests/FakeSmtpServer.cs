using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MimeKit;

namespace AowEmailWrapper.SmokeTests
{
    /// <summary>
    /// An SMTP server on this PC that takes whatever the Wrapper under test sends. It offers AUTH PLAIN and
    /// accepts any sign-in, answers the commands MailKit uses (EHLO, AUTH, MAIL, RCPT, DATA, QUIT) and keeps
    /// every message, so a test can see what a turn went out with.
    /// </summary>
    public sealed class FakeSmtpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly List<MimeMessage> _messages = new List<MimeMessage>();
        private readonly List<string> _log = new List<string>();
        private volatile bool _stopping;

        public int Port { get; }

        /// <summary>Every command received and every reply, for failure messages.</summary>
        public string Log { get { lock (_log) { return string.Join(Environment.NewLine, _log); } } }

        /// <summary>The messages received so far.</summary>
        public List<MimeMessage> Messages { get { lock (_messages) { return new List<MimeMessage>(_messages); } } }

        public FakeSmtpServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            new Thread(Accept) { IsBackground = true }.Start();
        }

        private void Accept()
        {
            while (!_stopping)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                new Thread(() => Serve(client)) { IsBackground = true }.Start();
            }
        }

        private void Serve(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                void Send(string line)
                {
                    lock (_log) { _log.Add("S: " + line); }
                    byte[] bytes = Encoding.ASCII.GetBytes(line + "\r\n");
                    stream.Write(bytes, 0, bytes.Length);
                }

                try
                {
                    Send("220 localhost fake SMTP");
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lock (_log) { _log.Add("C: " + line); }
                        string command = line.Length >= 4 ? line.Substring(0, 4).ToUpperInvariant() : line.ToUpperInvariant();
                        switch (command)
                        {
                            case "EHLO":
                                Send("250-localhost");
                                Send("250-AUTH PLAIN");
                                Send("250 8BITMIME");
                                break;
                            case "HELO":
                                Send("250 localhost");
                                break;
                            case "AUTH":
                                Send("235 2.7.0 Accepted");
                                break;
                            case "MAIL":
                            case "RCPT":
                            case "RSET":
                            case "NOOP":
                                Send("250 OK");
                                break;
                            case "DATA":
                                Send("354 End data with <CR><LF>.<CR><LF>");
                                StringBuilder data = new StringBuilder();
                                string dataLine;
                                while ((dataLine = reader.ReadLine()) != null && dataLine != ".")
                                {
                                    data.Append(dataLine.StartsWith("..") ? dataLine.Substring(1) : dataLine).Append("\r\n");
                                }
                                using (MemoryStream message = new MemoryStream(Encoding.UTF8.GetBytes(data.ToString())))
                                {
                                    MimeMessage parsed = MimeMessage.Load(message);
                                    lock (_messages) { _messages.Add(parsed); }
                                }
                                Send("250 OK queued");
                                break;
                            case "QUIT":
                                Send("221 Bye");
                                return;
                            default:
                                Send("502 Command not implemented");
                                break;
                        }
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        public void Dispose()
        {
            _stopping = true;
            _listener.Stop();
        }
    }
}
