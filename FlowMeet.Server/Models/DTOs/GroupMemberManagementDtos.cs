using System.ComponentModel.DataAnnotations;
using FlowMeet.Server.Models.Entities;

namespace FlowMeet.Server.Models.DTOs;

public class UpdateGroupMemberRoleRequest
{
    [Required]
    public GroupRole Role { get; set; }
}
