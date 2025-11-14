using BaggageDemo.Contracts;
using OpenTelemetry;
using System.Text.Json;

namespace BaggageDemo.Common;

public static class MyContextHelper
{
	public static void SetBaggage(MyContext myContext)
	{
		Baggage.SetBaggage(nameof(MyContext), JsonSerializer.Serialize(myContext));
	}

	public static MyContext? GetBaggage()
	{
		var context = Baggage.GetBaggage(nameof(MyContext));
		return context == null ? null : JsonSerializer.Deserialize<MyContext>(context);
	}
}