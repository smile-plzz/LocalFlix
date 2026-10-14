namespace LocalFlix.Metadata;

public static class FuzzyMatcher
{
    public static double CalculateTitleSimilarity(string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            return 0.0;
        }

        var s1 = Normalize(source);
        var s2 = Normalize(target);

        if (s1 == s2) return 1.0;
        if (s1.Contains(s2) || s2.Contains(s1))
        {
            var lenRatio = (double)Math.Min(s1.Length, s2.Length) / Math.Max(s1.Length, s2.Length);
            return Math.Max(0.85, 0.70 + (0.25 * lenRatio));
        }

        return CalculateDiceCoefficient(s1, s2);
    }

    public static double CalculateMatchConfidence(
        string parsedTitle,
        int? parsedYear,
        string candidateTitle,
        int? candidateYear)
    {
        var titleScore = CalculateTitleSimilarity(parsedTitle, candidateTitle);

        if (titleScore < 0.40)
        {
            return titleScore * 0.5;
        }

        double confidence = titleScore;

        if (parsedYear.HasValue && candidateYear.HasValue)
        {
            var yearDiff = Math.Abs(parsedYear.Value - candidateYear.Value);
            if (yearDiff == 0)
            {
                confidence = Math.Min(1.0, confidence + 0.08);
            }
            else if (yearDiff == 1)
            {
                // Acceptable international release difference
                confidence = Math.Min(1.0, confidence + 0.02);
            }
            else if (yearDiff <= 3)
            {
                confidence -= 0.10;
            }
            else
            {
                confidence -= 0.25;
            }
        }

        return Math.Max(0.0, Math.Min(1.0, confidence));
    }

    private static double CalculateDiceCoefficient(string s1, string s2)
    {
        if (s1.Length < 2 || s2.Length < 2) return 0.0;

        var bigrams1 = GetBigrams(s1);
        var bigrams2 = GetBigrams(s2);

        int matches = 0;
        foreach (var bg in bigrams1)
        {
            if (bigrams2.Remove(bg))
            {
                matches++;
            }
        }

        return (2.0 * matches) / (bigrams1.Count + bigrams2.Count + matches);
    }

    private static List<string> GetBigrams(string input)
    {
        var bigrams = new List<string>(input.Length - 1);
        for (int i = 0; i < input.Length - 1; i++)
        {
            bigrams.Add(input.Substring(i, 2));
        }
        return bigrams;
    }

    private static string Normalize(string input)
    {
        var chars = input.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            .ToArray();
        return string.Join(" ", new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
