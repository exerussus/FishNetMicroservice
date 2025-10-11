namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class StopServerSettings
    {
        public StopServerSettings(string messageToClients = null, float delay = 0)
        {
            MessageToClients = messageToClients;
            Delay = delay;
        }

        public string MessageToClients { get; }
        public float Delay { get; }
    }
}