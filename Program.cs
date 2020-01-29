using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace HttpServer
{
    class Program
    {
        private static string[][] _extToType = { new[] { ".htm", "text/html" }, new[] { ".html", "text/html" }, new[] { ".js", "text/javascript" }, new[] { ".css", "text/css" }, new[] { ".ico", "image/vnd.microsoft.icon" }, new[] { ".jpg", "image/jpeg" }, new[] { ".jpeg", "image/jpeg" }, new[] { ".png", "image/png" }, new[] { ".gif", "image/gif" }, new[] { ".bmp", "image/bmp" }, new[] { ".json", "application/json" }, new[] { ".svg", "image/svg+xml" }, new[] { ".tif", "image/tiff" }, new[] { ".tiff", "image/tiff" }, new[] { ".swf", "application/x-shockwave-flash" }, new[] { ".map", "text/javascript" }, new[] { ".aac", "audio/aac" }, new[] { ".abw", "application/x-abiword" }, new[] { ".arc", "application/x-freearc" }, new[] { ".avi", "video/x-msvideo" }, new[] { ".azw", "application/vnd.amazon.ebook" }, new[] { ".bin", "application/octet-stream" }, new[] { ".bz", "application/x-bzip" }, new[] { ".bz2", "application/x-bzip2" }, new[] { ".csh", "application/x-csh" }, new[] { ".csv", "text/csv" }, new[] { ".doc", "application/msword" }, new[] { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" }, new[] { ".eot", "application/vnd.ms-fontobject" }, new[] { ".epub", "application/epub+zip" }, new[] { ".gz", "application/gzip" }, new[] { ".ics", "text/calendar" }, new[] { ".jar", "application/java-archive" }, new[] { ".jsonld", "application/ld+json" }, new[] { ".mid", "audio/midi audio/x-midi" }, new[] { ".midi", "audio/midi audio/x-midi" }, new[] { ".mjs", "text/javascript" }, new[] { ".mp3", "audio/mpeg" }, new[] { ".mpeg", "video/mpeg" }, new[] { ".mpkg", "application/vnd.apple.installer+xml" }, new[] { ".odp", "application/vnd.oasis.opendocument.presentation" }, new[] { ".ods", "application/vnd.oasis.opendocument.spreadsheet" }, new[] { ".odt", "application/vnd.oasis.opendocument.text" }, new[] { ".oga", "audio/ogg" }, new[] { ".ogv", "video/ogg" }, new[] { ".ogx", "application/ogg" }, new[] { ".opus", "audio/opus" }, new[] { ".otf", "font/otf" }, new[] { ".pdf", "application/pdf" }, new[] { ".ppt", "application/vnd.ms-powerpoint" }, new[] { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" }, new[] { ".rar", "application/x-rar-compressed" }, new[] { ".rtf", "application/rtf" }, new[] { ".sh", "application/x-sh" }, new[] { ".tar", "application/x-tar" }, new[] { ".ts", "video/mp2t" }, new[] { ".ttf", "font/ttf" }, new[] { ".txt", "text/plain" }, new[] { ".vsd", "application/vnd.visio" }, new[] { ".wav", "audio/wav" }, new[] { ".weba", "audio/webm" }, new[] { ".webm", "video/webm" }, new[] { ".webp", "image/webp" }, new[] { ".woff", "font/woff" }, new[] { ".woff2", "font/woff2" }, new[] { ".xhtml", "application/xhtml+xml" }, new[] { ".xls", "application/vnd.ms-excel" }, new[] { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }, new[] { ".xml", "application/xml" }, new[] { ".xul", "application/vnd.mozilla.xul+xml" }, new[] { ".zip", "application/zip" }, new[] { ".3gp", "video/3gpp" }, new[] { ".3g2", "video/3gpp2" }, new[] { ".7z", "application/x-7z-compressed" } };
        private static string _injectScript = "<script>new WebSocket(\"ws://localhost:80/\").onmessage=function(o){location.reload(!0)};</script>";
        private static string _path;

        private static string _allowedCharacters = @"^[a-zA-Z0-9-_.\/]+$";
        private static Regex _regEx = new Regex(_allowedCharacters);
        private static CancellationTokenSource SocketLoopTokenSource;
        private static CancellationTokenSource SocketSendTokenSource = new CancellationTokenSource();
        private static CancellationToken SendCancel = SocketSendTokenSource.Token;

        private static object _listLock = new object();
        private static LinkedList<WebSocket> _webSockets = new LinkedList<WebSocket>();
        private static Timer _hashTimer;
        private static LinkedList<string> _hash = new LinkedList<string>();
        private static ArraySegment<byte> FilesChanged = new ArraySegment<byte>(Encoding.UTF8.GetBytes("FilesChanged"));

        public static void Main(string[] args)
        {
            var prefixes = new string[] { "http://*:80/" };
            var listener = new HttpListener();

            if (args != null && args.Length > 0)
            {
                _path = args[0];
                if (args.Length > 1)
                {
                    prefixes = args.Skip(1).Take(args.Length - 1).ToArray();
                }
            }
            if (string.IsNullOrEmpty(_path)) _path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            if (!_path.EndsWith("\\")) _path += "\\";

            var msg = new StringBuilder();
            msg.Append("Mapping HTTP: ");
            msg.Append(string.Join(", ", prefixes));
            msg.Append(" to \"");
            msg.Append(_path);
            msg.Append("\"...");

            Console.Write(msg.ToString());

            foreach (string s in prefixes)
            {
                listener.Prefixes.Add(s);
            }

            listener.Start();

            (new Thread(() =>
            {
                while (listener.IsListening)
                {
                    var context = listener.GetContext();
                    if (context.Request.IsWebSocketRequest)
                    {
                        HandleWebSocketRequest(context);
                    }
                    else
                    {
                        HandleHttpRequest(context);
                    }
                }
            })).Start();

            Console.WriteLine("Done.");

            _hashTimer = new Timer(TimerTick, null, 0, 500);

        }


        private static void TimerTick(object state)
        {
            var hash = GetFileSystemHash();
            if (!Enumerable.SequenceEqual(_hash, hash))
            {
                Console.WriteLine("File system change detected.");
                _hash = hash;
                lock (_listLock)
                {
                    foreach (var socket in _webSockets)
                    {
                        socket.SendAsync(FilesChanged, WebSocketMessageType.Text, true, SendCancel);
                    }
                }
            }
        }

        public static bool IsValidHttpRequest(string url)
        {
            if (url.StartsWith(".")) return false;
            if (url.EndsWith(".")) return false;
            if (url.Contains("..")) return false;
            if (url.Contains("./")) return false;
            if (url.Contains("/.")) return false;
            if (url.StartsWith("/")) return false;
            if (url.EndsWith("/")) return false;
            if (url.Contains("//")) return false;
            var matches = _regEx.Match(url);
            return (matches.Groups.Count == 1);
        }

        private static void HandleWebSocketRequest(HttpListenerContext context)
        {
            var webSocket = context.AcceptWebSocketAsync(null).Result.WebSocket;
            var thr = new Thread(WebSocketHandler);
            thr.Start(webSocket);
        }

        private static void WebSocketHandler(object socket)
        {
            var context = (WebSocket)socket;
            LinkedListNode<WebSocket> myNode;
            lock (_listLock)
            {
                myNode = _webSockets.AddLast(context);
            }
            var bytes = new byte[1024 * 4];
            var buffer = new ArraySegment<byte>(bytes, 0, 1024 * 4);
            SocketLoopTokenSource = new CancellationTokenSource();
            var killThisShit = SocketLoopTokenSource.Token;
            while (context.State == WebSocketState.Open && !killThisShit.IsCancellationRequested)
            {
                var taskResult = context.ReceiveAsync(buffer, killThisShit);
                try
                {
                    taskResult.Wait();
                    var result = Encoding.UTF8.GetString(bytes, 0, taskResult.Result.Count);
                    if (!string.IsNullOrEmpty(result)) Console.WriteLine("WebSocket: " + result);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    lock (_listLock)
                    {
                        try
                        {
                            _webSockets.Remove(myNode);
                        }
                        catch (Exception f)
                        {
                            Console.WriteLine(f);
                        }

                    }
                    Console.WriteLine("WebSocket Closed remotely...");
                }

            }
            Console.WriteLine("Socket closed.");
        }

        private static void HandleHttpRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var url = Uri.UnescapeDataString(request.RawUrl).Substring(1);

            if (string.IsNullOrEmpty(url.Trim())) url = "index.htm";

            Console.Write("Request: " + url + " ");

            var response = context.Response;

            if ((request.HttpMethod.ToUpper() != "GET") || (!IsValidHttpRequest(url)))
            {
                Console.WriteLine("(Bad Request)");
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.OutputStream.Close();
                return;
            }

            var filename = _path + url.Replace("/", "\\");

            if (!File.Exists(filename) && (!filename.EndsWith("index.htm")))
            {
                filename = filename + "\\index.htm";
            }

            if (!File.Exists(filename))
            {
                Console.WriteLine("(404)");
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.OutputStream.Close();
                return;
            }

            var contentType = _extToType.FirstOrDefault(e => e[0] == Path.GetExtension(filename).ToLower());
            if (contentType == null)
            {
                Console.WriteLine("(Forbidden)");
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                response.OutputStream.Close();
                return;
            }

            Console.WriteLine("(OK)");
            response.ContentType = contentType[1];
            response.StatusCode = (int)HttpStatusCode.OK;
            var responseBytes = File.ReadAllBytes(filename);
            if (filename.EndsWith("\\index.htm"))
            {
                var strData = Encoding.UTF8.GetString(responseBytes);
                var lData = strData.ToLower();
                var header = lData.IndexOf("</body>");
                var pre = strData.Substring(0, header);
                var suff = strData.Substring(header);
                strData = pre + _injectScript + suff;
                responseBytes = Encoding.UTF8.GetBytes(strData);
            }
            response.ContentLength64 = responseBytes.Length;
            var output = response.OutputStream;
            output.Write(responseBytes, 0, responseBytes.Length);
            output.Close();

        }

        private static LinkedList<string> GetFileSystemHash()
        {
            var hash = new LinkedList<string>();
            BuildFileList(hash, _path);
            return hash;
        }

        private static void BuildFileList(LinkedList<string> hash, string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith(".") || fileName.StartsWith("_")) continue;
                hash.AddLast(fileName);
                var fileInfo = new FileInfo(file);
                hash.AddLast(fileInfo.LastWriteTimeUtc.ToString("HHmmss"));
            }
            foreach (var dir in Directory.GetDirectories(path))
            {
                var lastSlash = dir.LastIndexOf("\\") + 1;
                var dirName = dir.Substring(lastSlash);
                if (dirName.StartsWith(".") || dirName.StartsWith("_")) continue;
                hash.AddLast(dirName);
                BuildFileList(hash, dir);
            }
        }
    }

}
