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
        /// <summary>
        /// Зарезервированное слово для получения истории.
        /// </summary>
        private const string HistoryKeyword = "GetHistory";

        /// <summary>
        /// Порт на котором работает сервер.
        /// </summary>
        private static int _port;

        /// <summary>
        /// Размер буфера для приема сообщений.
        /// </summary>
        private static int _bufferSize;

        /// <summary>
        /// Путь к файлу лога.
        /// </summary>
        private static string _path;

        /// <summary>
        /// Список подключенных клиентов.
        /// </summary>
        private static readonly List<TcpClient> Clients = new List<TcpClient>();

        /// <summary>
        /// Объект для синхронизации доступа к файлу.
        /// В файл одновременно может писать только один поток.
        /// </summary>
        private static readonly object FileLocker = new object();

        /// <summary>
        /// Объект для синхронизации к списку клиентов.
        /// Синхронизация необходимо т.к. доступ к списку клиентов может вестись из нескольких потоков.
        /// Пример: один поток удаляет клиента из списка, а другой поток пытается получить список потоков клиентов.
        ///     Это приведет к ошибке, т.к. коллекция будет изменена.
        /// </summary>
        private static readonly object ListLocker = new object();

        private static void Main()
        {
            Init();
            
            var listener = new TcpListener(IPAddress.Loopback, _port);
            listener.Start();

            while (true)
            {
                // Принимаем подключение от клиента.
                TcpClient client = listener.AcceptTcpClient();
                
                // Запускаем его обработку в отдельном потоке.
                Task.Run(() => HandleClient(client));
            }
        }

        /// <summary>
        /// Инициализация перед началом работы приложения.
        /// </summary>
        private static void Init()
        {
            // Получаем параметры из конфига.
            var reader = new AppSettingsReader();
            _port = (int)reader.GetValue("Port", typeof(int));
            _bufferSize = (int)reader.GetValue("BufferSize", typeof(int));
            _path = (string)reader.GetValue("Path", typeof(string));

            // Чистим лог.
            File.WriteAllText(_path, string.Empty);
        }

        /// <summary>
        /// Обработка клиента (включение/исключение из списока активных клиентов и т.д.).
        /// </summary>
        /// <param name="client">Клиент.</param>
        private static void HandleClient(TcpClient client)
        {
            lock (ListLocker)
            {
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

            lock (ListLocker)
            {
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
            var buffer = new byte[_bufferSize];
            var sb = new StringBuilder();

            // Читаем пока есть данные.
            while (stream.DataAvailable)
            {
                int count = stream.Read(buffer, 0, _bufferSize);
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
            lock (FileLocker)
            {
                // Проверяем наличие файла и создаем новый если его вдруг нет.
                if (!File.Exists(_path))
                {
                    File.Create(_path);
                }

                // Вычитываем все строки.
                var lines = File.ReadAllLines(_path).ToList();

                // Добавляем новую строку.
                lines.Add(message);

                // Переупорядочиваем.
                lines.Sort();

                // Пишем обратно в файл.
                File.WriteAllLines(_path, lines);
            }
        }

        /// <summary>
        /// Отправить сообщение всем подключенным клиентам.
        /// </summary>
        /// <param name="message">Сообщение.</param>
        private static void SendMessage(string message)
        {
            List<NetworkStream> streams;
            lock (ListLocker)
            {
                streams = Clients.Select(a => a.GetStream()).ToList();
            }

            SendMessage(streams, message);
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
            SendMessage(stream, File.ReadAllText(_path));
        }
    }
}
