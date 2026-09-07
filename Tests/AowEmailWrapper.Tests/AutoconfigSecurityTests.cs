using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using AowEmailWrapper.Classes;
using AowEmailWrapper.Helpers;
using Mozilla.Autoconfig;
using Xunit;
using SocketType = Mozilla.Autoconfig.SocketType;

namespace AowEmailWrapper.Tests
{
    /// <summary>
    /// Discovered mail settings decide where the user's password is sent, so they may only come
    /// from a source that cannot be tampered with, and must never point at an unencrypted server.
    /// </summary>
    public class AutoconfigSecurityTests
    {
        [Theory]
        [InlineData("https://autoconfig.thunderbird.net/v1.1/example.com", true)]
        [InlineData("https://autoconfig.example.com/mail/config-v1.1.xml", true)]
        [InlineData("file:///C:/Wrapper/isp/example.com.xml", true)]
        [InlineData("http://autoconfig.example.com/mail/config-v1.1.xml", false)]
        [InlineData("http://example.com/.well-known/autoconfig/mail/config-v1.1.xml", false)]
        [InlineData("ftp://example.com/config.xml", false)]
        [InlineData("not a url", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Only_https_and_shipped_files_may_supply_settings(string url, bool trusted)
        {
            Assert.Equal(trusted, IspDbHandler.IsTrustedSource(url));
        }

        [Fact]
        public void Plain_servers_are_dropped_from_a_discovered_provider()
        {
            EmailProvider provider = Provider(
                Incoming("imap.example.com", 993, SocketType.SSL),
                Incoming("imap.example.com", 143, SocketType.Plain),
                Incoming("pop.example.com", 110, SocketType.Unknown));
            provider.OutgoingServers.Add(Outgoing("smtp.example.com", 587, SocketType.STARTTLS));
            provider.OutgoingServers.Add(Outgoing("smtp.example.com", 25, SocketType.Plain));

            Assert.True(AutoconfigurationHelper.RemoveUnencryptedServers(provider));

            IncomingServer incoming = Assert.Single(provider.IncomingServers);
            Assert.Equal(993, incoming.Port);
            OutgoingServer outgoing = Assert.Single(provider.OutgoingServers);
            Assert.Equal(587, outgoing.Port);
        }

        [Fact]
        public void A_provider_with_only_plain_servers_is_rejected()
        {
            EmailProvider provider = Provider(Incoming("imap.example.com", 143, SocketType.Plain));
            provider.OutgoingServers.Add(Outgoing("smtp.example.com", 25, SocketType.Plain));

            Assert.False(AutoconfigurationHelper.RemoveUnencryptedServers(provider));
            Assert.Empty(provider.IncomingServers);
            Assert.Empty(provider.OutgoingServers);
        }

        [Fact]
        public void A_provider_missing_one_direction_is_rejected()
        {
            EmailProvider provider = Provider(Incoming("imap.example.com", 993, SocketType.SSL));
            provider.OutgoingServers.Add(Outgoing("smtp.example.com", 25, SocketType.Plain));

            Assert.False(AutoconfigurationHelper.RemoveUnencryptedServers(provider));
            Assert.False(AutoconfigurationHelper.RemoveUnencryptedServers(null));
        }

        [Fact]
        public void Server_probe_refuses_a_smtp_server_that_cannot_start_tls()
        {
            //A live SMTP server that answers EHLO without STARTTLS. Before the fix the probe
            //would settle for a plain connection here and call the server usable.
            using (FakePlainSmtpServer server = new FakePlainSmtpServer())
            {
                OutgoingServer candidate = Outgoing("127.0.0.1", server.Port, SocketType.Unknown);

                using (TimeOutServerTest probe = new TimeOutServerTest(candidate))
                {
                    probe.Test(10000);
                    Assert.True(probe.Wait(15000), "the probe did not finish");
                    Assert.False(probe.IsSuccess);
                }

                Assert.True(server.SawClient, "the probe never connected");
                Assert.NotEqual(SocketType.Plain, candidate.SocketType);
            }
        }

        #region Helpers

        private static EmailProvider Provider(params IncomingServer[] incoming)
        {
            EmailProvider provider = new EmailProvider();
            provider.IncomingServers = new List<IncomingServer>(incoming);
            provider.OutgoingServers = new List<OutgoingServer>();
            return provider;
        }

        private static IncomingServer Incoming(string host, int port, SocketType socket)
        {
            return new IncomingServer { Type = ServerType.IMAP, Hostname = host, Port = port, SocketType = socket, Authentication = AuthenticationType.PasswordClearText };
        }

        private static OutgoingServer Outgoing(string host, int port, SocketType socket)
        {
            return new OutgoingServer { Type = ServerType.SMTP, Hostname = host, Port = port, SocketType = socket, Authentication = AuthenticationType.PasswordClearText };
        }

        /// <summary>Speaks just enough SMTP to greet a client and decline everything but EHLO and QUIT.</summary>
        private sealed class FakePlainSmtpServer : IDisposable
        {
            private readonly TcpListener _listener;
            private volatile bool _sawClient;

            public FakePlainSmtpServer()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Thread thread = new Thread(Serve) { IsBackground = true };
                thread.Start();
            }

            public int Port
            {
                get { return ((IPEndPoint)_listener.LocalEndpoint).Port; }
            }

            public bool SawClient
            {
                get { return _sawClient; }
            }

            private void Serve()
            {
                try
                {
                    while (true)
                    {
                        using (TcpClient client = _listener.AcceptTcpClient())
                        using (NetworkStream stream = client.GetStream())
                        using (StreamReader reader = new StreamReader(stream, Encoding.ASCII))
                        using (StreamWriter writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" })
                        {
                            _sawClient = true;
                            writer.WriteLine("220 fake.test ESMTP");
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                string verb = line.Length >= 4 ? line.Substring(0, 4).ToUpperInvariant() : line.ToUpperInvariant();
                                if (verb == "EHLO" || verb == "HELO")
                                {
                                    writer.WriteLine("250-fake.test");
                                    writer.WriteLine("250 SIZE 1000000");
                                }
                                else if (verb == "QUIT")
                                {
                                    writer.WriteLine("221 bye");
                                    break;
                                }
                                else
                                {
                                    writer.WriteLine("502 not here");
                                }
                            }
                        }
                    }
                }
                catch
                {
                    //Listener closed
                }
            }

            public void Dispose()
            {
                _listener.Stop();
            }
        }

        #endregion
    }
}
