namespace Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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

        private const string Path = "Log.txt";

        private static void Main()
        {
            // Чистим лог.
            File.WriteAllText(Path, string.Empty);

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
                    if (message == "GetHistory")
                    {
                        SendHistory(client);
                    }
                    else
                    {
                        // Записываем полученное сообщение.
                        WriteMessageToFile(message);

                        // Пересылаем сообщение всем.
                        SendMessage(message);
                    }
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

        private static void WriteMessageToFile(string message)
        {
            // Вычитываем все строки.
            var lines = File.ReadAllLines(Path).ToList();

            // Добавляем новую строку.
            lines.Add(message);

            // Переупорядочиваем.
            lines.Sort();

            // Пишем обратно в файл.
            File.WriteAllLines(Path, lines);
        }

        private static void SendMessage(string message)
        {
            SendMessage(Clients, message);
        }

        private static void SendMessage(TcpClient client, string message)
        {
            SendMessage(new[] { client }, message);
        }

        private static void SendMessage(IEnumerable<TcpClient> clients, string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var clientStreams = clients.Select(client => client.GetStream());

            foreach (NetworkStream stream in clientStreams)
            {
                stream.Write(buffer, 0, buffer.Length);
            }
        }

        private static void SendHistory(TcpClient client)
        {
            SendMessage(client, File.ReadAllText(Path));
        }
    }
}
