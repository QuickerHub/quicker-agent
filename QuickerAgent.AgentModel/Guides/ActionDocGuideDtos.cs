namespace QuickerAgent.AgentModel.Guides;

public sealed class GetActionDocGuideResult
{
  public bool Success { get; set; }
  public string? ErrorMessage { get; set; }
  public string? Topic { get; set; }
  public string? Title { get; set; }
  public string? Markdown { get; set; }
  public List<string>? AvailableTopics { get; set; }
}

public sealed class ActionDocGuideSearchItem
{
  public string Topic { get; set; } = string.Empty;
  public string Title { get; set; } = string.Empty;
  public string Excerpt { get; set; } = string.Empty;
}

public sealed class SearchActionDocGuidesResult
{
  public bool Success { get; set; }
  public string? ErrorMessage { get; set; }
  public string? Keyword { get; set; }
  public int MatchCount { get; set; }
  public List<ActionDocGuideSearchItem> Items { get; set; } = [];
  public List<string>? AvailableTopics { get; set; }
}
