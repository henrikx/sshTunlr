using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Renci.SshNet;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace sshTunlr
{
    public partial class MainForm : Form
    {
        [DllImport("wininet.dll")]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
        public const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        public const int INTERNET_OPTION_REFRESH = 37;
        static bool settingsReturn, refreshReturn;
        public static MainForm _MainForm;
        public static ConnectionInfo connectionInfo;
        public MainForm(string path)
        {
            _MainForm = this;
            CheckForIllegalCrossThreadCalls = false;
            InitializeComponent();
            if (path != string.Empty && Path.GetExtension(path).ToLower() == ".stnlr"){
                LoadProfile(path);
            }
            SSHConnector.DoWork += new DoWorkEventHandler(SSHConnect);
        }
        BackgroundWorker SSHConnector = new BackgroundWorker();
        
        void SSHConnect(object sender, DoWorkEventArgs e)
        {
            if (_MainForm.textBox1.Text == string.Empty) { MessageBox.Show("No hostname specified"); return; }
            if (_MainForm.textBox2.Text == string.Empty) { MessageBox.Show("No username specified"); }
            if (!_MainForm.radioButton1.Checked && !_MainForm.radioButton2.Checked) { MessageBox.Show("No authentication specified"); return; }
            try
            {
                using (var client = new SshClient(_MainForm.textBox1.Text, _MainForm.textBox2.Text, _MainForm.getAuth()))
                {
                    var ProxyPort = new ForwardedPortDynamic("127.0.0.1", 1080);
                    _MainForm.WriteLog("Attempting connection to " + _MainForm.textBox1.Text);
                    client.Connect();
                    if (client.IsConnected)
                    {

                        //Connect
                        isConnected = true;
                        _MainForm.WriteLog("Connected");
                        _MainForm.WriteLog("Adding SOCKS port: " + ProxyPort.BoundHost + ":" + ProxyPort.BoundPort);
                        client.AddForwardedPort(ProxyPort);
                        ProxyPort.Start();
                        _MainForm.WriteLog("Ready for connections");
                        _MainForm.ConnectionButton.Text = "Disconnect";
                        _MainForm.textBox1.ReadOnly = true;
                        _MainForm.textBox2.ReadOnly = true;
                        _MainForm.textBox3.ReadOnly = true;
                        _MainForm.radioButton1.Enabled = false;
                        _MainForm.radioButton2.Enabled = false;
                        _MainForm.textBox4.ReadOnly = true;
                        _MainForm.textBox5.ReadOnly = true;
                        _MainForm.button3.Enabled = false;
                        _MainForm.WriteLog("Setting windows proxy");
                        _MainForm.WriteLog("Connected");
                        RegistryKey registry = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true);
                        registry.SetValue("ProxyEnable", 1);
                        registry.SetValue("ProxyServer", "socks=127.0.0.1:1080");
                        settingsReturn = InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                        refreshReturn = InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
                        ConnectionButton.Enabled = true;

                        while (isConnected)
                        {
                            Thread.Sleep(1000);
                        }
                        //Disconnect
                        WriteLog("Disconnecting");
                        _MainForm.WriteLog("Setting windows proxy to default values");
                        registry.SetValue("ProxyEnable", 0);
                        registry.DeleteValue("ProxyServer");
                        settingsReturn = InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                        refreshReturn = InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
                        ProxyPort.Stop();
                        client.RemoveForwardedPort(ProxyPort);
                        client.Disconnect();
                        Thread.Sleep(500);
                        WriteLog("Disconnected");
                        _MainForm.textBox1.ReadOnly = false;
                        _MainForm.textBox2.ReadOnly = false;
                        _MainForm.textBox3.ReadOnly = false;
                        _MainForm.radioButton1.Enabled = true;
                        _MainForm.radioButton2.Enabled = true;
                        _MainForm.textBox4.ReadOnly = false;
                        _MainForm.textBox5.ReadOnly = false;
                        _MainForm.button3.Enabled = true;
                        ConnectionButton.Enabled = true;
                        _MainForm.ConnectionButton.Text = "Connect";
                    }
                }
            } catch (Exception ex)
            {
                _MainForm.WriteLog(ex.Message);
                MessageBox.Show(ex.Message);
            }
        }
        bool isConnected ;
        private void ConnectionButton_Click(object sender, EventArgs e)
        {
            ConnectionButton.Enabled = false;
            if(ConnectionButton.Text == "Connect")
            {
                
                SSHConnector.RunWorkerAsync();
            }else
            {
                isConnected = false;
            }
            

        }
        public void WriteLog(string logString)
        {
            consoleBox.AppendText(logString + "\n");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                textBox4.Text = openFileDialog.FileName;
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton2.Checked)
            {
                panel2.Visible = false;
                panel3.Visible = true;
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            panel3.Visible = false;
            panel2.Visible = true;
        }
        private dynamic getAuth()
        {
            if (!radioButton1.Checked && !radioButton2.Checked)
            {
                return "";
            }
            if (radioButton1.Checked)
            {
                return textBox3.Text;
            }
            else
            {
                string key = File.ReadAllText(textBox4.Text);
                Regex removeSubjectRegex = new Regex("Subject:.*[\r\n]+", RegexOptions.IgnoreCase);
                key = removeSubjectRegex.Replace(key, "");
                MemoryStream buf = new MemoryStream(Encoding.UTF8.GetBytes(key));
                try
                {
                    PrivateKeyFile privateKeyFile = new PrivateKeyFile(buf, textBox5.Text);
                    return privateKeyFile;
                } catch (Renci.SshNet.Common.SshPassPhraseNullOrEmptyException)
                {
                    WriteLog("Invalid password for encrypted key");
                    
                }
            }
            return "";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string savePath = "";
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.AddExtension = true;
            saveFileDialog.DefaultExt = "stnlr";
            saveFileDialog.Filter = "sshTunlr Config file (*.stnlr)|*.stnlr";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                savePath = saveFileDialog.FileName;
            }
            else { return; }
            SaveProfile(savePath);
        }
        private void SaveProfile (string savePath)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.DocumentElement;
            XmlElement Config = doc.CreateElement(string.Empty, "Config", string.Empty);
            doc.AppendChild(Config);

            XmlElement Hostname = doc.CreateElement(string.Empty, "Hostname", string.Empty);
            XmlText HostnameValue = doc.CreateTextNode(textBox1.Text);
            Hostname.AppendChild(HostnameValue);
            Config.AppendChild(Hostname);

            XmlElement Username = doc.CreateElement(string.Empty, "Username", string.Empty);
            XmlText UsernameValue = doc.CreateTextNode(textBox2.Text);
            Username.AppendChild(UsernameValue);
            Config.AppendChild(Username);

            XmlElement AuthMode = doc.CreateElement(string.Empty, "PasswordAuth", string.Empty);
            XmlText AuthModeValue = doc.CreateTextNode(Convert.ToString(radioButton1.Checked));
            AuthMode.AppendChild(AuthModeValue);
            Config.AppendChild(AuthMode);

            XmlElement SavePass = doc.CreateElement(string.Empty, "SavePass", string.Empty);
            XmlText SavePassValue = doc.CreateTextNode(Convert.ToString(savePass.Checked));
            SavePass.AppendChild(SavePassValue);
            Config.AppendChild(SavePass);

            if (savePass.Checked && radioButton1.Checked)
            {
                XmlElement Password = doc.CreateElement(string.Empty, "Password", string.Empty);
                XmlText PasswordValue = doc.CreateTextNode(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(textBox3.Text)));
                Password.AppendChild(PasswordValue);
                Config.AppendChild(Password);
            }

            XmlElement KeyLocation = doc.CreateElement(string.Empty, "KeyLocation", string.Empty);
            XmlText KeyLocationValue = doc.CreateTextNode(textBox4.Text);
            KeyLocation.AppendChild(KeyLocationValue);
            Config.AppendChild(KeyLocation);

            XmlElement SaveKeyPass = doc.CreateElement(string.Empty, "SaveKeyPass", string.Empty);
            XmlText SaveKeyPassValue = doc.CreateTextNode(Convert.ToString(saveKeyPass.Checked));
            SaveKeyPass.AppendChild(SaveKeyPassValue);
            Config.AppendChild(SaveKeyPass);

            if (saveKeyPass.Checked && radioButton2.Checked)
            {
                XmlElement KeyPassword = doc.CreateElement(string.Empty, "KeyPassword", string.Empty);
                XmlText KeyPasswordValue = doc.CreateTextNode(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(textBox5.Text)));
                KeyPassword.AppendChild(KeyPasswordValue);
                Config.AppendChild(KeyPassword);
            }

            doc.Save(savePath);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string loadPath = "";
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "sshTunlr Config file (*.stnlr)|*.stnlr";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                loadPath = openFileDialog.FileName;
            }
            else
            {
                return;
            }
            LoadProfile(loadPath);
        }
            
        public void LoadProfile (string loadPath)
        {
            if (!File.Exists(loadPath))
            {
                MessageBox.Show("Path is invalid");
            }
            XmlDocument doc = new XmlDocument();
            doc.Load(loadPath);
            textBox1.Text = doc.SelectSingleNode("Config/Hostname").InnerText;
            textBox2.Text = doc.SelectSingleNode("Config/Username").InnerText;
            bool isPassAuth = Convert.ToBoolean(doc.SelectSingleNode("Config/PasswordAuth").InnerText);
            if (isPassAuth)
            {
                radioButton2.Checked = false;
                radioButton1.Checked = true;
                bool passIsSaved = Convert.ToBoolean(doc.SelectSingleNode("Config/SavePass").InnerText);
                if (passIsSaved)
                {
                    savePass.Checked = true;
                    textBox3.Text = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(doc.SelectSingleNode("Config/Password").InnerText));
                }
            }
            
            else
            {
                radioButton1.Checked = false;
                radioButton2.Checked = true;
                textBox4.Text = doc.SelectSingleNode("Config/KeyLocation").InnerText;
                bool keyPassIsSaved = Convert.ToBoolean(doc.SelectSingleNode("Config/SaveKeyPass").InnerText);
                if (keyPassIsSaved)
                {
                    saveKeyPass.Checked = true;
                    textBox5.Text = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(doc.SelectSingleNode("Config/KeyPassword").InnerText));
                }
            }
        }
    }
}
