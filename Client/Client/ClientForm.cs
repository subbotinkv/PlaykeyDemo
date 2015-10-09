namespace Client
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public partial class ClientForm : Form
    {
        private const int Port = 11000;
        private const int BufferSize = 1024;

        private readonly object _dummy=new object();

        private TcpClient _client;

        private TcpClient Client
        {
            get
            {
                if (_client == null)
                {
                    lock (_dummy)
                    {
                        if (_client == null)
                        {
                            _client = new TcpClient();
                            _client.Connect(IPAddress.Loopback, Port);
                        }
                    }
                }

                return _client;
            }
        }

        public ClientForm()
        {
            InitializeComponent();
            Task.Factory.StartNew(GetNewMessages, TaskCreationOptions.LongRunning);
        }

        private void GetNewMessages()
        {
            while (true)
            {
                string message = GetMessage();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    tbLog.AppendText(Environment.NewLine);
                    tbLog.AppendText(message);
                }
            }
        }

        private void BtnSendClick(object sender, EventArgs e)
        {
            SendMessage(tbMessage.Text);
            tbMessage.Text = string.Empty;
        }

        private void BtnHistoryClick(object sender, EventArgs e)
        {
            SendMessage("GetHistory");
        }

        private string GetMessage()
        {
            NetworkStream stream = Client.GetStream();
            var buffer = new byte[BufferSize];
            var sb = new StringBuilder();

            while (stream.DataAvailable)
            {
                int count = stream.Read(buffer, 0, BufferSize);
                sb.Append(Encoding.UTF8.GetString(buffer, 0, count));
            }

            return sb.ToString();
        }

        private void SendMessage(string message)
        {
            NetworkStream stream = Client.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
