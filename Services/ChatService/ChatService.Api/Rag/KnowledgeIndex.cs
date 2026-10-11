using System.Text.RegularExpressions;
using ChatService.Api.Foundry;
using ChatService.Api.Options;
using Microsoft.Extensions.Options;

namespace ChatService.Api.Rag
{
    public class KnowledgeChunk
    {
        public string Id { get; init; } = string.Empty;

        // "faq" for policy/help documents, "product" for catalog entries.
        public string Kind { get; init; } = string.Empty;

        public string Source { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;

        public int? ProductId { get; init; }

        public decimal? Price { get; init; }

        public string? ImageUrl { get; init; }

        public string? Category { get; init; }

        public float[]? Vector { get; set; }

        internal string[] Tokens { get; set; } = Array.Empty<string>();
    }

    public record SearchHit(KnowledgeChunk Chunk, double Score);

    // In-memory hybrid index: cosine similarity when embeddings exist, BM25 keyword scoring otherwise.
    public class KnowledgeIndex
    {
        private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "is", "are", "was", "were", "do", "does", "did", "i", "my", "me", "we", "you", "your",
            "to", "of", "in", "on", "for", "and", "or", "it", "this", "that", "can", "how", "what", "when", "where",
            "which", "with", "at", "be", "have", "has", "will", "if", "any", "about", "get", "want", "need"
        };

        private readonly IEmbeddingModel _embeddings;
        private readonly KnowledgeOptions _options;
        private readonly ILogger<KnowledgeIndex> _logger;
        private readonly object _gate = new();

        private Dictionary<string, List<KnowledgeChunk>> _bySource = new();
        private List<KnowledgeChunk> _all = new();

        public KnowledgeIndex(
            IEmbeddingModel embeddings,
            IOptions<KnowledgeOptions> options,
            ILogger<KnowledgeIndex> logger)
        {
            _embeddings = embeddings;
            _options = options.Value;
            _logger = logger;
        }

        public int Count => _all.Count;

        public async Task ReplaceSourceAsync(
            string source,
            IReadOnlyList<KnowledgeChunk> chunks,
            CancellationToken cancellationToken)
        {
            foreach (var chunk in chunks)
            {
                chunk.Tokens = Tokenize($"{chunk.Title} {chunk.Text}");
            }

            if (_embeddings.IsConfigured && chunks.Count > 0)
            {
                try
                {
                    // Skip the embedding call for chunks that did not change since the last refresh.
                    var previous = _bySource.TryGetValue(source, out var old)
                        ? old.Where(o => o.Vector is not null).ToDictionary(o => o.Id)
                        : new Dictionary<string, KnowledgeChunk>();

                    foreach (var chunk in chunks)
                    {
                        if (previous.TryGetValue(chunk.Id, out var same) &&
                            same.Title == chunk.Title && same.Text == chunk.Text)
                        {
                            chunk.Vector = same.Vector;
                        }
                    }

                    var missing = chunks.Where(c => c.Vector is null).ToList();

                    if (missing.Count > 0)
                    {
                        var vectors = await _embeddings.EmbedAsync(
                            missing.Select(c => $"{c.Title}\n{c.Text}").ToList(),
                            cancellationToken);

                        for (var i = 0; i < missing.Count; i++)
                        {
                            missing[i].Vector = vectors[i];
                        }
                    }
                }
                catch (ModelUnavailableException ex)
                {
                    _logger.LogWarning(ex, "Embedding failed for source {Source}; using keyword search.", source);
                }
            }

            lock (_gate)
            {
                var next = new Dictionary<string, List<KnowledgeChunk>>(_bySource)
                {
                    [source] = chunks.ToList()
                };

                _bySource = next;
                _all = next.Values.SelectMany(v => v).ToList();
            }
        }

        public async Task<IReadOnlyList<SearchHit>> SearchAsync(
            string query,
            string kind,
            int top,
            CancellationToken cancellationToken,
            Func<KnowledgeChunk, bool>? filter = null)
        {
            var candidates = _all
                .Where(c => c.Kind == kind && (filter is null || filter(c)))
                .ToList();

            if (candidates.Count == 0 || string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<SearchHit>();
            }

            if (_embeddings.IsConfigured && candidates.All(c => c.Vector is not null))
            {
                try
                {
                    var queryVector = (await _embeddings.EmbedAsync(new[] { query }, cancellationToken))[0];

                    var vectorHits = candidates
                        .Select(c => new SearchHit(c, Cosine(queryVector, c.Vector!)))
                        .Where(h => h.Score >= _options.MinVectorScore)
                        .OrderByDescending(h => h.Score)
                        .Take(top)
                        .ToList();

                    if (vectorHits.Count > 0)
                    {
                        return vectorHits;
                    }
                }
                catch (ModelUnavailableException ex)
                {
                    _logger.LogWarning(ex, "Query embedding failed; using keyword search.");
                }
            }

            return KeywordSearch(candidates, query, top);
        }

        private IReadOnlyList<SearchHit> KeywordSearch(List<KnowledgeChunk> candidates, string query, int top)
        {
            var terms = Tokenize(query).Distinct().ToArray();

            if (terms.Length == 0)
            {
                return Array.Empty<SearchHit>();
            }

            const double k1 = 1.5, b = 0.75;

            var avgLength = candidates.Average(c => (double)c.Tokens.Length);
            var n = candidates.Count;

            var scored = new List<SearchHit>();

            foreach (var chunk in candidates)
            {
                var score = 0.0;

                foreach (var term in terms)
                {
                    var tf = chunk.Tokens.Count(t => t == term);

                    if (tf == 0)
                    {
                        continue;
                    }

                    var df = candidates.Count(c => c.Tokens.Contains(term));
                    var idf = Math.Log(1 + (n - df + 0.5) / (df + 0.5));

                    score += idf * (tf * (k1 + 1)) /
                             (tf + k1 * (1 - b + b * chunk.Tokens.Length / Math.Max(avgLength, 1)));
                }

                if (score >= _options.MinKeywordScore)
                {
                    scored.Add(new SearchHit(chunk, score));
                }
            }

            return scored.OrderByDescending(h => h.Score).Take(top).ToList();
        }

        public static string[] Tokenize(string text)
        {
            return Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+")
                .Select(m => Stem(m.Value))
                .Where(t => t.Length > 1 && !Stopwords.Contains(t))
                .ToArray();
        }

        // Light plural stemming so "returns" matches "return".
        private static string Stem(string token)
        {
            if (token.Length > 4 && token.EndsWith("ies", StringComparison.Ordinal))
            {
                return token[..^3] + "y";
            }

            if (token.Length > 3 && token.EndsWith('s') && !token.EndsWith("ss", StringComparison.Ordinal))
            {
                return token[..^1];
            }

            return token;
        }

        private static double Cosine(float[] a, float[] b)
        {
            double dot = 0, na = 0, nb = 0;

            for (var i = 0; i < a.Length && i < b.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }

            return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }
    }
}
