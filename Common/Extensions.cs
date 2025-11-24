using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BaggageDemo.Common;

public static class Extensions
{
	public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
	{
		//builder.Logging.AddOpenTelemetry(x =>
		//{
		//	x.IncludeFormattedMessage = true;
		//	x.IncludeScopes = true;
		//});

		//builder.Services.AddOpenTelemetry()
		//	.WithTracing(x => x.AddSource(builder.Environment.ApplicationName)
		//		.AddAspNetCoreInstrumentation()
		//		.AddGrpcClientInstrumentation()
		//		.AddHttpClientInstrumentation()
		//		.AddConsoleExporter()
		//	);

		return builder;
	}

	public static string? FromBase64(this string? base64String)
	{
		if (base64String == null) return null;

		return Encoding.UTF8.GetString(Convert.FromBase64String(base64String));
	}

	public static string? ToBase64(this string? normalString)
	{
		if (normalString == null) return null;

		return Convert.ToBase64String(Encoding.UTF8.GetBytes(normalString));
	}
}

public class MessageTraceContext
{
	public string? TraceParent { get; set; }
	public string? Baggage { get; set; }
}

public static class ActivityHelper
{
	public static Activity StartNewActivity(string name, MessageTraceContext context)
	{
		var activity = new Activity(name);

		if (context.TraceParent != null)
		{
			activity.SetParentId(context.TraceParent);
		}

		if (context.Baggage != null)
		{
			Decode(activity, context.Baggage);
		}

		return activity.Start();
	}

	public static void Inject(IDictionary<string, object?> properties, Activity? activity)
	{
		if (activity == null) return;

		if (activity.Id != null)
		{
			properties["Diagnostic-Id"] = activity.Id;
			properties["traceparent"] = activity.Id;
		}

		if (activity.Baggage.Any() == true)
		{
			properties["baggage"] = ActivityHelper.Encode(activity.Baggage)!;
		}
	}

	public static void Extract(IEnumerable<KeyValuePair<string, object?>>? properties, Activity? activity)
	{
		if (properties == null || activity == null) return;

		if (properties != null)
		{
			if (TryGetValue(properties, "traceparent", out string parentId))
			{
				activity.SetParentId(parentId);
			}

			if (TryGetValue(properties, "baggage", out string baggageHeader))
			{
				ActivityHelper.Decode(activity, baggageHeader);
			}
		}
	}

	private static bool TryGetValue<T>(this IEnumerable<KeyValuePair<string, object?>> items, string key, out T value)
	{
		foreach (var item in items)
		{
			if (item.Key == key && item.Value is T v)
			{
				value = v;
				return true;
			}
		}

		value = default!;
		return false;
	}

	/// <summary>
	/// Encodes baggage into a W3C baggage header
	/// </summary>
	/// <param name="baggage"></param>
	/// <returns></returns>
	public static string? Encode(IEnumerable<KeyValuePair<string, string?>>? baggage)
	{
		if (baggage == null) return null;

		var parts = baggage.Select(x =>
			$"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value ?? "")}"
		);
		return string.Join(",", parts);
	}

	/// <summary>
	/// Decodes a W3C baggage header and sets it into the <see cref="Activity.Baggage" />
	/// </summary>
	/// <param name="activity"></param>
	/// <param name="baggageHeader"></param>
	public static void Decode(Activity activity, string baggageHeader)
	{
		if (activity == null) return;

		var baggage = Decode(baggageHeader);

		if (baggage != null)
		{
			foreach (var item in baggage)
			{
				activity.SetBaggage(item.Key, item.Value);
			}
		}
	}

	/// <summary>
	/// Decodes a W3C baggage header
	/// </summary>
	/// <param name="baggageHeader"></param>
	/// <returns></returns>
	public static IEnumerable<KeyValuePair<string, string?>>? Decode(string baggageHeader)
	{
		return baggageHeader?.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Where(x => x.Contains('='))
			.Select(x =>
			{
				var parts = x.Split('=', 2);
				var key = parts.First();
				var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;
				return new KeyValuePair<string, string?>(key, value);
			});
	}

    public static T? GetBaggage<T>(string key, Activity? activity = null)
    {
		activity ??= Activity.Current;
		var value = activity?.Baggage.FirstOrDefault(x => x.Key == key).Value;

		if (value == null) return default;

		return JsonSerializer.Deserialize<T>(value);
	}

	public static void SetBaggage<T>(string key, T value, Activity? activity = null)
	{
		activity ??= Activity.Current;
		var json = JsonSerializer.Serialize(value);
		activity?.SetBaggage(key, json);
	}
}