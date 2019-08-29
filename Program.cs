using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Post.NET
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //read configurationsAddJsonFile
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var configs = configurationBuilder.Build();

            using(var httpClientHandler = new HttpClientHandler())
            {
                //add client certificate if provided
                if(!string.IsNullOrEmpty(configs["clientCertificatePath"]))
                {
                    Console.WriteLine("Client certificate provided");
                    var clientCertificate = new X509Certificate2(configs["clientCertificatePath"]);
                    httpClientHandler.ClientCertificates.Add(clientCertificate);
                }

                using(var httpClient = new HttpClient(httpClientHandler))
                {
                    //set authentication
                    if((!string.IsNullOrEmpty(configs["userName"]) && !string.IsNullOrEmpty(configs["password"])) || !string.IsNullOrEmpty(configs["token"]))
                    {
                        if(!string.IsNullOrEmpty(configs["userName"]) && !string.IsNullOrEmpty(configs["password"]))
                        {
                            Console.WriteLine("Using basic authentication");
                            var userName = configs["userName"];
                            var password = configs["password"];
                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.Default.GetBytes($"{userName}:{password}")));
                        }
                        else
                        {
                            Console.WriteLine("Using bearer authentication");
                            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configs["token"]);
                        }
                    }

                    //setup request if needed
                    HttpContent requestContent = null;
                    if(configs["httpVerb"]?.ToUpper() != "GET" && configs["httpVerb"]?.ToUpper() != "DELETE")
                    {
                        //setup request content based on content type
                        var contentType = configs["contentType"];
                        switch(contentType)
                        {
                            case "application/x-www-form-urlencoded":
                                var formData = configs.GetSection("formDataKeyAndValues").GetChildren().ToList().Select(x => new KeyValuePair<string, string>(x.Key, x.Value));
                                requestContent = new FormUrlEncodedContent(formData);
                                break;
                            case "multipart/form-data":
                                try
                                {
                                    var fileBytes = await File.ReadAllBytesAsync(configs["fileToPostPath"]).ConfigureAwait(false);
                                    requestContent = new ByteArrayContent(fileBytes);
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine($"Error occurred loading file: {ex.Message}");
                                    return;
                                }
                                break;
                            default:
                                var requestText = await File.ReadAllTextAsync("payload.txt").ConfigureAwait(false);
                                requestContent = new StringContent(requestText, Encoding.UTF8, configs["contentType"]);
                                break;
                        }

                        //add additonal headers if necessary
                        var headers = configs.GetSection("additionalHeaders").GetChildren().ToList();
                        Console.WriteLine($"Number of additional headers: {headers.Count}");
                        foreach(var header in headers)
                        {
                            requestContent.Headers.Add(header.Key, header.Value);
                        }
                    }

                    //make request
                    HttpResponseMessage responseMessage = null;
                    var url = configs["url"];
                    var httpVerb = configs["httpVerb"].ToUpper();
                    Console.WriteLine($"HTTP Verb: {httpVerb}");
                    switch(httpVerb)
                    {
                        case "GET":
                            responseMessage = await httpClient.GetAsync(url).ConfigureAwait(false);
                            break;
                        case "POST":
                            responseMessage = await httpClient.PostAsync(url, requestContent).ConfigureAwait(false);
                            break;
                        case "PUT":
                            responseMessage = await httpClient.PutAsync(url, requestContent).ConfigureAwait(false);
                            break;
                        case "PATCH":
                            responseMessage = await httpClient.PatchAsync(url, requestContent).ConfigureAwait(false);
                            break;
                        case "DELETE":
                            responseMessage = await httpClient.DeleteAsync(url).ConfigureAwait(false);
                            break;
                        default:
                            Console.WriteLine("HTTP Verb is not valid");
                            break;
                    }

                    //write out response
                    await File.WriteAllTextAsync("response.txt", $"Status Code: {(int)responseMessage.StatusCode} {responseMessage.StatusCode.ToString()}").ConfigureAwait(false);
                    await File.AppendAllTextAsync("response.txt", Environment.NewLine);
                    await File.AppendAllTextAsync("response.txt", $"Response Body: {await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false)}");

                    Console.WriteLine("Done. Bow down to Reyes!");
                }
            }
        }
    }
}
