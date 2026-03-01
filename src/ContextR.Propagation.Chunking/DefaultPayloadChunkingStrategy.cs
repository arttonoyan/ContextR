using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace ContextR.Propagation.Chunking;

/// <summary>
/// Default UTF-8-safe payload chunking strategy.
/// </summary>
public sealed class DefaultPayloadChunkingStrategy<TContext> : IContextPayloadChunkingStrategy<TContext>
    where TContext : class
{
    private const string ChunkCountSuffix = "__chunks";
    private const string ChunkPartSuffix = "__chunk_";

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> Chunk(string key, string payload, int maxPayloadBytes)
    {
        if (maxPayloadBytes <= 0)
            throw new InvalidOperationException("Chunking requires MaxPayloadBytes > 0.");

        var chunks = SplitByUtf8Bytes(payload, maxPayloadBytes);
        if (chunks.Count == 0)
            return [];

        var values = new List<KeyValuePair<string, string>>(chunks.Count + 1)
        {
            new(GetChunkCountKey(key), chunks.Count.ToString())
        };

        for (var i = 0; i < chunks.Count; i++)
        {
            values.Add(new KeyValuePair<string, string>(GetChunkKey(key, i), chunks[i]));
        }

        return values;
    }

    /// <inheritdoc />
    public bool TryReassemble<TCarrier>(
        string key,
        TCarrier carrier,
        Func<TCarrier, string, string?> getter,
        out string? payload)
    {
        var chunkCountRaw = getter(carrier, GetChunkCountKey(key));
        if (!int.TryParse(chunkCountRaw, out var chunkCount) || chunkCount <= 0)
        {
            payload = null;
            return false;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < chunkCount; i++)
        {
            var chunk = getter(carrier, GetChunkKey(key, i));
            if (chunk is null)
            {
                payload = null;
                return false;
            }

            builder.Append(chunk);
        }

        payload = builder.ToString();
        return true;
    }

    private static List<string> SplitByUtf8Bytes(string payload, int maxBytes)
    {
        var chunks = new List<string>();
        var current = new StringBuilder();
        var currentBytes = 0;
        Span<byte> buffer = stackalloc byte[4];

        foreach (var rune in payload.EnumerateRunes())
        {
            var runeBytes = rune.EncodeToUtf8(buffer);

            if (currentBytes + runeBytes > maxBytes && current.Length > 0)
            {
                chunks.Add(current.ToString());
                current.Clear();
                currentBytes = 0;
            }

            if (runeBytes > maxBytes)
                throw new InvalidOperationException("Single rune exceeds configured chunk size.");

            current.Append(rune.ToString());
            currentBytes += runeBytes;
        }

        if (current.Length > 0)
            chunks.Add(current.ToString());

        return chunks;
    }

    private static string GetChunkCountKey(string key) => $"{key}{ChunkCountSuffix}";
    private static string GetChunkKey(string key, int index) => $"{key}{ChunkPartSuffix}{index}";
}
