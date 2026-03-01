using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using analyticsweb.Data; // adjust if your namespace differs
using analyticsweb.Models; // adjust if your models namespace differs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace analyticsweb.Pages
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public DashboardModel(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public int TotalDatasets { get; set; }
        public int DatasetsLast7Days { get; set; }
        public int DatasetsLast30Days { get; set; }

        public Dataset? MostRecentDataset { get; set; }
        public List<Dataset> RecentDatasets { get; set; } = new();

        public async Task OnGetAsync()
        {
            var nowUtc = DateTime.UtcNow;
            var since7 = nowUtc.AddDays(-7);
            var since30 = nowUtc.AddDays(-30);

            // If your Dataset has OwnerId, filter by user.
            // If it does NOT, this will not compile until you remove OwnerId filtering.
            var userId = _userManager.GetUserId(User);

            var query = _context.Datasets.AsNoTracking();

            // ✅ Uncomment this if your Dataset has OwnerId
            // query = query.Where(d => d.OwnerId == userId);

            TotalDatasets = await query.CountAsync();
            DatasetsLast7Days = await query.Where(d => d.CreatedAt >= since7).CountAsync();
            DatasetsLast30Days = await query.Where(d => d.CreatedAt >= since30).CountAsync();

            MostRecentDataset = await query
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            RecentDatasets = await query
                .OrderByDescending(d => d.CreatedAt)
                .Take(8)
                .ToListAsync();
        }
    }
}