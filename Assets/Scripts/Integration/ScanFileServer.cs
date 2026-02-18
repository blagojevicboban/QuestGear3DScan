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
            string httpMethod = context.Request.HttpMethod;
            string urlPath = context.Request.Url.AbsolutePath;
            
            urlPath = Uri.UnescapeDataString(urlPath);
            
            Debug.Log($"[FileServer] {httpMethod} {urlPath}");

            try
            {
                // Handle DELETE requests
                if (httpMethod == "DELETE")
                {
                    string relativePath = urlPath.TrimStart('/');
                    string fullPath = Path.Combine(_rootPath, relativePath);

                    if (Directory.Exists(fullPath))
                    {
                        Directory.Delete(fullPath, true);
                        response.StatusCode = 200;
                        byte[] msg = Encoding.UTF8.GetBytes("Deleted successfully");
                        response.OutputStream.Write(msg, 0, msg.Length);
                        Debug.Log($"[FileServer] Deleted: {fullPath}");
                    }
                    else
                    {
                        response.StatusCode = 404;
                    }
                }
                else if (urlPath == "/" || urlPath == "/index.html")
                {
                    string html = GenerateDirectoryListing(_rootPath, true);
                    byte[] buffer = Encoding.UTF8.GetBytes(html);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
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
                         string html = GenerateDirectoryListing(fullPath, false);
                         byte[] buffer = Encoding.UTF8.GetBytes(html);
                         response.ContentType = "text/html; charset=utf-8";
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

        private string GenerateDirectoryListing(string path, bool isRoot)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<html><head><title>QuestGear 3D Scans</title>");
            sb.Append("<style>");
            sb.Append("body{font-family:sans-serif;max-width:800px;margin:40px auto;padding:0 20px;background:#1a1a2e;color:#e0e0e0;}");
            sb.Append("h1{color:#00d4ff;} a{color:#4fc3f7;text-decoration:none;} a:hover{text-decoration:underline;}");
            sb.Append(".item{display:flex;align-items:center;padding:8px 12px;border-bottom:1px solid #333;}");
            sb.Append(".item:hover{background:#222244;}");
            sb.Append(".name{flex:1;} .size{color:#888;margin-right:16px;}");
            sb.Append(".btn{padding:4px 12px;border:none;border-radius:4px;cursor:pointer;font-size:12px;margin-left:4px;}");
            sb.Append(".btn-del{background:#c62828;color:white;} .btn-del:hover{background:#e53935;}");
            sb.Append("</style>");
            sb.Append("<script>");
            sb.Append("function delSession(name){if(confirm('Delete '+name+'?')){fetch('/'+name,{method:'DELETE'}).then(()=>location.reload());}}");
            sb.Append("</script>");
            sb.Append("</head><body>");
            sb.Append("<h1>QuestGear 3D Scans</h1>");

            if (!isRoot)
            {
                sb.Append("<div class='item'><span class='name'><a href='..'>.. Back</a></span></div>");
            }

            // Directories
            foreach (var dir in Directory.GetDirectories(path))
            {
                string dirName = new DirectoryInfo(dir).Name;
                long size = 0;
                try
                {
                    foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                        size += new FileInfo(f).Length;
                }
                catch { /* ignore */ }

                string sizeStr = QuestGear3D.Scan.Data.RecordingExporter.FormatSize(size);

                sb.Append("<div class='item'>");
                sb.Append($"<span class='name'><a href='{dirName}/'>{dirName}</a></span>");
                sb.Append($"<span class='size'>{sizeStr}</span>");
                if (isRoot)
                {
                    sb.Append($"<button class='btn btn-del' onclick=\"delSession('{dirName}')\">Delete</button>");
                }
                sb.Append("</div>");
            }

            // Files
            foreach (var file in Directory.GetFiles(path))
            {
                string fileName = Path.GetFileName(file);
                long size = new FileInfo(file).Length;
                string sizeStr = QuestGear3D.Scan.Data.RecordingExporter.FormatSize(size);
                sb.Append($"<div class='item'><span class='name'><a href='{fileName}'>{fileName}</a></span><span class='size'>{sizeStr}</span></div>");
            }

            sb.Append("</body></html>");
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
