using System.Text.Json;
using CommandLine;
using QuickerAgent.AgentModel.Guides;

namespace QuickerAgent.Console;

internal static partial class Program
{
  private static readonly ActionDocGuideService ActionDocGuide = new();

  private static Task<int> RunGuideAsync(GuideOptions options)
  {
    var verb = (options.Command ?? string.Empty).Trim().ToLowerInvariant();
    return verb switch
    {
      "get" => RunGuideGet(options),
      "search" => RunGuideSearch(options),
      _ => ReportUnknownGuideVerbAsync(options),
    };
  }

  private static async Task<int> ReportUnknownGuideVerbAsync(GuideOptions options)
  {
    await EmitErrorAsync(
      options.Json,
      "UNKNOWN_GUIDE_VERB",
      "Use: guide get --topic <id> [--json] | guide search [--query <keyword>] [--limit 10] [--json]")
      .ConfigureAwait(false);
    return ExitCodes.Error;
  }

  private static Task<int> RunGuideGet(GuideOptions options)
  {
    var response = ActionDocGuide.GetDoc(options.Topic ?? string.Empty);
    if (options.Json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = response.Success,
          action = "guide-get",
          success = response.Success,
          errorMessage = response.ErrorMessage,
          topic = response.Topic,
          title = response.Title,
          markdown = response.Markdown,
          availableTopics = response.AvailableTopics,
        },
        QkagentJson.CliOutput));
    }
    else if (response.Success && !string.IsNullOrWhiteSpace(response.Markdown))
    {
      global::System.Console.WriteLine(response.Markdown);
    }
    else
    {
      global::System.Console.Error.WriteLine(response.ErrorMessage ?? "guide get failed");
      if (response.AvailableTopics?.Count > 0)
      {
        global::System.Console.Error.WriteLine("Topics: " + string.Join(", ", response.AvailableTopics));
      }
    }

    return Task.FromResult(response.Success ? ExitCodes.Success : ExitCodes.Error);
  }

  private static Task<int> RunGuideSearch(GuideOptions options)
  {
    var response = ActionDocGuide.Search(options.Query, options.Limit);
    if (options.Json)
    {
      global::System.Console.WriteLine(JsonSerializer.Serialize(
        new
        {
          ok = response.Success,
          action = "guide-search",
          success = response.Success,
          keyword = response.Keyword,
          matchCount = response.MatchCount,
          items = response.Items,
          availableTopics = response.AvailableTopics,
        },
        QkagentJson.CliOutput));
    }
    else
    {
      foreach (var item in response.Items)
      {
        global::System.Console.WriteLine($"{item.Topic}\t{item.Title}\t{item.Excerpt}");
      }
    }

    return Task.FromResult(response.Success ? ExitCodes.Success : ExitCodes.Error);
  }
}

[Verb("guide", HelpText = "Embedded action-doc guides (no browser or credentials required).")]
public sealed class GuideOptions
{
  [Value(0, MetaName = "command", Required = true, HelpText = "get | search")]
  public string? Command { get; set; }

  [Option("topic", HelpText = "Topic id for guide get (e.g. workflow, page-html).")]
  public string? Topic { get; set; }

  [Option('q', "query", HelpText = "Keyword for guide search.")]
  public string? Query { get; set; }

  [Option("limit", Default = 10, HelpText = "Max results for guide search.")]
  public int Limit { get; set; }

  [Option("json", HelpText = "Emit JSON for automation.")]
  public bool Json { get; set; }
}
