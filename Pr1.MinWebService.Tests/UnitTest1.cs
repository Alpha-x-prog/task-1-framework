using Pr1.MinWebService.Errors;
using Pr1.MinWebService.Services;

namespace Pr1.MinWebService.Tests;

/// <summary>
/// Проверки логики предметной области: валидация и хранилище.
/// </summary>
public class DomainTests
{
    private readonly InMemoryItemRepository _repo = new();

    // --- Хранилище ---

    [Fact]
    public void Create_ReturnsItemWithGeneratedId()
    {
        var item = _repo.Create("Монитор", 15000m);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal("Монитор", item.Name);
        Assert.Equal(15000m, item.Price);
    }

    [Fact]
    public void GetById_ExistingItem_ReturnsItem()
    {
        var created = _repo.Create("Клавиатура", 3000m);

        var found = _repo.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
    }

    [Fact]
    public void GetById_NonExistingItem_ReturnsNull()
    {
        var found = _repo.GetById(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public void GetAll_ReturnsAllCreatedItems()
    {
        var repo = new InMemoryItemRepository();
        repo.Create("AAA", 100m);
        repo.Create("BBB", 200m);

        var all = repo.GetAll();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetAll_ReturnsSortedByName()
    {
        var repo = new InMemoryItemRepository();
        repo.Create("Яблоко", 50m);
        repo.Create("Арбуз", 120m);

        var all = repo.GetAll().ToList();

        Assert.Equal("Арбуз", all[0].Name);
        Assert.Equal("Яблоко", all[1].Name);
    }

    // --- Валидация (те же правила, что в Program.cs) ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyName_Throws(string? name)
    {
        Assert.Throws<ValidationException>(() => ValidateCreateRequest(name!, 100m));
    }

    [Fact]
    public void Validate_NameTooLong_Throws()
    {
        var longName = new string('A', 101);

        Assert.Throws<ValidationException>(() => ValidateCreateRequest(longName, 100m));
    }

    [Fact]
    public void Validate_NegativePrice_Throws()
    {
        Assert.Throws<ValidationException>(() => ValidateCreateRequest("Мышь", -1m));
    }

    [Fact]
    public void Validate_ValidData_DoesNotThrow()
    {
        var ex = Record.Exception(() => ValidateCreateRequest("Мышь", 0m));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_ZeroPrice_DoesNotThrow()
    {
        var ex = Record.Exception(() => ValidateCreateRequest("Бесплатный товар", 0m));
        Assert.Null(ex);
    }

    /// <summary>
    /// Повторяет правила валидации из Program.cs,
    /// чтобы проверить логику предметной области изолированно.
    /// </summary>
    private static void ValidateCreateRequest(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Поле name не должно быть пустым");

        if (name.Length > 100)
            throw new ValidationException("Поле name не должно быть длиннее 100 символов");

        if (price < 0)
            throw new ValidationException("Поле price не может быть отрицательным");
    }
}