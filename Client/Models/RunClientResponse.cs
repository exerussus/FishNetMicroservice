namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client.Models
{
    public class RunClientResponse
    {
        public RunResult Result { get; private set; }

        internal static class Handle
        {
            public static void SetResult(RunClientResponse response, RunResult runResult) => response.Result = runResult;
        }
    }

    public enum RunResult
    {
	    NotConnected = 0,
	    AuthenticationError = 1,
	    Authenticated = 2
    }
}