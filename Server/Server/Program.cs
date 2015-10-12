namespace Server
{
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Net;
    using System.Net.Sockets;

    class Program
    {
        private const string HistoryKeyword = "GetHistory";

        private static int port;

        private static int bufferSize;

        private static string path;

        private static readonly List<TcpClient> Clients = new List<TcpClient>();

        private static readonly object Dummy = new object();

        private static void Main()
        {
            Init();
            
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            while (true)
            {
                // Принимаем подключение от клиента.
                TcpClient client = listener.AcceptTcpClient();
                
                // Запускаем его обработку в отдельном потоке.
                Task.Factory.StartNew(() => HandleClient(client), TaskCreationOptions.LongRunning);
            }
        }

        /// <summary>
        /// Инициализация перед началом работы приложения.
        /// </summary>
        private static void Init()
        {
            // Получаем параметры из конфига.
            var reader = new AppSettingsReader();
            port = (int)reader.GetValue("Port", typeof(int));
            bufferSize = (int)reader.GetValue("BufferSize", typeof(int));
            path = (string)reader.GetValue("Path", typeof(string));

            // Чистим лог.
            File.WriteAllText(path, string.Empty);
        }

        /// <summary>
        /// Обработка клиента (включение/исключение из списока активных клиентов и т.д.).
        /// </summary>
        /// <param name="client">Клиент.</param>
        private static void HandleClient(TcpClient client)
        {
            // Т.к. доступ к списку подключенных клиентов может вестись из разных потоков, то используем блокировку.
            lock (Dummy)
            {
                // Добавим в список подключенных клиентов.
                Clients.Add(client);
            }

            NetworkStream stream = client.GetStream();

            while (client.Connected)
            {
                try
                {
                    HandleStream(stream);
                }
                catch (IOException)
                {
                    // Если клиент отключится (пропадет соединение и т.д.), то при работе с потоком будет вызвано исключение.
                    // После вызова исключения свойство client.Connected автоматически изменится на false.
                    // Дополнительной обработки это исключение не требует.
                }
            }

            lock (Dummy)
            {
                // Удалим из списка подключенных клиентов.
                Clients.Remove(client);
            }
        }

        /// <summary>
        /// Работа с потоком клиента (получение и отправка сообщений).
        /// </summary>
        /// <param name="stream">Поток.</param>
        private static void HandleStream(NetworkStream stream)
        {
            // Получаем сообщение от клиента.
            string message = GetMessage(stream);

            // Если сообщение не пустое, то обрабатываем его.
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (message == HistoryKeyword)
                {
                    // Если пришел запрос истории (ключевое словое "GetHitory").
                    SendHistory(stream);
                }
                else
                {
                    // Иначе записываем полученное сообщение.
                    WriteMessageToFile(message);

                    // Пересылаем это сообщение всем.
                    SendMessage(message);
                }
            }
        }

        /// <summary>
        /// Получить сообщение.
        /// </summary>
        /// <param name="stream">Поток.</param>
        /// <returns>Сообщение.</returns>
        private static string GetMessage(NetworkStream stream)
        {
            var buffer = new byte[bufferSize];
            var sb = new StringBuilder();

            // Читаем пока есть данные.
            while (stream.DataAvailable)
            {
                int count = stream.Read(buffer, 0, bufferSize);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, count));

            }

            return sb.ToString();
        }

        /// <summary>
        /// Записать новое сообщение в файл.
        /// </summary>
        /// <param name="message">Сообщение.</param>
        private static void WriteMessageToFile(string message)
        {
            // Файл является разделяемым ресурсом, поэтому нужно использовать блокировку.
            lock (Dummy)
            {
                // Проверяем наличие файла и создаем новый если его вдруг нет.
                if (!File.Exists(path))
                {
                    File.Create(path);
                }

                // Вычитываем все строки.
                var lines = File.ReadAllLines(path).ToList();

                // Добавляем новую строку.
                lines.Add(message);

                // Переупорядочиваем.
                lines.Sort();

                // Пишем обратно в файл.
                File.WriteAllLines(path, lines);
            }
        }

        /// <summary>
        /// Отправить сообщение всем подключенным клиентам.
        /// </summary>
        /// <param name="message">Сообщение.</param>
        private static void SendMessage(string message)
        {
            SendMessage(Clients.Select(a => a.GetStream()).ToList(), message);
        }

        /// <summary>
        /// Отправить сообщение в поток.
        /// </summary>
        /// <param name="stream">Поток.</param>
        /// <param name="message">Сообщение.</param>
        private static void SendMessage(NetworkStream stream, string message)
        {
            SendMessage(new[] { stream }, message);
        }

        /// <summary>
        /// Отправить сообщение в несколько потоков.
        /// </summary>
        /// <param name="streams">Потоки.</param>
        /// <param name="message">Сообщение.</param>
        private static void SendMessage(IEnumerable<NetworkStream> streams, string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            foreach (NetworkStream stream in streams)
            {
                try
                {
                    stream.Write(buffer, 0, buffer.Length);
                }
                catch (IOException)
                {
                    // При работе с потоком может оказаться что он уже закрыт, потеряно подключение и т.д. Это приведет к возникновению исключения.
                    // При возникновении исключения свойство Connected у связаного с потоком TCP-клиента автоматически изменится на false и тогда дальнейшая обработка клиента пректатится.
                    // Дополнительной обработки не требуется.
                }
            }
        }

        /// <summary>
        /// Отправить всю историю.
        /// </summary>
        /// <param name="stream">Поток.</param>
        private static void SendHistory(NetworkStream stream)
        {
            SendMessage(stream, File.ReadAllText(path));
        }
    }
}
