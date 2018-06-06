using System.Configuration;

namespace FaceGateway.FunctionsApp
{
    public static class AppSettings
    {
        public static class FaceApi
        {
            public static string Key => ConfigurationManager.AppSettings["FaceApiKey"].ToString();

            public static string Uri => ConfigurationManager.AppSettings["FaceApiUri"].ToString();

            public static string PersonGroupId => ConfigurationManager.AppSettings["PersonGroupId"].ToString();
        }

        public static class FaceGateway
        {
            public static string StorageConnectionString => ConfigurationManager.AppSettings["FaceGatewayStorage"].ToString();

            public static string ServiceBusConnectionString => ConfigurationManager.AppSettings["FaceGatewayServiceBus"].ToString();
        }
    }
}
