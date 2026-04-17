using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OllamaSharp;
using PizzaOrderSystem.Workflow;

namespace PizzaOrderSystem.Agents;

/// <summary>
/// Factory that creates all AIAgent instances backed by a local Ollama model.
/// </summary>
public class AgentFactory
{
    private readonly string _ollamaEndpoint;
    private readonly string _modelName;

    public AgentFactory(string ollamaEndpoint, string modelName)
    {
        _ollamaEndpoint = ollamaEndpoint;
        _modelName = modelName;
    }

    private OllamaApiClient NewClient() => new(new Uri(_ollamaEndpoint), _modelName);

    private string LoadPrompt(string name)
    {
        var paths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Prompts", name),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", name),
            Path.Combine(Directory.GetCurrentDirectory(), "Prompts", name)
        };
        foreach (var p in paths)
            if (File.Exists(p)) return File.ReadAllText(p);

        throw new FileNotFoundException($"Prompt file not found: {name}");
    }

    public AIAgent CreateOrchestratorAgent() =>
        NewClient().AsAIAgent(
            instructions: LoadPrompt("orchestrator.md"),
            name: "OrchestratorAgent");

    public AIAgent CreateCustomerIdentityAgent() =>
        NewClient().AsAIAgent(
            instructions: LoadPrompt("customer-identity.md"),
            name: "CustomerIdentityAgent");

    public AIAgent CreateOrderHistoryAgent() =>
        NewClient().AsAIAgent(
            instructions: LoadPrompt("order-history.md"),
            name: "OrderHistoryAgent");

    public AIAgent CreateMenuAgent(Tools.MenuTools menuTools) =>
        NewClient().AsAIAgent(
            instructions: LoadPrompt("menu.md"),
            name: "MenuAgent",
            tools:
            [
                AIFunctionFactory.Create(menuTools.GetMenuItems,     "GetMenuItems",     "Get all available pizzas with prices per size"),
                AIFunctionFactory.Create(menuTools.GetPizzaPrice,    "GetPizzaPrice",    "Get the price of a specific pizza at a given size"),
                AIFunctionFactory.Create(menuTools.GetPizzaSizes,    "GetPizzaSizes",    "List all available sizes with price multipliers"),
            ]);

    public AIAgent CreateOrderBuilderAgent(
        Tools.MenuTools menuTools,
        Tools.OrderTools orderTools) =>
        NewClient().AsAIAgent(
            instructions: LoadPrompt("order-builder.md"),
            name: "OrderBuilderAgent",
            tools:
            [
                AIFunctionFactory.Create(menuTools.GetMenuItems,         "GetMenuItems",         "Get all available pizzas with prices"),
                AIFunctionFactory.Create(menuTools.GetNextSize,          "GetNextSize",          "Get the next larger pizza size"),
                AIFunctionFactory.Create(menuTools.GetPreviousSize,      "GetPreviousSize",      "Get the next smaller pizza size"),
                AIFunctionFactory.Create(menuTools.GetPizzaPrice,        "GetPizzaPrice",        "Get price for a specific pizza and size"),
                AIFunctionFactory.Create(orderTools.CalculateOrderPrice, "CalculateOrderPrice",  "Calculate total price for a list of items"),
                AIFunctionFactory.Create(orderTools.ValidateOrder,       "ValidateOrder",        "Validate all pizza IDs in an order"),
            ]);

    public AIAgent CreateConfirmationAgent() =>
        NewClient().AsAIAgent(
            instructions: LoadPrompt("confirmation.md"),
            name: "ConfirmationAgent");

    public AIAgent CreateOrderSubmissionAgent(Tools.OrderSubmissionTools submissionTools) =>
        NewClient().AsAIAgent(
            instructions: LoadPrompt("order-submission.md"),
            name: "OrderSubmissionAgent",
            tools:
            [
                AIFunctionFactory.Create(submissionTools.SaveOrder,                  "SaveOrder",                  "Save a confirmed order to the database"),
                AIFunctionFactory.Create(submissionTools.GenerateConfirmationNumber, "GenerateConfirmationNumber", "Generate a human-readable confirmation number"),
            ]);
}
