using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.DataAccess;
using PijacaUDzepu.API.Hubs;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.DTO.Input;
using PijacaUDzepu.API.Models.DTO.Output;
using PijacaUDzepu.API.Services.Interfaces;

namespace PijacaUDzepu.API.Services;

public class SupportService : ISupportService
{
    public const string AdminsGroup = "admins";

    private readonly DataContext _context;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly IPushService _pushService;
    private readonly IEmailService _emailService;
    private readonly ILogger<SupportService> _logger;

    public SupportService(DataContext context, IHubContext<OrderHub> hubContext, IPushService pushService,
        IEmailService emailService, ILogger<SupportService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _pushService = pushService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Create(SupportRequestInputDto dto, int? userId)
    {
        var request = new SupportRequest
        {
            UserId = userId,
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim(),
            Category = dto.Category,
            Message = dto.Message.Trim(),
            Platform = dto.Platform?.Trim(),
            UserAgent = dto.UserAgent?.Trim()
        };
        _context.SupportRequests.Add(request);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group(AdminsGroup).SendAsync("NewSupportRequest", new
        {
            id = request.Id,
            name = request.Name,
            category = request.Category
        });

        var adminIds = await _context.Users
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == "SuperAdmin"))
            .Select(u => u.Id)
            .ToListAsync();
        await _pushService.SendToUsers(adminIds, "Novo pitanje za podršku",
            $"{request.Name}: {Shorten(request.Message, 80)}", "/admin-support");

        // E-mail podršci ide u pozadini, da korisnik ne čeka SMTP
        var emailService = _emailService;
        var logger = _logger;
        _ = Task.Run(async () =>
        {
            try
            {
                await emailService.SendSupportRequest(request);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Slanje pitanja #{Id} na e-mail podrške nije uspelo.", request.Id);
            }
        });
    }

    public async Task<List<SupportRequestDto>> GetAll(bool? resolved)
    {
        var query = _context.SupportRequests.Include(r => r.User).AsQueryable();
        if (resolved.HasValue)
            query = query.Where(r => r.IsResolved == resolved.Value);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .Select(r => MapToDto(r))
            .ToListAsync();
    }

    public Task<int> GetOpenCount() => _context.SupportRequests.CountAsync(r => !r.IsResolved);

    public async Task<SupportRequestDto> SetResolved(int id, bool resolved)
    {
        var request = await _context.SupportRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Pitanje nije pronađeno.");

        request.IsResolved = resolved;
        request.ResolvedAt = resolved ? DateTime.UtcNow : null;
        await _context.SaveChangesAsync();

        return MapToDto(request);
    }

    private static string Shorten(string text, int max) =>
        text.Length <= max ? text : text[..max].TrimEnd() + "…";

    private static SupportRequestDto MapToDto(SupportRequest r) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        UserName = r.User?.UserName,
        Name = r.Name,
        Email = r.Email,
        Category = r.Category,
        Message = r.Message,
        Platform = r.Platform,
        UserAgent = r.UserAgent,
        IsResolved = r.IsResolved,
        ResolvedAt = r.ResolvedAt,
        CreatedAt = r.CreatedAt
    };
}
