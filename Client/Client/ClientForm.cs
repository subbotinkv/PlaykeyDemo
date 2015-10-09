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
            NetworkStream stream = Client.GetStream();

            var buffer = new byte[1024];
            var sb = new StringBuilder();

            while (true)
            {
                while (stream.DataAvailable)
                {
                    int count = stream.Read(buffer, 0, buffer.Length);
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, count));
                }

                if (sb.Length > 0)
                {
                    tbLog.AppendText(Environment.NewLine);
                    tbLog.AppendText(sb.ToString());
                    sb.Clear();
                }
            }
        }

        private void BtnSendClick(object sender, EventArgs e)
        {
            NetworkStream stream = Client.GetStream();
            byte[] buffer = Encoding.UTF8.GetBytes(tbMessage.Text);
            stream.Write(buffer, 0, buffer.Length);
            tbMessage.Clear();
        }
    }
}
