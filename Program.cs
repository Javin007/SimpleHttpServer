using DamienG.Security.Cryptography;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace HttpServer
{
    class Program
    {
        static string _guid = "10C20077DAFE49EDA3FBE97A955F2B6E";
        static string _path;
        static string _hash;
        private static string _allowedCharacters = @"^[a-zA-Z0-9-_.\/]+$";
        private static Regex _regEx = new Regex(_allowedCharacters);
        private static Timer _hashTimer;

        private static Crc32 crc32 = new Crc32();

        static void Main(string[] args)
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
            msg.Append("Mapping: ");
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
                    HandleRequest(listener.GetContext());
                }
            })).Start();

            Console.WriteLine("Done.");

            _hashTimer = new Timer(TimerTick, null, 0, 1000);

        }

        private static void TimerTick(object state)
        {
            var hash = GetFileSystemHash();
            if (_hash != hash)
            {
                Console.WriteLine("File system change detected.");
                _hash = hash;
            }
        }

        public static bool IsValidRequest(string url)
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

        private static void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var url = Uri.UnescapeDataString(request.RawUrl).Substring(1);

            if (url.StartsWith(_guid))
            {
                var split = url.Split('/');
                if (_hash == split[1])
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Accepted;
                }
                context.Response.OutputStream.Close();
                return;
            }

            if (string.IsNullOrEmpty(url.Trim())) url = "index.htm";

            Console.Write("Request: " + url + " ");

            var response = context.Response;

            if ((request.HttpMethod.ToUpper() != "GET") || (!IsValidRequest(url)))
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

            var contentType = GetContentType(Path.GetExtension(filename).ToLower());
            if (contentType == null)
            {
                Console.WriteLine("(Forbidden)");
                response.StatusCode = (int)HttpStatusCode.Forbidden;
                response.OutputStream.Close();
                return;
            }

            Console.WriteLine("(OK)");
            response.ContentType = contentType;
            response.StatusCode = (int)HttpStatusCode.OK;
            var responseBytes = File.ReadAllBytes(filename);
            if (filename.EndsWith("\\index.htm")) {
                var strData = Encoding.UTF8.GetString(responseBytes);
                var lData = strData.ToLower();
                var header = lData.IndexOf("</body>");
                var pre = strData.Substring(0, header);
                var suff = strData.Substring(header);
                strData = pre + _injectScript.Replace("pH", _guid + "/" + _hash) + suff;
                responseBytes = Encoding.UTF8.GetBytes(strData);
            }
            response.ContentLength64 = responseBytes.Length;
            var output = response.OutputStream;
            output.Write(responseBytes, 0, responseBytes.Length);
            output.Close();

        }

        private static string GetContentType(string ext)
        {
            if (ext == ".htm") return "text/html";
            if (ext == ".html") return "text/html";
            if (ext == ".js") return "text/javascript";
            if (ext == ".css") return "text/css";
            if (ext == ".ico") return "image/vnd.microsoft.icon";
            if (ext == ".jpg") return "image/jpeg";
            if (ext == ".jpeg") return "image/jpeg";
            if (ext == ".png") return "image/png";
            if (ext == ".gif") return "image/gif";
            if (ext == ".bmp") return "image/bmp";
            if (ext == ".json") return "application/json";
            if (ext == ".svg") return "image/svg+xml";
            if (ext == ".tif") return "image/tiff";
            if (ext == ".tiff") return "image/tiff";
            if (ext == ".swf") return "application/x-shockwave-flash";
            if (ext == ".map") return "text/javascript";

            if (ext == ".aac") return "audio/aac";
            if (ext == ".abw") return "application/x-abiword";
            if (ext == ".arc") return "application/x-freearc";
            if (ext == ".avi") return "video/x-msvideo";
            if (ext == ".azw") return "application/vnd.amazon.ebook";
            if (ext == ".bin") return "application/octet-stream";
            if (ext == ".bz") return "application/x-bzip";
            if (ext == ".bz2") return "application/x-bzip2";
            if (ext == ".csh") return "application/x-csh";
            if (ext == ".csv") return "text/csv";
            if (ext == ".doc") return "application/msword";
            if (ext == ".docx") return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            if (ext == ".eot") return "application/vnd.ms-fontobject";
            if (ext == ".epub") return "application/epub+zip";
            if (ext == ".gz") return "application/gzip";
            if (ext == ".ics") return "text/calendar";
            if (ext == ".jar") return "application/java-archive";
            if (ext == ".jsonld") return "application/ld+json";
            if (ext == ".mid") return "audio/midi audio/x-midi";
            if (ext == ".midi") return "audio/midi audio/x-midi";
            if (ext == ".mjs") return "text/javascript";
            if (ext == ".mp3") return "audio/mpeg";
            if (ext == ".mpeg") return "video/mpeg";
            if (ext == ".mpkg") return "application/vnd.apple.installer+xml";
            if (ext == ".odp") return "application/vnd.oasis.opendocument.presentation";
            if (ext == ".ods") return "application/vnd.oasis.opendocument.spreadsheet";
            if (ext == ".odt") return "application/vnd.oasis.opendocument.text";
            if (ext == ".oga") return "audio/ogg";
            if (ext == ".ogv") return "video/ogg";
            if (ext == ".ogx") return "application/ogg";
            if (ext == ".opus") return "audio/opus";
            if (ext == ".otf") return "font/otf";
            if (ext == ".pdf") return "application/pdf";
            // if (ext == ".php") return "application/php";
            if (ext == ".ppt") return "application/vnd.ms-powerpoint";
            if (ext == ".pptx") return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
            if (ext == ".rar") return "application/x-rar-compressed";
            if (ext == ".rtf") return "application/rtf";
            if (ext == ".sh") return "application/x-sh";
            if (ext == ".tar") return "application/x-tar";
            if (ext == ".ts") return "video/mp2t";
            if (ext == ".ttf") return "font/ttf";
            if (ext == ".txt") return "text/plain";
            if (ext == ".vsd") return "application/vnd.visio";
            if (ext == ".wav") return "audio/wav";
            if (ext == ".weba") return "audio/webm";
            if (ext == ".webm") return "video/webm";
            if (ext == ".webp") return "image/webp";
            if (ext == ".woff") return "font/woff";
            if (ext == ".woff2") return "font/woff2";
            if (ext == ".xhtml") return "application/xhtml+xml";
            if (ext == ".xls") return "application/vnd.ms-excel";
            if (ext == ".xlsx") return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            if (ext == ".xml") return "application/xml";
            if (ext == ".xul") return "application/vnd.mozilla.xul+xml";
            if (ext == ".zip") return "application/zip";
            if (ext == ".3gp") return "video/3gpp";
            if (ext == ".3g2") return "video/3gpp2";
            if (ext == ".7z") return "application/x-7z-compressed";
            return null;
        }

        private static string GetFileSystemHash()
        {
            var sb = new StringBuilder();
            BuildFileList(sb, _path);
            var bytes = Encoding.ASCII.GetBytes(sb.ToString());
            sb.Clear();
            foreach (var b in crc32.ComputeHash(bytes))
            {
                sb.Append(b.ToString("x2").ToLower());
            }
            return sb.ToString();
        }

        private static void BuildFileList(StringBuilder sb, string path)
        {
            foreach (var file in Directory.GetFiles(path))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith(".") || fileName.StartsWith("_")) continue;
                sb.Append(fileName + "/");
                var fileInfo = new FileInfo(file);
                sb.Append(fileInfo.LastWriteTimeUtc.ToString("HHmmss"));
                sb.Append("|");
            }
            foreach (var dir in Directory.GetDirectories(path))
            {
                var lastSlash = dir.LastIndexOf("\\") + 1;
                var dirName = dir.Substring(lastSlash);
                if (dirName.StartsWith(".") || dirName.StartsWith("_")) continue;
                sb.Append(dirName);
                sb.Append("|");
                BuildFileList(sb, dir);
            }
        }

        private static string _injectScript = "<script>setInterval(function(){var e=new XMLHttpRequest(0);e.open(\"GET\",\"pH\"),e.onreadystatechange=(t=>{200!==e.status&&location.reload(!0)}),e.send()},1e3);</script>";

        //private static string _injectScript =
        //    "		<script>\r\n" +
        //    "           setInterval(function() {\r\n" +
        //    "   			var http = new XMLHttpRequest(0);\r\n" +
        //    "               var url='pH';\r\n" +
        //    "               http.open(\"GET\", url);\r\n" +
        //    "			    http.onreadystatechange=(e)=>{\r\n" +
        //    "   				if (http.status !== 200) {\r\n" +
        //    "                       //location.reload(true);\r\n" +
        //    "                   }\r\n" +
        //    "	    		}\r\n" +
        //    "		    	http.send();\r\n" +
        //    "           }, 1000);\r\n" +
        //    "		</script>";

    }
}
