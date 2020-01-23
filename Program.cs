using System;
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
        static string _path;

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
                Console.WriteLine("Done.");
                while (listener.IsListening)
                {
                    HandleRequest(listener.GetContext());
                }
            })).Start();
        }

        private static string _allowedCharacters = @"^[a-zA-Z0-9-_.\/]+$";
        private static Regex _regEx = new Regex(_allowedCharacters);
        public static bool IsValidRequest(string url)
        {
            if (url.StartsWith(".")) return false;
            if (url.EndsWith(".")) return false;
            if (url.Contains("..")) return false;
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

    }
}
