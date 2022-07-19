using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Mikutap_Editor
{
    public class HttpServer
    {
        public Thread thread { get; private set; }
        public bool IsRunning { get; private set; } = false;
        public Encoding encoding = Encoding.UTF8;
        public string RootPath;
        private Socket socket;

        static readonly Dictionary<string, string> EXT = new Dictionary<string, string>
        {
            { "htm", "text/html" },
            { "html", "text/html" },
            { "xml", "text/xml" },
            { "css", "text/css" },
            { "png", "image/png" },
            { "gif", "image/gif" },
            { "jpg", "image/jpg" },
            { "jpeg", "image/jpeg" },
            { "zip", "application/zip"},
            { "js", "application/javascript"},
            { "json", "application/json"}
        };
        public string startServer(IPAddress ipAddress, int port, int maxConnections = 10, int timeout = 10)
        {
            if (IsRunning) return "已有现有的 Http 服务器在运行了!";
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream,
                               ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(ipAddress, port));
                socket.Listen(maxConnections);
                socket.ReceiveTimeout = timeout;
                socket.SendTimeout = timeout;
                IsRunning = true;
            }
            catch(Exception e)
            {
                return "Http 服务器启动失败!\n" + e.Message;
            }
            new Thread(() => {
                while(IsRunning)
                {
                    try
                    {
                        Socket client = socket.Accept();
                        // Create new thread to handle the request and continue to listen the socket.
                        new Thread(() =>
                        {
                            client.ReceiveTimeout = timeout;
                            client.SendTimeout = timeout;
                            try { handleRequest(client); }
                            catch
                            {
                                try { client.Close(); } catch { }
                            }
                        }).Start();
                    }
                    catch { }
                }
            }).Start();
            return null;
        }
        public void stop()
        {
            if (IsRunning)
            {
                IsRunning = false;
                try { socket.Close(); }
                catch { }
                socket = null;
            }
        }
        private void handleRequest(Socket client)
        {
            byte[] buffer = new byte[10240]; // 10 kb, just in case
            int receivedBCount = client.Receive(buffer); // Receive the request
            string strReceived = encoding.GetString(buffer, 0, receivedBCount);

            // Parse method of the request
            string httpMethod = strReceived.Substring(0, strReceived.IndexOf(" "));

            if (!httpMethod.Equals("GET") && !httpMethod.Equals("POST"))
            {
                notImplemented(client);
                return;
            }

            int start = strReceived.IndexOf(httpMethod) + httpMethod.Length + 1;
            int length = strReceived.LastIndexOf("HTTP") - start - 1;
            string requestedUrl = strReceived.Substring(start, length);
            string requestedFile = requestedUrl.Split('?')[0];

            requestedFile = requestedFile.Replace("/", "\\").Replace("\\..", "");
            start = requestedFile.LastIndexOf('.') + 1;
            if (start > 0)
            {
                length = requestedFile.Length - start;
                string extension = requestedFile.Substring(start, length);
                if (File.Exists(RootPath + requestedFile))
                {
                    string type = "text/plain";
                    if (EXT.ContainsKey(extension)) type = EXT[extension];
                    sendOkResponse(client, File.ReadAllBytes(RootPath + requestedFile), type);
                    return;
                }
            }
            else
            {
                if (requestedFile.Substring(length - 1, 1) != @"\")
                    requestedFile += @"\";
                if (File.Exists(RootPath + requestedFile + "index.html"))
                {
                    sendOkResponse(client, File.ReadAllBytes(RootPath + requestedFile + "\\index.html"), "text/html");
                    return;
                }
            }
            notFound(client);
        }

        private void notImplemented(Socket clientSocket)
        {

            sendResponse(clientSocket, "<html><head><metahttp-equiv=\"Content-Type\" content=\"text/html;charset=utf-8\"> </head><body><h2>Mikutap Editor</h2><div>501 - Method Not Implemented</div></body></html>", "501 Not Implemented", "text/html");

        }

        private void notFound(Socket clientSocket)
        {
            sendResponse(clientSocket, "<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head><body><h2>Mikutap Editor</h2><div>404 - Not Found</div></body></html>", "404 Not Found", "text/html");
        }

        private void sendOkResponse(Socket clientSocket, byte[] bContent, string contentType)
        {
            sendResponse(clientSocket, bContent, "200 OK", contentType);
        }

        private void sendResponse(Socket clientSocket, string strContent, string responseCode, string contentType)
        {
            byte[] bContent = encoding.GetBytes(strContent);
            sendResponse(clientSocket, bContent, responseCode, contentType);
        }

        private void sendResponse(Socket clientSocket, byte[] bContent, string responseCode, string contentType)
        {
            try
            {
                byte[] bHeader = encoding.GetBytes(
                                    "HTTP/1.1 " + responseCode + "\r\n"
                                  + "Server: Mikutap Editor\r\n"
                                  + "Content-Length: " + bContent.Length.ToString() + "\r\n"
                                  + "Connection: close\r\n"
                                  + "Content-Type: " + contentType + "\r\n\r\n");
                clientSocket.Send(bHeader);
                clientSocket.Send(bContent);
                clientSocket.Close();
            }
            catch { }
        }
    }
}