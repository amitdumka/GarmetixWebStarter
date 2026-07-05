using Garmetix.Core.Models.Base;
using Garmetix.Core.Models.Stores;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Garmetix.Core.Models.SaaS
{
    public class TenantSubscription : BaseEntity
    {
        [ForeignKey("Company")]
        public Guid CompanyId { get; set; }
        public virtual Company? Company { get; set; }

        [Required]
        [MaxLength(50)]
        public string PlanName { get; set; } = "Trial"; // Trial, Basic, Pro, Enterprise

        public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
        public DateTime ValidTo { get; set; } = DateTime.UtcNow.AddDays(14); // 14-day trial by default

        public bool IsActive { get; set; } = true;

        public int MaxStores { get; set; } = 1;
        public int MaxUsers { get; set; } = 5;

        [MaxLength(500)]
        public string IncludedModulesCsv { get; set; } = "POS,Inventory"; // Comma-separated list of features
    }
}
