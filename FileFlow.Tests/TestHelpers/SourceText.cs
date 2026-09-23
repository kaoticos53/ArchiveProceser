using System.IO;
using System.Text;
using FluentAssertions;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Lectura de código fuente para los lints de texto.
///
/// <para>Existe porque un lint que busca una línea en el archivo se conforma con encontrarla <b>comentada</b>: la
/// lección la dejó el hito 165, donde un `new SplashScreenWindow()` comentado pasó la guardia que vigilaba
/// justamente que esa línea existiera. Todo lint que afirme «esto está en el código» tiene que leer el código,
/// no el archivo.</para>
/// </summary>
public static class SourceText
{
    /// <summary>
    /// Código del archivo indicado, sin comentarios de línea ni de bloque.
    /// </summary>
    /// <param name="relativePath">Ruta relativa a la raíz del repositorio (p. ej. <c>FileFlow.App/Program.cs</c>).</param>
    public static string CodeWithoutComments(string relativePath)
    {
        string path = Path.Combine(TestRepositoryLocator.RepositoryRoot(), relativePath);
        File.Exists(path).Should().BeTrue($"debe existir el archivo que el lint inspecciona: {relativePath}");

        return WithoutComments(File.ReadAllText(path));
    }

    /// <summary>
    /// Retira comentarios de línea y de bloque del código C#, respetando los literales de cadena (que pueden
    /// contener «//» legítimos, como «https://»).
    /// </summary>
    public static string WithoutComments(string source)
    {
        var output = new StringBuilder(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            char current = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (current == '"' || current == '\'')
            {
                // Copiar el literal íntegro: su contenido no abre comentarios.
                output.Append(current);
                i++;
                while (i < source.Length)
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        output.Append(source[i]).Append(source[i + 1]);
                        i += 2;
                        continue;
                    }

                    output.Append(source[i]);
                    if (source[i] == current)
                    {
                        // Sin consumir además el carácter siguiente: el `i++` del bucle exterior ya deja el
                        // índice después de la comilla. Con el consumo de más, el analizador se comía un
                        // carácter tras <b>cada</b> literal —una coma, un paréntesis, un `;`— y por eso un lint
                        // no podía buscar un fragmento de código que termínase en una llamada
                        // (`Pseudo(control, ":pointerover")` se leía sin el paréntesis) ni un literal seguido de
                        // coma, sin que nada avisara de que estaba mirando un texto que no es el código.
                        break;
                    }

                    i++;
                }

                continue;
            }

            if (current == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (current == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                {
                    i++;
                }

                i += 2;
                continue;
            }

            output.Append(current);
        }

        return output.ToString();
    }
}
