using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace MegaDeck;

/// <summary>
/// En Windows el tema clásico sustituye la barra de título del sistema por la suya. En Linux (X11)
/// eso no está soportado y se ven las dos, así que se quita la decoración del sistema y la barra
/// clásica se encarga de mover (arrastrar / doble clic para maximizar) y de redimensionar desde los bordes.
/// Se aplica a todas las ventanas, incluidos los MessageBox del tema.
/// </summary>
public static class LinuxWindowChrome
{
    private const double ResizeBorder = 6;

    private static readonly Dictionary<WindowEdge, Cursor> EdgeCursors = new()
    {
        [WindowEdge.West] = new Cursor(StandardCursorType.LeftSide),
        [WindowEdge.East] = new Cursor(StandardCursorType.RightSide),
        [WindowEdge.North] = new Cursor(StandardCursorType.TopSide),
        [WindowEdge.South] = new Cursor(StandardCursorType.BottomSide),
        [WindowEdge.NorthWest] = new Cursor(StandardCursorType.TopLeftCorner),
        [WindowEdge.NorthEast] = new Cursor(StandardCursorType.TopRightCorner),
        [WindowEdge.SouthWest] = new Cursor(StandardCursorType.BottomLeftCorner),
        [WindowEdge.SouthEast] = new Cursor(StandardCursorType.BottomRightCorner),
    };

    public static void Install(Application app)
    {
        if (!OperatingSystem.IsLinux())
            return;

        // Un estilo tiene prioridad sobre el ControlTheme del tema, que pone SystemDecorations="Full".
        app.Styles.Add(new Style(x => x.Is<Window>())
        {
            Setters = { new Setter(Window.SystemDecorationsProperty, SystemDecorations.None) }
        });

        InputElement.PointerPressedEvent.AddClassHandler<Window>(OnPointerPressed, RoutingStrategies.Tunnel);
        InputElement.PointerMovedEvent.AddClassHandler<Window>(OnPointerMoved, RoutingStrategies.Tunnel);
    }

    private static void OnPointerPressed(Window window, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
            return;

        if (GetResizeEdge(window, e) is { } edge)
        {
            window.BeginResizeDrag(edge, e);
            e.Handled = true;
            return;
        }

        var ancestors = (e.Source as Visual)?.GetSelfAndVisualAncestors().ToList() ?? [];
        if (!ancestors.OfType<TitleBar>().Any() || ancestors.OfType<Button>().Any())
            return; // fuera de la barra de título, o en sus botones (minimizar, maximizar, cerrar)

        if (e.ClickCount == 2 && window.CanResize)
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            window.BeginMoveDrag(e);
        e.Handled = true;
    }

    private static void OnPointerMoved(Window window, PointerEventArgs e)
    {
        window.Cursor = GetResizeEdge(window, e) is { } edge ? EdgeCursors[edge] : null;
    }

    private static WindowEdge? GetResizeEdge(Window window, PointerEventArgs e)
    {
        if (!window.CanResize || window.WindowState != WindowState.Normal)
            return null;

        var p = e.GetPosition(window);
        bool left = p.X < ResizeBorder, right = p.X > window.Bounds.Width - ResizeBorder;
        bool top = p.Y < ResizeBorder, bottom = p.Y > window.Bounds.Height - ResizeBorder;

        return (top, bottom, left, right) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (true, _, _, true) => WindowEdge.NorthEast,
            (_, true, true, _) => WindowEdge.SouthWest,
            (_, true, _, true) => WindowEdge.SouthEast,
            (true, _, _, _) => WindowEdge.North,
            (_, true, _, _) => WindowEdge.South,
            (_, _, true, _) => WindowEdge.West,
            (_, _, _, true) => WindowEdge.East,
            _ => null
        };
    }
}
