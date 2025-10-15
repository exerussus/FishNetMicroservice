namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client.Models
{
    public class RunClientResponse
    {
        public RunClientResponse(RunResult result)
        {
            Result = result;
        }

        public RunResult Result { get; private set; }

        internal static class Handle
        {
            public static void SetResult(RunClientResponse response, RunResult runResult) => response.Result = runResult;
        }
    }

    public enum RunResult
    {
	    NotConnected = 0,
	    ConnectorIsNull = 1,
	    AlreadyInProcess = 2,
	    AuthenticationError = 3,
	    Authenticated = 4
    }
}