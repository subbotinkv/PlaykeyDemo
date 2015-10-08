namespace Server
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using System.Net;
    using System.Net.Sockets;

    class Program
    {
        private const int Port = 11000;
        private const int BufferSize = 1024;

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
            Console.WriteLine("New client...");

            NetworkStream stream = client.GetStream();

            var buffer = new byte[BufferSize];
            var sb = new StringBuilder();

            while (client.Connected)
            {
                // Получаем данные от клиента.
                while (stream.DataAvailable)
                {
                    int count = stream.Read(buffer, 0, BufferSize);
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, count));
                }

                if (sb.Length > 0)
                {
                    Console.WriteLine(sb.ToString());
                    sb.Clear();
                }
            }
        }
    }
}
