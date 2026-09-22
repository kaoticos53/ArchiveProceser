namespace FileFlow.App.Services;

/// <summary>
/// Qué puerto vigente se parece más al que falta, para poder ofrecer la reconexión en un clic.
///
/// <para>
/// Es una regla sobre <b>nombres</b> y nada más: no conoce el grafo, ni los cables, ni quién pregunta. Lo que
/// decide es la distancia de edición entre el nombre que se perdió y cada candidato, y quien pregunta elige
/// los candidatos —un puerto de entrada que ya tiene cable no es candidato, porque reconectar ahí tiraría el
/// que ya estaba—.
/// </para>
///
/// <para>
/// Y hay <b>umbral</b>, a propósito: proponer cualquier cosa antes que nada convierte el botón en una trampa.
/// Por debajo de la mitad del nombre no hay propuesta, y la fila del aviso sólo ofrece ir al nodo, que es lo
/// que sí se puede afirmar.
/// </para>
/// </summary>
public static class PortNameProposal
{
    /// <summary>
    /// Parecido mínimo —de 0 a 1, sobre la mitad del nombre más largo— para que una propuesta se considere
    /// una propuesta. Con la mitad basta para un nombre con un sufijo o una errata, y deja fuera los que no se
    /// parecen en nada: <c>Alternate</c>/<c>Alternates</c> es 0,9; <c>Errores</c>/<c>Error</c>, 0,71;
    /// <c>Alternate</c>/<c>Out</c>, 0,11.
    /// </summary>
    public const double MinimumSimilarity = 0.5;

    /// <summary>
    /// El candidato que más se parece al nombre que falta, o <c>null</c> si ninguno llega al umbral. Ante un
    /// empate gana el <b>primero</b>, y el orden lo decide quien pregunta: el de los puertos del nodo, que es
    /// estable y es el que el usuario ve en la tarjeta.
    /// </summary>
    public static string? Suggest(string missingName, IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (string.IsNullOrWhiteSpace(missingName))
        {
            return null;
        }

        string? best = null;
        double bestSimilarity = 0;

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            double similarity = Similarity(missingName, candidate);

            // Justo en el umbral sí se propone —el umbral es «no por debajo de la mitad»—, y un candidato
            // empatado no desbanca al anterior: gana el primero que aparezca.
            if (similarity < MinimumSimilarity || (best != null && similarity <= bestSimilarity))
            {
                continue;
            }

            best = candidate;
            bestSimilarity = similarity;
        }

        return best;
    }

    /// <summary>
    /// Cuánto se parecen dos nombres: 1 menos las ediciones necesarias sobre la longitud del más largo. No
    /// distingue mayúsculas ni espacios de sobra, porque el usuario no los distingue cuando escribe el nombre
    /// de un puerto.
    /// </summary>
    public static double Similarity(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        string a = left.Trim();
        string b = right.Trim();

        if (a.Length == 0 || b.Length == 0)
        {
            return a.Length == b.Length ? 1.0 : 0.0;
        }

        int longest = Math.Max(a.Length, b.Length);
        return 1.0 - (double)Distance(a, b) / longest;
    }

    /// <summary>Ediciones mínimas entre dos nombres, con la distancia de Levenshtein habitual.</summary>
    private static int Distance(string left, string right)
    {
        // Una fila de la matriz, no la matriz: cada fila sólo necesita la anterior, y estos nombres son cortos
        // pero se comparan por cada puerto de cada cable perdido.
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (int j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (int j = 1; j <= right.Length; j++)
            {
                int substitution = previous[j - 1] + (char.ToUpperInvariant(left[i - 1]) == char.ToUpperInvariant(right[j - 1]) ? 0 : 1);
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
