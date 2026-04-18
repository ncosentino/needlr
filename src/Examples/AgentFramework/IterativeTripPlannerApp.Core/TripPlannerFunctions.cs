using System.ComponentModel;
using System.Text.Json;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace IterativeTripPlannerApp.Core;

/// <summary>
/// DI-resolved tool functions for the iterative trip planner.
/// Each method accesses the workspace via <see cref="IAgentExecutionContextAccessor"/>
/// — the same pattern BrandGhost and other real consumers use.
/// </summary>
/// <remarks>
/// <para>
/// These tools handle mutable booking actions (adding legs, booking hotels,
/// validating constraints). All research is done via the Copilot
/// <c>web_search</c> tool — there is no fake search tool here.
/// </para>
/// <para>
/// The <see cref="IAgentExecutionContextAccessor"/> is populated by
/// <see cref="NexusLabs.Needlr.AgentFramework.Iterative.IIterativeAgentLoop"/>
/// automatically when it has access to the accessor via DI. Tools never
/// need to know about <c>IterativeContext</c> or captured closures.
/// </para>
/// </remarks>
[AgentFunctionGroup("trip-planner")]
internal sealed class TripPlannerFunctions(
    IAgentExecutionContextAccessor contextAccessor)
{
    private IWorkspace Workspace =>
        contextAccessor.GetRequired().GetRequiredWorkspace();

    [AgentFunction]
    [Description(
        "Add a flight leg to the itinerary. Use real flight data from web_search results. " +
        "Each leg must connect to the previous one (route continuity). " +
        "Use SIMPLE city names like 'New York', 'Los Angeles', 'Tokyo' — NOT airport codes or parenthesized forms.")]
    public string AddLeg(string from, string to, string airline, string flight, int price, string duration)
    {
        var workspace = Workspace;
        var itineraryJson = workspace.TryReadFile("itinerary.json").Value.Content;
        var legs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(itineraryJson) ?? [];

        // Route continuity: new leg must connect to the existing chain
        if (legs.Count > 0)
        {
            var lastTo = legs[^1]["to"].ToString()!;
            if (!CityMatch(from, lastTo))
            {
                return $"ERROR: Route continuity violation. Your last leg ends at {lastTo}, "
                    + $"but this leg starts at {from}. Either add a leg from {lastTo} to {from} first, "
                    + "or call clear_itinerary to start over.";
            }
        }
        else
        {
            var configJson = workspace.TryReadFile("config.json").Value.Content;
            var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson)!;
            var origin = config["origin"].ToString()!;
            if (!CityMatch(from, origin))
            {
                return $"ERROR: First leg must start from {origin}, not {from}. "
                    + "Use simple city names without airport codes (e.g. 'New York' not 'New York (JFK)').";
            }
        }

        legs.Add(new Dictionary<string, object>
        {
            ["leg"] = legs.Count + 1,
            ["from"] = from,
            ["to"] = to,
            ["airline"] = airline,
            ["flight"] = flight,
            ["price"] = price,
            ["duration"] = duration,
        });
        workspace.TryWriteFile("itinerary.json", JsonSerializer.Serialize(legs, new JsonSerializerOptions { WriteIndented = true }));
        return $"Added leg {legs.Count}: {from} → {to} on {airline} {flight} (${price}, {duration})";
    }

    [AgentFunction]
    [Description("Remove a leg from the itinerary by leg number. " +
        "WARNING: After removal, remaining legs are renumbered starting from 1.")]
    public string RemoveLeg(int legNumber)
    {
        var workspace = Workspace;
        var itineraryJson = workspace.TryReadFile("itinerary.json").Value.Content;
        var legs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(itineraryJson) ?? [];
        if (legNumber < 1 || legNumber > legs.Count)
            return $"Error: leg {legNumber} not found (have {legs.Count} legs)";
        var removed = legs[legNumber - 1];
        legs.RemoveAt(legNumber - 1);
        for (int i = 0; i < legs.Count; i++)
            legs[i]["leg"] = i + 1;
        workspace.TryWriteFile("itinerary.json", JsonSerializer.Serialize(legs, new JsonSerializerOptions { WriteIndented = true }));
        var remaining = legs.Count == 0
            ? "Itinerary is now empty."
            : $"Remaining {legs.Count} leg(s): " + string.Join(" → ",
                legs.Select(l => $"{l["from"]}→{l["to"]}"));
        return $"Removed leg {legNumber} ({removed["from"]} → {removed["to"]}). {remaining}";
    }

    [AgentFunction]
    [Description("Clear ALL legs and hotels to start building a completely new route. " +
        "Use this when your current route cannot meet the budget.")]
    public string ClearItinerary()
    {
        var workspace = Workspace;
        workspace.TryWriteFile("itinerary.json", "[]");

        var hotelFiles = workspace.GetFilePaths().Where(p => p.StartsWith("hotel-")).ToList();
        foreach (var hf in hotelFiles)
            workspace.TryWriteFile(hf, "");

        var statusJson = workspace.TryReadFile("status.json").Value.Content;
        var status = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson)!;
        status["phase"] = "research";
        status["validated"] = false;
        status["overBudgetAttempts"] = 0;
        workspace.TryWriteFile("status.json", JsonSerializer.Serialize(status));

        return $"Cleared all legs and {hotelFiles.Count} hotel booking(s). "
            + "Itinerary is empty. Use web_search to research a new route.";
    }

    [AgentFunction]
    [Description(
        "Book a hotel in a layover city. Provide the hotel name, city, number of nights, " +
        "price per night, and star rating from your web_search results. " +
        "Hotels below 3.5 stars are rejected.")]
    public string BookHotel(string hotel, string city, int nights, int pricePerNight, double rating)
    {
        if (rating < 3.5)
            return $"ERROR: {hotel} is rated {rating}★ — minimum 3.5★ required. Pick a higher-rated hotel.";

        var workspace = Workspace;
        var key = $"hotel-{city.ToLowerInvariant().Replace(' ', '-')}.json";
        workspace.TryWriteFile(key, JsonSerializer.Serialize(new
        {
            hotel,
            city,
            nights,
            pricePerNight,
            rating,
            total = nights * pricePerNight,
        }, new JsonSerializerOptions { WriteIndented = true }));
        return $"Booked {hotel} in {city}: {nights} nights × ${pricePerNight} = ${nights * pricePerNight} ({rating}★)";
    }

    [AgentFunction]
    [Description("Remove hotel booking for a city so you can book a cheaper alternative.")]
    public string RemoveHotel(string city)
    {
        var workspace = Workspace;
        var key = $"hotel-{city.ToLowerInvariant().Replace(' ', '-')}.json";
        if (!workspace.FileExists(key))
            return $"No hotel found for {city}";
        workspace.TryWriteFile(key, "");
        return $"Removed hotel booking in {city}";
    }

    [AgentFunction]
    [Description("Validate the entire trip against budget, stop limits, and route continuity.")]
    public string ValidateTrip()
    {
        var workspace = Workspace;
        var configJson = workspace.TryReadFile("config.json").Value.Content;
        var itineraryJson = workspace.TryReadFile("itinerary.json").Value.Content;
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson)!;
        var legs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(itineraryJson)!;
        var budgetVal = int.Parse(config["budget"].ToString()!);
        var maxStopsVal = int.Parse(config["maxStops"].ToString()!);
        var minStopsVal = int.Parse(config.GetValueOrDefault("minStops", 3).ToString()!);

        var flightCost = legs.Sum(l => int.Parse(l["price"].ToString()!));
        var hotelCost = 0;
        foreach (var path in workspace.GetFilePaths().Where(p => p.StartsWith("hotel-")))
        {
            var content = workspace.TryReadFile(path).Value.Content;
            if (string.IsNullOrWhiteSpace(content)) continue;
            var hotelData = JsonSerializer.Deserialize<Dictionary<string, object>>(content)!;
            hotelCost += int.Parse(hotelData["total"].ToString()!);
        }

        var totalCost = flightCost + hotelCost;
        var issues = new List<object>();
        var originCity = config["origin"].ToString()!;
        var destCity = config["destination"].ToString()!;

        var statusJson = workspace.TryReadFile("status.json").Value.Content;
        var status = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson)!;
        var overBudgetAttempts = status.TryGetValue("overBudgetAttempts", out var oba)
            ? int.Parse(oba.ToString()!)
            : 0;

        if (totalCost > budgetVal)
        {
            overBudgetAttempts++;
            status["overBudgetAttempts"] = overBudgetAttempts;
            workspace.TryWriteFile("status.json", JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));

            var overBy = totalCost - budgetVal;
            var action = overBudgetAttempts >= 3
                ? $"You have tried to fix the budget {overBudgetAttempts} times without success. "
                    + "STOP tweaking — call clear_itinerary and try a COMPLETELY DIFFERENT route. "
                    + $"You need total cost under ${budgetVal}."
                : $"Remove expensive legs or hotels and replace with cheaper options. "
                    + $"You need to save at least ${overBy}.";
            issues.Add(new
            {
                code = "OVER_BUDGET",
                detail = $"${totalCost} > ${budgetVal} (over by ${overBy}). Attempt {overBudgetAttempts}.",
                action,
            });
        }

        if (legs.Count > maxStopsVal + 1)
        {
            issues.Add(new
            {
                code = "TOO_MANY_LEGS",
                detail = $"{legs.Count} legs exceeds maximum {maxStopsVal + 1}",
                action = "Call remove_leg to remove excess legs.",
            });
        }

        if (legs.Count == 0)
        {
            issues.Add(new
            {
                code = "NO_LEGS",
                detail = "Itinerary is empty.",
                action = $"Use web_search to find flights from {originCity} toward {destCity} and call add_leg.",
            });
        }
        else if (legs.Count < minStopsVal + 1)
        {
            var currentTo = legs[^1]["to"].ToString()!;
            issues.Add(new
            {
                code = "NOT_ENOUGH_STOPS",
                detail = $"Have {legs.Count} leg(s), need at least {minStopsVal + 1} ({minStopsVal} intermediate cities).",
                action = $"Add more legs. Your last leg ends at {currentTo}. "
                    + $"Use web_search to find flights from {currentTo} toward {destCity}.",
            });
        }

        // Check intermediate city hotels
        var intermediateCities = legs.Take(legs.Count > 0 ? legs.Count - 1 : 0)
            .Select(l => l["to"].ToString()!).ToList();
        if (legs.Count > 0)
            intermediateCities.RemoveAll(c => CityMatch(c, destCity));

        foreach (var intermCity in intermediateCities)
        {
            var cityKey = $"hotel-{intermCity.ToLowerInvariant().Replace(' ', '-')}.json";
            if (!workspace.FileExists(cityKey) || string.IsNullOrWhiteSpace(workspace.TryReadFile(cityKey).Value.Content))
            {
                issues.Add(new
                {
                    code = "MISSING_HOTEL",
                    detail = $"No hotel booked for layover in {intermCity}.",
                    action = $"Use web_search to find hotels in {intermCity}, then call book_hotel.",
                });
            }
            else
            {
                var hotelData = JsonSerializer.Deserialize<Dictionary<string, object>>(workspace.TryReadFile(cityKey).Value.Content)!;
                if (hotelData.TryGetValue("rating", out var ratingObj) &&
                    double.TryParse(ratingObj.ToString(), out var rating) && rating < 3.5)
                {
                    issues.Add(new
                    {
                        code = "LOW_RATING",
                        detail = $"Hotel in {intermCity} rated {rating}★ — minimum 3.5★ required.",
                        action = $"Call remove_hotel for {intermCity}, then use web_search for better-rated hotels.",
                    });
                }
            }
        }

        // Route continuity
        for (int i = 1; i < legs.Count; i++)
        {
            if (!CityMatch(legs[i - 1]["to"].ToString()!, legs[i]["from"].ToString()!))
                issues.Add(new
                {
                    code = "ROUTE_GAP",
                    detail = $"Leg {i} ends at {legs[i - 1]["to"]} but leg {i + 1} starts at {legs[i]["from"]}.",
                    action = $"Call remove_leg({i + 1}) and re-add it starting from {legs[i - 1]["to"]}.",
                });
        }

        if (legs.Count > 0 && !CityMatch(legs[0]["from"].ToString()!, originCity))
            issues.Add(new
            {
                code = "WRONG_ORIGIN",
                detail = $"First leg starts at {legs[0]["from"]}, expected {originCity}.",
                action = $"Call remove_leg(1) and re-add the first leg starting from {originCity}.",
            });

        if (legs.Count > 0 && !CityMatch(legs[^1]["to"].ToString()!, destCity))
            issues.Add(new
            {
                code = "WRONG_DESTINATION",
                detail = $"Last leg ends at {legs[^1]["to"]}, expected {destCity}.",
                action = $"Add another leg from {legs[^1]["to"]} to {destCity}.",
            });

        var result = new
        {
            status = issues.Count == 0 ? "VALID" : "ISSUES_FOUND",
            flightCost,
            hotelCost,
            totalCost,
            budget = budgetVal,
            remaining = budgetVal - totalCost,
            legCount = legs.Count,
            issueCount = issues.Count,
            issues,
        };

        status["validated"] = issues.Count == 0;
        status["phase"] = issues.Count == 0 ? "finalize" : "fix";
        if (issues.Count == 0)
            status["overBudgetAttempts"] = 0;
        workspace.TryWriteFile("status.json", JsonSerializer.Serialize(status));

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [AgentFunction]
    [Description("Write the final trip summary to the workspace and mark as complete.")]
    public string FinalizeTrip(string summary)
    {
        var workspace = Workspace;
        workspace.TryWriteFile("trip-summary.md", summary);
        var status = JsonSerializer.Deserialize<Dictionary<string, object>>(workspace.TryReadFile("status.json").Value.Content)!;
        status["finalized"] = true;
        workspace.TryWriteFile("status.json", JsonSerializer.Serialize(status));
        return "Trip finalized and summary saved.";
    }

    [AgentFunction]
    [Description(
        "Save research notes from web_search results to the workspace. " +
        "Call this after each web_search to persist the data you found so you " +
        "don't lose it between iterations. Keep notes concise — just the key " +
        "facts (route, airline, price, duration for flights; name, price, rating for hotels).")]
    public string SaveResearch(string notes)
    {
        var workspace = Workspace;
        var existing = workspace.FileExists("research-notes.md")
            ? workspace.TryReadFile("research-notes.md").Value.Content
            : "";

        // Cap at ~3000 chars to keep prompt size reasonable
        var updated = existing + "\n" + notes;
        if (updated.Length > 3000)
            updated = updated[^3000..];

        workspace.TryWriteFile("research-notes.md", updated);
        return $"Research notes saved ({updated.Length} chars total).";
    }

    /// <summary>
    /// Fuzzy city name matching. Handles cases where the LLM sends
    /// "New York (JFK)" but the config says "New York".
    /// </summary>
    private static bool CityMatch(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return true;

        var aNorm = NormalizeCity(a);
        var bNorm = NormalizeCity(b);
        return string.Equals(aNorm, bNorm, StringComparison.OrdinalIgnoreCase)
            || aNorm.StartsWith(bNorm, StringComparison.OrdinalIgnoreCase)
            || bNorm.StartsWith(aNorm, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCity(string city)
    {
        // Strip parenthesized airport codes: "New York (JFK)" → "New York"
        var parenIdx = city.IndexOf('(');
        if (parenIdx > 0)
            city = city[..parenIdx];
        return city.Trim();
    }
}
