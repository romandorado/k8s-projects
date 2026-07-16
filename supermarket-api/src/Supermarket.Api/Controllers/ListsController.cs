using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Supermarket.Api.Data;
using Supermarket.Api.Models;

namespace Supermarket.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ListsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ListsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Listar todas las listas de compra
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShoppingList>>> GetLists()
    {
        return await _context.Lists
            .Include(l => l.Items)
            .ToListAsync();
    }

    /// <summary>
    /// Obtener una lista por ID con sus items
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ShoppingList>> GetList(Guid id)
    {
        var list = await _context.Lists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (list == null)
            return NotFound();

        return list;
    }

    /// <summary>
    /// Crear una nueva lista de compra
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ShoppingList>> CreateList(CreateListRequest request)
    {
        var list = new ShoppingList
        {
            Name = request.Name,
            Budget = request.Budget
        };

        _context.Lists.Add(list);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetList), new { id = list.Id }, list);
    }

    /// <summary>
    /// Actualizar una lista
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateList(Guid id, CreateListRequest request)
    {
        var list = await _context.Lists.FindAsync(id);
        if (list == null)
            return NotFound();

        list.Name = request.Name;
        list.Budget = request.Budget;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Eliminar una lista y todos sus items
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteList(Guid id)
    {
        var list = await _context.Lists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (list == null)
            return NotFound();

        _context.Lists.Remove(list);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Obtener resumen de una lista (total gastado, restante, etc.)
    /// </summary>
    [HttpGet("{id}/summary")]
    public async Task<ActionResult<object>> GetListSummary(Guid id)
    {
        var list = await _context.Lists
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (list == null)
            return NotFound();

        var totalSpent = list.Items
            .Where(i => i.Checked)
            .Sum(i => i.Price * i.Quantity);

        var itemCount = list.Items.Count;
        var checkedCount = list.Items.Count(i => i.Checked);

        return Ok(new
        {
            listId = list.Id,
            listName = list.Name,
            budget = list.Budget,
            totalSpent,
            remaining = list.Budget - totalSpent,
            itemCount,
            checkedCount,
            percentage = list.Budget > 0 ? (totalSpent / list.Budget) * 100 : 0
        });
    }
}
