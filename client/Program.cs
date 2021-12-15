using Blog;
using Calculator;
using Dummy;
using Greet;
using Grpc.Core;
using Sqrt;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace client
{
    internal class Program
    {
        const string target = "127.0.0.1:50051";

        static async Task Main(string[] args)
        {
            Channel channel = new Channel(target, ChannelCredentials.Insecure);
            await channel.ConnectAsync().ContinueWith((task) =>
            {
                if (task.Status == TaskStatus.RanToCompletion)
                    Console.WriteLine("The client connected successfully");
            });

            #region gRPC Unary
            var client = new DummyService.DummyServiceClient(channel);
            var clGreetingService = new GreetingService.GreetingServiceClient(channel);

            var greeting = new Greeting() { FirstName = "Piotr", LastName = "Gamorski" };
            var request = new GreetingRequest() { Greeting = greeting };

            var response = clGreetingService.Greet(request);
            Console.WriteLine(response.Result);
            var asyncResponse = clGreetingService.GreetAsync(request);
            Console.WriteLine(asyncResponse.ResponseAsync.Result.Result + "(Async response)");

            var clCalculatorService = new CalculatorService.CalculatorServiceClient(channel);
            var calculatorRequest = new CalculatorRequest() { FirstNumber = 10, SecondNumber = 3 };
            var calculatorResponse = clCalculatorService.Sum(calculatorRequest);
            Console.WriteLine(calculatorResponse.Result);

            var clSqrtService = new SqrtService.SqrtServiceClient(channel);
            await DoSafeSqrt(clSqrtService, -1);

            await DoGreetWithDeadline(clGreetingService, 200);
            #endregion

            #region gRPC Server Streaming
            var greetManyTimesRequest = new GreetingManyTimesRequest() { Greeting = greeting };
            var greetManyTimesResponse = clGreetingService.GreetManyTimes(greetManyTimesRequest);
            while (await greetManyTimesResponse.ResponseStream.MoveNext())
            {
                Console.WriteLine(greetManyTimesResponse.ResponseStream.Current.Result);
                await Task.Delay(200);
            }

            //Prime Number Decomposition Excercise (see Calculator proto file)
            var primeNumRequest = new PrimeNumRequest() { Number = 120 };
            var primeNumResponse = clCalculatorService.PrimeNumberDecomposition(primeNumRequest);
            while (await primeNumResponse.ResponseStream.MoveNext())
            {
                Console.WriteLine(primeNumResponse.ResponseStream.Current.PrimeNum);
                await Task.Delay(200);
            }

            // ComputeAllDivisors Excercise (see Calculator proto file)
            var computeDivisorsRequest = new ComputeDivisorsRequest() { Number = 120 };
            var computeDivisorsResponse = clCalculatorService.ComputeAllDivisors(computeDivisorsRequest);
            while (await computeDivisorsResponse.ResponseStream.MoveNext())
            {
                Console.WriteLine($"Divisor of {computeDivisorsRequest.Number} is: {computeDivisorsResponse.ResponseStream.Current.Divisor}");
                await Task.Delay(200);
            }
            #endregion

            #region gRPC Client Streaming
            var longGreetRequest = new LongGreetRequest() { Greeting = greeting };
            var longGreetStream = clGreetingService.LongGreet();
            foreach (int i in Enumerable.Range(1, 10))
            {
                await longGreetStream.RequestStream.WriteAsync(longGreetRequest);
            }
            await longGreetStream.RequestStream.CompleteAsync();
            var longGreetResponse = await longGreetStream.ResponseAsync;
            Console.WriteLine(longGreetResponse.Result);

            // Compute Average Exercise (see Calculator proto file)
            var computeAverageStream = clCalculatorService.ComputeAverage();
            foreach (int i in Enumerable.Range(1, 9000))
            {
                var computeAverageRequest = new ComputeAverageRequest() { Number = i };
                await computeAverageStream.RequestStream.WriteAsync(computeAverageRequest);
            }
            await computeAverageStream.RequestStream.CompleteAsync();
            var computeAverageResponse = await computeAverageStream.ResponseAsync;
            Console.WriteLine(computeAverageResponse.Result);

            // ComputeMinMax Exercise (see Calculator proto file)
            var computeMinMaxStream = clCalculatorService.ComputeMinMax();
            foreach (int i in Enumerable.Range(1, 1000))
            {
                var computeMinMaxRequest = new ComputeMinMaxRequest() { Number = (float)new Random().NextDouble() * 100 };
                await computeMinMaxStream.RequestStream.WriteAsync(computeMinMaxRequest);
            }
            await computeMinMaxStream.RequestStream.CompleteAsync();
            var computeMinMaxResponse = await computeMinMaxStream.ResponseAsync;
            Console.WriteLine($"The min value of the stream is: {computeMinMaxResponse.Min} and the max is: {computeMinMaxResponse.Max}");
            #endregion

            #region gRPC Bi-Directional Streaming
            await DoGreetEveryone(clGreetingService);
            await DoComputeCurrentMax(clCalculatorService);
            #endregion

            #region MongoDB CRUD
            var clBlogService = new BlogService.BlogServiceClient(channel);
            var createBlogRequest = new CreateBlogRequest()
            {
                Blog = new Blog.Blog()
                {
                    AuthorId = "Piotr",
                    Title = "New blog!",
                    Content = "This blog was created at" + DateTime.UtcNow.ToString(),
                }
            };
            var readBlogRequest = new ReadBlogRequest() { BlogId = "61b4e0a3f17dbd0e7901c611" };

            //await DoCreateBlog(clBlogService, createBlogRequest);

            await DoReadBlog(clBlogService, readBlogRequest);
            var updateBlogRequest = new UpdateBlogRequest()
            {
                Blog = new Blog.Blog()
                {
                    Id = "61b4e0a3f17dbd0e7901c611",
                    AuthorId = "Piotr",
                    Title = "Blog Updated at " + DateTime.UtcNow.ToString(),
                    Content = "This Updated blog!"
                }
            };
            await DoUpdateBlog(clBlogService, updateBlogRequest);

            var deleteBlogRequest = new DeleteBlogRequest() { BlogId = "61b90d011e73ee1176429729" };
            await DoDeleteBlog(clBlogService, deleteBlogRequest);
            await DoListBlog(clBlogService);
            #endregion

            channel.ShutdownAsync().Wait();
            Console.ReadKey();
        }

        public static async Task DoGreetEveryone(GreetingService.GreetingServiceClient client)
        {
            var stream = client.GreetEverone();
            var responseReaderTask = Task.Run(async () =>
            {
                while (await stream.ResponseStream.MoveNext())
                {
                    Console.WriteLine("Recived: " + stream.ResponseStream.Current.Result);
                }
            });

            Greeting[] greetings = {new Greeting() { FirstName = "John", LastName = "Doe"},
                                    new Greeting() { FirstName = "Clement", LastName = "Jean"},
                                    new Greeting() { FirstName = "Jan", LastName = "Kowalski"}};

            foreach (var greeting in greetings)
            {
                Console.WriteLine("Sending: " + greeting.ToString());
                await stream.RequestStream.WriteAsync(new GreetEveryoneRequest() { Greeting = greeting });
            }
            await stream.RequestStream.CompleteAsync();
            await responseReaderTask;
        }

        public static async Task DoComputeCurrentMax(CalculatorService.CalculatorServiceClient client)
        {
            var stream = client.ComputeCurrentMax();
            var responseReaderTask = Task.Run(async () => 
            {
                while (await stream.ResponseStream.MoveNext())
                {
                    Console.WriteLine($"Current Max num: {stream.ResponseStream.Current.Max} from the Server");
                }
            });

            // Handle sending the stream data to the Server
            foreach (int i in Enumerable.Range(1, 10))
            {
                float number = (float)new Random().NextDouble() * 10;
                var request = new ComputeCurrentMaxRequest() { Number =  number};
                await Task.Delay(1);
                Console.WriteLine($"Client sending:  {request.Number.ToString()} to the Server");
                await stream.RequestStream.WriteAsync(request);
            }
            await stream.RequestStream.CompleteAsync();
            // Now, handle the incoming data from the Server
            await responseReaderTask;
        }

        public static async Task DoSafeSqrt(SqrtService.SqrtServiceClient client, int number)
        {
            try
            {
                var response = client.sqrt(new SqrtRequest() { Number = number });
                Console.WriteLine(response.SquareRoot);
            }
            catch (RpcException e)
            {
                Console.WriteLine("Error: " + e.Status.Detail);
            }
        }

        public static async Task DoGreetWithDeadline(GreetingService.GreetingServiceClient client, int deadline)
        {
            try
            {
                var response = client.GreetWithDeadline(new GreetWithDeadlineRequest() { Name = "Piotr" }, 
                                                        deadline: DateTime.UtcNow.AddMilliseconds(deadline));
                Console.WriteLine(response.Result);
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.DeadlineExceeded)
            {
                Console.WriteLine("Error: " + e.Status.Detail);
            }
        }

        public static Task<CreateBlogResponse> DoCreateBlog(BlogService.BlogServiceClient client, CreateBlogRequest request)
        {
            var response = client.CreateBlog(request);
            Console.WriteLine("The blog: " + response.Blog.Id + " was created!");
            return Task.FromResult(response);
        }

        public static Task<ReadBlogResponse> DoReadBlog(BlogService.BlogServiceClient client, ReadBlogRequest request)
        {
            try
            {
                var response = client.ReadBlog(request);
                Console.WriteLine(response.Blog.ToString());
                return Task.FromResult(response);
            }
            catch (RpcException e)
            {
                Console.WriteLine(e.Status.Detail);
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public static async Task<UpdateBlogResponse> DoUpdateBlog(BlogService.BlogServiceClient client, UpdateBlogRequest request)
        {
            try
            {
                var response = await client.UpdateBlogAsync(request);
                Console.WriteLine(response.Blog.ToString());
                return response;
            }
            catch (RpcException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public static async Task<DeleteBlogResponse> DoDeleteBlog(BlogService.BlogServiceClient client, DeleteBlogRequest request)
        {
            try
            {
                var result = await client.DeleteBlogAsync(request);
                Console.WriteLine("Successfully deleted blog with id " + result.BlogId);
                return result;
            }
            catch (RpcException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public static async Task DoListBlog(BlogService.BlogServiceClient client)
        {
            var result = client.ListBlog(new ListBlogRequest() { });
            while (await result.ResponseStream.MoveNext())
            {
                Console.WriteLine();
                Console.WriteLine(result.ResponseStream.Current.ToString());
                Console.WriteLine();
            }
        }
    }
}
