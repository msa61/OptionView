using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;


namespace DxLink
{
    public class WebSocketQueue
    {
        private readonly ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public async Task EnqueueMessageAsync(string message)
        {
            await semaphore.WaitAsync();
            try
            {
                messageQueue.Enqueue(message);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<string> DequeueMessageAsync()
        {
            await semaphore.WaitAsync();
            try
            {
                if (messageQueue.TryDequeue(out string message))
                {
                    return message;
                }
                else
                {
                    return null;
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }

    public class TSWebSocketHandler 
    {
        private readonly WebSocketQueue messageQueue = new WebSocketQueue();
        private readonly WebSocket webSocket;

        public TSWebSocketHandler(WebSocket webSocket)
        {
            this.webSocket = webSocket;
            Task.Run(() => ProcessQueue());
        }

        public async Task QueueMessageAsync(string message)
        {
            await messageQueue.EnqueueMessageAsync(message);
        }

        private async Task ProcessQueue()
        {
            while (true)
            {
                string message = await messageQueue.DequeueMessageAsync();
                if (message != null)
                {
                    try
                    {
                        // Send message through WebSocket
                        await SendMessageAsync(message);
                    }
                    catch (Exception ex)
                    {
                        // Handle exceptions
                        Debug.WriteLine($"Error sending message: {ex.Message}");
                    }
                }
                else
                {
                    // Queue is empty, wait for new messages
                    await Task.Delay(100); // Adjust delay as needed
                }
            }
        }

        private async Task SendMessageAsync(string message)
        {
            // Convert message to byte array
            var buffer = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes(message));
            await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
    }
