namespace PTGOilSystem.Web.Models;

public enum FilterOperator
{
    Equal,
    NotEqual,
    In,
    Between,
    Contains
}

public sealed record FilterToken(
    string Field,
    FilterOperator Operator,
    IReadOnlyList<string> Values
);

public sealed record ListFilterRequest(
    string? Text,
    IReadOnlyList<FilterToken> Filters,
    int Page = 1,
    int PageSize = 25
);

public sealed record FilterOption(
    string Key,
    string Label
);

public sealed record FilterDefinition(
    string Key,
    string Label,
    string Type,
    IReadOnlyList<FilterOperator> Operators,
    IReadOnlyList<FilterOption>? Values = null,
    bool Multiple = false
);
