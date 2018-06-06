using Newtonsoft.Json;
using System;

namespace FaceGateway.FunctionsApp
{
    public class AlertMessage
    {
        [JsonProperty(PropertyName = "camId")]
        public Guid CameraId { get; set; }

        [JsonProperty(PropertyName = "in")]
        public string ImageName { get; set; }

        [JsonProperty(PropertyName = "t")]
        public long Timestamp { get; set; }

        [JsonProperty(PropertyName = "faceIds")]
        public Guid[] FaceIds { get; set; }
    }
}
