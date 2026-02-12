using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using UnityEngine;

namespace QuestGear3D.Scan.Integration
{
    public class ScanFileServer : MonoBehaviour
    {
        public int port = 8080;
        public bool startOnAwake = true;
        
        private HttpListener _listener;
        private Thread _serverThread;
        private bool _isRunning = false;
        private string _rootPath;
        
        // UI Feedback
        public string ServerAddress { get; private set; }

        void Start()
        {
            _rootPath = Path.Combine(Application.persistentDataPath, "Scans");
            if (!Directory.Exists(_rootPath)) Directory.CreateDirectory(_rootPath);

            if (startOnAwake)
            {
                StartServer();
            }
        }

        public void StartServer()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{port}/");
                _listener.Start();
                
                _isRunning = true;
                _serverThread = new Thread(ServerLoop);
                _serverThread.Start();
                
                string ip = GetLocalIPAddress();
                ServerAddress = $"http://{ip}:{port}/";
                Debug.Log($"[FileServer] Started at {ServerAddress}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileServer] Failed to start: {e.Message}");
            }
        }

        public void StopServer()
        {
            _isRunning = false;
            if (_listener != null)
            {
                _listener.Stop();
                _listener.Close();
                _listener = null;
            }
            if (_serverThread != null)
            {
                _serverThread.Abort(); // Force stop
                _serverThread = null;
            }
            Debug.Log("[FileServer] Stopped");
        }

        void OnDestroy()
        {
            StopServer();
        }

        private void ServerLoop()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem((o) => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FileServer] Error in loop: {e.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;
            string urlPath = context.Request.Url.AbsolutePath; // e.g., / or /Scan_.../color/...
            
            // Decoded path (spaces, legacy, etc)
            urlPath = Uri.UnescapeDataString(urlPath);
            
            Debug.Log($"[FileServer] Request: {urlPath}");

            try
            {
                if (urlPath == "/" || urlPath == "/index.html")
                {
                    // Serve Directory Listing
                    string html = GenerateDirectoryListing(_rootPath);
                    byte[] buffer = Encoding.UTF8.GetBytes(html);
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    // Serve File
                    // Remove leading slash to combine with root
                    string relativePath = urlPath.TrimStart('/');
                    string fullPath = Path.Combine(_rootPath, relativePath);
                    
                    if (File.Exists(fullPath))
                    {
                        byte[] buffer = File.ReadAllBytes(fullPath);
                        response.ContentLength64 = buffer.Length;
                        response.ContentType = GetContentType(fullPath);
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                    else if (Directory.Exists(fullPath))
                    {
                        // Sub-directory listing
                         string html = GenerateDirectoryListing(fullPath);
                         byte[] buffer = Encoding.UTF8.GetBytes(html);
                         response.ContentLength64 = buffer.Length;
                         response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        response.StatusCode = 404;
                    }
                }
            }
            catch (Exception e)
            {
                response.StatusCode = 500;
                Debug.LogError($"[FileServer] Error handling request: {e.Message}");
            }
            finally
            {
                response.Close();
            }
        }

        private string GenerateDirectoryListing(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<html><head><title>QuestGear 3D Scans</title></head><body>");
            sb.Append($"<h1>Data at {path}</h1><ul>");

            // Up link
            sb.Append("<li><a href=\"..\">.. (Up)</a></li>");

            // Directories
            foreach (var dir in Directory.GetDirectories(path))
            {
                string dirName = new DirectoryInfo(dir).Name;
                // Relative link logic is tricky with HttpListener paths, keeping it simple:
                // If we are at root, link is just Name/
                // If we are deep, we need to construct proper relative path logic or use absolute from root context
                // For simplicity assuming flat or basic structure
                sb.Append($"<li><a href=\"{dirName}/\">{dirName}/</a></li>");
            }

            // Files
            foreach (var file in Directory.GetFiles(path))
            {
                string fileName = Path.GetFileName(file);
                sb.Append($"<li><a href=\"{fileName}\">{fileName}</a></li>");
            }

            sb.Append("</ul></body></html>");
            return sb.ToString();
        }

        private string GetContentType(string path)
        {
            String ext = Path.GetExtension(path).ToLower();
            switch (ext)
            {
                case ".jpg": return "image/jpeg";
                case ".png": return "image/png";
                case ".json": return "application/json";
                case ".html": return "text/html";
                default: return "application/octet-stream";
            }
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "127.0.0.1";
        }
    }
}
