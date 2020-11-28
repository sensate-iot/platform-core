﻿/*
 * MQTT handler interface.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System.Threading;
using System.Threading.Tasks;

namespace SensateIoT.Platform.Network.Common.MQTT
{
	public abstract class IMqttHandler
	{
		public abstract Task OnMessageAsync(string topic, string message, CancellationToken ct = default);
	}
}
