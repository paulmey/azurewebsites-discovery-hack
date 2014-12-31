using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using OwinDuplex.Messages;

namespace OwinDuplex
{
    public class ReflectNetMiddleware : OwinMiddleware
    {
        const string reflect = "_reflect";

        private readonly string _me;
        private Task _buildTask;
        private readonly ConcurrentDictionary<string, WebSocket> sockets = new ConcurrentDictionary<string, WebSocket>();
        private string _reflectUri;

        public ReflectNetMiddleware(OwinMiddleware next, string clusterHostAndPort)
            : this(next, clusterHostAndPort, null) { }

        public ReflectNetMiddleware(OwinMiddleware next, string clusterHostAndPort, string identity)
            : base(next)
        {
            _me = identity ?? Dns.GetHostName();
            _reflectUri = string.Format("ws://{0}/{1}", clusterHostAndPort, reflect);
            _buildTask = BuildNetwork();
        }

        private async Task BuildNetwork()
        {
            int delay = 10;
            while (true)
            {
                if (!await FindNewNode())
                    delay = Math.Min(delay * 2, 1000);
                await Task.Delay(delay);
            }
        }

        private async Task<bool> FindNewNode()
        {
            var ct = new CancellationToken(false);
            try
            {
                var ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(_reflectUri), ct);

                var msg = new Hello {From = _me};
                await ws.SendAsJsonAsync(msg, ct);
                var reply = await ws.ReceiveJsonAsync<Hello>(ct);

                var otherNode = reply.From;
                if (otherNode != _me)
                {
                    if (sockets.TryAdd(otherNode, ws))
                    {
                        return true;
                    }
                }
                Debug.WriteLine("CLIENT: discarding connection to {0}", (object) otherNode);
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "You're me or I know you already", ct);
            }
            catch (WebSocketException ex)
            {
                Debug.WriteLine("CLIENT: ai... failure: {0}", ex);
            }
            return false;
        }

        public override async Task Invoke(IOwinContext context)
        {
            if (context.Request.Uri.AbsolutePath == string.Format("/{0}", reflect))
            {
                TryAcceptConnection(context);
                return;
            }
            context.Set("reflectNet.Network", new NetworkImpl(this));
            await Next.Invoke(context);
        }

        private void TryAcceptConnection(IOwinContext context)
        {
            var accept = context.Get<Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>>("websocket.Accept");
            if (accept != null)
                accept(null, AttachOwin);
        }

        private async Task AttachOwin(IDictionary<string, object> environment)
        {
            var context = (WebSocketContext)environment["System.Net.WebSockets.WebSocketContext"];
            var callCancelled = (CancellationToken)environment["websocket.CallCancelled"];

            var ws = context.WebSocket;
            var request = await ws.ReceiveJsonAsync<Hello>(callCancelled);
            await ws.SendAsJsonAsync(new Hello { From = _me }, callCancelled);

            if (request.From != _me && sockets.TryAdd(request.From, ws)) return;
            Debug.WriteLine("SERVER: discarding connection to {0}", (object) request.From);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "You're me or I know you already", callCancelled);
        }

        class NetworkImpl : INetwork
        {
            private readonly ReflectNetMiddleware _source;

            public NetworkImpl(ReflectNetMiddleware source)
            {
                _source = source;
            }

            public string NodeIdentity { get { return _source._me; } }
            public string[] GetKnownNodes()
            {
                return _source.sockets.Keys.ToArray();
            }
        }
    }

    public interface INetwork
    {
        string NodeIdentity { get; }
        string[] GetKnownNodes();
    }
}