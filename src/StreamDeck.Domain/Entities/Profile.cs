namespace StreamDeck.Domain.Entities;

public sealed class Profile
{
    private readonly List<Page> _pages = new();

    public Guid Id { get; }
    public string Name { get; private set; }
    public IReadOnlyList<Page> Pages => _pages.AsReadOnly();

    private Profile(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public static Profile Create(string name)
    {
        var profile = new Profile(Guid.NewGuid(), NormalizeName(name));
        profile._pages.Add(new Page(Guid.NewGuid(), "Página 1"));
        return profile;
    }

    public static Profile Reconstitute(Guid id, string name, IEnumerable<(Guid Id, string Name)> pages)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El id del perfil no puede estar vacío.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(pages);

        var pageList = pages.ToList();

        if (pageList.Count == 0)
        {
            throw new ArgumentException("Un perfil debe tener al menos una página.", nameof(pages));
        }

        var seenIds = new HashSet<Guid>();
        var reconstitutedPages = new List<Page>(pageList.Count);

        foreach (var (pageId, pageName) in pageList)
        {
            if (pageId == Guid.Empty)
            {
                throw new ArgumentException("El id de una página no puede estar vacío.", nameof(pages));
            }

            if (!seenIds.Add(pageId))
            {
                throw new ArgumentException($"El id de página '{pageId}' está duplicado.", nameof(pages));
            }

            reconstitutedPages.Add(new Page(pageId, NormalizeName(pageName)));
        }

        var profile = new Profile(id, NormalizeName(name));
        profile._pages.AddRange(reconstitutedPages);
        return profile;
    }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    public Page AddPage(string name)
    {
        var page = new Page(Guid.NewGuid(), NormalizeName(name));
        _pages.Add(page);
        return page;
    }

    public void RemovePage(Guid pageId)
    {
        var page = FindPageOrThrow(pageId);

        if (_pages.Count <= 1)
        {
            throw new InvalidOperationException("No se puede eliminar la última página de un perfil.");
        }

        _pages.Remove(page);
    }

    public void RenamePage(Guid pageId, string name)
    {
        var page = FindPageOrThrow(pageId);
        page.Rename(NormalizeName(name));
    }

    public void ReorderPage(Guid pageId, int newIndex)
    {
        if (newIndex < 0 || newIndex >= _pages.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newIndex),
                newIndex,
                $"El índice debe estar entre 0 y {_pages.Count - 1}.");
        }

        var page = FindPageOrThrow(pageId);
        _pages.Remove(page);
        _pages.Insert(newIndex, page);
    }

    private Page FindPageOrThrow(Guid pageId)
    {
        var page = _pages.FirstOrDefault(p => p.Id == pageId);

        if (page is null)
        {
            throw new KeyNotFoundException($"No existe ninguna página con el id '{pageId}' en este perfil.");
        }

        return page;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre no puede estar vacío.", nameof(name));
        }

        return name.Trim();
    }
}