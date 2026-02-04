using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlowMeet.Server.Models.Entities;

public enum FriendshipStatus
{
    Pending = 0,
    Accepted = 1,
    Blocked = 2
}

public class Friendship
{
    public Guid RequesterId { get; set; }
    public User? Requester { get; set; }

    public Guid AddresseeId { get; set; }
    public User? Addressee { get; set; }

    public FriendshipStatus Status { get; set; }
}