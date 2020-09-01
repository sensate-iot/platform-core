﻿/*
 * Measurement with its origin JSON representation.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

namespace SensateService.Common.Data.Dto.Authorization
{
	public class JsonMeasurement
	{
		public Measurement Measurement { get; set; }
		public string Json { get; set; }
	}
}