using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.SaaS
{
    public class SaaSClient : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        public string ClientCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string Email { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string Mobile { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string State { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string Country { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string ZipCode { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string GSTIN { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
