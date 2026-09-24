using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FileFlow.Tests.TestHelpers;

/// <summary>
/// Entrada <b>real</b> sobre la sesión headless: mueve el puntero, pulsa, escribe teclas y suelta datos, por
/// los mismos caminos que la aplicación en marcha.
///
/// <para><b>Por qué existe</b>: hasta ahora el suite no simulaba ni un clic, ni una tecla, ni un arrastre.
/// Todo lo que solo ocurre al interactuar —los estados de estilo (hover, pressed, focus, disabled), los
/// atajos del editor, soltar un nodo del cajón en el lienzo, el foco del buscador— se ejecutaba únicamente en
/// la aplicación, así que una regresión ahí no la veía nadie hasta usarla. Las capturas visuales congelan
/// estados quietos y los lints de estilo comprueban el texto de las reglas, no que el estado se aplique.</para>
///
/// <para><b>Fotogramas</b>: los estados del sistema de diseño van con transiciones (<c>BrushTransition</c>),
/// de modo que el valor no salta: se anima. <see cref="Settle"/> bombea el dispatcher, avanza el reloj de
/// render y <b>pulsa el reloj de animación virtual</b> (<see cref="AnimationClock"/>) un fotograma por
/// vuelta: la transición llega a su token de forma exacta y sin esperar ni un milisegundo real. Antes esto
/// se conseguía bombeando 180 ms de reloj: tiempo real que no probaba la animación, sólo que el tiempo pasa.</para>
/// </summary>
public static class InputSimulator
{
    /// <summary>
    /// Fotogramas que se avanzan en cada asentado. El presupuesto y el mecanismo viven en
    /// <see cref="AnimationClock"/> porque son los mismos que usa una captura visual: aquí sólo se lee con el
    /// nombre que le da la entrada simulada.
    /// </summary>
    public const int SettleFrames = AnimationClock.SettleFrames;

    /// <summary>Bombea las operaciones pendientes del dispatcher (incluidas las diferidas por prioridad).</summary>
    public static void Pump() => Dispatcher.UIThread.RunJobs();

    /// <summary>
    /// Asienta la interfaz: layout, hit test, un paso de render y 16 ms de reloj de animación por vuelta, hasta
    /// que transiciones y animaciones quedan en su valor final. Es el mismo asentado que usa una captura visual
    /// (<see cref="AnimationClock.Settle"/>), para que la interfaz que se afirma y la que se fotografía sean la
    /// misma interfaz asentada.
    /// </summary>
    public static void Settle(int frames = SettleFrames) => AnimationClock.Settle(frames);

    /// <summary>
    /// Asienta y bombea <b>hasta que la condición se cumpla</b>, en lugar de adivinar cuántos fotogramas
    /// bastan para que una transición llegue a su destino.
    ///
    /// <para>El presupuesto también es <b>virtual</b>: <paramref name="budgetMilliseconds"/> es tiempo de
    /// animación (lo que tardaría la interfaz en hacerlo), no tiempo real. El anterior medía el reloj del
    /// sistema, así que el sondeo podía rendirse por lentitud de la máquina en lugar de porque el estado no
    /// llegase nunca —un fallo intermitente con el diagnóstico equivocado—. Se ejecuta en el hilo de UI:
    /// bombea el dispatcher, no espera con <c>await</c>.</para>
    ///
    /// <para>Devuelve <c>false</c> si se agota el presupuesto, para que quien llama afirme con su propio
    /// mensaje —donde se ve el valor observado— en vez de fallar aquí sin contexto.</para>
    /// </summary>
    public static bool SettleUntil(Func<bool> condition, int budgetMilliseconds = 3000)
    {
        ArgumentNullException.ThrowIfNull(condition);

        int frames = Math.Max(1, budgetMilliseconds / AnimationClock.FrameMilliseconds);

        for (int frame = 0; frame <= frames; frame++)
        {
            if (condition())
            {
                return true;
            }

            Settle(1);
        }

        return condition();
    }

    /// <summary>Centro del control, expresado en coordenadas de la ventana (donde se entrega la entrada).</summary>
    public static Point CenterOf(Visual target, Visual window)
    {
        var center = new Point(target.Bounds.Width / 2, target.Bounds.Height / 2);
        return target.TranslatePoint(center, window) ?? center;
    }

    /// <summary>Mueve el puntero a un punto de la ventana, sin pulsar (deja el estado <c>:pointerover</c>).</summary>
    public static void MovePointer(Window window, Point point)
    {
        window.MouseMove(point);
        Settle();
    }

    /// <summary>Pone el puntero encima del control (dispara el estado <c>:pointerover</c>).</summary>
    public static void Hover(Window window, Visual target) => MovePointer(window, CenterOf(target, window));

    /// <summary>Clic completo en un punto de la ventana (lo que hace el usuario sobre el lienzo).</summary>
    public static void ClickAt(Window window, Point point, MouseButton button = MouseButton.Left)
    {
        window.MouseMove(point);
        window.MouseDown(point, button);
        window.MouseUp(point, button);
        Settle();
    }

    /// <summary>Mueve el puntero sobre el control y pulsa (el botón queda presionado).</summary>
    public static void Press(Window window, Visual target, MouseButton button = MouseButton.Left)
    {
        Point point = CenterOf(target, window);
        MovePointer(window, point);
        window.MouseDown(point, button);
        Settle();
    }

    /// <summary>Suelta el botón sobre el control.</summary>
    public static void Release(Window window, Visual target, MouseButton button = MouseButton.Left)
    {
        window.MouseUp(CenterOf(target, window), button);
        Settle();
    }

    /// <summary>Clic completo: mover, pulsar y soltar sobre el mismo punto.</summary>
    public static void Click(Window window, Visual target, MouseButton button = MouseButton.Left)
    {
        Press(window, target, button);
        Release(window, target, button);
    }

    /// <summary>Pulsa una tecla (con sus modificadores) sobre el control que tenga el foco.</summary>
    public static void Key(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Settle();
    }

    /// <summary>Escribe texto como entrada de teclado real sobre el control con el foco.</summary>
    public static void Type(Window window, string text)
    {
        window.KeyTextInput(text);
        Settle();
    }

    /// <summary>
    /// Suelta un texto en el punto indicado, por el mismo camino que usa el cajón de nodos
    /// (<c>DataTransfer</c> con un ítem de texto) y que el lienzo lee con <c>TryGetText</c>.
    /// </summary>
    public static void DropText(Window window, Point point, string text,
        DragDropEffects effects = DragDropEffects.Copy)
    {
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(text));

        window.DragDrop(point, RawDragEventType.DragOver, data, effects);
        window.DragDrop(point, RawDragEventType.Drop, data, effects);
        Settle();
    }

    /// <summary>Busca el primer descendiente del tipo pedido (atajo para no repetir el recorrido del árbol).</summary>
    public static T FirstDescendant<T>(Visual root) where T : Visual =>
        root.GetVisualDescendants().OfType<T>().First();
}
