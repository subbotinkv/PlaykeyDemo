namespace Server
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Text;

    class Program
    {
        private const int Size = 1024;

        private static void Main()
        {
            TcpListener listener = TcpListener.Create(11000);
            listener.Start();

            var clients = new List<TcpClient>();

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                if (!clients.Contains(client))
                {
                    clients.Add(client);
                }

                NetworkStream stream = client.GetStream();
                var buffer = new byte[Size];
                int offset = 0;
                var builder = new StringBuilder();
                while (stream.Read(buffer, offset, Size) != 0)
                {
                    builder.Append(Encoding.UTF8.GetString(buffer));
                    offset += Size;
                }

                Console.WriteLine(builder.ToString());
            }
        }
    }
}
