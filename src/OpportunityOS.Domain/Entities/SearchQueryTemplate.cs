namespace OpportunityOS.Domain.Entities;

/// <summary>
/// A reusable query template with placeholders (e.g. {role}, {stack}, {company}) that the
/// QueryExpansionService fills from dimensions to produce concrete search queries.
/// </summary>
public sealed class SearchQueryTemplate
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Template { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private SearchQueryTemplate() { }

    public SearchQueryTemplate(string name, string template, string category, bool isActive = true)
    {
        Id = Guid.NewGuid();
        Name = name.Trim();
        Template = template.Trim();
        Category = category.Trim();
        IsActive = isActive;
    }

    public void SetActive(bool active) => IsActive = active;
}
