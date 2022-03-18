using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTCPayServer.Plugins.PodServer.Data.Models;

public class Contribution
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [DisplayName("Contribution ID")]
    public string ContributionId { get; set; }
    
    // Properties
    public string Role { get; set; }
    
    [Required]
    [Range(1, int.MaxValue)]
    public int Split { get; set; }
    
    // Relations
    public string PersonId { get; set; }
    public Person Person { get; set; }
}
