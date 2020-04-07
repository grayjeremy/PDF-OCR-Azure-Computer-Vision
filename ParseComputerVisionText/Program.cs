using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ParseComputerVisionText
{
    class Program
    {
        static string subscriptionKey = "";

        static string endpoint = "https://eastus.api.cognitive.microsoft.com/";
        

        // the Analyze method endpoint
        static string uriAnalyzeBase = endpoint + "/vision/v2.0/read/core/asyncBatchAnalyze";

        // the Read method endpoint
        static string uriReadBase = endpoint + "/vision/v2.0/read/operations";        

        static void Main(string[] args)
        {           
            DirectoryInfo d = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\pdf");

            foreach (var pdfFile in d.GetFiles("*.pdf"))
            {
                Console.WriteLine("OCR: " + pdfFile.Name);
                var requestId = MakeAnalysisRequest(pdfFile.FullName);
                Console.WriteLine("Waiting for Response");
                var jsonContents = PollForResponse(requestId);
                
                var jsonFile = WriteJsonFile(pdfFile, jsonContents);
                Console.WriteLine("Wrote File: " + jsonFile.Name);
                var txtFile = WriteTextFile(jsonFile);
                Console.WriteLine("Wrote file : " + txtFile.Name);
            }
            Console.WriteLine("Done!");
            Console.ReadLine();
        }

        private static FileInfo WriteJsonFile(FileInfo pdfFile, string json)
        {
            var jsonFile = new FileInfo (Path.GetFileNameWithoutExtension(pdfFile.Name) + ".json");                     
            File.WriteAllText(jsonFile.FullName, json);
            return jsonFile;            
        }

        private static string PollForResponse(string requestId)
        {
            var operationId = new Uri(requestId).Segments.Last();            
            HttpClient client = new HttpClient();

            // Request headers.
            client.DefaultRequestHeaders.Add(
                "Ocp-Apim-Subscription-Key", subscriptionKey);

            // Assemble the URI for the REST API method.
            string uri = uriReadBase + "/" + operationId;

            var content = "";
            bool loopAgain = true;
            
            while(loopAgain)
            { 
                HttpResponseMessage response;
                response = client.GetAsync(uri).Result;

                content = response.Content.ReadAsStringAsync().Result;

                loopAgain = (content == "{\"status\":\"Running\"}" || content == "{\"status\":\"NotStarted\"}") ? true : false;

                if (loopAgain)
                {
                    Console.WriteLine("Requesting Operation: " + operationId);
                    Thread.Sleep(1000);
                }
            }

            return content;
        }

        private static FileInfo WriteTextFile(FileInfo jsonFile)
        {                      
            var outputFile = new FileInfo(Path.GetFileNameWithoutExtension(jsonFile.Name) + ".txt");

            using (StreamReader r = new StreamReader(jsonFile.FullName))
            {
                string json = r.ReadToEnd();
                VisionResponse items = JsonConvert.DeserializeObject<VisionResponse>(json);

                //TODO: only select lines that are within the bounding box of a body.  section.IsWithin(body) where section and body are arrays of ints [5.5584, 1.7242, 5.9043, 1.7246, 5.9087, 1.8956, 5.5629, 1.8978]
                //var text = items.recognitionResults.Select(page => page.lines.Where(box => box.boundingBox[0] > .75 && box.boundingBox[1] > 1.8).Select(line => line.text));
                var text = items.recognitionResults.Select(page => page.lines.Select(line => line.text));                

                using (StreamWriter writer = new StreamWriter(outputFile.FullName))
                {
                    foreach (var page in text)
                    {
                        foreach (var line in page)
                        {
                            //TODO: if the "line" is a q:,a:,b:, don't writeline, do a write + a tab to identify the speaker. Regex: "[A-Z]?:"
                            writer.WriteLine(line);                       
                        }
                    }
                }
            }
            return outputFile;
        }

        /// <summary>
        /// Gets the analysis of the specified image file by using
        /// the Computer Vision REST API.
        /// </summary>
        /// <param name="imageFilePath">The image file to analyze.</param>
        static string MakeAnalysisRequest(string imageFilePath)
        {
            try
            {
                HttpClient client = new HttpClient();

                // Request headers.
                client.DefaultRequestHeaders.Add(
                    "Ocp-Apim-Subscription-Key", subscriptionKey);
              
                // Assemble the URI for the REST API method.
                string uri = uriAnalyzeBase;

                HttpResponseMessage response;

                // Read the contents of the specified local image
                // into a byte array.
                byte[] byteData = GetImageAsByteArray(imageFilePath);

                // Add the byte array as an octet stream to the request body.
                using (ByteArrayContent content = new ByteArrayContent(byteData))
                {
                    // This example uses the "application/octet-stream" content type.
                    // The other content types you can use are "application/json"
                    // and "multipart/form-data".
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/octet-stream");

                    // Asynchronously call the REST API method.
                    response = client.PostAsync(uri, content).Result;
                }

                // Asynchronously get the JSON response.
                string responseHeader = response.Headers.GetValues("Operation-Location").First();

                // Display the JSON response.
                Console.WriteLine("Response Accepted Id: " + responseHeader);

                return responseHeader;
            }
            catch (Exception e)
            {
                Console.WriteLine("\n" + e.Message);
            }
            return null;
        }

        /// <summary>
        /// Returns the contents of the specified file as a byte array.
        /// </summary>
        /// <param name="imageFilePath">The image file to read.</param>
        /// <returns>The byte array of the image data.</returns>
        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            // Open a read-only file stream for the specified file.
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                // Read the file's contents into a byte array.
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }
    }
}
