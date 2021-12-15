using Greet;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Greet.GreetingService;

namespace server
{
    public class GreetingServiceImpl : GreetingServiceBase
    {
        #region gRPC Unary
        public override Task<GreetingResponse> Greet(GreetingRequest request, ServerCallContext context)
        {
            string result = String.Format("hello {0} {1}", request.Greeting.FirstName, request.Greeting.LastName);
            
            return Task.FromResult(new GreetingResponse() { Result = result});
        }

        public override async Task<GreetWithDeadlineResponse> GreetWithDeadline(GreetWithDeadlineRequest request, ServerCallContext context)
        {
            await Task.Delay(300);
            return new GreetWithDeadlineResponse() { Result = $"Hello {request.Name} (with Deadline)"};
        }
        #endregion

        #region gRPC Server Streaming
        public override async Task GreetManyTimes(GreetingManyTimesRequest request, IServerStreamWriter<GreetingManyTimesResponse> responseStream, ServerCallContext context)
        {
            Console.WriteLine("The server received the request: ");
            Console.WriteLine(request.ToString());

            string result = String.Format("hello {0} {1}", request.Greeting.FirstName, request.Greeting.LastName);

            foreach (int i in Enumerable.Range(1, 10))
            { 
                await responseStream.WriteAsync(new GreetingManyTimesResponse() { Result = $"{result} ({i} -th) Server Stream Response" });
            }
        }
        #endregion

        #region gRPC Client Streaming
        public override async Task<LongGreetResponse> LongGreet(IAsyncStreamReader<LongGreetRequest> requestStream, ServerCallContext context)
        {
            string result = "";
            while (await requestStream.MoveNext())
            {
                result += String.Format("Hello {0} {1} {2}",
                    requestStream.Current.Greeting.FirstName,
                    requestStream.Current.Greeting.LastName,
                    "_");
            }

            return new LongGreetResponse() { Result = result };
        }
        #endregion

        #region gRPC Bi-Directional Streaming
        public override async Task GreetEverone(IAsyncStreamReader<GreetEveryoneRequest> requestStream, IServerStreamWriter<GreetEveryoneResponse> responseStream, ServerCallContext context)
        {
            int counter = 0;
            while (await requestStream.MoveNext())
            {
                counter++;
                var result = String.Format("Hello {0} {1} for the {2} time",
                    requestStream.Current.Greeting.FirstName,
                    requestStream.Current.Greeting.LastName,
                    counter.ToString());

                Console.WriteLine("Received: " + result);
                // response is not sent after while loop ends, but immediatelly when processed
                await responseStream.WriteAsync(new GreetEveryoneResponse() { Result = result });
            }    
        }
        #endregion
    }
}
