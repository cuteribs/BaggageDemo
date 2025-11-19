using BaggageDemo.Common;

namespace BaggageDemo.FunctionApp;

public class WorkflowContext
{
	public OrderCreatedMessage? Message { get; set; }
	public string? MyContext { get; set; }
}
