/*
 * Abstract measurement repository
 *
 * @author: Michel Megens
 * @email:  dev@bietje.net
 */

using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MongoDB.Bson;

using SensateService.Infrastructure.Repositories;
using SensateService.Infrastructure.Cache;
using SensateService.Models;
using SensateService.Exceptions;


namespace SensateService.Infrastructure.Document
{
	public class CachedMeasurementRepository : AbstractMeasurementRepository, IMeasurementRepository
	{
		private ICacheStrategy<string> _cache;

		public const int CacheTimeout = 10;
		public const int CacheTimeoutShort = 1;

		public CachedMeasurementRepository(
			SensateContext context,
			ILogger<AbstractMeasurementRepository> logger,
			ICacheStrategy<string> cache) : base(context, logger)
		{
			this._cache = cache;
		}

		public override void Commit(Measurement obj)
		{
			try {
				this._cache.Set(obj.InternalId.ToString(), obj.ToJson());
			} catch(Exception ex) {
				this._logger.LogWarning($"Unable to cache measurement {ex.Message}");
				throw new CachingException(
					$"Unable to cache measurement: {ex.Message}",
					obj.InternalId.ToString(), ex
				);
			}
		}

		public override async Task CommitAsync(Measurement obj)
		{
			try {
				await this._cache.SetAsync(obj.InternalId.ToString(), obj.ToJson(), CacheTimeout);
			} catch(Exception ex) {
				this._logger.LogWarning($"Unable to log measurement {ex.Message}");
				throw new CachingException(
					$"Unable to cache measurement: {ex.Message}",
					obj.InternalId.ToString(), ex
				);
			}
		}

		public override void Delete(string id)
		{
			this._cache.Remove(id);
			base.Delete(id);
		}

		public override async Task DeleteAsync(string id)
		{
			await this._cache.RemoveAsync(id);
			await base.DeleteAsync(id);
		}

		private void CacheData(string key, object data)
		{
			this.CacheData(key, data.ToJson());
		}

		private void CacheData(string key, string json)
		{
			if(key == null || json == null)
				return;

			this._cache.Set(key, json, CacheTimeout);
		}

		private async Task CacheDataAsync(string key, object data, int tmo)
		{
			await this.CacheDataAsync(key, data.ToJson(), tmo);
		}

		private async Task CacheDataAsync(string key, string json, int tmo)
		{
			if(key == null || json == null)
				return;

			await this._cache.SetAsync(key, json, tmo);
		}

		public override Measurement GetById(string id)
		{
			Measurement m;
			string data;

			data = this._cache.Get(id);
			if(data == null)
				return base.GetById(id);

			try {
				m = JsonConvert.DeserializeObject<Measurement>(data);
			} catch(JsonSerializationException) {
				return null;
			}

			return m;
		}

		public override async Task<IEnumerable<Measurement>> GetMeasurementsBySensorAsync(Sensor sensor)
		{
			string key;

			key = $"{sensor.InternalId.ToString()}::{sensor.Secret}";
			return await this.TryGetMeasurementsAsync(key, x =>
				x.CreatedBy == sensor.InternalId, CacheTimeoutShort
			);
		}

		public override IEnumerable<Measurement> GetMeasurementsBySensor(Sensor sensor)
		{
			string key;

			key = $"{sensor.InternalId.ToString()}::{sensor.Secret}";
			return this.TryGetMeasurements(key, x =>
				x.CreatedBy == sensor.InternalId, CacheTimeoutShort
			);
		}

		public override void Update(Measurement obj)
		{
			this._cache.Set(obj.InternalId.ToString(),
				JsonConvert.SerializeObject(obj), CacheTimeout);
			base.Update(obj);
		}

		private async Task<IEnumerable<Measurement>> TryGetMeasurementsAsync(
			string key, Expression<Func<Measurement, bool>> expression, int tmo)
		{
			string data = null;
			IEnumerable<Measurement> measurements;

			if(key != null)
				data = await this._cache.GetAsync(key);

			if(data == null) {
				measurements = await base.TryGetMeasurementsAsync(key, expression);
				await this.CacheDataAsync(key, measurements, tmo);
				return measurements;
			}

			return JsonConvert.DeserializeObject<IEnumerable<Measurement>>(data);
		}

		public async override Task<IEnumerable<Measurement>> TryGetMeasurementsAsync(
			string key, Expression<Func<Measurement, bool>> expression)
		{
			return await this.TryGetMeasurementsAsync(key, expression, CacheTimeout);
		}

		public override Measurement TryGetMeasurement(
			string key, Expression<Func<Measurement, bool>> expression)
		{
			string data = null;
			Measurement m;

			if(key != null)
				data = this._cache.Get(key);

			if(data == null) {
				m = base.TryGetMeasurement(key, expression);
				this.CacheData(key, m);
				return m;
			}

			return JsonConvert.DeserializeObject<Measurement>(data);
		}

		public async override Task<Measurement> TryGetMeasurementAsync(
			string key, Expression<Func<Measurement, bool>> expression)
		{
			string data = null;
			Measurement m;

			if(key != null)
				data = await this._cache.GetAsync(key);

			if(data == null) {
				m = await base.TryGetMeasurementAsync(key, expression);
				await this.CacheDataAsync(key, m, CacheTimeout);
				return m;
			}

			return JsonConvert.DeserializeObject<Measurement>(data);
		}

		private IEnumerable<Measurement> TryGetMeasurements(
			string key, Expression<Func<Measurement, bool>> selector, int tmo
		)
		{
			string data = null;
			IEnumerable<Measurement> measurements;

			if(key != null)
				data = this._cache.Get(key);

			if(data == null) {
				measurements = base.TryGetMeasurements(key, selector);
				this.CacheData(key, measurements);
				return measurements;
			}

			return JsonConvert.DeserializeObject<IEnumerable<Measurement>>(data);
		}

		public override IEnumerable<Measurement> TryGetMeasurements(
			string key, Expression<Func<Measurement, bool>> selector
		)
		{
			return this.TryGetMeasurements(key, selector, CacheTimeout);
		}

		public Measurement GetMeasurement(string key, Expression<Func<Measurement, bool>> selector)
		{
			return TryGetMeasurement(key, selector);
		}

		public override IEnumerable<Measurement> TryGetBetween(Sensor sensor, DateTime start, DateTime end)
		{
			string key, data;
			IEnumerable<Measurement> measurements;

			key = $"{sensor.Secret}::{start.ToString()}::{end.ToString()}";
			data = this._cache.Get(key);

			if(data == null) {
				measurements = base.TryGetBetween(sensor, start, end);
				this.CacheData(key, measurements);
				return measurements;
			}

			return JsonConvert.DeserializeObject<IEnumerable<Measurement>>(data);
		}

		public async override Task<IEnumerable<Measurement>> TryGetBetweenAsync(Sensor sensor, DateTime start, DateTime end)
		{
			string key, data;
			IEnumerable<Measurement> measurements;

			key = $"{sensor.Secret}::{start.ToString()}::{end.ToString()}";
			data = await this._cache.GetAsync(key);

			if(data == null) {
				measurements = await base.TryGetBetweenAsync(sensor, start, end);
				await this.CacheDataAsync(key, measurements.ToJson(), CacheTimeout);
				return measurements;
			}


			return JsonConvert.DeserializeObject<IEnumerable<Measurement>>(data);
		}

		public async Task<Measurement> GetMeasurementAsync(string key, Expression<Func<Measurement, bool>> selector)
		{
			return await this.TryGetMeasurementAsync(key, selector);
		}
	}
}
