using Calculator;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Calculator.CalculatorService;

namespace server
{
    public class CalculatorServiceImpl : CalculatorServiceBase
    {
        #region gRPC Unary
        public override Task<CalculatorResponse> Sum(CalculatorRequest request, ServerCallContext context)
        {
            var result = request.FirstNumber + request.SecondNumber;
            return Task.FromResult(new CalculatorResponse() { Result = result});
        }
        #endregion

        #region gRPC Server Streaming
        public override async Task PrimeNumberDecomposition(PrimeNumRequest request, IServerStreamWriter<PrimeNumResponse> responseStream, ServerCallContext context)
        {
            Console.WriteLine("The server received the request: ");
            Console.WriteLine(request.ToString());

            int number = request.Number;
            int divisior = 2;
            while (number > 1)
            {
                if (number % divisior == 0)
                {
                    await responseStream.WriteAsync(new PrimeNumResponse() { PrimeNum = divisior });
                    number = number / divisior;
                }
                else
                    divisior = divisior + 1;
            }
        }

        public override async Task ComputeAllDivisors(ComputeDivisorsRequest request, IServerStreamWriter<ComputeDivisorsResponse> responseStream, ServerCallContext context)
        {
            int number = request.Number;
            for (int i = 2; i <= number / 2; i++)
            {
                if (number % i == 0)
                    await responseStream.WriteAsync(new ComputeDivisorsResponse() { Divisor = i });
            }
        }
        #endregion

        #region gRPC Client Streaming
        public override async Task<ComputeAverageResponse> ComputeAverage(IAsyncStreamReader<ComputeAverageRequest> requestStream, ServerCallContext context)
        {
            double sum = 0;
            int counter = 0;
            while (await requestStream.MoveNext())
            {
                sum += requestStream.Current.Number;
                counter++;
            }

            return new ComputeAverageResponse() { Result = (sum / counter) };
        }

        public override async Task<ComputeMinMaxResponse> ComputeMinMax(IAsyncStreamReader<ComputeMinMaxRequest> requestStream, ServerCallContext context)
        {
            List<float> numbers = new List<float>();
            while (await requestStream.MoveNext())
            { 
                numbers.Add(requestStream.Current.Number);
            }
            return new ComputeMinMaxResponse() 
            {  
                Min = numbers.Min(),
                Max = numbers.Max()
            };
        }
        #endregion

        #region gRPC Bi-Directional Streaming
        public override async Task ComputeCurrentMax(IAsyncStreamReader<ComputeCurrentMaxRequest> requestStream, IServerStreamWriter<ComputeCurrentMaxResponse> responseStream, ServerCallContext context)
        {
            //First, handle incoming data to the Server from the Client
            float? max = null;
            while (await requestStream.MoveNext())
            {
                    if (max == null || max < requestStream.Current.Number)
                    { 
                        max = requestStream.Current.Number;
                        Console.WriteLine("Received: " + max.ToString());
                        //Now, handle sending the processed data back to the Client
                        await Task.Delay(1);
                        await responseStream.WriteAsync(new ComputeCurrentMaxResponse() { Max = max.Value });
                    }
            }
        }
        #endregion
    }
}