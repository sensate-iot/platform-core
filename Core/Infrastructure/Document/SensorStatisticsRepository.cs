﻿/*
 * Sensor statistics implementation.
 *
 * @author Michel Megens
 * @email  michel.megens@sonatolabs.com
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using MongoDB.Bson;
using MongoDB.Driver;
using SensateService.Enums;
using SensateService.Exceptions;
using SensateService.Helpers;
using SensateService.Infrastructure.Repositories;
using SensateService.Models;

namespace SensateService.Infrastructure.Document
{
	public class SensorStatisticsRepository : AbstractDocumentRepository<SensorStatisticsEntry>, ISensorStatisticsRepository
	{
		private readonly ILogger<SensorStatisticsRepository> _logger;
		private readonly IMongoCollection<SensorStatisticsEntry> _stats;

		public SensorStatisticsRepository(SensateContext context, ILogger<SensorStatisticsRepository> logger) : base(context.Statistics)
		{
			this._logger = logger;
			this._stats = context.Statistics;
		}

		public async Task<IEnumerable<SensorStatisticsEntry>> GetBetweenAsync(IEnumerable<Sensor> sensors, DateTime start, DateTime end)
		{
			FilterDefinition<SensorStatisticsEntry> filter;

			var builder = Builders<SensorStatisticsEntry>.Filter;
			var ids = sensors.Select(x => x.InternalId);

			filter = builder.In(x => x.SensorId, ids);
			var raw = await this._collection.FindAsync(filter).AwaitBackground();

			return raw.ToList();
		}

		public async Task<IEnumerable<SensorStatisticsEntry>> GetAsync(Expression<Func<SensorStatisticsEntry, bool>> expr)
		{
			var worker = this._stats.FindAsync(expr);
			var data = await worker.AwaitBackground();
			return data.ToList();
		}

		public void Delete(string id)
		{
			ObjectId objectId;

			objectId = ObjectId.Parse(id);
			this._stats.DeleteOne(x => x.InternalId == objectId);
		}

		public async Task DeleteAsync(string id)
		{
			ObjectId objectId;

			objectId = ObjectId.Parse(id);
			await this._stats.DeleteOneAsync(x => x.InternalId == objectId).AwaitBackground();
		}

		public async Task DeleteBySensorAsync(Sensor sensor)
		{
			var query = Builders<SensorStatisticsEntry>.Filter.Eq(x => x.SensorId, sensor.InternalId);

			try {
				await this._stats.DeleteManyAsync(query).AwaitBackground();
			} catch(Exception ex) {
				this._logger.LogWarning(ex.Message);
			}
		}

		public async Task DeleteBySensorAsync(Sensor sensor, DateTime from, DateTime to)
		{
			var f = from.ThisHour();
			var t = to.ThisHour();

			var worker = this._collection.DeleteManyAsync(stat => stat.SensorId == sensor.InternalId &&
			                                                      stat.Date >= f && stat.Date <= t);
			await worker.AwaitBackground();
		}

#region Entry creation

		public Task IncrementAsync(Sensor sensor, RequestMethod method)
		{
			return this.IncrementManyAsync(sensor, method, 1, default(CancellationToken));
		}

		public async Task<SensorStatisticsEntry> CreateForAsync(Sensor sensor)
		{
			SensorStatisticsEntry entry;

			entry = new SensorStatisticsEntry {
				InternalId = base.GenerateId(DateTime.Now),
				Date = DateTime.Now.ThisHour(),
				Measurements = 0,
				SensorId = sensor.InternalId
			};

			await this.CreateAsync(entry).AwaitBackground();
			return entry;
		}

		public async Task IncrementManyAsync(Sensor sensor, RequestMethod method, int num, CancellationToken token)
		{
			var update = Builders<SensorStatisticsEntry>.Update;
			UpdateDefinition<SensorStatisticsEntry> updateDefinition;

			updateDefinition = update.Inc(x => x.Measurements, num)
				.SetOnInsert(x => x.Method, method);

			var opts = new UpdateOptions {IsUpsert = true};
			try {
				await this._collection.UpdateOneAsync(x => x.SensorId == sensor.InternalId &&
				                                           x.Date == DateTime.Now.ThisHour() && x.Method == method,
					updateDefinition, opts, token).AwaitBackground();
			} catch(Exception ex) {
				throw new DatabaseException("Unable to update measurement statistics!", "Statistics", ex);
			}
		}

#endregion

#region Entry Getters

		public async Task<SensorStatisticsEntry> GetByDateAsync(Sensor sensor, DateTime dt)
		{
			FilterDefinition<SensorStatisticsEntry> filter;
			var filterBuilder = Builders<SensorStatisticsEntry>.Filter;
			var date = dt.ThisHour();

			filter = filterBuilder.Eq(x => x.SensorId, sensor.InternalId) & filterBuilder.Eq(x => x.Date, date);
			var result = await this._stats.FindAsync(filter).AwaitBackground();

			if(result == null)
				return null;

			return await result.FirstOrDefaultAsync().AwaitBackground();
		}

		public async Task<IEnumerable<SensorStatisticsEntry>> GetBeforeAsync(Sensor sensor, DateTime dt)
		{
			FilterDefinition<SensorStatisticsEntry> filter;
			var filterBuilder = Builders<SensorStatisticsEntry>.Filter;
			var date = dt.ThisHour();

			filter = filterBuilder.Eq(x => x.SensorId, sensor.InternalId) & filterBuilder.Lte(x => x.Date, date);
			var result = await this._stats.FindAsync(filter).AwaitBackground();

			if(result == null)
				return null;

			return await result.ToListAsync().AwaitBackground();
		}

		public async Task<IEnumerable<SensorStatisticsEntry>> GetAfterAsync(Sensor sensor, DateTime dt)
		{
			FilterDefinition<SensorStatisticsEntry> filter;
			var filterBuilder = Builders<SensorStatisticsEntry>.Filter;
			var date = dt.ThisHour();

			filter = filterBuilder.Eq(x => x.SensorId, sensor.InternalId) & filterBuilder.Gte(x => x.Date, date);
			var result = await this._stats.FindAsync(filter).AwaitBackground();

			if(result == null)
				return null;

			return await result.ToListAsync().AwaitBackground();
		}

		public async Task<IEnumerable<SensorStatisticsEntry>> GetAfterAsync(DateTime date)
		{
			FilterDefinition<SensorStatisticsEntry> filter;
			var filterBuilder = Builders<SensorStatisticsEntry>.Filter;

			filter = filterBuilder.Gte(x => x.Date, date);
			var result = await this._stats.FindAsync(filter).AwaitBackground();

			if(result == null)
				return null;

			return await result.ToListAsync().AwaitBackground();
		}

		public async Task<IEnumerable<SensorStatisticsEntry>> GetBetweenAsync(Sensor sensor, DateTime start, DateTime end)
		{
			FilterDefinition<SensorStatisticsEntry> filter;

			var builder = Builders<SensorStatisticsEntry>.Filter;
			var startDate = start.ThisHour();
			var endDate = end.ThisHour();

			filter = builder.Eq(x => x.SensorId, sensor.InternalId) & builder.Gte(x => x.Date, startDate) &
			         builder.Lte(x => x.Date, endDate);
			var result = await this._stats.FindAsync(filter).AwaitBackground();

			if(result == null)
				return null;

			return await result.ToListAsync().AwaitBackground();
		}
#endregion
	}
}