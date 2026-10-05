using Avalonia.Media.Imaging;

namespace MegaDeck.Models;

/// <summary>Nodo del árbol de la izquierda: "MegaDeck" con un hijo por sistema.</summary>
public class LibraryNode
{
    public required string Name { get; init; }
    public required Bitmap Icon { get; init; }
    public GameSystem? System { get; init; }
    public List<LibraryNode> Children { get; init; } = new();
}
