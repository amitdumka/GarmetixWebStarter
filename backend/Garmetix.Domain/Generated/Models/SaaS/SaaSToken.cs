using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Garmetix.Core.Models.SaaS
{
    public class SaaSToken : BaseEntity
    {
        [ForeignKey("SaaSClient")]
        public Guid SaaSClientId { get; set; }
        public virtual SaaSClient? SaaSClient { get; set; }

        [ForeignKey("SaaSPlan")]
        public Guid SaaSPlanId { get; set; }
        public virtual SaaSPlan? SaaSPlan { get; set; }

        [Required]
        [MaxLength(1000)]
        public string TokenString { get; set; } = string.Empty;

        public bool IsActivated { get; set; } = false;

        public DateTime? ActivatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }
    }
}
