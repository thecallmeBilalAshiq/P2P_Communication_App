using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ChatClient
{
    public partial class MainForm : Form
    {
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        private volatile bool isClosing = false;
        private string username;

        public MainForm()
        {
            InitializeComponent();
            InitializeEmojiButtons();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ConnectToServer();
        }


        //  ========================== >>> Connect to Server <<< ===========================

        private void ConnectToServer()
        {
            try
            {
                client = new TcpClient("localhost", 8080);
                stream = client.GetStream();
                username = Prompt.ShowDialog("Enter your username: ", "Login");
                if (string.IsNullOrEmpty(username))
                {
                    MessageBox.Show("Username cannot be empty. Exiting.");
                    Application.Exit();
                    return;
                }

                UpdateStatus(username + " connected");
                receiveThread = new Thread(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error connecting to server: " + ex.Message);
                Application.Exit();
            }
        }

        //  ========================== >>> Recieve messgages <<< ===========================
        private void ReceiveMessages()
        {
            byte[] buffer = new byte[4096];
            while (!isClosing)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        if (message.StartsWith("START_FILE_TRANSFER:"))
                        {
                            string filename = message.Split(':')[1];
                            ReceiveFile(filename);
                        }
                        else
                        {
                            UpdateChatBox(message);
                        }
                    }
                }
                catch
                {
                    UpdateStatus("Disconnected from server.");
                    break;
                }
            }
        }


        //  ========================== >>> Recieve File  <<< ===========================
        private void ReceiveFile(string filename)
        {
            string downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloadsFolder);

            string filePath = Path.Combine(downloadsFolder, filename);

            using (FileStream fs = new FileStream(filePath, FileMode.Create))
            {
                byte[] buffer = new byte[4096];
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0) break;

                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (data.Contains("END_FILE_TRANSFER"))
                    {
                        int endIndex = data.IndexOf("END_FILE_TRANSFER");
                        fs.Write(buffer, 0, endIndex);
                        break;
                    }

                    fs.Write(buffer, 0, bytesRead);
                }
            }

            UpdateChatBox($"File saved to Downloads: {filePath}");
        }


        //  ========================== >>>  <<< ===========================

        private void UpdateChatBox(string message)
        {
            if (chatBox.InvokeRequired)
            {
                Invoke(new Action(() => chatBox.AppendText(message + Environment.NewLine)));
            }
            else
            {
                chatBox.AppendText(message + Environment.NewLine);
            }
        }


        //  ========================== >>>  <<< ===========================
        private void UpdateStatus(string message)
        {
            if (this.IsDisposed)
                return;

            if (statusLabel.InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    if (!this.IsDisposed)
                        statusLabel.Text = message;
                }));
            }
            else
            {
                if (!this.IsDisposed)
                    statusLabel.Text = message;
            }
        }

        //  ========================== >>> send button  <<< ===========================
        private void sendButton_Click(object sender, EventArgs e)
        {
            string message = messageBox.Text.Trim();
            if (!string.IsNullOrEmpty(message) && stream != null)
            {
                try
                {
                    byte[] buffer = Encoding.UTF8.GetBytes($"SEND_MESSAGE:{username}: {message}");
                    stream.Write(buffer, 0, buffer.Length);
                    UpdateChatBox($"You::     {message}");
                    messageBox.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to send message: {ex.Message}");
                }
            }
        }

        //  ========================== >>> send file button <<< ===========================
        private void sendFileButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = fileDialog.FileName;
                string fileName = Path.GetFileName(filePath);
                byte[] fileData = File.ReadAllBytes(filePath);

                string fileHeader = $"UPLOAD_FILE:{fileName}";
                byte[] headerBuffer = Encoding.UTF8.GetBytes(fileHeader);
                stream.Write(headerBuffer, 0, headerBuffer.Length);

                stream.Write(fileData, 0, fileData.Length);

                byte[] endBuffer = Encoding.UTF8.GetBytes("END_FILE_UPLOAD");
                stream.Write(endBuffer, 0, endBuffer.Length);

                UpdateChatBox($"You sent a file: {fileName}");
            }
        }


        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            isClosing = true;
            try
            {
                if (receiveThread != null && receiveThread.IsAlive)
                {
                    receiveThread.Join(500);
                }

                stream?.Close();
                client?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during cleanup: {ex.Message}");
            }
        }



        //  ================================================= >>>  Emojis <<< ===========================================================


        private void InitializeEmojiButtons()
        {
            string[] emojis = { "😀", "😂", "😍", "👍", "❤️" };
            int xPos = 20;
            int yPos = 340;

            foreach (string emoji in emojis)
            {
                Button emojiButton = new Button
                {
                    Text = emoji,
                    Location = new System.Drawing.Point(xPos, yPos),
                    Size = new System.Drawing.Size(35, 35),
                    FlatStyle = FlatStyle.Flat,
                    Font = new System.Drawing.Font("Segoe UI Emoji", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0))),
                    BackColor = System.Drawing.Color.SpringGreen  // Set background color
                };

                emojiButton.Click += (sender, e) =>
                {
                    SendEmoji(emoji);
                };

                Controls.Add(emojiButton);
                xPos += 50;
            }
        }


        //  ========================== >>> Send Emoji  <<< ===========================
        private void SendEmoji(string emoji)
        {
            if (stream != null)
            {
                try
                {
                    string message = $"SEND_EMOJI:{username}: {emoji}";
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    stream.Write(buffer, 0, buffer.Length);
                    UpdateChatBox($"You:     {emoji}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to send emoji: {ex.Message}");
                }
            }
        }

        public static class Prompt
        {
            public static string ShowDialog(string text, string caption)
            {
                Form prompt = new Form
                {
                    Width = 400,
                    Height = 150,
                    Text = caption
                };

                Label textLabel = new Label() { Left = 10, Top = 20, Text = text };
                TextBox inputBox = new TextBox() { Left = 10, Top = 50, Width = 350 };
                Button confirmButton = new Button() { Text = "OK", Left = 280, Width = 80, Top = 80 };

                confirmButton.Click += (sender, e) =>
                {
                    prompt.DialogResult = DialogResult.OK;
                    prompt.Close();
                };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(inputBox);
                prompt.Controls.Add(confirmButton);
                prompt.AcceptButton = confirmButton;

                return prompt.ShowDialog() == DialogResult.OK ? inputBox.Text : string.Empty;
            }
        }

        private void chatBox_TextChanged(object sender, EventArgs e)
        {

        }
    }
}

