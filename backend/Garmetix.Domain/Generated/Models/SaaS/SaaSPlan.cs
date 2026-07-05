using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.SaaS
{
    public class SaaSPlan : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string PlanName { get; set; } = string.Empty;

        public int MaxCompanies { get; set; } = 1;
        
        public int MaxStoreGroups { get; set; } = 1;
        
        public int MaxStores { get; set; } = 2;
        
        public int MaxUsers { get; set; } = 20;
        
        [MaxLength(500)]
        public string IncludedModulesCsv { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
