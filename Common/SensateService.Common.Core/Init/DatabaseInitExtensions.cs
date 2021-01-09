/*
 * Helper methods to create new database connections.
 *
 * @author Michel Megens
 * @email  michel.megens@sonatolabs.com
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using SensateService.Converters;
using SensateService.Infrastructure.Document;
using SensateService.Infrastructure.Sql;

namespace SensateService.Init
{
	public static class DatabaseInitExtensions
	{
		public static void AddDocumentStore(this IServiceCollection services, string conn, string db, int max)
		{
			services.Configure<MongoDBSettings>(options => {
				options.DatabaseName = db;
				options.ConnectionString = conn;
				options.MaxConnections = max;
			});

			BsonSerializer.RegisterSerializationProvider(new BsonDecimalSerializationProvider());
			services.AddSingleton<SensateContext>();
		}

		public static void AddPostgres(this IServiceCollection services, string sensateiot, string network)
		{
			services.AddDbContextPool<SensateSqlContext>(options => {
				options.UseNpgsql(sensateiot);
			}, 256);

			services.AddDbContextPool<NetworkContext>(options => {
				options.UseNpgsql(network);
			}, 256);
		}
	}
}
