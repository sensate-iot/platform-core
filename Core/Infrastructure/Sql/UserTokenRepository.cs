/*
 * Repository for the SensateAuthUserToken table.
 *
 * @author Michel Megens
 * @email  michel.megens@sonatolabs.com
 */

using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading;
using Microsoft.IdentityModel.Tokens;

using SensateService.Exceptions;
using SensateService.Infrastructure.Repositories;
using SensateService.Models;
using SensateService.Helpers;

namespace SensateService.Infrastructure.Sql
{
	public class UserTokenRepository : AbstractSqlRepository<AuthUserToken>, IUserTokenRepository
	{
		private readonly Random _rng;
		private const int JwtRefreshTokenLength = 64;
		public const string JwtRefreshTokenProvider = "JWTrt";
		public const string JwtTokenProvider = "JWT";

		public UserTokenRepository(SensateSqlContext context) : base(context)
		{
			this._rng = new Random();
		}

		public override void Create(AuthUserToken obj)
		{
			var asyncResult = this.CreateAsync(obj);
			asyncResult.RunSynchronously();
		}

		public override async Task CreateAsync(AuthUserToken obj, CancellationToken ct = default(CancellationToken))
		{
			if(obj.Value == null && obj.LoginProvider == JwtRefreshTokenProvider) {
				obj.Value = this.GenerateRefreshToken();
			} else if(obj.Value == null) {
				throw new DatabaseException("User token must have a value!");
			}

			await base.CreateAsync(obj, ct).AwaitBackground();
		}

		public Task<long> CountAsync(Expression<Func<AuthUserToken, bool>> expr)
		{
			return Task.Run(() => this._sqlContext.UserTokens.LongCount(expr));
		}


		public AuthUserToken GetById(Tuple<SensateUser, string> id) => this.GetById(id.Item1, id.Item2);

		public AuthUserToken GetById(SensateUser user, string value)
		{
			return this.Data.FirstOrDefault(
				x => x.UserId == user.Id && x.Value == value
			);
		}

		public IEnumerable<AuthUserToken> GetByUser(SensateUser user)
		{
			IEnumerable<AuthUserToken> data;

			data = from token in this.Data
				   where token.UserId == user.Id
				   select token;

			return data.ToList();
		}

		public void InvalidateToken(AuthUserToken token)
		{
			this.StartUpdate(token);
			token.Valid = false;
			this.EndUpdate();
		}

		public async Task InvalidateTokenAsync(AuthUserToken token)
		{
			this.StartUpdate(token);
			token.Valid = false;
			await this.EndUpdateAsync().AwaitBackground();
		}

		public void InvalidateToken(SensateUser user, string value)
		{
			var token = this.GetById(user, value);

			if(token == null)
				return;

			this.InvalidateToken(token);
		}

		public async Task InvalidateTokenAsync(SensateUser user, string value)
		{
			var token = this.GetById(user, value);

			if(token == null)
				return;

			await this.InvalidateTokenAsync(token).AwaitBackground();
		}

		public string GenerateJwtToken(SensateUser user, IEnumerable<string> roles, UserAccountSettings settings)
		{
			List<Claim> claims;
			JwtSecurityToken token;

			claims = new List<Claim> {
				new Claim(JwtRegisteredClaimNames.Sub, user.Email),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
				new Claim(ClaimTypes.NameIdentifier, user.Id),
				new Claim(JwtRegisteredClaimNames.NameId, user.Id)
			};

			roles.ToList().ForEach(x => {
				claims.Add(new Claim(ClaimTypes.Role, x));
			});

			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtKey));
			var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
			var expires = DateTime.Now.AddMinutes(settings.JwtExpireMinutes);
			token = new JwtSecurityToken(
				issuer: settings.JwtIssuer,
				audience: settings.JwtIssuer,
				claims: claims,
				expires: expires,
				signingCredentials: creds
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}

		public string GenerateRefreshToken()
		{
			return this._rng.NextString(JwtRefreshTokenLength);
		}

		public async Task InvalidateManyAsync(IEnumerable<AuthUserToken> tokens)
		{
			List<AuthUserToken> _tokens;

			_tokens = tokens.ToList();
			_tokens.ForEach(x => {
				this.StartUpdate(x);
				x.Valid = false;
			});

			await this.CommitAsync().AwaitBackground();
		}
	}
}
