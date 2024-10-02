using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using CsharpJson;
using System.Diagnostics;

namespace Mikutap_Editor
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public readonly string PATH_WWW = Environment.CurrentDirectory + "\\www";
        public readonly string PATH_OUTPUT = Environment.CurrentDirectory + "\\output";
        public string PATH_MAIN;
        public DirectoryInfo DIR_MAIN;
        public string PATH_TRACK;
        public DirectoryInfo DIR_TRACK;
        private HttpServer server = new HttpServer();
        public MainWindow()
        {
            PATH_MAIN = www("data\\main\\main.json");
            PATH_TRACK = www("data\\track\\track.json");
            DIR_MAIN = new DirectoryInfo(output("main"));
            DIR_TRACK = new DirectoryInfo(output("track"));
            InitializeComponent();
            server.RootPath = PATH_WWW;

            TextPort.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, null, DenyExcute));
        }

        private void DenyExcute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            e.Handled = true;
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", "http://localhost:" + TextPort.Text);
        }

        public string www(string path)
        {
            return PATH_WWW + "\\" + path;
        }

        public string output(string path)
        {
            return PATH_OUTPUT + "\\" + path;
        }

        public void SaveAudio(DirectoryInfo dir, JsonObject json)
        {
            foreach (string key in json.Keys)
            {
                if (json[key].IsString())
                {
                    string filepath = dir.FullName.Replace("/", "\\");
                    filepath = filepath + (!filepath.EndsWith("\\") ? "\\" : "") + key;

                    string data = json[key].ToString();
                    if (!data.StartsWith("data:audio/mp3;base64,")) continue;
                    data = data.Substring(22);

                    byte[] bytes = Convert.FromBase64String(data);

                    if (File.Exists(filepath)) File.Delete(filepath);
                    File.WriteAllBytes(filepath, bytes);
                }
            }
        }

        public JsonObject LoadAudio(DirectoryInfo dir)
        {
            JsonObject json = new JsonObject();
            foreach (FileInfo file in dir.GetFiles("*.mp3"))
            {
                string key = file.Name;
                byte[] bytes = File.ReadAllBytes(file.FullName);
                string data = "data:audio/mp3;base64," + Convert.ToBase64String(bytes);
                json.Add(key, data);
            }
            return json;
        }
        delegate void Message(string m);
        Message message = new Message((string m) =>
        {
            MessageBox.Show(m);
        });
        private void msg(string m)
        {
            Dispatcher.BeginInvoke(message, m);
        }

        private void CheckEnableHttpServer_Checked(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TextPort.Text, out int port) && port >= 0 && port <= 65535) {
                string s = server.startServer(System.Net.IPAddress.Loopback, port);
                if (s != null)
                {
                    e.Handled = true;
                    msg(s);
                    return;
                }
                msg("Http 服务器已启动");
            }
            else
            {
                e.Handled = true;
                msg("错误: 输入的端口应为 [0, 65535] 区间内的整数");
            }
        }

        private void CheckEnableHttpServer_Unchecked(object sender, RoutedEventArgs e)
        {
            server.stop();
            msg("已执行关闭操作");
        }

        private void IntegerBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) e.Handled = false;
            else if ((e.Key >= Key.D0 && e.Key <= Key.D9) &&
                     e.KeyboardDevice.Modifiers != ModifierKeys.Shift) e.Handled = false;
            else  e.Handled = true;
            
        }
        private void NumberBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if ((e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) || e.Key == Key.Decimal)
            {
                if (tb.Text.Contains(".") && e.Key == Key.Decimal) e.Handled = true;
                else e.Handled = false;
            }
            else if (((e.Key >= Key.D0 && e.Key <= Key.D9) || e.Key == Key.OemPeriod) &&
                     e.KeyboardDevice.Modifiers != ModifierKeys.Shift)
            {
                if (tb.Text.Contains(".") && e.Key == Key.OemPeriod)
                    e.Handled = true;
                else e.Handled = false;
            }
            else  e.Handled = true;
            
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            server.stop();
        }

        private void Button_OpenWWW_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", PATH_WWW);
        }

        private void Button_OpenOutput_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", PATH_OUTPUT);
        }

        private void Pack_Click(object sender, RoutedEventArgs e)
        {
            if (!DIR_TRACK.Exists) { msg("output/track 文件夹不存在!"); return; }
            if (!DIR_MAIN.Exists) { msg("output/main 文件夹不存在!"); return;}

            JsonObject track = LoadAudio(DIR_TRACK);
            JsonObject main = LoadAudio(DIR_MAIN);
            File.WriteAllText(PATH_TRACK, track.ToString());
            File.WriteAllText(PATH_MAIN, main.ToString());

            msg("打包完成!");
        }

        private void UnPack_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(PATH_TRACK)) { msg("www/data/track/track.json 不存在!"); return; }
            if (!File.Exists(PATH_MAIN)) { msg("www/data/main/main.json 不存在!"); return; }

            if (DIR_TRACK.Exists) DIR_TRACK.Delete(true);
            if (DIR_MAIN.Exists) DIR_MAIN.Delete(true);
            if (!DIR_TRACK.Exists) Directory.CreateDirectory(DIR_TRACK.FullName);
            if (!DIR_MAIN.Exists) Directory.CreateDirectory(DIR_MAIN.FullName);

            JsonDocument docTrack = JsonDocument.FromString(File.ReadAllText(PATH_TRACK));
            if (!docTrack.IsObject()) { msg("www/data/track/track.json 不是有效的 JsonObject"); return; }
            JsonDocument docMain = JsonDocument.FromString(File.ReadAllText(PATH_MAIN));
            if (!docMain.IsObject()) { msg("www/data/main/main.json 不是有效的 JsonObject"); return; }

            SaveAudio(DIR_TRACK, docTrack.Object);
            SaveAudio(DIR_MAIN, docMain.Object);

            msg("解包完成!");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Activate();
        }
    }
}
