using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace database_server
{
    class BasicHttpListener
    {
        
        public BasicHttpListener()
        {
            mainHTTPThread = new Thread((myParams) =>
            {
                var myDatabase = LogDataBase.getTheDatabase();
                string[] prefixes = { "http://localhost:8000/" };
                // Create a listener.
                HttpListener listener = new HttpListener();
                // Add the prefixes.
                foreach (string s in prefixes)
                {
                    listener.Prefixes.Add(s);
                }
                listener.Start();
                Console.WriteLine("Listening...");
                
                while (!isInhterrupted)
                {
                    // Note: The GetContext method blocks while waiting for a request. 
                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    var myQuery = request.QueryString;
                    //Console.WriteLine(myQuery);
                    //Console.WriteLine(myQuery["key"]);
                    //Console.WriteLine(myQuery["value"]);
                    var myKey = myQuery["key"];
                    var myValue = myQuery["value"];
                    string responseString="";
                    if (myValue == null)
                    {
                        // PUT into the database
                        var newValue = myDatabase.Get(myKey).Result;
                        responseString = $"<HTML><BODY>Found Correpdoning value for ${myKey} to be ${newValue} in DataBase!</BODY></HTML>";
                    }
                    else
                    {
                        try
                        {
                            _ = myDatabase.Add(myKey, myValue).Result;
                            responseString = $"<HTML><BODY>Added ${myKey} -> ${myValue} to DataBase!</BODY></HTML>";
                        }
                        catch (Exception ex)
                        {
                            responseString = $"<HTML><BODY>Caught Exception while adding ${myKey} with value ${myValue} to DataBase!, key and value not added! </BODY></HTML>";
                        }
                    }
                    // Obtain a response object.
                    HttpListenerResponse response = context.Response;
                    // Construct a response.
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    // Get a response stream and write the response to it.
                    response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    // You must close the output stream.
                    output.Close();
                }
                isFinsihed = true;
                listener.Stop();
            });
            mainHTTPThread.Start();
        }
        private Thread mainHTTPThread;
        private volatile bool isInhterrupted = false;
        public bool TerminateServer(long timeOutMS)
        {
            isInhterrupted = true;
            
            while(isFinsihed != true)
            {
                Thread.Sleep(1000);
                // if timeout return false;
            }
            return true;
        }
        private bool isFinsihed = false;
    }
}
