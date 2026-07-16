namespace Supermarket.Api.Models;

public enum ItemCategory
{
    Fruits,
    Dairy,
    Meat,
    Bakery,
    Drinks,
    Cleaning,
    Other
}

public class ShoppingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public ItemCategory Category { get; set; } = ItemCategory.Other;
    public decimal Price { get; set; }
    public bool Checked { get; set; }
    public string? UserId { get; set; }
    public Guid? ShoppingListId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ShoppingList
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public decimal Budget { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ShoppingItem> Items { get; set; } = new();
}

public class CreateItemRequest
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public ItemCategory Category { get; set; } = ItemCategory.Other;
    public decimal Price { get; set; }
}

public class UpdateItemRequest
{
    public string? Name { get; set; }
    public int? Quantity { get; set; }
    public ItemCategory? Category { get; set; }
    public decimal? Price { get; set; }
    public bool? Checked { get; set; }
}

public class CreateListRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Budget { get; set; }
}
