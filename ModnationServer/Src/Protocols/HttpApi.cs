﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using HttpLib;

using ModnationServer.Src.Log;


namespace ModnationServer.Src.Protocols
{
    class HttpApi
    {
        public string ServiceName { get; }

        TcpListener listener;
        X509Certificate2 cert;

        Dictionary<string, Action<TcpClient, HttpRequest, HttpResponse>> methods = new Dictionary<string, Action<TcpClient, HttpRequest, HttpResponse>>();

        public HttpApi(string serviceName, string ip, ushort port)
        {
            ServiceName = serviceName;
            listener = new TcpListener(IPAddress.Parse(ip), port);
            new Thread(() => ListenThread()).Start();
        }

        public HttpApi(string ip, ushort port, string certPath, string pass)
        {
            cert = new X509Certificate2(certPath, pass);
            listener = new TcpListener(IPAddress.Parse(ip), port);
            new Thread(() => ListenThread()).Start();
        }

        public void RegisterMethod(string path, Action<TcpClient, HttpRequest, HttpResponse> method)
        {
            methods.Add(path, method);
            Logging.Log(typeof(HttpApi), "[{0}] Registered method {1}", LogType.Debug, ServiceName, path);
        }

        void ListenThread()
        {
            listener.Start();
            while (true)
            {
                var client = listener.AcceptTcpClient();
                new Thread(() => ProcessThread(client)).Start();
            }
        }

        void ProcessThread(TcpClient client)
        {
            HttpStream stream;
            try
            {
                if (cert == null)
                    stream = new HttpStream(client.GetStream());
                else
                {
                    var ssl = new SslStream(client.GetStream(), false);
                    ssl.AuthenticateAsServer(cert, false, System.Security.Authentication.SslProtocols.Tls12, false);
                    stream = new HttpStream(ssl);
                }
                var request = stream.Read();
                var response = new HttpResponse();
                response.Headers["server"] = string.Format(@"{0}/1.0", ServiceName);
                response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
                response.Headers.Add("Connection", "close");
                if (request.ContainsHeader("Cookie"))
                {
                    var header = request.GetHeader("Cookie");
                    response.Headers.Add("Cookie", header);
                }
                Logging.Log(typeof(HttpApi), "[{0}] IP: {1}", LogType.Debug, ServiceName, ((IPEndPoint)client.Client.RemoteEndPoint).Address);
                Logging.Log(typeof(HttpApi), "[{0}] {1} {2}", LogType.Info, ServiceName, request.RequestMethod, request.Uri);
                if (methods.ContainsKey(request.Uri))
                {
                    methods[request.Uri](client, request, response);
                }
                else
                {
                    Logging.Log(typeof(HttpApi), "Method does not exist!", LogType.Error);
                    response.StatusCode = new HttpLib.HttpStatusCode(HttpLib.HttpStatusCode.StatusCode.NotFound);
                }
                response.Headers.Add("ETag", "\"" + BitConverter.ToString(MD5.Create().ComputeHash(response.Data)).Replace("-", "").ToLower() + "\"");
                stream.Write(response);
            }
            catch (Exception e)
            {
                Logging.Log(typeof(HttpApi), e.ToString(), LogType.Error);
            }
            client.Close();
        }

        public static Dictionary<string, string> DecodeUriParameters(byte[] data, Encoding enc) => DecodeUriParameters(enc.GetString(data));

        public static Dictionary<string, string> DecodeUriParameters(string data)
        {

            var uriParameters = new Dictionary<string, string>();
            if (data.Length > 1)
            {
                foreach (var param in data.Split('&'))
                {
                    var paramPair = param.Split('=');
                    uriParameters.Add(System.Web.HttpUtility.UrlDecode(paramPair[0]), paramPair.Length > 1 ? System.Web.HttpUtility.UrlDecode(paramPair[1]) : null);
                }
            }
            return uriParameters;
        }
    }
}
