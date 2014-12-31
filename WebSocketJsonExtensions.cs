using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OwinDuplex
{
    static class WebSocketJsonExtensions
    {
        public static async Task SendAsJsonAsync(this WebSocket socket, object message, CancellationToken ct)
        {
            var b = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
            await socket.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, ct);
        }

        public static async Task<T> ReceiveJsonAsync<T>(this WebSocket socket, CancellationToken ct)
        {
            // TODO: paulmey - handle cases where buffer is too small or message is in multiple pieces...

            var buffer = new byte[1024];
            var rresult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            var json = JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(buffer, 0, rresult.Count));
            return json;
        }
    }
}