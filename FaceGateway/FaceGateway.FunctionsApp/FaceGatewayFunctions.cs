using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ProjectOxford.Face;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FaceGateway.FunctionsApp
{
    public static class FaceGatewayFunctions
    {
        private static ExecutionContext _context;
        private static TraceWriter _log;

        [FunctionName("FaceDetectorTrigger")]
        public static async Task RunAsync([BlobTrigger("face-detection-trigger-tray/{name}", Connection = "FaceGatewayStorage")]Stream inputImageStream,
                                          [Blob("face-identified-tray/{name}", FileAccess.Write, Connection = "FaceGatewayStorage")]Stream outputImage,
                                          [ServiceBus("recognition", AccessRights.Manage, Connection = "FaceGatewayServiceBus")] ICollector<string> messages,
                                          string name, TraceWriter log, ExecutionContext context)
        {
            _context = context;
            _log = log;
            _log.Info($"[{_context.InvocationId.ToString()}] Processing image => {{ name: {name}, size: {inputImageStream.Length} bytes }}");

            var identifiedFaces = await IdentifyAsync(inputImageStream, outputImage, name);
            await DeleteBlobAsync(name);

            if (identifiedFaces.Any())
            {
                messages.Add(CreateMessage(name, identifiedFaces));
                _log.Info($"[{_context.InvocationId.ToString()}] Message sent...");
            }

            _log.Info($"[{_context.InvocationId.ToString()}] Face detection process successfully completed!");
        }

        private static string CreateMessage(string name, IEnumerable<Guid> identifiedFaces)
        {
            _log.Info($"[{_context.InvocationId.ToString()}] Creating message...");

            // TODO: get timestamp from blob name
            DateTime datetime = DateTime.UtcNow;
            long timestamp = ((DateTimeOffset)datetime).ToUnixTimeSeconds();

            var message = new AlertMessage
            {
                CameraId = Guid.NewGuid(),
                ImageName = name,
                FaceIds = identifiedFaces.ToArray(),
                Timestamp = timestamp

            };

            return JsonConvert.SerializeObject(message);
        }

        private static async Task DeleteBlobAsync(string blobName)
        {
            var storageAccount = CloudStorageAccount.Parse(AppSettings.FaceGateway.StorageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference("face-detection-trigger-tray");

            blobContainer.CreateIfNotExists();
            blobContainer.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

            var blockBlob = blobContainer.GetBlockBlobReference(blobName);

            await blockBlob.DeleteAsync();

            _log.Info($"[{_context.InvocationId.ToString()}] Blob deleted: {blobName}");
        }

        private static async Task<IEnumerable<Guid>> IdentifyAsync(Stream inputImageStream, Stream outputImage, string name)
        {
            var identifiedFaces = new List<Guid>();
            var faceServiceClient = new FaceServiceClient(AppSettings.FaceApi.Key, AppSettings.FaceApi.Uri);
            var personGroupId = AppSettings.FaceApi.PersonGroupId;
            var image = Image.FromStream(inputImageStream);
            var pen = new Pen(Color.Red, 2);            

            inputImageStream.Seek(0, SeekOrigin.Begin);
                
            var faces = await faceServiceClient.DetectAsync(inputImageStream);
            var faceIds = faces.Select(face => face.FaceId).ToArray();

            if (!faceIds.Any())
            {
                _log.Info($"[{_context.InvocationId.ToString()}] No faces detected");

                return null;
            }

            var results = await faceServiceClient.IdentifyAsync(personGroupId, faceIds);

            foreach (var result in results)
            {
                if (result.Candidates.Length == 0) continue;

                var candidateId = result.Candidates[0].PersonId;
                var person = await faceServiceClient.GetPersonAsync(personGroupId, candidateId);
                var face = faces.Where(f => f.FaceId == result.FaceId).FirstOrDefault().FaceRectangle;

                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.DrawRectangle(pen, new Rectangle(face.Left, face.Top, face.Width, face.Height));
                }

                identifiedFaces.Add(result.FaceId);
                _log.Info($"[{_context.InvocationId.ToString()}] FaceId: {result.FaceId} => Identified as: {person.Name}");
            }

            using (var stream = new MemoryStream())
            {
                image.Save(stream, ImageFormat.Png);

                var byteArray = stream.ToArray();

                await outputImage.WriteAsync(byteArray, 0, byteArray.Length);
            }

            return identifiedFaces;
        }
    }
}
