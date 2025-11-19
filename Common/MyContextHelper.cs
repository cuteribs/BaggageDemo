using OpenTelemetry;
using System.Text.Json;

namespace BaggageDemo.Common;

public static class MyContextHelper
{
	public static void SetBaggage(MyContext myContext)
	{
		Baggage.SetBaggage(nameof(MyContext), SerializeToBase64(myContext));
	}

	public static MyContext? GetBaggage()
	{
		var context = Baggage.GetBaggage(nameof(MyContext));
		return DeserializeFromBase64(context);
	}

	public static string SerializeToBase64(MyContext myContext)
	{
		var json = JsonSerializer.Serialize(myContext);
		var bytes = System.Text.Encoding.UTF8.GetBytes(json);
		return Convert.ToBase64String(bytes);
	}

	public static MyContext? DeserializeFromBase64(string? base64String)
	{
		if (string.IsNullOrEmpty(base64String))
		{
			return null;
		}

		var bytes = Convert.FromBase64String(base64String);
		var json = System.Text.Encoding.UTF8.GetString(bytes);
		return JsonSerializer.Deserialize<MyContext>(json);
	}
}