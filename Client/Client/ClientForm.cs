namespace Client
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public partial class ClientForm : Form
    {
        /// <summary>
        /// Зарезервированное слово для получения истории.
        /// </summary>
        private const string HistoryKeyword = "GetHistory";

        /// <summary>
        /// Порт сервера.
        /// </summary>
        private int _port;

        /// <summary>
        /// Размер буфера сообщений.
        /// </summary>
        private int _bufferSize;

        /// <summary>
        /// TCP-клиент для общения с сервером.
        /// </summary>
        private TcpClient _client;

        public ClientForm()
        {
            InitializeComponent();

            // Подготовка к работе приложения.
            Init();

            // Пытаемся автоматически подключиться при запуске приложения.
            Connect();
        }

        /// <summary>
        /// Инициализация перед началом работы приложения.
        /// </summary>
        private void Init()
        {
            // Вычитиваем настройки.
            var reader = new AppSettingsReader();
            _port = (int)reader.GetValue("Port", typeof(int));
            _bufferSize = (int)reader.GetValue("BufferSize", typeof(int));
        }

        /// <summary>
        /// Обработать подключение к серверу.
        /// </summary>
        private void Connect()
        {
            try
            {
                // Подключаемся.
                _client = new TcpClient();
                _client.Connect(IPAddress.Loopback, _port);

                // Меняем состояние управляющих кнопок.
                ChangeButtonsState();

                // Запускаем получение новых сообщений в фоне.
                Task.Run(() => GetNewMessages());
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Не удалось подключиться к серверу. Попробуйте подключиться позднее с помощью кнопки \"Подключить\".",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Обработать отключение от сервера.
        /// </summary>
        private void Disconnect()
        {
            ChangeButtonsState();
            MessageBox.Show(
                "Потеряно соединение с сервером. Попробуйте подключиться позднее с помощью кнопки \"Подключить\".",
                "Ошибка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        /// <summary>
        /// Изменить состояние управляющих кнопок.
        /// </summary>
        private void ChangeButtonsState()
        {
            btnConnect.Enabled = !_client.Connected;
            btnSend.Enabled = _client.Connected;
            btnHistory.Enabled = _client.Connected;
        }

        /// <summary>
        /// Дописать сообщение в лог из другого потока.
        /// </summary>
        /// <param name="message">Сообщение.</param>
        public void AppendLog(string message)
        {
            if (!InvokeRequired)
            {
                tbLog.AppendText(message);
            }
            else
            {
                Invoke(new Action<string>(AppendLog), message);
            }
        }

        /// <summary>
        /// Получить новые сообщения.
        /// </summary>
        private void GetNewMessages()
        {
            // Пока клиент подключен вычитываем новые сообщения.
            // Как только произойдет потеря связи с сервером, 
            //    то будет вызвано исключение и свойство Connected станет false.
            while (_client.Connected)
            {
                try
                {
                    GetNewMessage();
                }
                catch (IOException)
                {
                    Disconnect();
                }
            }
        }

        /// <summary>
        /// Получить новое сообщение.
        /// </summary>
        private void GetNewMessage()
        {
            // Получаем сообщение.
            string message = GetMessage();

            // Если оно не пустое, то пишем его в текстбокс.
            if (!string.IsNullOrWhiteSpace(message))
            {
                AppendLog($"{message}{Environment.NewLine}");
            }
        }

        /// <summary>
        /// Прочитать сообщение от сервера.
        /// </summary>
        /// <returns>Сообщение.</returns>
        private string GetMessage()
        {
            NetworkStream stream = _client.GetStream();
            var buffer = new byte[_bufferSize];
            var sb = new StringBuilder();

            // Пока в потоке есть данные читаем.
            while (stream.DataAvailable)
            {
                int count = stream.Read(buffer, 0, _bufferSize);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, count));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Отправить сообщение на сервер.
        /// </summary>
        /// <param name="message">Сообщение.</param>
        private void SendMessage(string message)
        {
            NetworkStream stream = _client.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            try
            {
                stream.Write(buffer, 0, buffer.Length);
            }
            catch (IOException)
            {
                Disconnect();
            }
        }

        private void BtnConnectClick(object sender, EventArgs e)
        {
            Connect();
        }

        private void BtnSendClick(object sender, EventArgs e)
        {
            // Отправим сообщение на сервер.
            SendMessage(tbMessage.Text);

            // И почистим окно 
            tbMessage.Text = string.Empty;
        }

        private void BtnHistoryClick(object sender, EventArgs e)
        {
            // Отправим на сервер заразервированное слово для плучения истории.
            SendMessage(HistoryKeyword);
        }
    }
}
