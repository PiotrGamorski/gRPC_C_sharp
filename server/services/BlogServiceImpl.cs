using System.Linq;
using System.Threading.Tasks;
using Blog;
using Grpc.Core;
using MongoDB.Bson;
using MongoDB.Driver;
using static Blog.BlogService;

namespace server
{
    public class BlogServiceImpl : BlogServiceBase
    {
        // instead of using static fields, one can create a constructor
        private static MongoClient mongoClient = new MongoClient("mongodb://localhost:27017");
        private static IMongoDatabase mongoDatabase = mongoClient.GetDatabase("mybd");
        private static IMongoCollection<BsonDocument> mongoCollection = mongoDatabase.GetCollection<BsonDocument>("blog");

        public override Task<CreateBlogResponse> CreateBlog(CreateBlogRequest request, ServerCallContext context)
        {
            var blog = request.Blog;
            BsonDocument doc = new BsonDocument("author_id", blog.AuthorId)
                                           .Add("title", blog.Title)
                                           .Add("content", blog.Content);
            
            mongoCollection.InsertOne(doc);
            string id = doc.GetValue("_id").ToString();
            blog.Id = id;

            return Task.FromResult(new CreateBlogResponse()
            { 
                Blog = blog,
            });
        }

        public override async Task<ReadBlogResponse> ReadBlog(ReadBlogRequest request, ServerCallContext context)
        {
            var blogId = request.BlogId;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(blogId));
            var result = await mongoCollection.FindAsync(filter).Result.FirstOrDefaultAsync();

            if (result == null)
                throw new RpcException(new Status(StatusCode.NotFound, "The blog id " + blogId + " wans't found"));

            Blog.Blog blog = new Blog.Blog()
            {
                AuthorId = result.GetValue("author_id").AsString,
                Title = result.GetValue("title").AsString,
                Content = result.GetValue("content").AsString,
            };

            return new ReadBlogResponse() {Blog = blog};
        }

        public override async Task<UpdateBlogResponse> UpdateBlog(UpdateBlogRequest request, ServerCallContext context)
        {
            var blogId = request.Blog.Id;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", new ObjectId(blogId));
            var result = mongoCollection.Find(filter).FirstOrDefault();

            if (result == null)
                throw new RpcException(new Status(StatusCode.NotFound, "The blog id " + blogId + " wasn't found"));

            var doc = new BsonDocument("author_id", request.Blog.AuthorId)
                            .Add("title", request.Blog.Title)
                            .Add("content", request.Blog.Content);

            await mongoCollection.ReplaceOneAsync(filter, doc);

            var blog = new Blog.Blog()
            {
                AuthorId = doc.GetValue("author_id").AsString,
                Title = doc.GetValue("title").AsString,
                Content = doc.GetValue("content").AsString,
            };

            blog.Id = blogId;

            return new UpdateBlogResponse() { Blog = blog };
        }

        public override async Task<DeleteBlogResponse> DeleteBlog(DeleteBlogRequest request, ServerCallContext context)
        {
            var filter = new FilterDefinitionBuilder<BsonDocument>().Eq("_id", new ObjectId(request.BlogId));
            var result = await mongoCollection.DeleteOneAsync(filter);

            if (result.DeletedCount == 0)
                throw new RpcException(new Status(StatusCode.NotFound, "The blog with id " + request.BlogId + " wasn't found."));

            return new DeleteBlogResponse() { BlogId = request.BlogId };
        }

        public override async Task ListBlog(ListBlogRequest request, IServerStreamWriter<ListBlogResponse> responseStream, ServerCallContext context)
        {
            var filter = new FilterDefinitionBuilder<BsonDocument>().Empty;
            var result = await mongoCollection.FindAsync(filter).Result.ToListAsync();
            
            foreach (var item in result)
            {
                await responseStream.WriteAsync(new ListBlogResponse() 
                { Blog = new Blog.Blog() 
                    {
                        Id = item.GetValue("_id").ToString(),
                        AuthorId = item.GetValue("author_id").AsString,
                        Title = item.GetValue("title").AsString,
                        Content = item.GetValue("content").AsString,
                    }
                });
            }
        }
    }
}
