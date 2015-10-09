namespace Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Net;
    using System.Net.Sockets;

    class Program
    {
        private const int Port = 11000;
        private const int BufferSize = 1024;

        private static readonly List<TcpClient> Clients = new List<TcpClient>();

        private static void Main()
        {
            var listener = new TcpListener(IPAddress.Loopback, Port);
            listener.Start();

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Task.Factory.StartNew(() => HandleClient(client), TaskCreationOptions.LongRunning);
            }
        }

        private static void HandleClient(TcpClient client)
        {
            Clients.Add(client);
            Console.WriteLine("New client...");

            NetworkStream stream = client.GetStream();
            
            while (client.Connected)
            {
                // Получаем сообщение от клиента.
                string message = GetMessage(stream);

                // Если действительно получено сообщение, то обрабатываем его.
                if (!string.IsNullOrWhiteSpace(message))
                {
                    // Записываем полученное сообщение.
                    Console.WriteLine(message);

                    // Пересылаем сообщение всем.
                    SendMessage(message);
                }
            }
        }

        private static string GetMessage(NetworkStream stream)
        {
            var buffer = new byte[BufferSize];
            var sb = new StringBuilder();

            while (stream.DataAvailable)
            {
                int count = stream.Read(buffer, 0, BufferSize);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, count));
            }

            return sb.ToString();
        }

        private static void SendMessage(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var clientStreams = Clients.Select(client => client.GetStream());

            foreach (NetworkStream stream in clientStreams)
            {
                stream.Write(buffer, 0, buffer.Length);
            }
        }
    }
}
