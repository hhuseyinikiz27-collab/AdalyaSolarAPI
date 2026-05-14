using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdalyaSolarAPI.Data;
using AdalyaSolarAPI.Models;
using System.Security.Claims;

namespace AdalyaSolarAPI.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize]
public class AddressController : ControllerBase
{
    private readonly AppDbContext _db;

    public AddressController(AppDbContext db) => _db = db;

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var uid = GetUserId();
        var addresses = await _db.Addresses
            .Where(a => a.UserId == uid)
            .OrderByDescending(a => a.IsDefault)
            .ToListAsync();
        return Ok(addresses);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AddressDto dto)
    {
        var uid = GetUserId();

        if (dto.IsDefault)
            await _db.Addresses.Where(a => a.UserId == uid)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));

        var address = new Address
        {
            UserId = uid,
            Title = dto.Title,
            FullName = dto.FullName,
            Phone = dto.Phone,
            City = dto.City,
            District = dto.District,
            Neighborhood = dto.Neighborhood,
            Street = dto.Street,
            IsDefault = dto.IsDefault,
        };

        _db.Addresses.Add(address);
        await _db.SaveChangesAsync();
        return Ok(address);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] AddressDto dto)
    {
        var uid = GetUserId();
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == uid);
        if (address == null) return NotFound();

        if (dto.IsDefault)
            await _db.Addresses.Where(a => a.UserId == uid && a.Id != id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false));

        address.Title = dto.Title;
        address.FullName = dto.FullName;
        address.Phone = dto.Phone;
        address.City = dto.City;
        address.District = dto.District;
        address.Neighborhood = dto.Neighborhood;
        address.Street = dto.Street;
        address.IsDefault = dto.IsDefault;

        await _db.SaveChangesAsync();
        return Ok(address);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var uid = GetUserId();
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == uid);
        if (address == null) return NotFound();

        _db.Addresses.Remove(address);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class AddressDto
{
    public string Title { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
