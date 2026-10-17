using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed partial class Bm25RelevanceScorer : IRelevanceScorer
{
    private const double K1 = 1.5;
    private const double B = 0.75;

    public Task<IReadOnlyList<RelevanceScore>> ScoreAsync(
        string query,
        IReadOnlyList<PlaceCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<RelevanceScore>>([]);
        }

        // 1. Build document corpus
        // Colab: index_text = search_text + " " + category_raw.replace('_', ' ')
        var docs = new List<List<string>>(candidates.Count);
        var docLengths = new double[candidates.Count];
        var termDocFreq = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            var categoryClean = c.Category?.Replace('_', ' ') ?? string.Empty;
            var docText = $"{c.Name} {c.Address} {categoryClean}";
            var tokens = Tokenize(docText);
            docs.Add(tokens);
            docLengths[i] = tokens.Count;

            var uniqueTokens = new HashSet<string>(tokens, StringComparer.Ordinal);
            foreach (var t in uniqueTokens)
            {
                termDocFreq[t] = termDocFreq.GetValueOrDefault(t) + 1;
            }
        }

        int N = candidates.Count;
        double avgDocLength = Math.Max(docLengths.Average(), 1e-9);

        // 2. Compute IDF for query terms
        var queryTokens = Tokenize(query);
        var idfMap = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var qTerm in queryTokens)
        {
            if (!idfMap.ContainsKey(qTerm))
            {
                int n = termDocFreq.GetValueOrDefault(qTerm, 0);
                // Colab: math.log(1 + (N - n + 0.5) / (n + 0.5))
                double idf = Math.Log(1.0 + (N - n + 0.5) / (n + 0.5));
                idfMap[qTerm] = Math.Max(0.0, idf);
            }
        }

        // 3. Compute raw BM25 scores
        var rawScores = new double[N];
        for (int i = 0; i < N; i++)
        {
            var docTokens = docs[i];
            var tf = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var token in docTokens)
            {
                tf[token] = tf.GetValueOrDefault(token) + 1;
            }

            double score = 0.0;
            double docLen = docLengths[i];
            foreach (var qTerm in queryTokens)
            {
                if (tf.TryGetValue(qTerm, out int f))
                {
                    double idf = idfMap[qTerm];
                    double denom = f + K1 * (1.0 - B + B * docLen / avgDocLength);
                    score += idf * f * (K1 + 1.0) / Math.Max(denom, 1e-9);
                }
            }
            rawScores[i] = score;
        }

        // 4. Normalize: bm25_norm = bm25_score / max_bm25_score (Colab: max=0 -> all 0)
        double maxScore = rawScores.Length > 0 ? rawScores.Max() : 0.0;
        var result = new List<RelevanceScore>(N);
        for (int i = 0; i < N; i++)
        {
            double normScore = maxScore > 0 ? rawScores[i] / maxScore : 0.0;
            result.Add(new RelevanceScore(candidates[i].Id, Math.Round(normScore, 6)));
        }

        return Task.FromResult<IReadOnlyList<RelevanceScore>>(result);
    }

    private static List<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        // Colab:
        // s = unicodedata.normalize('NFD', str(s or '')).lower().replace('đ','d')
        // s = ''.join(c for c in s if not unicodedata.combining(c))
        // re.findall(r'[a-z0-9]+', s)
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            if (c is 'đ' or 'Đ')
            {
                sb.Append('d');
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        var clean = sb.ToString();
        var matches = WordRegex().Matches(clean);
        var tokens = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            tokens.Add(match.Value);
        }
        return tokens;
    }

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex WordRegex();
}
