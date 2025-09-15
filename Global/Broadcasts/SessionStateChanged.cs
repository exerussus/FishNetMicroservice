using FishNet.Broadcast;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Global.Broadcasts
{
    internal struct SessionStateChanged : IBroadcast
    {
        public SessionStateChanged(bool started)
        {
            Started = started;
        }

        public readonly bool Started;
    }
}