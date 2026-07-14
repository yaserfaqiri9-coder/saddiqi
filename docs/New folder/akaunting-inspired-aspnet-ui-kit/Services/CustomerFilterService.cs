using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Models;

namespace PTGOilSystem.Web.Services;

public static class CustomerFilterService
{
    // Replace Customer with your actual entity type.
    public static IQueryable<Customer> Apply(
        this IQueryable<Customer> query,
        ListFilterRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Text))
        {
            var text = request.Text.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, $"%{text}%") ||
                (x.Email != null && EF.Functions.ILike(x.Email, $"%{text}%")) ||
                (x.Phone != null && EF.Functions.ILike(x.Phone, $"%{text}%")));
        }

        foreach (var filter in request.Filters)
        {
            query = filter.Field switch
            {
                "status" => ApplyStatus(query, filter),
                "country" => ApplyCountry(query, filter),
                "created_at" => ApplyCreatedAt(query, filter),
                "balance" => ApplyBalance(query, filter),
                _ => query
            };
        }

        return query;
    }

    private static IQueryable<Customer> ApplyStatus(
        IQueryable<Customer> query, FilterToken token)
    {
        var values = token.Values.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        return token.Operator switch
        {
            FilterOperator.Equal when values.Length > 0 =>
                query.Where(x => x.Status == values[0]),

            FilterOperator.NotEqual when values.Length > 0 =>
                query.Where(x => x.Status != values[0]),

            FilterOperator.In when values.Length > 0 =>
                query.Where(x => values.Contains(x.Status)),

            _ => query
        };
    }

    private static IQueryable<Customer> ApplyCountry(
        IQueryable<Customer> query, FilterToken token)
    {
        var values = token.Values.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        return token.Operator switch
        {
            FilterOperator.Equal when values.Length > 0 =>
                query.Where(x => x.CountryCode == values[0]),

            FilterOperator.NotEqual when values.Length > 0 =>
                query.Where(x => x.CountryCode != values[0]),

            FilterOperator.In when values.Length > 0 =>
                query.Where(x => x.CountryCode != null &&
                                 values.Contains(x.CountryCode)),

            _ => query
        };
    }

    private static IQueryable<Customer> ApplyCreatedAt(
        IQueryable<Customer> query, FilterToken token)
    {
        if (token.Values.Count == 0 ||
            !DateTime.TryParse(token.Values[0], CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var start))
        {
            return query;
        }

        if (token.Operator == FilterOperator.Between)
        {
            var end = start;
            if (token.Values.Count > 1)
            {
                DateTime.TryParse(token.Values[1], CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out end);
            }

            // Inclusive calendar-day range without applying a function to the DB column.
            var endExclusive = end.Date.AddDays(1);
            return query.Where(x => x.CreatedAt >= start.Date &&
                                    x.CreatedAt < endExclusive);
        }

        var next = start.Date.AddDays(1);
        return token.Operator switch
        {
            FilterOperator.Equal =>
                query.Where(x => x.CreatedAt >= start.Date && x.CreatedAt < next),
            FilterOperator.NotEqual =>
                query.Where(x => x.CreatedAt < start.Date || x.CreatedAt >= next),
            _ => query
        };
    }

    private static IQueryable<Customer> ApplyBalance(
        IQueryable<Customer> query, FilterToken token)
    {
        if (token.Values.Count == 0 ||
            !decimal.TryParse(token.Values[0], NumberStyles.Number,
                CultureInfo.InvariantCulture, out var value))
        {
            return query;
        }

        return token.Operator switch
        {
            FilterOperator.Equal => query.Where(x => x.Balance == value),
            FilterOperator.NotEqual => query.Where(x => x.Balance != value),
            _ => query
        };
    }
}

/*
Expected fields on your entity:
public sealed class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CountryCode { get; set; }
    public required string Status { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
}
*/
