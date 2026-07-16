using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Supermarket.Api.Data;
using Supermarket.Api.Models;

namespace Supermarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ItemsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Listar todos los items de compra
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShoppingItem>>> GetItems([FromQuery] Guid? listId)
    {
        var query = _context.Items.AsQueryable();
        
        if (listId.HasValue)
            query = query.Where(i => EF.Property<Guid?>(i, "ShoppingListId") == listId);

        return await query.ToListAsync();
    }

    /// <summary>
    /// Obtener un item por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ShoppingItem>> GetItem(Guid id)
    {
        var item = await _context.Items.FindAsync(id);
        if (item == null)
            return NotFound();

        return item;
    }

    /// <summary>
    /// Crear un nuevo item
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ShoppingItem>> CreateItem(CreateItemRequest request, [FromQuery] Guid? listId)
    {
        var item = new ShoppingItem
        {
            Name = request.Name,
            Quantity = request.Quantity,
            Category = request.Category,
            Price = request.Price,
            Checked = false
        };

        if (listId.HasValue)
        {
            var list = await _context.Lists.FindAsync(listId);
            if (list == null)
                return NotFound("Lista no encontrada");
            item.ShoppingListId = listId;
        }

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
    }

    /// <summary>
    /// Actualizar un item
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateItem(Guid id, UpdateItemRequest request)
    {
        var item = await _context.Items.FindAsync(id);
        if (item == null)
            return NotFound();

        if (request.Name != null) item.Name = request.Name;
        if (request.Quantity.HasValue) item.Quantity = request.Quantity.Value;
        if (request.Category.HasValue) item.Category = request.Category.Value;
        if (request.Price.HasValue) item.Price = request.Price.Value;
        if (request.Checked.HasValue) item.Checked = request.Checked.Value;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Eliminar un item
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(Guid id)
    {
        var item = await _context.Items.FindAsync(id);
        if (item == null)
            return NotFound();

        _context.Items.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Marcar item como completado/no completado
    /// </summary>
    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> ToggleItem(Guid id)
    {
        var item = await _context.Items.FindAsync(id);
        if (item == null)
            return NotFound();

        item.Checked = !item.Checked;
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { id = item.Id, isChecked = item.Checked });
    }

    /// <summary>
    /// Eliminar todos los items marcados como completados
    /// </summary>
    [HttpDelete("completed")]
    public async Task<IActionResult> DeleteCompleted([FromQuery] Guid? listId)
    {
        var query = _context.Items.Where(i => i.Checked);
        
        if (listId.HasValue)
            query = query.Where(i => EF.Property<Guid?>(i, "ShoppingListId") == listId);

        var items = await query.ToListAsync();
        _context.Items.RemoveRange(items);
        await _context.SaveChangesAsync();

        return Ok(new { deleted = items.Count });
    }
}
