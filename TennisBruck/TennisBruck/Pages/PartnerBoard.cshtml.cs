using Microsoft.AspNetCore.Identity;
using TennisBruck.Features.PartnerBoard;

namespace TennisBruck.Pages;

[Authorize]
public class PartnerBoardModel(
    TennisContext db,
    UserManager<IdentityUser> userManager,
    CurrentPlayerService currentPlayerService,
    PartnerBoardService partnerBoardService) : PageModel
{
    public Dictionary<DateTime, List<AvailabilitySlot>> SlotsByDay { get; set; } = new();
    public int CurrentPlayerId { get; set; }
    public List<AvailabilitySlot> MyFixedMatches { get; set; } = [];
    public List<AvailabilitySlot> MyOpenSlots { get; set; } = [];
    public List<AvailabilitySlot> OtherOpenSlots { get; set; } = [];
    public bool HasAnySlots { get; set; }
    public bool IsFilterActive { get; set; }

    [BindProperty(SupportsGet = true)] public DateTime? FilterDateFrom { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FilterDateTo { get; set; }
    [BindProperty(SupportsGet = true)] public TimeSpan? FilterTimeFrom { get; set; }
    [BindProperty(SupportsGet = true)] public TimeSpan? FilterTimeTo { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentPlayerId = currentPlayerService.GetCurrentUser()!.Id;

        var overview = await partnerBoardService.GetOverviewAsync(
            CurrentPlayerId, FilterDateFrom, FilterDateTo, FilterTimeFrom, FilterTimeTo);

        SlotsByDay = overview.SlotsByDay;
        MyFixedMatches = overview.MyFixedMatches;
        MyOpenSlots = overview.MyOpenSlots;
        OtherOpenSlots = overview.OtherOpenSlots;
        HasAnySlots = overview.HasAnySlots;
        IsFilterActive = overview.IsFilterActive;
        FilterDateFrom = overview.FilterDateFrom;
        FilterDateTo = overview.FilterDateTo;
        FilterTimeFrom = overview.FilterTimeFrom;
        FilterTimeTo = overview.FilterTimeTo;

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptMatchAsync(int slotId)
    {
        CurrentPlayerId = currentPlayerService.GetCurrentUser()!.Id;
        var (success, message) = await partnerBoardService.AcceptMatchAsync(slotId, CurrentPlayerId);

        if (success) TempData["SuccessMessage"] = message;
        else TempData["ErrorMessage"] = message;

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateSlotAsync(
        DateTime date, TimeSpan startTime, TimeSpan endTime, string message, int neededPlayers)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var dbUser = db.Players.FirstOrDefault(x => x.IdentityUserId == user.Id);
        if (dbUser == null) return Unauthorized();

        var (success, msg) = await partnerBoardService.CreateSlotAsync(
            dbUser.Id, date, startTime, endTime, message, neededPlayers);

        if (success) TempData["SuccessMessage"] = msg;
        else TempData["ErrorMessage"] = msg;

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditSlotAsync(
        int editSlotId, DateTime editDate, TimeSpan editStartTime, TimeSpan editEndTime,
        string editMessage, int editNeededPlayers)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var dbUser = db.Players.FirstOrDefault(x => x.IdentityUserId == user.Id);
        if (dbUser == null) return Challenge();

        var (success, msg) = await partnerBoardService.EditSlotAsync(
            editSlotId, dbUser.Id, editDate, editStartTime, editEndTime, editMessage, editNeededPlayers);

        if (success) TempData["SuccessMessage"] = msg;
        else TempData["ErrorMessage"] = msg;

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteSlotAsync(int slotId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var dbUser = db.Players.FirstOrDefault(x => x.IdentityUserId == user.Id);
        if (dbUser == null) return Challenge();

        var (success, msg) = await partnerBoardService.DeleteOrLeaveSlotAsync(slotId, dbUser.Id);

        if (success) TempData["SuccessMessage"] = msg;
        else TempData["ErrorMessage"] = msg;

        return RedirectToPage();
    }
}