using System.ComponentModel;
using System.Text.Json;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace IterativeTripPlannerApp;

/// <summary>
/// DI-resolved tool functions for the iterative trip planner.
/// Each method accesses the workspace via <see cref="IAgentExecutionContextAccessor"/>
/// — the same pattern BrandGhost and other real consumers use.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="IAgentExecutionContextAccessor"/> is populated by
/// <see cref="NexusLabs.Needlr.AgentFramework.Iterative.IIterativeAgentLoop"/>
/// automatically when it has access to the accessor via DI. Tools never
/// need to know about <c>IterativeContext</c> or captured closures.
/// </para>
/// <para>
/// Console output has been removed from all tools — progress reporting is
/// handled entirely by lifecycle hooks on <c>IterativeLoopOptions</c>.
/// </para>
/// </remarks>
[AgentFunctionGroup("trip-planner")]
internal sealed class TripPlannerFunctions(
    IAgentExecutionContextAccessor contextAccessor)
{
    private IWorkspace Workspace =>
        contextAccessor.GetRequired().GetRequiredWorkspace();

    // ── Static data: flight routes ──────────────────────────────────────
    private static readonly (string[] fromTerms, string[] toTerms, string data)[] FlightRoutes =
    [
        (["new york", "nyc", "jfk"], ["los angeles", "la", "lax"],
            """[{"airline":"United","flight":"UA201","price":380,"duration":"5h30m","stops":0},{"airline":"Delta","flight":"DL445","price":340,"duration":"5h45m","stops":0},{"airline":"JetBlue","flight":"B6123","price":290,"duration":"6h10m","stops":0}]"""),
        (["new york", "nyc", "jfk"], ["london", "lhr", "heathrow"],
            """[{"airline":"British Airways","flight":"BA178","price":520,"duration":"7h00m","stops":0},{"airline":"Virgin Atlantic","flight":"VS4","price":480,"duration":"7h15m","stops":0},{"airline":"Delta","flight":"DL1","price":560,"duration":"7h30m","stops":0}]"""),
        (["new york", "nyc", "jfk"], ["paris", "cdg", "charles de gaulle"],
            """[{"airline":"Air France","flight":"AF9","price":510,"duration":"7h45m","stops":0},{"airline":"Delta","flight":"DL264","price":490,"duration":"8h00m","stops":0},{"airline":"United","flight":"UA57","price":530,"duration":"7h50m","stops":0}]"""),
        (["new york", "nyc", "jfk"], ["tokyo", "nrt", "narita", "hnd", "haneda"],
            """[{"airline":"ANA","flight":"NH9","price":1250,"duration":"14h00m","stops":0},{"airline":"JAL","flight":"JL5","price":1350,"duration":"13h45m","stops":0},{"airline":"United","flight":"UA79","price":1180,"duration":"14h30m","stops":0}]"""),
        (["los angeles", "la", "lax"], ["honolulu", "hnl"],
            """[{"airline":"Hawaiian","flight":"HA12","price":420,"duration":"5h40m","stops":0},{"airline":"United","flight":"UA877","price":380,"duration":"5h55m","stops":0},{"airline":"Delta","flight":"DL302","price":350,"duration":"6h20m","stops":0}]"""),
        (["los angeles", "la", "lax"], ["tokyo", "nrt", "narita", "hnd", "haneda"],
            """[{"airline":"ANA","flight":"NH105","price":890,"duration":"11h30m","stops":0},{"airline":"JAL","flight":"JL15","price":950,"duration":"11h15m","stops":0},{"airline":"United","flight":"UA32","price":820,"duration":"12h00m","stops":0}]"""),
        (["honolulu", "hnl"], ["tokyo", "nrt", "narita", "hnd", "haneda"],
            """[{"airline":"JAL","flight":"JL73","price":650,"duration":"8h30m","stops":0},{"airline":"ANA","flight":"NH183","price":720,"duration":"8h15m","stops":0},{"airline":"Hawaiian","flight":"HA441","price":580,"duration":"9h10m","stops":0}]"""),
        (["london", "lhr", "heathrow"], ["tokyo", "nrt", "narita", "hnd", "haneda"],
            """[{"airline":"ANA","flight":"NH902","price":980,"duration":"12h30m","stops":0},{"airline":"JAL","flight":"JL44","price":920,"duration":"12h00m","stops":0},{"airline":"British Airways","flight":"BA5","price":1050,"duration":"11h45m","stops":0}]"""),
        (["london", "lhr", "heathrow"], ["paris", "cdg"],
            """[{"airline":"British Airways","flight":"BA304","price":120,"duration":"1h15m","stops":0},{"airline":"Air France","flight":"AF1681","price":110,"duration":"1h20m","stops":0},{"airline":"EasyJet","flight":"U28443","price":75,"duration":"1h25m","stops":0}]"""),
        (["paris", "cdg"], ["tokyo", "nrt", "narita", "hnd", "haneda"],
            """[{"airline":"Air France","flight":"AF276","price":950,"duration":"12h15m","stops":0},{"airline":"JAL","flight":"JL46","price":910,"duration":"12h30m","stops":0},{"airline":"ANA","flight":"NH216","price":980,"duration":"12h00m","stops":0}]"""),
    ];

    // ── Static data: hotel database ─────────────────────────────────────
    private static readonly Dictionary<string, (string[] cityTerms, (string name, int price, double rating)[] hotels)> HotelDatabase = new()
    {
        ["honolulu"] = (["honolulu", "hnl", "waikiki"], [
            ("Waikiki Beach Hotel", 180, 4.2),
            ("Ala Moana Suites", 145, 3.9),
            ("Pacific Hostel", 65, 2.8),
        ]),
        ["los angeles"] = (["los angeles", "la", "lax"], [
            ("LAX Hilton", 195, 4.0),
            ("Venice Beach Inn", 155, 3.8),
            ("Downtown Budget", 89, 3.2),
        ]),
        ["london"] = (["london", "lhr", "heathrow"], [
            ("The Langham London", 280, 4.5),
            ("Premier Inn Heathrow", 120, 3.8),
            ("Holiday Inn Express", 95, 3.2),
        ]),
        ["paris"] = (["paris", "cdg"], [
            ("Hotel Le Marais", 220, 4.3),
            ("Ibis Paris CDG", 110, 3.5),
            ("Generator Paris", 75, 3.0),
        ]),
        ["tokyo"] = (["tokyo", "nrt", "narita", "hnd", "haneda"], [
            ("Shinjuku Granbell", 160, 4.1),
            ("APA Hotel Asakusa", 95, 3.9),
            ("Tokyo Bay Hilton", 250, 4.4),
        ]),
    };

    [AgentFunction]
    [Description("Search for flights, hotels, or travel tips. Use specific city pairs for best results.")]
    public string Search(string query)
    {
        var workspace = Workspace;
        var q = query.ToLowerInvariant();

        string? results = null;

        // Flight routes — match on city names OR airport codes
        foreach (var (fromTerms, toTerms, data) in FlightRoutes)
        {
            if (fromTerms.Any(t => q.Contains(t)) && toTerms.Any(t => q.Contains(t)))
            {
                results = data;
                break;
            }
        }

        // Hotel searches
        if (results is null)
        {
            if (q.Contains("hotel") || q.Contains("accommodation") || q.Contains("stay"))
            {
                foreach (var (_, (cityTerms, hotels)) in HotelDatabase)
                {
                    if (cityTerms.Any(t => q.Contains(t)))
                    {
                        results = "[" + string.Join(",", hotels.Select(h =>
                            $"{{\"hotel\":\"{h.name}\",\"price_per_night\":{h.price},\"rating\":{h.rating}}}")) + "]";
                        break;
                    }
                }
            }
        }

        // Budget/tips fallback
        if (results is null && (q.Contains("budget") || q.Contains("cheap") || q.Contains("tip")))
        {
            results = """[{"tip":"Book 3+ weeks ahead for 15-20% savings"},{"tip":"Tuesday/Wednesday departures are cheapest"},{"tip":"Consider layover in Honolulu to break up Pacific crossing"}]""";
        }

        // Generic fallback — list available routes so the LLM knows what queries will work.
        // This is critical: without explicit guidance, LLMs use airport codes or free-form
        // queries that miss the matching logic, causing death spirals of useless searches.
        if (results is null)
        {
            var availableRoutes = string.Join(", ",
                FlightRoutes.Select(r => $"{r.fromTerms[0]} → {r.toTerms[0]}"));
            var availableHotelCities = string.Join(", ",
                HotelDatabase.Keys);
            results = $"{{\"error\":\"No results found for '{query}'.\","
                + $"\"available_flight_routes\":[\"{string.Join("\",\"", FlightRoutes.Select(r => $"{r.fromTerms[0]} to {r.toTerms[0]}"))}\"],"
                + $"\"available_hotel_cities\":[\"{string.Join("\",\"", HotelDatabase.Keys)}\"],"
                + "\"hint\":\"Search using the exact city pair names above, e.g. 'flights new york to london' or 'hotel london'\"}";
        }

        // Append to research notes (cap at last 5 searches for prompt brevity)
        var existing = workspace.ReadFile("research-notes.md");
        var newEntry = $"\n### Search: {query}\n{results}\n";
        var allEntries = existing + newEntry;

        var sections = allEntries.Split("\n### Search: ", StringSplitOptions.RemoveEmptyEntries);
        if (sections.Length > 5)
        {
            allEntries = string.Join("", sections[^5..].Select(s => "\n### Search: " + s));
        }
        workspace.WriteFile("research-notes.md", allEntries);

        // Also append to full search cache (never capped) — used by AddLeg
        // to validate that flights actually came from search results.
        var cache = workspace.ReadFile("search-cache.txt");
        workspace.WriteFile("search-cache.txt", cache + results + "\n");

        return results;
    }

    [AgentFunction]
    [Description(
        "Add a flight leg to the itinerary. The flight MUST appear in your research " +
        "notes from a prior search call. Hallucinated or invented flights are rejected.")]
    public string AddLeg(string from, string to, string airline, string flight, int price, string duration)
    {
        var workspace = Workspace;

        // Validate that this flight was actually found by a search call.
        // This prevents the LLM from hallucinating flights and prices.
        var searchCache = workspace.ReadFile("search-cache.txt");
        if (string.IsNullOrWhiteSpace(searchCache) || !searchCache.Contains(flight, StringComparison.OrdinalIgnoreCase))
        {
            var fromLower = from.ToLowerInvariant();
            var toLower = to.ToLowerInvariant();
            return $"ERROR: Flight {flight} was not found in your research notes. "
                + $"Call search('flights {fromLower} to {toLower}') first to find available flights, "
                + "then use a flight number from the results.";
        }

        var itineraryJson = workspace.ReadFile("itinerary.json");
        var legs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(itineraryJson) ?? [];
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
        workspace.WriteFile("itinerary.json", JsonSerializer.Serialize(legs, new JsonSerializerOptions { WriteIndented = true }));
        return $"Added leg {legs.Count}: {from} → {to} on {airline} {flight} (${price}, {duration})";
    }

    [AgentFunction]
    [Description("Remove a leg from the itinerary by leg number.")]
    public string RemoveLeg(int legNumber)
    {
        var workspace = Workspace;
        var itineraryJson = workspace.ReadFile("itinerary.json");
        var legs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(itineraryJson) ?? [];
        if (legNumber < 1 || legNumber > legs.Count)
            return $"Error: leg {legNumber} not found (have {legs.Count} legs)";
        var removed = legs[legNumber - 1];
        legs.RemoveAt(legNumber - 1);
        for (int i = 0; i < legs.Count; i++)
            legs[i]["leg"] = i + 1;
        workspace.WriteFile("itinerary.json", JsonSerializer.Serialize(legs, new JsonSerializerOptions { WriteIndented = true }));
        return $"Removed leg {legNumber} ({removed["from"]} → {removed["to"]})";
    }

    [AgentFunction]
    [Description("Book a hotel for a layover city. Must use a hotel name from search results. Price is looked up automatically.")]
    public string BookHotel(string hotel, string city, int nights)
    {
        var workspace = Workspace;
        var cityLower = city.ToLowerInvariant();
        (string name, int price, double rating)? match = null;
        foreach (var (_, (cityTerms, hotels)) in HotelDatabase)
        {
            if (cityTerms.Any(t => cityLower.Contains(t)))
            {
                match = hotels.FirstOrDefault(h =>
                    h.name.Equals(hotel, StringComparison.OrdinalIgnoreCase));
                break;
            }
        }

        if (match is null)
            return $"ERROR: Hotel '{hotel}' not found in {city}. Search for hotels first to see available options.";

        if (match.Value.rating < 3.5)
            return $"ERROR: {match.Value.name} is rated {match.Value.rating}★ — minimum 3.5★ required. Pick a higher-rated hotel.";

        var realPrice = match.Value.price;
        var key = $"hotel-{cityLower.Replace(' ', '-')}.json";
        workspace.WriteFile(key, JsonSerializer.Serialize(new
        {
            hotel = match.Value.name,
            city,
            nights,
            pricePerNight = realPrice,
            rating = match.Value.rating,
            total = nights * realPrice,
        }, new JsonSerializerOptions { WriteIndented = true }));
        return $"Booked {match.Value.name} in {city}: {nights} nights × ${realPrice} = ${nights * realPrice}";
    }

    [AgentFunction]
    [Description("Remove hotel booking for a city so you can book a cheaper alternative.")]
    public string RemoveHotel(string city)
    {
        var workspace = Workspace;
        var key = $"hotel-{city.ToLowerInvariant().Replace(' ', '-')}.json";
        if (!workspace.FileExists(key))
            return $"No hotel found for {city}";
        workspace.WriteFile(key, "");
        return $"Removed hotel booking in {city}";
    }

    [AgentFunction]
    [Description("Validate the entire trip against budget, stop limits, and route continuity.")]
    public string ValidateTrip()
    {
        var workspace = Workspace;
        var configJson = workspace.ReadFile("config.json");
        var itineraryJson = workspace.ReadFile("itinerary.json");
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson)!;
        var legs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(itineraryJson)!;
        var budgetVal = int.Parse(config["budget"].ToString()!);
        var maxStopsVal = int.Parse(config["maxStops"].ToString()!);

        var flightCost = legs.Sum(l => int.Parse(l["price"].ToString()!));
        var hotelCost = 0;
        foreach (var path in workspace.GetFilePaths().Where(p => p.StartsWith("hotel-")))
        {
            var content = workspace.ReadFile(path);
            if (string.IsNullOrWhiteSpace(content)) continue;
            var hotelData = JsonSerializer.Deserialize<Dictionary<string, object>>(content)!;
            hotelCost += int.Parse(hotelData["total"].ToString()!);
        }

        var totalCost = flightCost + hotelCost;
        var issues = new List<object>();
        var originCity = config["origin"].ToString()!;
        var destCity = config["destination"].ToString()!;

        if (totalCost > budgetVal)
        {
            var overBy = totalCost - budgetVal;
            issues.Add(new
            {
                code = "OVER_BUDGET",
                detail = $"${totalCost} > ${budgetVal} (over by ${overBy})",
                action = $"Remove expensive legs or hotels and replace with cheaper options. "
                    + "Call remove_leg or remove_hotel, then search for cheaper alternatives. "
                    + $"You need to save at least ${overBy}.",
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
                detail = "Itinerary is empty — no flight legs added.",
                action = $"Search for flights from {originCity} and call add_leg. "
                    + $"You need at least 3 legs from {originCity} to {destCity} with 2 intermediate stops.",
            });
        }
        else if (legs.Count < 3)
        {
            var currentTo = legs[^1]["to"].ToString()!;
            issues.Add(new
            {
                code = "NOT_ENOUGH_STOPS",
                detail = $"Have {legs.Count} leg(s), need at least 3 (2 intermediate cities).",
                action = $"Add more legs. Your last leg ends at {currentTo}. "
                    + $"Search for flights from {currentTo} toward {destCity} and call add_leg.",
            });
        }

        // Check intermediate city hotels
        var intermediateCities = legs.Take(legs.Count > 0 ? legs.Count - 1 : 0)
            .Select(l => l["to"].ToString()!).ToList();
        if (legs.Count > 0)
            intermediateCities.Remove(destCity);

        foreach (var intermCity in intermediateCities)
        {
            var cityKey = $"hotel-{intermCity.ToLowerInvariant().Replace(' ', '-')}.json";
            if (!workspace.FileExists(cityKey) || string.IsNullOrWhiteSpace(workspace.ReadFile(cityKey)))
            {
                issues.Add(new
                {
                    code = "MISSING_HOTEL",
                    detail = $"No hotel booked for layover in {intermCity}.",
                    action = $"Search for 'hotel {intermCity.ToLowerInvariant()}' and then call book_hotel for {intermCity}.",
                });
            }
            else
            {
                var hotelData = JsonSerializer.Deserialize<Dictionary<string, object>>(workspace.ReadFile(cityKey))!;
                if (hotelData.TryGetValue("rating", out var ratingObj) &&
                    double.TryParse(ratingObj.ToString(), out var rating) && rating < 3.5)
                {
                    issues.Add(new
                    {
                        code = "LOW_RATING",
                        detail = $"Hotel in {intermCity} rated {rating}★ — minimum 3.5★ required.",
                        action = $"Call remove_hotel for {intermCity}, then search for better-rated hotels and book one rated 3.5★ or higher.",
                    });
                }
            }
        }

        // Check route continuity
        for (int i = 1; i < legs.Count; i++)
        {
            if (legs[i - 1]["to"].ToString() != legs[i]["from"].ToString())
                issues.Add(new
                {
                    code = "ROUTE_GAP",
                    detail = $"Leg {i} ends at {legs[i - 1]["to"]} but leg {i + 1} starts at {legs[i]["from"]}.",
                    action = $"Call remove_leg({i + 1}) and re-add it starting from {legs[i - 1]["to"]}.",
                });
        }

        if (legs.Count > 0 && legs[0]["from"].ToString() != originCity)
            issues.Add(new
            {
                code = "WRONG_ORIGIN",
                detail = $"First leg starts at {legs[0]["from"]}, expected {originCity}.",
                action = $"Call remove_leg(1) and re-add the first leg starting from {originCity}.",
            });

        if (legs.Count > 0 && legs[^1]["to"].ToString() != destCity)
            issues.Add(new
            {
                code = "WRONG_DESTINATION",
                detail = $"Last leg ends at {legs[^1]["to"]}, expected {destCity}.",
                action = $"Add another leg from {legs[^1]["to"]} to {destCity}, or remove the last leg and replace it.",
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

        // Update status
        var statusJson = workspace.ReadFile("status.json");
        var status = JsonSerializer.Deserialize<Dictionary<string, object>>(statusJson)!;
        status["validated"] = issues.Count == 0;
        status["phase"] = issues.Count == 0 ? "finalize" : "fix";
        workspace.WriteFile("status.json", JsonSerializer.Serialize(status));

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [AgentFunction]
    [Description("Write the final trip summary to the workspace and mark as complete.")]
    public string FinalizeTrip(string summary)
    {
        var workspace = Workspace;
        workspace.WriteFile("trip-summary.md", summary);
        var status = JsonSerializer.Deserialize<Dictionary<string, object>>(workspace.ReadFile("status.json"))!;
        status["finalized"] = true;
        workspace.WriteFile("status.json", JsonSerializer.Serialize(status));
        return "Trip finalized and summary saved.";
    }
}
