namespace RadarInnowacji.Infrastructure.Services;

/// <summary>Operacje na wektorach wykorzystywane przy grupowaniu projektów.</summary>
public static class EmbeddingMath
{
    public static float[] Normalize(IReadOnlyList<float> vector)
    {
        var result = new float[vector.Count];
        double sum = 0;
        foreach (var value in vector)
        {
            sum += (double)value * value;
        }

        var length = Math.Sqrt(sum);
        if (length <= 0)
        {
            return result;
        }

        for (var i = 0; i < vector.Count; i++)
        {
            result[i] = (float)(vector[i] / length);
        }

        return result;
    }

    public static double CosineSimilarity(IReadOnlyList<float> first, IReadOnlyList<float> second)
    {
        var count = Math.Min(first.Count, second.Count);
        double dot = 0;
        double firstLength = 0;
        double secondLength = 0;
        for (var i = 0; i < count; i++)
        {
            dot += (double)first[i] * second[i];
            firstLength += (double)first[i] * first[i];
            secondLength += (double)second[i] * second[i];
        }

        var denominator = Math.Sqrt(firstLength * secondLength);
        return denominator <= 0 ? 0 : Math.Clamp(dot / denominator, -1, 1);
    }

    public static float[] Average(IReadOnlyList<IReadOnlyList<float>> vectors)
    {
        if (vectors.Count == 0)
        {
            return [];
        }

        var sum = new double[vectors[0].Count];
        foreach (var vector in vectors)
        {
            for (var i = 0; i < sum.Length && i < vector.Count; i++)
            {
                sum[i] += vector[i];
            }
        }

        return Normalize(sum.Select(x => (float)(x / vectors.Count)).ToArray());
    }
}
